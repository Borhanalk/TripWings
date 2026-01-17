using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;

namespace TripWings.Controllers;

[Authorize(Roles = "Admin")]
public class AdminDashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminDashboardController> _logger;

    public AdminDashboardController(ApplicationDbContext context, ILogger<AdminDashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var totalTrips = await _context.TravelPackages.CountAsync();
        var activeTrips = await _context.TravelPackages.CountAsync(t => t.IsVisible);
        var totalCartItems = await _context.CartItems.CountAsync();
        var totalUsers = await _context.Users.CountAsync();
        var totalBookings = await _context.Bookings.CountAsync();
        var totalPayments = await _context.Payments.CountAsync();
        var totalWaitingListEntries = await _context.WaitingListEntries.CountAsync(w => w.IsActive);
        var activeBookings = await _context.Bookings.CountAsync(b => b.Status == Models.BookingStatus.Active);
        var cancelledBookings = await _context.Bookings.CountAsync(b => b.Status == Models.BookingStatus.Cancelled);
        var paidPayments = await _context.Payments.CountAsync(p => p.Status == Models.PaymentStatus.Paid);
        var totalRevenue = await _context.Payments
            .Where(p => p.Status == Models.PaymentStatus.Paid)
            .SumAsync(p => (decimal?)p.FinalAmount) ?? 0;

        ViewBag.TotalTrips = totalTrips;
        ViewBag.ActiveTrips = activeTrips;
        ViewBag.TotalCartItems = totalCartItems;
        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalBookings = totalBookings;
        ViewBag.TotalPayments = totalPayments;
        ViewBag.TotalWaitingListEntries = totalWaitingListEntries;
        ViewBag.ActiveBookings = activeBookings;
        ViewBag.CancelledBookings = cancelledBookings;
        ViewBag.PaidPayments = paidPayments;
        ViewBag.TotalRevenue = totalRevenue;

        return View();
    }
}
