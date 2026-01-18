using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class TravelPackage
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Country { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Total rooms must be at least 1")]
    [Display(Name = "Total Rooms / מספר חדרים כולל")]
    public int TotalRooms { get; set; }

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Available rooms cannot be negative")]
    [Display(Name = "Available Rooms / מספר חדרים זמינים")]
    public int AvailableRooms { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Package Type")]
    public string PackageType { get; set; } = string.Empty; // e.g., "Luxury", "Budget", "Family"

    [Range(0, 120, ErrorMessage = "Age limit must be between 0 and 120")]
    [Display(Name = "Age Limit")]
    public int? AgeLimit { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "Is Visible")]
    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PackageImage> PackageImages { get; set; } = new List<PackageImage>();
    public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitingListEntry> WaitingListEntries { get; set; } = new List<WaitingListEntry>();
    public ICollection<ReviewTrip> ReviewTrips { get; set; } = new List<ReviewTrip>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public bool IsAvailable => IsVisible && RemainingRooms > 0 && EndDate > DateTime.UtcNow;
    // Only count paid bookings (PaymentStatus == Paid) as booked rooms
    public int BookedRooms => Bookings.Count(b => b.Status == BookingStatus.Active && b.PaymentStatus == PaymentStatus.Paid);
    public int RemainingRooms => Math.Max(0, AvailableRooms - BookedRooms);
}
