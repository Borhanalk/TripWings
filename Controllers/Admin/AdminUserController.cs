using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;

namespace TripWings.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class AdminUserController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminUserController> _logger;

    public AdminUserController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminUserController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var userViewModels = new List<AdminUserViewModel>();
        foreach (var user in users)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var bookingsCount = await _context.Bookings.CountAsync(b => b.UserId == user.Id);
            var paymentsCount = await _context.Payments.CountAsync(p => p.UserId == user.Id);
            var totalSpent = await _context.Payments
                .Where(p => p.UserId == user.Id && p.Status == PaymentStatus.Paid)
                .SumAsync(p => (decimal?)p.FinalAmount) ?? 0;

            userViewModels.Add(new AdminUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                CreatedAt = user.CreatedAt,
                IsAdmin = isAdmin,
                BookingsCount = bookingsCount,
                PaymentsCount = paymentsCount,
                TotalSpent = totalSpent
            });
        }

        return View("~/Views/AdminUser/Index.cshtml", userViewModels);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        var bookings = await _context.Bookings
            .Include(b => b.TravelPackage)
            .Where(b => b.UserId == user.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var payments = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.TravelPackage)
            .Where(p => p.UserId == user.Id)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var totalSpent = payments
            .Where(p => p.Status == PaymentStatus.Paid)
            .Sum(p => p.FinalAmount);

        var viewModel = new AdminUserDetailsViewModel
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FirstName = user.FirstName ?? "",
            LastName = user.LastName ?? "",
            CreatedAt = user.CreatedAt,
            IsAdmin = isAdmin,
            Bookings = bookings,
            Payments = payments,
            TotalSpent = totalSpent
        };

        return View("~/Views/AdminUser/Details.cshtml", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["Error"] = "המשתמש לא נמצא.";
            return RedirectToAction("Index");
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        
        if (isAdmin)
        {
            await _userManager.RemoveFromRoleAsync(user, "Admin");
            TempData["Success"] = "המשתמש הוסר מתפקיד המנהל.";
        }
        else
        {
            await _userManager.AddToRoleAsync(user, "Admin");
            TempData["Success"] = "המשתמש נוסף לתפקיד המנהל.";
        }

        _logger.LogInformation($"User {user.Email} admin status toggled by {User.Identity?.Name}");
        return RedirectToAction("Details", new { id });
    }
}
