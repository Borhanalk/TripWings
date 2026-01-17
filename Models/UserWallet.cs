using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class UserWallet
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "האיזון חייב להיות חיובי")]
    [Display(Name = "איזון")]
    public decimal Balance { get; set; } = 0;
    
    [DataType(DataType.DateTime)]
    [Display(Name = "נוצר ב")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [DataType(DataType.DateTime)]
    [Display(Name = "עודכן ב")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}

public class WalletTransaction
{
    public int Id { get; set; }
    
    [Required]
    public int WalletId { get; set; }
    
    [Required]
    [Display(Name = "סוג עסקה")]
    public WalletTransactionType Type { get; set; }
    
    [Required]
    [Display(Name = "סכום")]
    public decimal Amount { get; set; }
    
    [Display(Name = "תיאור")]
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Display(Name = "מזהה הזמנה")]
    public int? BookingId { get; set; }
    
    [Display(Name = "מזהה תשלום")]
    public int? PaymentId { get; set; }
    
    [DataType(DataType.DateTime)]
    [Display(Name = "תאריך")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Display(Name = "מזהה משיכה")]
    public int? WithdrawalId { get; set; }

    public UserWallet Wallet { get; set; } = null!;
    public Booking? Booking { get; set; }
    public Payment? Payment { get; set; }
    public BankWithdrawal? Withdrawal { get; set; }
}

public enum WalletTransactionType
{
    Deposit,      // הפקדה (refund)
    Withdrawal,   // משיכה (to bank)
    Payment       // תשלום (from wallet)
}

public class BankWithdrawal
{
    public int Id { get; set; }
    
    [Required]
    public int WalletId { get; set; }
    
    [Required]
    [Display(Name = "סכום")]
    [Range(0.01, double.MaxValue, ErrorMessage = "הסכום חייב להיות גדול מ-0")]
    public decimal Amount { get; set; }
    
    [Required]
    [Display(Name = "מספר חשבון")]
    [StringLength(50)]
    public string AccountNumber { get; set; } = string.Empty;
    
    [Required]
    [Display(Name = "שם בעל החשבון")]
    [StringLength(100)]
    public string AccountHolderName { get; set; } = string.Empty;
    
    [Required]
    [Display(Name = "מספר סניף")]
    [StringLength(10)]
    public string BranchNumber { get; set; } = string.Empty;
    
    [Required]
    [Display(Name = "שם הבנק")]
    [StringLength(100)]
    public string BankName { get; set; } = string.Empty;
    
    [Display(Name = "מספר זהות")]
    [StringLength(20)]
    public string? IdNumber { get; set; }
    
    [Display(Name = "תיאור נוסף")]
    [StringLength(500)]
    public string? AdditionalInfo { get; set; }
    
    [Required]
    [Display(Name = "סטטוס")]
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;
    
    [DataType(DataType.DateTime)]
    [Display(Name = "תאריך בקשה")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    
    [DataType(DataType.DateTime)]
    [Display(Name = "תאריך עיבוד")]
    public DateTime? ProcessedAt { get; set; }
    
    [Display(Name = "הערות")]
    [StringLength(1000)]
    public string? Notes { get; set; }

    public UserWallet Wallet { get; set; } = null!;
    public WalletTransaction Transaction { get; set; } = null!;
}

public enum WithdrawalStatus
{
    Pending,      // ממתין
    Approved,     // אושר
    Processed,    // עובד
    Rejected,     // נדחה
    Completed     // הושלם
}
