using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;
using TripWings.Services;
using System.IO;

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
        // Include Bookings to calculate RemainingRooms correctly
        var packages = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .Include(t => t.Bookings)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        _logger.LogInformation($"Loaded {packages.Count} packages for admin index view");
        foreach (var package in packages)
        {
            // Only count paid bookings (PaymentStatus == Paid) as booked rooms
            var bookedRooms = package.Bookings.Count(b => b.Status == BookingStatus.Active && b.PaymentStatus == PaymentStatus.Paid);
            var remainingRooms = package.AvailableRooms - bookedRooms;
            _logger.LogInformation($"Package {package.Id} ({package.Destination}): AvailableRooms={package.AvailableRooms}, BookedRooms={bookedRooms}, RemainingRooms={remainingRooms}");
        }

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

        // Check if start date is at least one day from today
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        if (model.StartDate.Date < tomorrow)
        {
            ModelState.AddModelError("StartDate", "תאריך ההתחלה חייב להיות לפחות יום אחד מהיום (מתחיל ממחר) / Start date must be at least one day from today (starts from tomorrow)");
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
        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation($"✓ Package created successfully: ID={package.Id}, Destination={package.Destination}, AvailableRooms={package.AvailableRooms}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"✗ Failed to save package to database: {ex.Message}");
            ModelState.AddModelError("", "אירעה שגיאה בעת שמירת החבילה. אנא נסה שוב. / An error occurred while saving the package. Please try again.");
            return View("~/Views/AdminTravelPackage/Create.cshtml", model);
        }

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
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✓ Discount created successfully: ID={discount.Id}, PackageId={package.Id}, OldPrice={discount.OldPrice}, NewPrice={discount.NewPrice}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"✗ Failed to save discount to database: {ex.Message}");
                ModelState.AddModelError("", "אירעה שגיאה בעת שמירת ההנחה. אנא נסה שוב. / An error occurred while saving the discount. Please try again.");
                return View("~/Views/AdminTravelPackage/Create.cshtml", model);
            }
        }

        // After saving, check if there are rooms available and notify waiting list
        var bookedRooms = await _context.Bookings
            .CountAsync(b => b.TravelPackageId == package.Id && b.Status == BookingStatus.Active);
        var remainingRooms = package.AvailableRooms - bookedRooms;
        
        _logger.LogInformation($"=== CREATE PACKAGE: Package {package.Id} - AvailableRooms: {package.AvailableRooms}, BookedRooms: {bookedRooms}, RemainingRooms: {remainingRooms} ===");
        
        if (remainingRooms > 0)
        {
            _logger.LogInformation($"Package {package.Id} has {remainingRooms} remaining rooms. Attempting to notify waiting list...");
            var waitingListService = HttpContext.RequestServices.GetRequiredService<IWaitingListService>();
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
            await waitingListService.NotifyNextInQueueAsync(package.Id, notificationService);
            _logger.LogInformation($"Package {package.Id} created with {remainingRooms} available rooms (AvailableRooms: {package.AvailableRooms}, BookedRooms: {bookedRooms}). Waiting list notification process completed.");
        }
        else
        {
            _logger.LogInformation($"Package {package.Id} created but no rooms available (AvailableRooms: {package.AvailableRooms}, BookedRooms: {bookedRooms}). No waiting list notification sent.");
        }

        if (model.Images != null && model.Images.Count > 0)
        {
            if (model.Images.Count > 2)
            {
                ModelState.AddModelError("Images", "ניתן להעלות עד 2 תמונות בלבד. / You can upload up to 2 images only.");
                return View("~/Views/AdminTravelPackage/Create.cshtml", model);
            }

            var packageImages = new List<PackageImage>();
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "packages");
            
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            foreach (var imageFile in model.Images)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("Images", $"פורמט קובץ לא נתמך: {fileExtension}. פורמטים נתמכים: JPG, PNG, GIF / Unsupported file format: {fileExtension}. Supported formats: JPG, PNG, GIF");
                        return View("~/Views/AdminTravelPackage/Create.cshtml", model);
                    }

                    if (imageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("Images", $"הקובץ {imageFile.FileName} גדול מדי. גודל מקסימלי: 5MB / File {imageFile.FileName} is too large. Maximum size: 5MB");
                        return View("~/Views/AdminTravelPackage/Create.cshtml", model);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    var imageUrl = $"/images/packages/{uniqueFileName}";
                    packageImages.Add(new PackageImage
                    {
                        TravelPackageId = package.Id,
                        ImageUrl = imageUrl
                    });

                    _logger.LogInformation($"✓ Image uploaded successfully: {uniqueFileName}");
                }
            }

            if (packageImages.Any())
            {
                _context.PackageImages.AddRange(packageImages);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"✓ Package images saved successfully: PackageId={package.Id}, ImageCount={packageImages.Count}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"✗ Failed to save package images to database: {ex.Message}");
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrls))
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
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"✓ Package images saved successfully: PackageId={package.Id}, ImageCount={packageImages.Count}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"✗ Failed to save package images to database: {ex.Message}");
                }
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

        // Check if start date is at least one day from today
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        if (model.StartDate.Date < tomorrow)
        {
            ModelState.AddModelError("StartDate", "תאריך ההתחלה חייב להיות לפחות יום אחד מהיום (מתחיל ממחר) / Start date must be at least one day from today (starts from tomorrow)");
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
        // Only count paid bookings (PaymentStatus == Paid) as booked rooms
        var oldRemainingRooms = package.AvailableRooms - package.Bookings.Count(b => b.Status == BookingStatus.Active && b.PaymentStatus == PaymentStatus.Paid);

        _logger.LogInformation($"=== EDIT PACKAGE START: Package {id} ===");
        _logger.LogInformation($"Before: Destination={package.Destination}, AvailableRooms={package.AvailableRooms}, Price={package.Price}, IsVisible={package.IsVisible}");
        _logger.LogInformation($"After: Destination={model.Destination}, AvailableRooms={model.AvailableRooms}, Price={model.Price}, IsVisible={model.IsVisible}");

        // Update all package properties
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

        // Mark entity as modified to ensure EF tracks the changes
        _context.Entry(package).State = Microsoft.EntityFrameworkCore.EntityState.Modified;

        // Save package changes first
        try
        {
            var savedChanges = await _context.SaveChangesAsync();
            _logger.LogInformation($"✓✓✓ Package updated successfully in database: ID={package.Id}, SavedChanges={savedChanges}, Destination={package.Destination}, AvailableRooms={oldAvailableRooms} -> {model.AvailableRooms}, Price={package.Price}");
            
            // Detach and reload to ensure fresh data
            _context.Entry(package).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            var verifiedPackage = await _context.TravelPackages
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
            
            if (verifiedPackage != null)
            {
                _logger.LogInformation($"✓✓✓ Verified package in database: ID={verifiedPackage.Id}, Destination={verifiedPackage.Destination}, AvailableRooms={verifiedPackage.AvailableRooms}, Price={verifiedPackage.Price}, IsVisible={verifiedPackage.IsVisible}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"✗✗✗ Failed to save package changes to database: ID={package.Id}, Error={ex.Message}");
            ModelState.AddModelError("", "אירעה שגיאה בעת שמירת השינויים. אנא נסה שוב. / An error occurred while saving changes. Please try again.");
            return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
        }

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
            
            try
            {
                var savedChanges = await _context.SaveChangesAsync();
                _logger.LogInformation($"✓ Package images updated successfully: PackageId={package.Id}, ImageCount={newImageUrls.Count}, SavedChanges={savedChanges}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"✗ Failed to save package images to database: {ex.Message}");
                // Don't fail the entire operation if images fail, but log it
            }
        }

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

            try
            {
                var savedChanges = await _context.SaveChangesAsync();
                _logger.LogInformation($"✓ Discount saved successfully: PackageId={package.Id}, OldPrice={model.DiscountOldPrice.Value}, NewPrice={model.DiscountNewPrice.Value}, SavedChanges={savedChanges}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"✗ Failed to save discount to database: {ex.Message}");
                ModelState.AddModelError("", "אירעה שגיאה בעת שמירת ההנחה. אנא נסה שוב. / An error occurred while saving the discount. Please try again.");
                return View("~/Views/AdminTravelPackage/Edit.cshtml", model);
            }
        }

        // After saving, recalculate remaining rooms with fresh data from database
        // IMPORTANT: Reload package from database to ensure we have the latest saved data
        // Detach the current entity and reload to get fresh data
        _context.Entry(package).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var reloadedPackage = await _context.TravelPackages
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id);
        
        if (reloadedPackage == null)
        {
            _logger.LogError($"Failed to reload package {id} from database after update");
            TempData["Error"] = "אירעה שגיאה בעת עדכון החבילה. אנא נסה שוב. / An error occurred while updating the package. Please try again.";
            return RedirectToAction("Index");
        }
        
        package = reloadedPackage;
        
        // Only count paid bookings (PaymentStatus == Paid) as booked rooms
        var bookedRoomsAfterUpdate = package.Bookings.Count(b => b.Status == BookingStatus.Active && b.PaymentStatus == PaymentStatus.Paid);
        var newRemainingRooms = package.AvailableRooms - bookedRoomsAfterUpdate;
        var roomsIncreased = package.AvailableRooms > oldAvailableRooms;
        var wasFull = oldRemainingRooms <= 0;
        var nowHasRooms = newRemainingRooms > 0;
        
        _logger.LogInformation($"=== EDIT PACKAGE: Package {package.Id} ===");
        _logger.LogInformation($"AvailableRooms: {oldAvailableRooms} -> {package.AvailableRooms} (Increased: {roomsIncreased})");
        _logger.LogInformation($"BookedRooms: {bookedRoomsAfterUpdate}");
        _logger.LogInformation($"RemainingRooms: {oldRemainingRooms} -> {newRemainingRooms}");
        _logger.LogInformation($"WasFull: {wasFull}, NowHasRooms: {nowHasRooms}");
        
        // Notify waiting list if:
        // 1. Package now has rooms available (newRemainingRooms > 0), AND
        // 2. Either:
        //    - Rooms were increased (roomsIncreased), OR
        //    - Package was previously full (oldRemainingRooms <= 0) and now has rooms
        // This covers the case: 0 rooms -> user joins waiting list -> admin adds rooms -> notify user
        // IMPORTANT: Always check waiting list if rooms are available, regardless of previous state
        // This ensures users who joined waiting list get notified when admin adds rooms
        var shouldNotify = nowHasRooms && (roomsIncreased || wasFull || newRemainingRooms > oldRemainingRooms);
        
        if (shouldNotify)
        {
            _logger.LogInformation($"✓✓✓ CONDITIONS MET FOR NOTIFICATION:");
            _logger.LogInformation($"  - NowHasRooms: {nowHasRooms}");
            _logger.LogInformation($"  - RoomsIncreased: {roomsIncreased}");
            _logger.LogInformation($"  - WasFull: {wasFull}");
            _logger.LogInformation($"  - RemainingRoomsIncreased: {newRemainingRooms > oldRemainingRooms}");
            _logger.LogInformation($"Package {package.Id} has {newRemainingRooms} remaining rooms (was {oldRemainingRooms}). Attempting to notify waiting list...");
            
            var waitingListService = HttpContext.RequestServices.GetRequiredService<IWaitingListService>();
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
            
            // Check if there are users in waiting list before notifying
            var waitingListCount = await waitingListService.GetWaitingListCountAsync(package.Id);
            _logger.LogInformation($"Waiting list count for package {package.Id}: {waitingListCount}");
            
            if (waitingListCount > 0)
            {
                _logger.LogInformation($"📧 Notifying waiting list users...");
                await waitingListService.NotifyNextInQueueAsync(package.Id, notificationService);
                _logger.LogInformation($"✓✓✓ Waiting list notification process completed for package {package.Id} (RemainingRooms: {newRemainingRooms}, WaitingListCount: {waitingListCount})");
                TempData["Info"] = $"החבילה עודכנה בהצלחה! {waitingListCount} משתמש/משתמשים ברשימת ההמתנה קיבלו הודעה. / Package updated successfully! {waitingListCount} user(s) in waiting list have been notified.";
            }
            else
            {
                _logger.LogInformation($"No users in waiting list for package {package.Id}. No notification sent.");
            }
        }
        else
        {
            _logger.LogInformation($"⚠ Conditions NOT met for notification:");
            _logger.LogInformation($"  - NowHasRooms: {nowHasRooms}");
            _logger.LogInformation($"  - RoomsIncreased: {roomsIncreased}");
            _logger.LogInformation($"  - WasFull: {wasFull}");
            _logger.LogInformation($"  - RemainingRoomsIncreased: {newRemainingRooms > oldRemainingRooms}");
            if (!nowHasRooms)
            {
                _logger.LogInformation($"Package {package.Id} has no remaining rooms (AvailableRooms: {package.AvailableRooms}, BookedRooms: {bookedRoomsAfterUpdate}). No waiting list notification sent.");
            }
            else
            {
                _logger.LogInformation($"Package {package.Id} has {newRemainingRooms} remaining rooms but conditions not met for notification.");
            }
        }

        // Final verification: Reload package from database to confirm changes were saved
        try
        {
            var verificationPackage = await _context.TravelPackages
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
            
            if (verificationPackage != null)
            {
                _logger.LogInformation($"=== FINAL VERIFICATION: Package {id} ===");
                _logger.LogInformation($"Database values: Destination={verificationPackage.Destination}, AvailableRooms={verificationPackage.AvailableRooms}, Price={verificationPackage.Price}, IsVisible={verificationPackage.IsVisible}");
                
                // Verify key fields match what we tried to save
                if (verificationPackage.Destination == model.Destination &&
                    verificationPackage.AvailableRooms == model.AvailableRooms &&
                    verificationPackage.Price == model.Price &&
                    verificationPackage.IsVisible == model.IsVisible)
                {
                    _logger.LogInformation($"✓✓✓ VERIFICATION SUCCESS: All changes confirmed in database!");
                }
                else
                {
                    _logger.LogWarning($"⚠⚠⚠ VERIFICATION WARNING: Some changes may not have been saved correctly!");
                    _logger.LogWarning($"Expected: Destination={model.Destination}, AvailableRooms={model.AvailableRooms}, Price={model.Price}, IsVisible={model.IsVisible}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during final verification: {ex.Message}");
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

        try
        {
            _context.TravelPackages.Remove(package);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"✓ Package deleted successfully: ID={id}, Destination={package.Destination}");
            TempData["Success"] = "החבילה נמחקה בהצלחה!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"✗ Failed to delete package from database: {ex.Message}");
            TempData["Error"] = "אירעה שגיאה בעת מחיקת החבילה. אנא נסה שוב. / An error occurred while deleting the package. Please try again.";
        }
        return RedirectToAction("Index");
    }
}
