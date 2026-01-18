using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Services;

public interface IBookingService
{
    Task<(bool CanBook, string? ErrorMessage)> CanUserBookAsync(string userId, int travelPackageId, int roomsCount);
    Task<(bool Success, string? ErrorMessage, Booking? Booking)> CreateBookingAsync(string userId, int travelPackageId, int roomsCount);
    Task<(bool IsFull, int RemainingRooms)> CheckAvailabilityAsync(int travelPackageId);
    Task<(bool HasActiveNotification, string? NotifiedUserId)> HasActiveWaitingListNotificationAsync(int travelPackageId);
}

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BookingService> _logger;

    public BookingService(ApplicationDbContext context, ILogger<BookingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool CanBook, string? ErrorMessage)> CanUserBookAsync(string userId, int travelPackageId, int roomsCount)
    {

        var travelPackage = await _context.TravelPackages
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);

        if (travelPackage == null)
        {
            return (false, "Travel package not found.");
        }

        if (!travelPackage.IsVisible)
        {
            return (false, "This travel package is not available.");
        }

        if (travelPackage.EndDate <= DateTime.UtcNow)
        {
            return (false, "This travel package has already ended.");
        }

        // Only count paid upcoming bookings (PaymentStatus == Paid)
        var upcomingBookingsCount = await _context.Bookings
            .Include(b => b.TravelPackage)
            .Where(b => b.UserId == userId && 
                       b.Status == BookingStatus.Active && 
                       b.PaymentStatus == PaymentStatus.Paid &&
                       b.TravelPackage.StartDate > DateTime.UtcNow)
            .CountAsync();

        if (upcomingBookingsCount >= 3)
        {
            return (false, "You have reached the maximum limit of 3 upcoming paid bookings. Please complete or cancel existing bookings first.");
        }

        return (true, null);
    }

    public async Task<(bool IsFull, int RemainingRooms)> CheckAvailabilityAsync(int travelPackageId)
    {
        var travelPackage = await _context.TravelPackages
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);

        if (travelPackage == null)
        {
            return (true, 0);
        }

        // Only count paid bookings (PaymentStatus == Paid) as booked rooms
        var bookedRooms = travelPackage.Bookings
            .Count(b => b.Status == BookingStatus.Active && b.PaymentStatus == PaymentStatus.Paid);

        var remainingRooms = travelPackage.AvailableRooms - bookedRooms;
        return (remainingRooms <= 0, Math.Max(0, remainingRooms));
    }

    public async Task<(bool Success, string? ErrorMessage, Booking? Booking)> CreateBookingAsync(string userId, int travelPackageId, int roomsCount)
    {

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {

            var travelPackage = await _context.TravelPackages
                .Include(t => t.Bookings)
                .FirstOrDefaultAsync(t => t.Id == travelPackageId);

            if (travelPackage == null)
            {
                return (false, "Travel package not found.", null);
            }

            if (!travelPackage.IsVisible)
            {
                return (false, "This travel package is not available.", null);
            }

            // Only count paid bookings (PaymentStatus == Paid) as booked rooms
            var bookedRooms = travelPackage.Bookings
                .Count(b => b.Status == BookingStatus.Active && b.PaymentStatus == PaymentStatus.Paid);

            var remainingRooms = travelPackage.AvailableRooms - bookedRooms;

            if (roomsCount > remainingRooms)
            {
                await transaction.RollbackAsync();
                return (false, $"Only {remainingRooms} room(s) available. You requested {roomsCount} room(s).", null);
            }

            // Check if package is full
            if (remainingRooms <= 0)
            {
                // Get the first user in waiting list (position 1) who has valid notification
                var firstInQueue = await _context.WaitingListEntries
                    .Where(w => w.TravelPackageId == travelPackageId && 
                              w.IsActive &&
                              w.Position == 1 &&
                              w.NotifiedAt.HasValue &&
                              w.NotificationExpiresAt.HasValue &&
                              w.NotificationExpiresAt.Value > DateTime.UtcNow)
                    .FirstOrDefaultAsync();
                
                // Check if current user is the first in queue with valid notification
                if (firstInQueue == null || firstInQueue.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    
                    // Check if user is in waiting list but not first
                    var userInWaitingList = await _context.WaitingListEntries
                        .FirstOrDefaultAsync(w => w.UserId == userId && 
                                                 w.TravelPackageId == travelPackageId && 
                                                 w.IsActive);
                    
                    if (userInWaitingList != null)
                    {
                        if (userInWaitingList.Position > 1)
                        {
                            return (false, $"This package is full. You are position #{userInWaitingList.Position} in the waiting list. Only position #1 can book when a room becomes available.", null);
                        }
                        else if (!userInWaitingList.NotifiedAt.HasValue || 
                                !userInWaitingList.NotificationExpiresAt.HasValue ||
                                userInWaitingList.NotificationExpiresAt.Value <= DateTime.UtcNow)
                        {
                            return (false, "This package is full. Your notification has expired. Please wait for the next available room.", null);
                        }
                    }
                    
                    return (false, "This package is full. You need to join the waiting list and wait for notification to book. Only position #1 in the waiting list can book when a room becomes available.", null);
                }
                
                // User is first in queue with valid notification, allow booking
                _logger.LogInformation($"User {userId} (position #1) booking with valid waiting list notification for package {travelPackageId}");
            }
            else
            {
                // Package has rooms available, but check if there are users in waiting list
                // If there are users in waiting list, only the first one with valid notification can book
                var firstInQueue = await _context.WaitingListEntries
                    .Where(w => w.TravelPackageId == travelPackageId && 
                              w.IsActive &&
                              w.Position == 1 &&
                              w.NotifiedAt.HasValue &&
                              w.NotificationExpiresAt.HasValue &&
                              w.NotificationExpiresAt.Value > DateTime.UtcNow)
                    .FirstOrDefaultAsync();
                
                if (firstInQueue != null && firstInQueue.UserId != userId)
                {
                    // There's a user in position 1 with valid notification, only they can book
                    await transaction.RollbackAsync();
                    
                    var userInWaitingList = await _context.WaitingListEntries
                        .FirstOrDefaultAsync(w => w.UserId == userId && 
                                                 w.TravelPackageId == travelPackageId && 
                                                 w.IsActive);
                    
                    if (userInWaitingList != null && userInWaitingList.Position > 1)
                    {
                        return (false, $"A room is available, but user #1 in the waiting list has priority. You are position #{userInWaitingList.Position}. Please wait for your turn.", null);
                    }
                    
                    return (false, "A room is available, but user #1 in the waiting list has priority. Please wait for your turn or join the waiting list.", null);
                }
            }

            var canBookResult = await CanUserBookAsync(userId, travelPackageId, roomsCount);
            if (!canBookResult.CanBook)
            {
                await transaction.RollbackAsync();
                return (false, canBookResult.ErrorMessage, null);
            }

            var booking = new Booking
            {
                UserId = userId,
                TravelPackageId = travelPackageId,
                RoomsCount = roomsCount,
                Status = BookingStatus.Active,
                PaymentStatus = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Remove user from waiting list after successful booking
            var waitingListEntry = await _context.WaitingListEntries
                .FirstOrDefaultAsync(w => w.UserId == userId && 
                                         w.TravelPackageId == travelPackageId && 
                                         w.IsActive);
            
            if (waitingListEntry != null)
            {
                waitingListEntry.IsActive = false;
                _logger.LogInformation($"Removed user {userId} from waiting list (position {waitingListEntry.Position}) for package {travelPackageId} after successful booking");
            }
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation($"Booking created successfully: User {userId}, Package {travelPackageId}, Rooms {roomsCount}");
            return (true, null, booking);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, $"Concurrency conflict while creating booking for user {userId}");
            return (false, "The room availability has changed. Please try again.", null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, $"Error creating booking for user {userId}");
            return (false, "An error occurred while creating your booking. Please try again.", null);
        }
    }

    public async Task<(bool HasActiveNotification, string? NotifiedUserId)> HasActiveWaitingListNotificationAsync(int travelPackageId)
    {
        var firstInQueue = await _context.WaitingListEntries
            .Where(w => w.TravelPackageId == travelPackageId && 
                      w.IsActive &&
                      w.Position == 1 &&
                      w.NotifiedAt.HasValue &&
                      w.NotificationExpiresAt.HasValue &&
                      w.NotificationExpiresAt.Value > DateTime.UtcNow)
            .FirstOrDefaultAsync();
        
        if (firstInQueue != null)
        {
            return (true, firstInQueue.UserId);
        }
        
        return (false, null);
    }
}
