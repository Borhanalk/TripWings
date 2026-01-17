using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;

namespace TripWings.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {

        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "AdminDashboard");
        }

        var featuredTrips = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .Include(t => t.Discounts)
            .Include(t => t.Bookings) // Include bookings to calculate RemainingRooms correctly
            .Where(t => t.IsVisible)
            .OrderByDescending(t => t.CreatedAt)
            .Take(6)
            .ToListAsync();

        var totalTripsCount = await _context.TravelPackages
            .Where(t => t.IsVisible)
            .CountAsync();

        ViewBag.TotalTripsCount = totalTripsCount;

        return View(featuredTrips);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
