using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;

namespace TripWings.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class AdminDiscountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminDiscountController> _logger;

    public AdminDiscountController(
        ApplicationDbContext context,
        ILogger<AdminDiscountController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var discounts = await _context.Discounts
            .Include(d => d.TravelPackage)
            .OrderByDescending(d => d.StartAt)
            .ToListAsync();

        return View(discounts);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var packages = await _context.TravelPackages
            .Where(t => t.IsVisible)
            .OrderBy(t => t.Destination)
            .ToListAsync();

        ViewBag.TravelPackages = packages;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminDiscountViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        if (model.NewPrice >= model.OldPrice)
        {
            ModelState.AddModelError("NewPrice", "New price must be less than old price.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        var duration = (model.EndAt - model.StartAt).TotalDays;
        if (duration > 7)
        {
            ModelState.AddModelError("EndAt", "Discount duration cannot exceed 7 days.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        if (model.EndAt <= model.StartAt)
        {
            ModelState.AddModelError("EndAt", "End date must be after start date.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        var travelPackage = await _context.TravelPackages.FindAsync(model.TravelPackageId);
        if (travelPackage == null)
        {
            ModelState.AddModelError("TravelPackageId", "Travel package not found.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        if (model.OldPrice != travelPackage.Price)
        {
            travelPackage.Price = model.OldPrice;
        }

        var currentTime = DateTime.UtcNow;
        var startAt = model.StartAt > currentTime ? currentTime : model.StartAt;
        var endAt = model.EndAt;

        if (endAt <= startAt)
        {
            endAt = startAt.AddDays(7);
        }

        var discount = new Discount
        {
            TravelPackageId = model.TravelPackageId,
            OldPrice = model.OldPrice,
            NewPrice = model.NewPrice,
            StartAt = startAt,
            EndAt = endAt
        };

        _context.Discounts.Add(discount);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Discount created successfully.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var discount = await _context.Discounts
            .Include(d => d.TravelPackage)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (discount == null)
        {
            return NotFound();
        }

        var packages = await _context.TravelPackages
            .Where(t => t.IsVisible)
            .OrderBy(t => t.Destination)
            .ToListAsync();

        ViewBag.TravelPackages = packages;

        var model = new AdminDiscountViewModel
        {
            Id = discount.Id,
            TravelPackageId = discount.TravelPackageId,
            OldPrice = discount.OldPrice,
            NewPrice = discount.NewPrice,
            StartAt = discount.StartAt,
            EndAt = discount.EndAt
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminDiscountViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        if (model.NewPrice >= model.OldPrice)
        {
            ModelState.AddModelError("NewPrice", "New price must be less than old price.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        var duration = (model.EndAt - model.StartAt).TotalDays;
        if (duration > 7)
        {
            ModelState.AddModelError("EndAt", "Discount duration cannot exceed 7 days.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        if (model.EndAt <= model.StartAt)
        {
            ModelState.AddModelError("EndAt", "End date must be after start date.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        var discount = await _context.Discounts.FindAsync(id);
        if (discount == null)
        {
            return NotFound();
        }

        var travelPackage = await _context.TravelPackages.FindAsync(model.TravelPackageId);
        if (travelPackage == null)
        {
            ModelState.AddModelError("TravelPackageId", "Travel package not found.");
            var packages = await _context.TravelPackages
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Destination)
                .ToListAsync();

            ViewBag.TravelPackages = packages;
            return View(model);
        }

        if (model.OldPrice != travelPackage.Price)
        {
            travelPackage.Price = model.OldPrice;
        }

        discount.TravelPackageId = model.TravelPackageId;
        var currentTime = DateTime.UtcNow;
        var startAt = model.StartAt > currentTime ? currentTime : model.StartAt;
        var endAt = model.EndAt;

        if (endAt <= startAt)
        {
            endAt = startAt.AddDays(7);
        }

        discount.OldPrice = model.OldPrice;
        discount.NewPrice = model.NewPrice;
        discount.StartAt = startAt;
        discount.EndAt = endAt;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Discount updated successfully.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var discount = await _context.Discounts
            .Include(d => d.TravelPackage)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (discount == null)
        {
            return NotFound();
        }

        return View(discount);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var discount = await _context.Discounts.FindAsync(id);
        if (discount == null)
        {
            return NotFound();
        }

        _context.Discounts.Remove(discount);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Discount deleted successfully.";
        return RedirectToAction("Index");
    }
}
