namespace TripWings.Services;

public interface INotificationService
{
    Task SendWaitingListNotificationAsync(string userEmail, string userName, int travelPackageId, int position);
    Task SendWaitingListConfirmationAsync(string userEmail, string userName, int travelPackageId, int position);
    Task SendBookingConfirmationAsync(string userEmail, string userName, int bookingId);
    Task SendBookingCancellationAsync(string userEmail, string userName, int bookingId);
    Task SendPaymentConfirmationAsync(string userEmail, string userName, int bookingId, decimal amount);
    Task SendTripReminderAsync(string userEmail, string userName, int bookingId, DateTime departureDate);
    Task SendEmailAsync(string toEmail, string subject, string body);
}
