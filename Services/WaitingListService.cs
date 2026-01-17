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
        return await _context.WaitingListEntries
            .Include(w => w.User)
            .Where(w => w.TravelPackageId == travelPackageId && 
                      w.IsActive && 
                      (!w.NotifiedAt.HasValue || 
                       (w.NotificationExpiresAt.HasValue && w.NotificationExpiresAt.Value < now)))
            .OrderBy(w => w.Position)
            .Take(count)
            .ToListAsync();
    }

    public async Task NotifyNextInQueueAsync(int travelPackageId, INotificationService notificationService)
    {
        var nextEntries = await GetNextInQueueAsync(travelPackageId, 1);
        
        if (!nextEntries.Any())
        {
            return;
        }

        var entry = nextEntries.First();
        var now = DateTime.UtcNow;
        entry.NotifiedAt = now;
        entry.NotificationExpiresAt = now.AddMinutes(10); // 10 minutes to book
        await _context.SaveChangesAsync();

        await notificationService.SendWaitingListNotificationAsync(
            entry.User.Email!,
            $"{entry.User.FirstName} {entry.User.LastName}",
            travelPackageId,
            entry.Position);

        _logger.LogInformation($"Notified user {entry.UserId} about available room in package {travelPackageId}. Expires at {entry.NotificationExpiresAt}");
    }

    public async Task RemoveExpiredNotificationsAsync(int travelPackageId, INotificationService notificationService)
    {
        var now = DateTime.UtcNow;

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

        expiredEntry.IsActive = false;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Removed expired waiting list entry for user {expiredEntry.UserId} in package {travelPackageId}");

        var (isFull, remainingRooms) = await CheckAvailabilityAsync(travelPackageId);
        if (!isFull && remainingRooms > 0)
        {

            await NotifyNextInQueueAsync(travelPackageId, notificationService);
        }
    }

    private async Task<(bool IsFull, int RemainingRooms)> CheckAvailabilityAsync(int travelPackageId)
    {
        var travelPackage = await _context.TravelPackages
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);

        if (travelPackage == null)
        {
            return (true, 0);
        }

        var bookedRooms = travelPackage.Bookings
            .Count(b => b.Status == BookingStatus.Active);

        var remainingRooms = travelPackage.AvailableRooms - bookedRooms;
        return (remainingRooms <= 0, Math.Max(0, remainingRooms));
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
