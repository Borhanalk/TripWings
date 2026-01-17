using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class WaitingListEntry
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "User")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Travel Package")]
    public int TravelPackageId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Joined At")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Position must be at least 1")]
    [Display(Name = "Queue Order")]
    public int Position { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Notified At")]
    public DateTime? NotifiedAt { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Notification Expires At")]
    public DateTime? NotificationExpiresAt { get; set; }

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;

    public ApplicationUser User { get; set; } = null!;
    public TravelPackage TravelPackage { get; set; } = null!;
}
