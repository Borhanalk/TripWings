using TripWings.Models;

namespace TripWings.Services;

public interface IWalletService
{
    Task<UserWallet> GetOrCreateWalletAsync(string userId);
    Task<decimal> GetBalanceAsync(string userId);
    Task<(bool Success, string? ErrorMessage)> AddToWalletAsync(string userId, decimal amount, string? description, int? bookingId = null, int? paymentId = null);
    Task<(bool Success, string? ErrorMessage)> WithdrawToBankAsync(string userId, decimal amount, BankWithdrawalRequest request);
    Task<List<WalletTransaction>> GetTransactionsAsync(string userId, int? limit = null);
    Task<List<BankWithdrawal>> GetWithdrawalsAsync(string userId);
}

public class BankWithdrawalRequest
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string BranchNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? IdNumber { get; set; }
    public string? AdditionalInfo { get; set; }
}
