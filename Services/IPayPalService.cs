namespace TripWings.Services;

public interface IPayPalService
{
    Task<(bool Success, string? PaymentId, string? ApprovalUrl, string? ErrorMessage)> CreatePaymentAsync(
        decimal amount, 
        string currency, 
        string description,
        string returnUrl,
        string cancelUrl);
    
    Task<(bool Success, string? ErrorMessage)> ExecutePaymentAsync(string paymentId, string payerId);
    
    Task<string?> GetAccessTokenAsync();
}
