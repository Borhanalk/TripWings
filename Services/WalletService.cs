using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Services;

public class WalletService : IWalletService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WalletService> _logger;

    public WalletService(ApplicationDbContext context, ILogger<WalletService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserWallet> GetOrCreateWalletAsync(string userId)
    {
        var wallet = await _context.UserWallets
            .FirstOrDefaultAsync(w => w.UserId == userId);

        if (wallet == null)
        {
            wallet = new UserWallet
            {
                UserId = userId,
                Balance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserWallets.Add(wallet);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Created wallet for user {userId}");
        }

        return wallet;
    }

    public async Task<decimal> GetBalanceAsync(string userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        return wallet.Balance;
    }

    public async Task<(bool Success, string? ErrorMessage)> AddToWalletAsync(
        string userId, 
        decimal amount, 
        string? description, 
        int? bookingId = null, 
        int? paymentId = null)
    {
        try
        {
            if (amount <= 0)
            {
                return (false, "הסכום חייב להיות גדול מ-0");
            }

            var wallet = await GetOrCreateWalletAsync(userId);

            var transaction = new WalletTransaction
            {
                WalletId = wallet.Id,
                Type = WalletTransactionType.Deposit,
                Amount = amount,
                Description = description ?? "הפקדה לארנק",
                BookingId = bookingId,
                PaymentId = paymentId,
                CreatedAt = DateTime.UtcNow
            };

            wallet.Balance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Added {amount} to wallet for user {userId}. New balance: {wallet.Balance}");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding to wallet for user {userId}");
            return (false, "אירעה שגיאה בעת הוספת כסף לארנק");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> WithdrawToBankAsync(
        string userId, 
        decimal amount, 
        BankWithdrawalRequest request)
    {
        try
        {
            if (amount <= 0)
            {
                return (false, "הסכום חייב להיות גדול מ-0");
            }

            var wallet = await GetOrCreateWalletAsync(userId);

            if (wallet.Balance < amount)
            {
                return (false, "אין מספיק כסף בארנק");
            }

            var withdrawal = new BankWithdrawal
            {
                WalletId = wallet.Id,
                Amount = amount,
                AccountNumber = request.AccountNumber,
                AccountHolderName = request.AccountHolderName,
                BranchNumber = request.BranchNumber,
                BankName = request.BankName,
                IdNumber = request.IdNumber,
                AdditionalInfo = request.AdditionalInfo,
                Status = WithdrawalStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            var transaction = new WalletTransaction
            {
                WalletId = wallet.Id,
                Type = WalletTransactionType.Withdrawal,
                Amount = amount,
                Description = $"משיכה לחשבון בנק: {request.BankName}",
                CreatedAt = DateTime.UtcNow
            };

            wallet.Balance -= amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            _context.BankWithdrawals.Add(withdrawal);
            _context.WalletTransactions.Add(transaction);

            await _context.SaveChangesAsync();
            transaction.WithdrawalId = withdrawal.Id;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Withdrawal request created for user {userId}. Amount: {amount}");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating withdrawal for user {userId}");
            return (false, "אירעה שגיאה בעת יצירת בקשת משיכה");
        }
    }

    public async Task<List<WalletTransaction>> GetTransactionsAsync(string userId, int? limit = null)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        
        var query = _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<WalletTransaction>)query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<List<BankWithdrawal>> GetWithdrawalsAsync(string userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        
        return await _context.BankWithdrawals
            .Where(w => w.WalletId == wallet.Id)
            .OrderByDescending(w => w.RequestedAt)
            .ToListAsync();
    }
}
