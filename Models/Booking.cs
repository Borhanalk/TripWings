using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWings.Models;

public class Booking
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "User")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Travel Package")]
    public int TravelPackageId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Rooms count must be at least 1")]
    [Display(Name = "Rooms Count")]
    public int RoomsCount { get; set; } = 1;

    [Required]
    [Display(Name = "Status")]
    public BookingStatus Status { get; set; } = BookingStatus.Active;

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Created At")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Display(Name = "Payment Status")]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    [Display(Name = "Reminder Sent")]
    public bool ReminderSent { get; set; } = false;

    public ApplicationUser User { get; set; } = null!;
    public TravelPackage TravelPackage { get; set; } = null!;

    [NotMapped]
    public bool IsUpcoming => Status == BookingStatus.Active && TravelPackage.StartDate > DateTime.UtcNow;
}

public enum BookingStatus
{
    Active,
    Cancelled,
    Completed
}
