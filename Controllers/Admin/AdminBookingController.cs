using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Services;

namespace TripWings.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class AdminBookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBookingService _bookingService;
    private readonly IWaitingListService _waitingListService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AdminBookingController> _logger;

    public AdminBookingController(
        ApplicationDbContext context,
        IBookingService bookingService,
        IWaitingListService waitingListService,
        INotificationService notificationService,
        ILogger<AdminBookingController> logger)
    {
        _context = context;
        _bookingService = bookingService;
        _waitingListService = waitingListService;
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var bookings = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.TravelPackage)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View("~/Views/AdminBooking/Index.cshtml", bookings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        var wasActive = booking.Status == BookingStatus.Active;
        booking.Status = BookingStatus.Cancelled;
        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation($"✓ Booking cancelled successfully: ID={id}, UserId={booking.UserId}, PackageId={booking.TravelPackageId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"✗ Failed to cancel booking in database: {ex.Message}");
            TempData["Error"] = "An error occurred while cancelling the booking. Please try again.";
            return RedirectToAction("Index");
        }

        if (wasActive)
        {

            var (isFull, _) = await _bookingService.CheckAvailabilityAsync(booking.TravelPackageId);
            if (!isFull)
            {
                await _waitingListService.NotifyNextInQueueAsync(booking.TravelPackageId, _notificationService);
                TempData["Success"] = "Booking cancelled and next in waiting list has been notified.";
            }
            else
            {
                TempData["Success"] = "Booking cancelled successfully.";
            }
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyWaitingList(int travelPackageId)
    {
        var (isFull, _) = await _bookingService.CheckAvailabilityAsync(travelPackageId);
        if (isFull)
        {
            TempData["Error"] = "Package is still full. Cannot notify waiting list.";
            return RedirectToAction("Index");
        }

        await _waitingListService.NotifyNextInQueueAsync(travelPackageId, _notificationService);
        TempData["Success"] = "Next in waiting list has been notified.";
        return RedirectToAction("Index");
    }
}
