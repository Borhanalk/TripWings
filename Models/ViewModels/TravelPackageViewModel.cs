using System.ComponentModel.DataAnnotations;

namespace TripWings.Models.ViewModels;

public class AdminTravelPackageViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "שדה חובה")]
    [StringLength(200, ErrorMessage = "מקסימום 200 תווים")]
    [Display(Name = "יעד")]
    public string Destination { get; set; } = string.Empty;

    [Required(ErrorMessage = "שדה חובה")]
    [StringLength(100, ErrorMessage = "מקסימום 100 תווים")]
    [Display(Name = "מדינה")]
    public string Country { get; set; } = string.Empty;

    [Required(ErrorMessage = "שדה חובה")]
    [DataType(DataType.Date)]
    [Display(Name = "תאריך התחלה")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(30);

    [Required(ErrorMessage = "שדה חובה")]
    [DataType(DataType.Date)]
    [Display(Name = "תאריך סיום")]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(37);

    [Required(ErrorMessage = "שדה חובה")]
    [Range(0.01, double.MaxValue, ErrorMessage = "המחיר חייב להיות גדול מ-0")]
    [DataType(DataType.Currency)]
    [Display(Name = "מחיר")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "שדה חובה")]
    [Range(1, int.MaxValue, ErrorMessage = "מספר החדרים הכולל חייב להיות לפחות 1")]
    [Display(Name = "מספר חדרים כולל / Total Rooms")]
    public int TotalRooms { get; set; } = 1;

    [Required(ErrorMessage = "שדה חובה")]
    [Range(0, int.MaxValue, ErrorMessage = "מספר החדרים הזמינים לא יכול להיות שלילי")]
    [Display(Name = "מספר חדרים זמינים / Available Rooms")]
    public int AvailableRooms { get; set; } = 1;

    [Required(ErrorMessage = "שדה חובה")]
    [StringLength(50, ErrorMessage = "מקסימום 50 תווים")]
    [Display(Name = "סוג חבילה")]
    public string PackageType { get; set; } = string.Empty;

    [Range(0, 120, ErrorMessage = "גיל מינימלי חייב להיות בין 0 ל-120")]
    [Display(Name = "גיל מינימלי")]
    public int? AgeLimit { get; set; }

    [StringLength(2000, ErrorMessage = "מקסימום 2000 תווים")]
    [Display(Name = "תיאור")]
    public string? Description { get; set; }

    [Display(Name = "נראה")]
    public bool IsVisible { get; set; } = true;

    [Display(Name = "כתובות תמונות (מופרדות בפסיק או שורה חדשה)")]
    public string? ImageUrls { get; set; }

    public List<string> ImageUrlList { get; set; } = new();

    [Display(Name = "Add Discount")]
    public bool AddDiscount { get; set; } = false;

    [Range(0.01, double.MaxValue, ErrorMessage = "Discount old price must be greater than 0")]
    [DataType(DataType.Currency)]
    [Display(Name = "Discount Old Price")]
    public decimal? DiscountOldPrice { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Discount new price must be greater than 0")]
    [DataType(DataType.Currency)]
    [Display(Name = "Discount New Price")]
    public decimal? DiscountNewPrice { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Discount Start Date")]
    public DateTime? DiscountStartAt { get; set; } = DateTime.UtcNow;

    [DataType(DataType.DateTime)]
    [Display(Name = "Discount End Date")]
    public DateTime? DiscountEndAt { get; set; } = DateTime.UtcNow.AddDays(7);
}
