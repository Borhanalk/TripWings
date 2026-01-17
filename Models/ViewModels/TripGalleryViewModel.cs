using System.ComponentModel.DataAnnotations;

namespace TripWings.Models.ViewModels;

public class TripGalleryViewModel
{
    public List<TravelPackageViewModel> Trips { get; set; } = new();

    public string? SearchQuery { get; set; }
    public string? Destination { get; set; }
    public string? Country { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTime? TravelDateFrom { get; set; }
    public DateTime? TravelDateTo { get; set; }
    public bool OnSaleOnly { get; set; }

    public string SortBy { get; set; } = "date"; // price-asc, price-desc, popular, category, date
    public string SortOrder { get; set; } = "asc";

    public List<string> Destinations { get; set; } = new();
    public List<string> Countries { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}

public class TravelPackageViewModel
{
    public int Id { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public int AvailableRooms { get; set; }
    public string PackageType { get; set; } = string.Empty;
    public int? AgeLimit { get; set; }
    public string? Description { get; set; }
    public List<string> ImageUrls { get; set; } = new();

    public bool HasActiveDiscount { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }

    public int RemainingRooms { get; set; }
    public bool IsAvailable { get; set; }
    public int BookingCount { get; set; } // For popularity sorting
}
