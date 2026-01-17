using System.ComponentModel.DataAnnotations;

namespace TripWings.Models.ViewModels;

public class AdminDiscountViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Travel package is required.")]
    [Display(Name = "Travel Package")]
    public int TravelPackageId { get; set; }

    [Required(ErrorMessage = "Old price is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Old price must be greater than 0")]
    [DataType(DataType.Currency)]
    [Display(Name = "Old Price")]
    public decimal OldPrice { get; set; }

    [Required(ErrorMessage = "New price is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "New price must be greater than 0")]
    [DataType(DataType.Currency)]
    [Display(Name = "New Price")]
    public decimal NewPrice { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Start Date")]
    public DateTime StartAt { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "End date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "End Date")]
    public DateTime EndAt { get; set; } = DateTime.UtcNow.AddDays(7);
}
