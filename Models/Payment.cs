using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? TransactionId { get; set; }
    public int? DiscountId { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal FinalAmount { get; set; }
    
    [Range(1, 3, ErrorMessage = "מספר התשלומים חייב להיות בין 1 ל-3")]
    [Display(Name = "מספר תשלומים")]
    public int InstallmentsCount { get; set; } = 1;
    
    [Display(Name = "סכום לכל תשלום")]
    public decimal InstallmentAmount { get; set; }

    public Booking Booking { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public Discount? Discount { get; set; }
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    BankTransfer,
    Cash
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Refunded,
    Failed
}
