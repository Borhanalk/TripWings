using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWings.Models;

public class Discount
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Travel Package")]
    public int TravelPackageId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Old price must be greater than 0")]
    [DataType(DataType.Currency)]
    [Display(Name = "Old Price")]
    public decimal OldPrice { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "New price must be greater than 0")]
    [DataType(DataType.Currency)]
    [Display(Name = "New Price")]
    public decimal NewPrice { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Start At")]
    public DateTime StartAt { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "End At")]
    public DateTime EndAt { get; set; }

    public TravelPackage TravelPackage { get; set; } = null!;

    [NotMapped]
    public bool IsActive => DateTime.UtcNow >= StartAt && DateTime.UtcNow <= EndAt && (EndAt - StartAt).TotalDays <= 7;

    [NotMapped]
    public decimal DiscountPercentage => OldPrice > 0 ? ((OldPrice - NewPrice) / OldPrice) * 100 : 0;

    [NotMapped]
    public decimal SavingsAmount => OldPrice - NewPrice;
}
