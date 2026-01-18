using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Services;

public interface IWaitingListService
{
    Task<(bool Success, string? ErrorMessage, WaitingListEntry? Entry)> JoinWaitingListAsync(string userId, int travelPackageId);
    Task<(bool Success, string? ErrorMessage, WaitingListEntry? Entry)> JoinWaitingListWithNotificationAsync(string userId, int travelPackageId, INotificationService notificationService);
    Task<bool> IsUserInWaitingListAsync(string userId, int travelPackageId);
    Task<int> GetWaitingListCountAsync(int travelPackageId);
    Task<int> GetUserPositionAsync(string userId, int travelPackageId);
    Task<TimeSpan?> EstimateWaitTimeAsync(int travelPackageId, int position);
    Task<List<WaitingListEntry>> GetNextInQueueAsync(int travelPackageId, int count = 1);
    Task NotifyNextInQueueAsync(int travelPackageId, INotificationService notificationService);
    Task RemoveExpiredNotificationsAsync(int travelPackageId, INotificationService notificationService);
    Task<(bool Success, string? ErrorMessage)> RemoveFromWaitingListAsync(int waitingListEntryId, string userId);
}

public class WaitingListService : IWaitingListService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WaitingListService> _logger;

    public WaitingListService(ApplicationDbContext context, ILogger<WaitingListService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage, WaitingListEntry? Entry)> JoinWaitingListAsync(string userId, int travelPackageId)
    {

        var existing = await _context.WaitingListEntries
            .FirstOrDefaultAsync(w => w.UserId == userId && w.TravelPackageId == travelPackageId && w.IsActive);

        if (existing != null)
        {
            return (false, "You are already on the waiting list for this package.", existing);
        }

        var maxPosition = await _context.WaitingListEntries
            .Where(w => w.TravelPackageId == travelPackageId && w.IsActive)
            .OrderByDescending(w => w.Position)
            .Select(w => w.Position)
            .FirstOrDefaultAsync();

        var entry = new WaitingListEntry
        {
            UserId = userId,
            TravelPackageId = travelPackageId,
            Position = maxPosition + 1,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.WaitingListEntries.Add(entry);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {userId} joined waiting list for package {travelPackageId} at position {entry.Position}");
        return (true, null, entry);
    }

    public async Task<(bool Success, string? ErrorMessage, WaitingListEntry? Entry)> JoinWaitingListWithNotificationAsync(
        string userId, 
        int travelPackageId, 
        INotificationService notificationService)
    {
        var result = await JoinWaitingListAsync(userId, travelPackageId);
        
        if (result.Success && result.Entry != null)
        {

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                await notificationService.SendWaitingListConfirmationAsync(
                    user.Email,
                    $"{user.FirstName} {user.LastName}",
                    travelPackageId,
                    result.Entry.Position);
                
                _logger.LogInformation($"Sent waiting list confirmation email to user {userId} for package {travelPackageId}");
            }
        }
        
        return result;
    }

    public async Task<bool> IsUserInWaitingListAsync(string userId, int travelPackageId)
    {
        return await _context.WaitingListEntries
            .AnyAsync(w => w.UserId == userId && w.TravelPackageId == travelPackageId && w.IsActive);
    }

    public async Task<int> GetWaitingListCountAsync(int travelPackageId)
    {
        return await _context.WaitingListEntries
            .CountAsync(w => w.TravelPackageId == travelPackageId && w.IsActive);
    }

    public async Task<int> GetUserPositionAsync(string userId, int travelPackageId)
    {
        var entry = await _context.WaitingListEntries
            .FirstOrDefaultAsync(w => w.UserId == userId && w.TravelPackageId == travelPackageId && w.IsActive);

        return entry?.Position ?? 0;
    }

    public async Task<TimeSpan?> EstimateWaitTimeAsync(int travelPackageId, int position)
    {


        var recentCancellations = await _context.Bookings
            .Include(b => b.TravelPackage)
            .Where(b => b.TravelPackageId == travelPackageId && 
                       b.Status == BookingStatus.Cancelled &&
                       b.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            .CountAsync();

        if (recentCancellations == 0)
        {
            return null; // Cannot estimate
        }

        var estimatedDays = position * 7.0 / Math.Max(1, recentCancellations / 4.0);
        return TimeSpan.FromDays(Math.Min(estimatedDays, 90)); // Cap at 90 days
    }

    public async Task<List<WaitingListEntry>> GetNextInQueueAsync(int travelPackageId, int count = 1)
    {
        var now = DateTime.UtcNow;
        _logger.LogInformation($"GetNextInQueueAsync called for package {travelPackageId}, count={count}, current time={now:yyyy-MM-dd HH:mm:ss} UTC");
        
        // Get users who:
        // 1. Are active in waiting list
        // 2. Either haven't been notified yet, OR their notification has expired
        // 3. Ordered by position (first come, first served)
        // IMPORTANT: Only get position 1 first, then position 2, etc.
        var result = await _context.WaitingListEntries
            .Include(w => w.User)
            .Where(w => w.TravelPackageId == travelPackageId && 
                      w.IsActive && 
                      (!w.NotifiedAt.HasValue || 
                       (w.NotificationExpiresAt.HasValue && w.NotificationExpiresAt.Value <= now)))
            .OrderBy(w => w.Position)
            .Take(count)
            .ToListAsync();
        
        _logger.LogInformation($"GetNextInQueueAsync found {result.Count} eligible entry/entries:");
        foreach (var e in result)
        {
            var status = !e.NotifiedAt.HasValue 
                ? "Never notified" 
                : $"Previously notified, expired at {e.NotificationExpiresAt:yyyy-MM-dd HH:mm:ss} UTC";
            _logger.LogInformation($"  Position #{e.Position}: User {e.UserId} (Email: {e.User?.Email}), {status}");
        }
        
        return result;
    }

    public async Task NotifyNextInQueueAsync(int travelPackageId, INotificationService notificationService)
    {
        _logger.LogInformation($"=== NotifyNextInQueueAsync STARTED for package {travelPackageId} ===");
        
        // Check if there are rooms available first
        var (isFull, remainingRooms) = await CheckAvailabilityAsync(travelPackageId);
        _logger.LogInformation($"Package {travelPackageId} availability check: IsFull={isFull}, RemainingRooms={remainingRooms}");
        
        if (isFull || remainingRooms <= 0)
        {
            _logger.LogWarning($"No rooms available for package {travelPackageId}. Cannot notify waiting list. IsFull={isFull}, RemainingRooms={remainingRooms}");
            return;
        }

        // Get all active waiting list entries for debugging
        var allActiveEntries = await _context.WaitingListEntries
            .Include(w => w.User)
            .Where(w => w.TravelPackageId == travelPackageId && w.IsActive)
            .OrderBy(w => w.Position)
            .ToListAsync();
        
        _logger.LogInformation($"Found {allActiveEntries.Count} active waiting list entries for package {travelPackageId}:");
        foreach (var e in allActiveEntries)
        {
            var notifiedStatus = e.NotifiedAt.HasValue 
                ? $"Notified at {e.NotifiedAt:yyyy-MM-dd HH:mm:ss} UTC, Expires at {e.NotificationExpiresAt:yyyy-MM-dd HH:mm:ss} UTC" 
                : "Never notified";
            _logger.LogInformation($"  Position #{e.Position}: User {e.UserId} (Email: {e.User?.Email}), {notifiedStatus}");
        }

        var nextEntries = await GetNextInQueueAsync(travelPackageId, 1);
        _logger.LogInformation($"GetNextInQueueAsync returned {nextEntries.Count} entry/entries");
        
        if (!nextEntries.Any())
        {
            _logger.LogWarning($"No users in waiting list for package {travelPackageId} to notify. All users may have active notifications.");
            return;
        }

        var entry = nextEntries.First();
        var now = DateTime.UtcNow;
        
        _logger.LogInformation($"Selected user for notification: Position #{entry.Position}, UserId: {entry.UserId}, Email: {entry.User?.Email}");
        
        // Double check that this user hasn't been notified recently (shouldn't happen, but safety check)
        if (entry.NotifiedAt.HasValue && 
            entry.NotificationExpiresAt.HasValue && 
            entry.NotificationExpiresAt.Value > now)
        {
            _logger.LogWarning($"User {entry.UserId} already has active notification for package {travelPackageId}. Notification expires at {entry.NotificationExpiresAt:yyyy-MM-dd HH:mm:ss} UTC. Skipping.");
            return;
        }
        
        entry.NotifiedAt = now;
        entry.NotificationExpiresAt = now.AddMinutes(10); // 10 minutes to book
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Updated waiting list entry: NotifiedAt={entry.NotifiedAt:yyyy-MM-dd HH:mm:ss} UTC, NotificationExpiresAt={entry.NotificationExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");

        try
        {
            if (string.IsNullOrEmpty(entry.User?.Email))
            {
                _logger.LogError($"✗✗✗ User {entry.UserId} has no email address. Cannot send notification.");
                return;
            }

            _logger.LogInformation($"📧 Preparing to send email notification...");
            _logger.LogInformation($"  - User ID: {entry.UserId}");
            _logger.LogInformation($"  - User Email: {entry.User.Email}");
            _logger.LogInformation($"  - User Name: {entry.User.FirstName} {entry.User.LastName}");
            _logger.LogInformation($"  - Package ID: {travelPackageId}");
            _logger.LogInformation($"  - Position: #{entry.Position}");
            _logger.LogInformation($"  - Notification Expires At: {entry.NotificationExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
            
            _logger.LogInformation($"📧 Calling SendWaitingListNotificationAsync...");
            await notificationService.SendWaitingListNotificationAsync(
                entry.User.Email,
                $"{entry.User.FirstName} {entry.User.LastName}",
                travelPackageId,
                entry.Position);

            _logger.LogInformation($"✓✓✓ SUCCESS: Email notification sent successfully!");
            _logger.LogInformation($"  - User: {entry.UserId} (Email: {entry.User.Email})");
            _logger.LogInformation($"  - Position: #{entry.Position}");
            _logger.LogInformation($"  - Package: {travelPackageId}");
            _logger.LogInformation($"  - Notification Expires: {entry.NotificationExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");
            _logger.LogInformation($"  - Time Remaining: 10 minutes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"✗✗✗ ERROR: Failed to send waiting list notification email!");
            _logger.LogError($"  - User ID: {entry.UserId}");
            _logger.LogError($"  - User Email: {entry.User?.Email ?? "NULL"}");
            _logger.LogError($"  - Package ID: {travelPackageId}");
            _logger.LogError($"  - Exception Type: {ex.GetType().Name}");
            _logger.LogError($"  - Exception Message: {ex.Message}");
            _logger.LogError($"  - Stack Trace: {ex.StackTrace}");
            // Don't throw - we still want to mark them as notified even if email fails
            // The user can still see the notification in their waiting list page
        }
        
        _logger.LogInformation($"=== NotifyNextInQueueAsync COMPLETED for package {travelPackageId} ===");
    }

    public async Task RemoveExpiredNotificationsAsync(int travelPackageId, INotificationService notificationService)
    {
        var now = DateTime.UtcNow;

        // Find the first expired notification (should be position 1)
        // We process one at a time to ensure proper queue order
        var expiredEntry = await _context.WaitingListEntries
            .Include(w => w.User)
            .Where(w => w.TravelPackageId == travelPackageId &&
                       w.IsActive &&
                       w.NotifiedAt.HasValue &&
                       w.NotificationExpiresAt.HasValue &&
                       w.NotificationExpiresAt.Value <= now)
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync();

        if (expiredEntry == null)
        {
            return;
        }

        // Remove expired entry (user who didn't book within 10 minutes)
        var expiredPosition = expiredEntry.Position;
        expiredEntry.IsActive = false;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Removed expired waiting list entry for user {expiredEntry.UserId} (position #{expiredPosition}) in package {travelPackageId}. Notification expired at {expiredEntry.NotificationExpiresAt:yyyy-MM-dd HH:mm:ss} UTC");

        // Check if there are still rooms available and notify next user (now position 1)
        var (isFull, remainingRooms) = await CheckAvailabilityAsync(travelPackageId);
        if (!isFull && remainingRooms > 0)
        {
            _logger.LogInformation($"Rooms available ({remainingRooms}) after removing expired entry. Notifying next user (now position #1) in queue for package {travelPackageId}");
            await NotifyNextInQueueAsync(travelPackageId, notificationService);
        }
    }

    private async Task<(bool IsFull, int RemainingRooms)> CheckAvailabilityAsync(int travelPackageId)
    {
        // Use AsNoTracking to ensure we get fresh data from database
        var travelPackage = await _context.TravelPackages
            .AsNoTracking()
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);

        if (travelPackage == null)
        {
            _logger.LogWarning($"Package {travelPackageId} not found in CheckAvailabilityAsync");
            return (true, 0);
        }

        // Only count paid bookings (PaymentStatus == Paid) as booked rooms
        var bookedRooms = travelPackage.Bookings
            .Count(b => b.Status == BookingStatus.Active && b.PaymentStatus == PaymentStatus.Paid);

        var remainingRooms = travelPackage.AvailableRooms - bookedRooms;
        var isFull = remainingRooms <= 0;
        
        _logger.LogInformation($"CheckAvailabilityAsync for package {travelPackageId}: AvailableRooms={travelPackage.AvailableRooms}, BookedRooms={bookedRooms}, RemainingRooms={remainingRooms}, IsFull={isFull}");
        
        return (isFull, Math.Max(0, remainingRooms));
    }

    public async Task<(bool Success, string? ErrorMessage)> RemoveFromWaitingListAsync(int waitingListEntryId, string userId)
    {
        var entry = await _context.WaitingListEntries
            .Include(w => w.TravelPackage)
            .FirstOrDefaultAsync(w => w.Id == waitingListEntryId && w.UserId == userId);

        if (entry == null)
        {
            return (false, "Waiting list entry not found or you don't have permission to remove it.");
        }

        if (!entry.IsActive)
        {
            return (false, "This entry is already removed from the waiting list.");
        }

        var travelPackageId = entry.TravelPackageId;
        var position = entry.Position;

        entry.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {userId} removed from waiting list entry {waitingListEntryId} for package {travelPackageId}");

        var remainingEntries = await _context.WaitingListEntries
            .Where(w => w.TravelPackageId == travelPackageId && 
                       w.IsActive && 
                       w.Position > position)
            .OrderBy(w => w.Position)
            .ToListAsync();

        if (remainingEntries.Any())
        {
            int newPosition = position;
            foreach (var remainingEntry in remainingEntries)
            {
                remainingEntry.Position = newPosition;
                newPosition++;
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Reordered {remainingEntries.Count} waiting list entries for package {travelPackageId}");
        }

        return (true, null);
    }
}
