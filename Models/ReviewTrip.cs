using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class ReviewTrip
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "User")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Travel Package")]
    public int TravelPackageId { get; set; }

    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    [Display(Name = "Rating")]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Created At")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public TravelPackage TravelPackage { get; set; } = null!;
}
