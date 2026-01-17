using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;
using TripWings.Services;

namespace TripWings.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class AdminTravelPackageController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminTravelPackageController> _logger;

    public AdminTravelPackageController(ApplicationDbContext context, ILogger<AdminTravelPackageController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var packages = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View("~/Views/AdminTravelPackage/Index.cshtml", packages);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("~/Views/AdminTravelPackage/Create.cshtml", new AdminTravelPackageViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminTravelPackageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/AdminTravelPackage/Create.cshtml", model);
        }

        if (model.EndDate <= model.StartDate)
        {
            ModelState.AddModelError("EndDate", "תאריך הסיום חייב להיות אחרי תאריך ההתחלה");
            return View("~/Views/AdminTravelPackage/Create.cshtml", model);
        }

        if (model.AvailableRooms > model.TotalRooms)
        {
            ModelState.AddModelError("AvailableRooms", "מספר החדרים הזמינים לא יכול להיות גדול ממספר החדרים הכולל");
            return View("~/Views/AdminTravelPackage/Create.cshtml", model);
        }

        var package = new TravelPackage
        {
            Destination = model.Destination,
            Country = model.Country,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            Price = model.Price,
            TotalRooms = model.TotalRooms,
            AvailableRooms = model.AvailableRooms,
            PackageType = model.PackageType,
            AgeLimit = model.AgeLimit,
            Description = model.Description,
            IsVisible = model.IsVisible,
            CreatedAt = DateTime.UtcNow
        };

        _context.TravelPackages.Add(package);
        await _context.SaveChangesAsync();

        if (model.AddDiscount && model.DiscountOldPrice.HasValue && model.DiscountNewPrice.HasValue && 
            model.DiscountStartAt.HasValue && model.DiscountEndAt.HasValue)
        {
            if (model.DiscountNewPrice >= model.DiscountOldPrice)
            {
                ModelState.AddModelError("DiscountNewPrice", "New price must be less than old price.");
                return View("~/Views/AdminTravelPackage/Create.cshtml", model);
            }

            var discountDuration = (model.DiscountEndAt.Value - model.DiscountStartAt.Value).TotalDays;
            if (discountDuration > 7)
            {
                ModelState.AddModelError("DiscountEndAt", "Discount duration cannot exceed 7 days.");
                return View("~/Views/AdminTravelPackage/Create.cshtml", model);
            }

            if (model.DiscountEndAt <= model.DiscountStartAt)
            {
                ModelState.AddModelError("DiscountEndAt", "End date must be after start date.");
                return View("~/Views/AdminTravelPackage/Create.cshtml", model);
            }

            var currentTime = DateTime.UtcNow;
            var startAt = model.DiscountStartAt.Value > currentTime ? currentTime : model.DiscountStartAt.Value;
            var endAt = model.DiscountEndAt.Value;

            if (endAt <= startAt)
            {
                endAt = startAt.AddDays(7);
            }

            var discount = new Discount
            {
                TravelPackageId = package.Id,
                OldPrice = model.DiscountOldPrice.Value,
                NewPrice = model.DiscountNewPrice.Value,
                StartAt = startAt,
                EndAt = endAt
            };

            _context.Discounts.Add(discount);
            await _context.SaveChangesAsync();
        }

        if (model.AvailableRooms > 0)
        {
            var waitingListService = HttpContext.RequestServices.GetRequiredService<IWaitingListService>();
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
            await waitingListService.NotifyNextInQueueAsync(package.Id, notificationService);
        }

        if (!string.IsNullOrWhiteSpace(model.ImageUrls))
        {
            var imageUrls = model.ImageUrls
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(url => url.Trim())
                .Where(url => !string.IsNullOrWhiteSpace(url) && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .ToList();

            if (imageUrls.Any())
            {
                var packageImages = imageUrls.Select(url => new PackageImage
                {
                    TravelPackageId = package.Id,
                    ImageUrl = url
                }).ToList();

                _context.PackageImages.AddRange(packageImages);
                await _context.SaveChangesAsync();
            }
        }

        TempData["Success"] = "החבילה נוספה בהצלחה!";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var package = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (package == null)
        {
            return NotFound();
        }

        var model = new AdminTravelPackageViewModel
        {
            Id = package.Id,
            Destination = package.Destination,
            Country = package.Country,
            StartDate = package.StartDate,
            EndDate = package.EndDate,
            Price = package.Price,
            TotalRooms = package.TotalRooms,
            AvailableRooms = package.AvailableRooms,
            PackageType = package.PackageType,
            AgeLimit = package.AgeLimit,
            Description = package.Description,
            IsVisible = package.IsVisible,
            ImageUrlList = package.PackageImages.Select(img => img.ImageUrl).ToList()
        };

        return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminTravelPackageViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
        }

        if (model.EndDate <= model.StartDate)
        {
            ModelState.AddModelError("EndDate", "תאריך הסיום חייב להיות אחרי תאריך ההתחלה");
            return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
        }

        var package = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (package == null)
        {
            return NotFound();
        }

        var oldAvailableRooms = package.AvailableRooms;
        var oldRemainingRooms = package.AvailableRooms - package.Bookings.Count(b => b.Status == BookingStatus.Active);

        package.Destination = model.Destination;
        package.Country = model.Country;
        package.StartDate = model.StartDate;
        package.EndDate = model.EndDate;
        package.Price = model.Price;
        package.TotalRooms = model.TotalRooms;
        package.AvailableRooms = model.AvailableRooms;
        package.PackageType = model.PackageType;
        package.AgeLimit = model.AgeLimit;
        package.Description = model.Description;
        package.IsVisible = model.IsVisible;

        if (!string.IsNullOrWhiteSpace(model.ImageUrls))
        {
            var newImageUrls = model.ImageUrls
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(url => url.Trim())
                .Where(url => !string.IsNullOrWhiteSpace(url) && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .ToList();

            var oldImages = package.PackageImages.ToList();
            _context.PackageImages.RemoveRange(oldImages);

            if (newImageUrls.Any())
            {
                var packageImages = newImageUrls.Select(url => new PackageImage
                {
                    TravelPackageId = package.Id,
                    ImageUrl = url
                }).ToList();

                _context.PackageImages.AddRange(packageImages);
            }
        }

        await _context.SaveChangesAsync();

        if (model.AddDiscount && model.DiscountOldPrice.HasValue && model.DiscountNewPrice.HasValue && 
            model.DiscountStartAt.HasValue && model.DiscountEndAt.HasValue)
        {
            if (model.DiscountNewPrice >= model.DiscountOldPrice)
            {
                ModelState.AddModelError("DiscountNewPrice", "New price must be less than old price.");
                return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
            }

            var discountDuration = (model.DiscountEndAt.Value - model.DiscountStartAt.Value).TotalDays;
            if (discountDuration > 7)
            {
                ModelState.AddModelError("DiscountEndAt", "Discount duration cannot exceed 7 days.");
                return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
            }

            if (model.DiscountEndAt <= model.DiscountStartAt)
            {
                ModelState.AddModelError("DiscountEndAt", "End date must be after start date.");
                return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
            }

            var existingDiscount = await _context.Discounts
                .FirstOrDefaultAsync(d => d.TravelPackageId == package.Id && 
                                         d.StartAt <= DateTime.UtcNow && 
                                         d.EndAt >= DateTime.UtcNow);

            if (existingDiscount != null)
            {
                var currentTime = DateTime.UtcNow;
                var startAt = model.DiscountStartAt.Value > currentTime ? currentTime : model.DiscountStartAt.Value;
                var endAt = model.DiscountEndAt.Value;

                if (endAt <= startAt)
                {
                    endAt = startAt.AddDays(7);
                }

                existingDiscount.OldPrice = model.DiscountOldPrice.Value;
                existingDiscount.NewPrice = model.DiscountNewPrice.Value;
                existingDiscount.StartAt = startAt;
                existingDiscount.EndAt = endAt;
            }
            else
            {
                var currentTime = DateTime.UtcNow;
                var startAt = model.DiscountStartAt.Value > currentTime ? currentTime : model.DiscountStartAt.Value;
                var endAt = model.DiscountEndAt.Value;

                if (endAt <= startAt)
                {
                    endAt = startAt.AddDays(7);
                }

                var discount = new Discount
                {
                    TravelPackageId = package.Id,
                    OldPrice = model.DiscountOldPrice.Value,
                    NewPrice = model.DiscountNewPrice.Value,
                    StartAt = startAt,
                    EndAt = endAt
                };

                _context.Discounts.Add(discount);
            }

            await _context.SaveChangesAsync();
        }

        var newRemainingRooms = model.AvailableRooms - package.Bookings.Count(b => b.Status == BookingStatus.Active);
        if (model.AvailableRooms > oldAvailableRooms && newRemainingRooms > 0)
        {

            var waitingListService = HttpContext.RequestServices.GetRequiredService<IWaitingListService>();
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
            await waitingListService.NotifyNextInQueueAsync(package.Id, notificationService);
            _logger.LogInformation($"AvailableRooms increased for package {package.Id} from {oldAvailableRooms} to {model.AvailableRooms}. Notified waiting list.");
        }

        TempData["Success"] = "החבילה עודכנה בהצלחה!";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var package = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (package == null)
        {
            return NotFound();
        }

        return View("~/Views/AdminTravelPackage/Delete.cshtml", package);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var package = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (package == null)
        {
            return NotFound();
        }

        var hasActiveBookings = await _context.Bookings
            .AnyAsync(b => b.TravelPackageId == id && b.Status == BookingStatus.Active);

        if (hasActiveBookings)
        {
            TempData["Error"] = "לא ניתן למחוק חבילה שיש לה הזמנות פעילות!";
            return RedirectToAction("Index");
        }

        _context.TravelPackages.Remove(package);
        await _context.SaveChangesAsync();

        TempData["Success"] = "החבילה נמחקה בהצלחה!";
        return RedirectToAction("Index");
    }
}
