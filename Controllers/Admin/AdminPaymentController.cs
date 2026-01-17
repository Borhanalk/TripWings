using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class AdminPaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminPaymentController> _logger;

    public AdminPaymentController(
        ApplicationDbContext context,
        ILogger<AdminPaymentController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var payments = await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Booking)
                .ThenInclude(b => b.TravelPackage)
            .Include(p => p.Discount)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        return View("~/Views/AdminPayment/Index.cshtml", payments);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Booking)
                .ThenInclude(b => b.TravelPackage)
            .Include(p => p.Discount)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            return NotFound();
        }

        return View("~/Views/AdminPayment/Details.cshtml", payment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            TempData["Error"] = "התשלום לא נמצא.";
            return RedirectToAction("Index");
        }

        if (payment.Status == PaymentStatus.Refunded)
        {
            TempData["Error"] = "התשלום כבר הוחזר.";
            return RedirectToAction("Details", new { id });
        }

        if (payment.Status != PaymentStatus.Paid)
        {
            TempData["Error"] = "ניתן להחזיר רק תשלומים ששולמו.";
            return RedirectToAction("Details", new { id });
        }

        payment.Status = PaymentStatus.Refunded;
        
        if (payment.Booking != null)
        {
            payment.Booking.PaymentStatus = PaymentStatus.Refunded;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "התשלום הוחזר בהצלחה.";
        _logger.LogInformation($"Payment {id} refunded by admin {User.Identity?.Name}");
        
        return RedirectToAction("Details", new { id });
    }
}
