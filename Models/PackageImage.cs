using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class PackageImage
{
    public int Id { get; set; }

    [Required]
    [Url]
    [StringLength(500)]
    [Display(Name = "Image URL")]
    public string ImageUrl { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Travel Package")]
    public int TravelPackageId { get; set; }

    public TravelPackage TravelPackage { get; set; } = null!;
}
