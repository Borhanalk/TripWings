using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;
using TripWings.Services;

namespace TripWings.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IItineraryService _itineraryService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IItineraryService itineraryService,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _userManager = userManager;
        _itineraryService = itineraryService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {

        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var now = DateTime.UtcNow;

        var bookings = await _context.Bookings
            .Include(b => b.TravelPackage)
            .Where(b => b.UserId == user.Id && b.Status == BookingStatus.Active)
            .OrderBy(b => b.TravelPackage.StartDate)
            .ToListAsync();

        var currentBookings = bookings
            .Where(b => b.TravelPackage.StartDate <= now && b.TravelPackage.EndDate >= now)
            .ToList();

        var upcomingBookings = bookings
            .Where(b => b.TravelPackage.StartDate > now)
            .Select(b => new UpcomingBookingViewModel
            {
                Booking = b,
                TimeUntilDeparture = b.TravelPackage.StartDate - now
            })
            .ToList();

        var cartItemsCount = await _context.CartItems
            .Where(c => c.UserId == user.Id)
            .CountAsync();

        var viewModel = new DashboardViewModel
        {
            UserName = $"{user.FirstName} {user.LastName}",
            CurrentBookings = currentBookings,
            UpcomingBookings = upcomingBookings,
            CartItemsCount = cartItemsCount
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadItinerary(int bookingId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
                .ThenInclude(t => t.PackageImages)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == user.Id);

        if (booking == null) return NotFound();

        try
        {
            var pdfBytes = await _itineraryService.GenerateItineraryPdfAsync(booking);
            var fileName = $"Itinerary_{booking.TravelPackage.Destination}_{booking.Id}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating itinerary PDF for booking {bookingId}");
            TempData["Error"] = "Error generating itinerary. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }
}
