using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;
using TripWings.Services;

namespace TripWings.Controllers;

public class TripsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TripsController> _logger;

    public TripsController(ApplicationDbContext context, ILogger<TripsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Gallery(
        string? searchQuery,
        string? destination,
        string? country,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        DateTime? travelDateFrom,
        DateTime? travelDateTo,
        bool? onSaleOnly = null,
        string sortBy = "date",
        string sortOrder = "asc")
    {
        var today = DateTime.UtcNow.Date;
        
        var query = _context.TravelPackages
            .Include(t => t.PackageImages)
            .Include(t => t.Discounts)
            .Include(t => t.Bookings)
            .Where(t => t.IsVisible && t.StartDate.Date > today);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(t => 
                t.Destination.Contains(searchQuery) ||
                t.Country.Contains(searchQuery) ||
                t.Description != null && t.Description.Contains(searchQuery) ||
                t.PackageType.Contains(searchQuery));
        }

        if (!string.IsNullOrWhiteSpace(destination))
        {
            query = query.Where(t => t.Destination == destination);
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(t => t.Country == country);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.PackageType == category);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(t => t.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(t => t.Price <= maxPrice.Value);
        }

        if (travelDateFrom.HasValue)
        {
            query = query.Where(t => t.StartDate >= travelDateFrom.Value);
        }

        if (travelDateTo.HasValue)
        {
            query = query.Where(t => t.StartDate <= travelDateTo.Value);
        }

        var currentTime = DateTime.UtcNow;
        var maxEndDate = currentTime.AddDays(7);

        var trips = await query.ToListAsync();
        
        if (onSaleOnly == true)
        {
            var allDiscounts = await _context.Discounts
                .Include(d => d.TravelPackage)
                .ToListAsync();
            
            _logger.LogInformation($"=== DISCOUNT FILTER DEBUG ===");
            _logger.LogInformation($"Total discounts in database: {allDiscounts.Count}");
            _logger.LogInformation($"Current time (UTC): {currentTime}");
            _logger.LogInformation($"Max end date (current + 7 days): {maxEndDate}");
            
            var activeDiscountIds = new List<int>();
            
            foreach (var discount in allDiscounts)
            {
                var isDateActive = discount.StartAt <= currentTime && discount.EndAt >= currentTime;
                var isDurationValid = (discount.EndAt - discount.StartAt).TotalDays <= 7;
                var isActive = isDateActive && isDurationValid;
                var isVisible = discount.TravelPackage?.IsVisible ?? false;
                var travelPackageId = discount.TravelPackageId;
                
                _logger.LogInformation($"Discount ID {discount.Id}: " +
                    $"TravelPackageId={travelPackageId}, " +
                    $"StartAt={discount.StartAt:yyyy-MM-dd HH:mm:ss}, " +
                    $"EndAt={discount.EndAt:yyyy-MM-dd HH:mm:ss}, " +
                    $"Duration={(discount.EndAt - discount.StartAt).TotalDays:F1} days, " +
                    $"IsDateActive={isDateActive}, " +
                    $"IsDurationValid={isDurationValid}, " +
                    $"IsActive={isActive}, " +
                    $"TravelPackageIsVisible={isVisible}");
                
                if (isActive && isVisible)
                {
                    activeDiscountIds.Add(discount.TravelPackageId);
                }
            }
            
            _logger.LogInformation($"Active discount TravelPackageIds: [{string.Join(", ", activeDiscountIds)}]");
            _logger.LogInformation($"Total trips before filter: {trips.Count}");
            
            trips = trips.Where(t => 
                t.Discounts != null && 
                t.Discounts.Any(d => 
                    d.StartAt <= currentTime && 
                    d.EndAt >= currentTime && 
                    (d.EndAt - d.StartAt).TotalDays <= 7)).ToList();
            
            _logger.LogInformation($"Total trips after filter: {trips.Count}");
            _logger.LogInformation($"=== END DISCOUNT FILTER DEBUG ===");
        }

        var tripViewModels = trips.Select(t => new TravelPackageViewModel
        {
            Id = t.Id,
            Destination = t.Destination,
            Country = t.Country,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            Price = t.Price,
            AvailableRooms = t.AvailableRooms,
            TotalRooms = t.TotalRooms,
            PackageType = t.PackageType,
            AgeLimit = t.AgeLimit,
            Description = t.Description,
            ImageUrls = t.PackageImages.Select(img => img.ImageUrl).ToList(),
            RemainingRooms = t.RemainingRooms,
            IsAvailable = t.IsAvailable,
            BookingCount = t.Bookings.Count(b => b.Status == BookingStatus.Active)
        }).ToList();

        foreach (var tripViewModel in tripViewModels)
        {
            var trip = trips.First(t => t.Id == tripViewModel.Id);
            if (trip.Discounts != null && trip.Discounts.Any())
            {
                var activeDiscount = trip.Discounts
                    .FirstOrDefault(d => d.StartAt <= currentTime && 
                                       d.EndAt >= currentTime && 
                                       (d.EndAt - d.StartAt).TotalDays <= 7);

                if (activeDiscount != null)
                {
                    tripViewModel.HasActiveDiscount = true;
                    tripViewModel.OldPrice = activeDiscount.OldPrice;
                    tripViewModel.NewPrice = activeDiscount.NewPrice;
                    tripViewModel.DiscountPercentage = activeDiscount.DiscountPercentage;
                }
            }
        }

        tripViewModels = sortBy.ToLower() switch
        {
            "price" => sortOrder == "desc" 
                ? tripViewModels.OrderByDescending(t => t.HasActiveDiscount ? t.NewPrice : t.Price).ToList()
                : tripViewModels.OrderBy(t => t.HasActiveDiscount ? t.NewPrice : t.Price).ToList(),
            "popular" => tripViewModels.OrderByDescending(t => t.BookingCount).ToList(),
            "category" => sortOrder == "desc"
                ? tripViewModels.OrderByDescending(t => t.PackageType).ToList()
                : tripViewModels.OrderBy(t => t.PackageType).ToList(),
            "date" => sortOrder == "desc"
                ? tripViewModels.OrderByDescending(t => t.StartDate).ToList()
                : tripViewModels.OrderBy(t => t.StartDate).ToList(),
            _ => tripViewModels.OrderBy(t => t.StartDate).ToList()
        };

        var allTrips = await _context.TravelPackages.Where(t => t.IsVisible && t.StartDate.Date > today).ToListAsync();
        var destinations = allTrips.Select(t => t.Destination).Distinct().OrderBy(d => d).ToList();
        var countries = allTrips.Select(t => t.Country).Distinct().OrderBy(c => c).ToList();
        var categories = allTrips.Select(t => t.PackageType).Distinct().OrderBy(c => c).ToList();

        var viewModel = new TripGalleryViewModel
        {
            Trips = tripViewModels,
            SearchQuery = searchQuery,
            Destination = destination,
            Country = country,
            Category = category,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            TravelDateFrom = travelDateFrom,
            TravelDateTo = travelDateTo,
            OnSaleOnly = onSaleOnly ?? false,
            SortBy = sortBy,
            SortOrder = sortOrder,
            Destinations = destinations,
            Countries = countries,
            Categories = categories
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Discounts()
    {
        var currentTime = DateTime.UtcNow;
        var maxEndDate = currentTime.AddDays(7);

        var today = DateTime.UtcNow.Date;
        
        var discounts = await _context.Discounts
            .Include(d => d.TravelPackage)
            .ThenInclude(t => t.PackageImages)
            .Include(d => d.TravelPackage)
            .ThenInclude(t => t.Bookings)
            .Where(d => d.StartAt <= currentTime && 
                       d.EndAt >= currentTime && 
                       d.EndAt <= maxEndDate &&
                       d.TravelPackage.IsVisible &&
                       d.TravelPackage.StartDate.Date > today)
            .ToListAsync();

        var activeDiscounts = discounts.Where(d => 
            (d.EndAt - d.StartAt).TotalDays <= 7).ToList();

        var discountViewModels = activeDiscounts.Select(d => new TravelPackageViewModel
        {
            Id = d.TravelPackageId,
            Destination = d.TravelPackage.Destination,
            Country = d.TravelPackage.Country,
            StartDate = d.TravelPackage.StartDate,
            EndDate = d.TravelPackage.EndDate,
            Price = d.TravelPackage.Price,
            AvailableRooms = d.TravelPackage.AvailableRooms,
            TotalRooms = d.TravelPackage.TotalRooms,
            PackageType = d.TravelPackage.PackageType,
            AgeLimit = d.TravelPackage.AgeLimit,
            Description = d.TravelPackage.Description,
            ImageUrls = d.TravelPackage.PackageImages.Select(img => img.ImageUrl).ToList(),
            RemainingRooms = d.TravelPackage.RemainingRooms,
            IsAvailable = d.TravelPackage.IsAvailable,
            BookingCount = d.TravelPackage.Bookings.Count(b => b.Status == BookingStatus.Active),
            HasActiveDiscount = true,
            OldPrice = d.OldPrice,
            NewPrice = d.NewPrice,
            DiscountPercentage = d.DiscountPercentage
        }).OrderByDescending(d => d.DiscountPercentage).ToList();

        return View("~/Views/Trips/Discounts.cshtml", discountViewModels);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        // Use AsNoTracking to ensure we get fresh data from database
        var trip = await _context.TravelPackages
            .AsNoTracking()
            .Include(t => t.PackageImages)
            .Include(t => t.Discounts)
            .Include(t => t.Bookings) // Include bookings to calculate RemainingRooms correctly
            .Include(t => t.WaitingListEntries) // Include waiting list entries to show count
            .Include(t => t.ReviewTrips)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (trip == null) return NotFound();

        // Check if current user has valid waiting list notification (position #1)
        bool userHasValidNotification = false;
        string? currentUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(currentUserId))
            {
                var waitingListEntry = await _context.WaitingListEntries
                    .Where(w => w.UserId == currentUserId && 
                               w.TravelPackageId == id.Value && 
                               w.IsActive &&
                               w.Position == 1 &&
                               w.NotifiedAt.HasValue &&
                               w.NotificationExpiresAt.HasValue &&
                               w.NotificationExpiresAt.Value > DateTime.UtcNow)
                    .FirstOrDefaultAsync();
                
                userHasValidNotification = waitingListEntry != null;
            }
        }

        // Check if there's an active waiting list notification for another user
        var bookingService = HttpContext.RequestServices.GetRequiredService<IBookingService>();
        var (hasActiveNotification, notifiedUserId) = await bookingService.HasActiveWaitingListNotificationAsync(id.Value);
        bool hasOtherUserActiveNotification = hasActiveNotification && notifiedUserId != currentUserId;

        // Recalculate remaining rooms to ensure accuracy from database
        // Only count paid bookings (PaymentStatus == Paid) as booked rooms
        var allBookings = await _context.Bookings
            .Where(b => b.TravelPackageId == id.Value && 
                       b.Status == BookingStatus.Active && 
                       b.PaymentStatus == PaymentStatus.Paid)
            .CountAsync();
        
        var bookedRooms = allBookings;
        var remainingRooms = Math.Max(0, trip.AvailableRooms - bookedRooms);
        var isAvailable = trip.IsVisible && remainingRooms > 0 && trip.EndDate > DateTime.UtcNow;
        
        // Get waiting list count from database
        var waitingListCount = await _context.WaitingListEntries
            .Where(w => w.TravelPackageId == id.Value && w.IsActive)
            .CountAsync();

        _logger.LogInformation($"=== TRIP DETAILS: Package {id} ===");
        _logger.LogInformation($"AvailableRooms: {trip.AvailableRooms}, BookedRooms: {bookedRooms}, RemainingRooms: {remainingRooms}");
        _logger.LogInformation($"Total Bookings in DB: {allBookings}, Waiting List Count: {waitingListCount}");
        _logger.LogInformation($"HasActiveNotification: {hasActiveNotification}, NotifiedUserId: {notifiedUserId}, CurrentUserId: {currentUserId}");

        ViewBag.UserHasValidNotification = userHasValidNotification;
        ViewBag.IsAvailable = (isAvailable || userHasValidNotification) && !hasOtherUserActiveNotification;
        ViewBag.HasOtherUserActiveNotification = hasOtherUserActiveNotification;
        ViewBag.RemainingRooms = remainingRooms;
        ViewBag.AvailableRooms = trip.AvailableRooms;
        ViewBag.BookedRooms = bookedRooms;
        ViewBag.TotalRooms = trip.TotalRooms;
        ViewBag.WaitingListCount = waitingListCount;

        return View(trip);
    }
}
