namespace TripWings.Services;

public interface IEmailService
{
    Task SendBookingConfirmationAsync(string toEmail, string userName, int bookingId);
    Task SendBookingCancellationAsync(string toEmail, string userName, int bookingId);
    Task SendWaitingListNotificationAsync(string toEmail, string userName, int packageId);
    Task SendPaymentConfirmationAsync(string toEmail, string userName, int paymentId);
}
