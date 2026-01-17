using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;

namespace TripWings.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CartController> _logger;

    public CartController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CartController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى عربة التسوق.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var cartItems = await _context.CartItems
            .Include(c => c.TravelPackage)
            .Where(c => c.UserId == user.Id)
            .ToListAsync();

        decimal total = 0;
        foreach (var item in cartItems)
        {

            var now = DateTime.UtcNow;
            var activeDiscount = item.TravelPackage.Discounts
                .FirstOrDefault(d => d.StartAt <= now && d.EndAt >= now && (d.EndAt - d.StartAt).TotalDays <= 7);
            
            var price = activeDiscount?.NewPrice ?? item.TravelPackage.Price;
            total += price * item.Quantity;
        }

        ViewBag.Total = total;
        return View(cartItems);
    }

    [HttpGet]
    public async Task<IActionResult> AddToCart(int travelPackageId)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى عربة التسوق.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var travelPackage = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .Include(t => t.Discounts)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);

        if (travelPackage == null)
        {
            TempData["Error"] = "החבילה לא נמצאה.";
            return RedirectToAction("Gallery", "Trips");
        }

        if (!travelPackage.IsAvailable)
        {
            TempData["Error"] = "החבילה לא זמינה כרגע.";
            return RedirectToAction("Details", "Trips", new { id = travelPackageId });
        }

        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.TravelPackageId == travelPackageId);

        ViewBag.ExistingQuantity = existingItem?.Quantity ?? 0;
        return View(travelPackage);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int travelPackageId, int quantity = 1)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى عربة التسوق.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var travelPackage = await _context.TravelPackages.FindAsync(travelPackageId);
        if (travelPackage == null || !travelPackage.IsAvailable)
        {
            TempData["Error"] = "החבילה לא זמינה.";
            return RedirectToAction("Details", "Trips", new { id = travelPackageId });
        }

        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.TravelPackageId == travelPackageId);

        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            var cartItem = new CartItem
            {
                UserId = user.Id,
                TravelPackageId = travelPackageId,
                Quantity = quantity
            };
            _context.CartItems.Add(cartItem);
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "הפריט נוסף לעגלה בהצלחה!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromCart(int id)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى عربة التسوق.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (cartItem != null)
        {
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
            TempData["Success"] = "הפריט הוסר מהעגלה.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int id, int quantity)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى عربة التسوق.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (cartItem != null)
        {
            if (quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
            }
            else
            {
                cartItem.Quantity = quantity;
            }
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Checkout()
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى عربة التسوق.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var cartItems = await _context.CartItems
            .Include(c => c.TravelPackage)
                .ThenInclude(t => t.Discounts)
            .Where(c => c.UserId == user.Id)
            .ToListAsync();

        if (!cartItems.Any())
        {
            TempData["Error"] = "העגלה שלך ריקה.";
            return RedirectToAction(nameof(Index));
        }

        decimal total = 0;
        var now = DateTime.UtcNow;
        foreach (var item in cartItems)
        {
            var activeDiscount = item.TravelPackage.Discounts
                .FirstOrDefault(d => d.StartAt <= now && d.EndAt >= now && (d.EndAt - d.StartAt).TotalDays <= 7);
            
            var price = activeDiscount?.NewPrice ?? item.TravelPackage.Price;
            total += price * item.Quantity;
        }

        var viewModel = new CheckoutViewModel
        {
            CartItems = cartItems,
            TotalAmount = total
        };

        return View(viewModel);
    }
}
