using System.ComponentModel.DataAnnotations;

namespace TripWings.Models.ViewModels;

public class PaymentViewModel
{

    public List<int>? CartItemIds { get; set; }

    public int? TravelPackageId { get; set; }
    public int? RoomsCount { get; set; } = 1;

    [Required(ErrorMessage = "אנא בחר אמצעי תשלום")]
    [Display(Name = "אמצעי תשלום")]
    public string PaymentMethod { get; set; } = "CreditCard";

    [Display(Name = "מספר כרטיס")]
    [StringLength(19, MinimumLength = 13, ErrorMessage = "מספר הכרטיס חייב להיות בין 13 ל-19 ספרות")]
    public string? CardNumber { get; set; }

    [Display(Name = "שם בעל הכרטיס")]
    [StringLength(100)]
    public string? CardHolderName { get; set; }

    [Display(Name = "תאריך תפוגה")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "תאריך התפוגה חייב להיות בפורמט MM/YY")]
    public string? ExpiryDate { get; set; }

    [Display(Name = "CVV")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV חייב להיות 3 או 4 ספרות")]
    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV חייב להכיל רק ספרות")]
    public string? CVV { get; set; }

    [Display(Name = "כתובת חיוב")]
    public string? BillingAddress { get; set; }

    [Range(1, 3, ErrorMessage = "מספר התשלומים חייב להיות בין 1 ל-3")]
    [Display(Name = "מספר תשלומים")]
    public int InstallmentsCount { get; set; } = 1;
}

public class CheckoutViewModel
{
    public List<CartItem> CartItems { get; set; } = new();
    public decimal TotalAmount { get; set; }
}
