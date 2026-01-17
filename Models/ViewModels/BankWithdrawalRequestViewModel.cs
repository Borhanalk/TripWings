using System.ComponentModel.DataAnnotations;

namespace TripWings.Models.ViewModels;

public class BankWithdrawalRequestViewModel
{
    [Required(ErrorMessage = "הסכום נדרש")]
    [Range(0.01, double.MaxValue, ErrorMessage = "הסכום חייב להיות גדול מ-0")]
    [Display(Name = "סכום למשיכה")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "מספר חשבון נדרש")]
    [StringLength(50, MinimumLength = 5, ErrorMessage = "מספר החשבון חייב להיות בין 5 ל-50 תווים")]
    [Display(Name = "מספר חשבון")]
    public string AccountNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "שם בעל החשבון נדרש")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "שם בעל החשבון חייב להיות בין 2 ל-100 תווים")]
    [Display(Name = "שם בעל החשבון")]
    public string AccountHolderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "מספר סניף נדרש")]
    [StringLength(10, MinimumLength = 3, ErrorMessage = "מספר הסניף חייב להיות בין 3 ל-10 תווים")]
    [Display(Name = "מספר סניף")]
    public string BranchNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "שם הבנק נדרש")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "שם הבנק חייב להיות בין 2 ל-100 תווים")]
    [Display(Name = "שם הבנק")]
    public string BankName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "מספר זהות לא יכול להיות יותר מ-20 תווים")]
    [Display(Name = "מספר זהות (אופציונלי)")]
    public string? IdNumber { get; set; }

    [StringLength(500, ErrorMessage = "מידע נוסף לא יכול להיות יותר מ-500 תווים")]
    [Display(Name = "מידע נוסף (אופציונלי)")]
    public string? AdditionalInfo { get; set; }
}
