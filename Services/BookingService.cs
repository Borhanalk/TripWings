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

        var upcomingBookingsCount = await _context.Bookings
            .Include(b => b.TravelPackage)
            .Where(b => b.UserId == userId && 
                       b.Status == BookingStatus.Active && 
                       b.TravelPackage.StartDate > DateTime.UtcNow)
            .CountAsync();

        if (upcomingBookingsCount >= 3)
        {
            return (false, "You have reached the maximum limit of 3 upcoming bookings. Please complete or cancel existing bookings first.");
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

        var bookedRooms = travelPackage.Bookings
            .Count(b => b.Status == BookingStatus.Active);

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

            var bookedRooms = travelPackage.Bookings
                .Count(b => b.Status == BookingStatus.Active);

            var remainingRooms = travelPackage.AvailableRooms - bookedRooms;

            if (roomsCount > remainingRooms)
            {
                await transaction.RollbackAsync();
                return (false, $"Only {remainingRooms} room(s) available. You requested {roomsCount} room(s).", null);
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

            var waitingListEntry = await _context.WaitingListEntries
                .FirstOrDefaultAsync(w => w.UserId == userId && 
                                         w.TravelPackageId == travelPackageId && 
                                         w.IsActive &&
                                         w.NotifiedAt.HasValue);
            
            if (waitingListEntry != null)
            {
                waitingListEntry.IsActive = false;
                _logger.LogInformation($"Removed user {userId} from waiting list for package {travelPackageId} after successful booking");
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
}
