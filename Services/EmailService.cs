using System.Net;
using System.Net.Mail;

namespace TripWings.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var smtpServer = emailSettings["SmtpServer"];
            var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
            var senderEmail = emailSettings["SenderEmail"];
            var senderName = emailSettings["SenderName"];
            var username = emailSettings["Username"];
            var password = emailSettings["Password"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogWarning("Email settings not configured. Email not sent.");
                return;
            }

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail!, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);
            await client.SendMailAsync(message);
            _logger.LogInformation($"Email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to {toEmail}");
        }
    }

    public async Task SendBookingConfirmationAsync(string toEmail, string userName, int bookingId)
    {
        var subject = "Booking Confirmation - TripWings";
        var body = $@"
            <h2>Booking Confirmed!</h2>
            <p>Dear {userName},</p>
            <p>Your booking (ID: {bookingId}) has been confirmed.</p>
            <p>Thank you for choosing TripWings!</p>
        ";
        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendBookingCancellationAsync(string toEmail, string userName, int bookingId)
    {
        var subject = "Booking Cancelled - TripWings";
        var body = $@"
            <h2>Booking Cancelled</h2>
            <p>Dear {userName},</p>
            <p>Your booking (ID: {bookingId}) has been cancelled.</p>
            <p>If you have any questions, please contact us.</p>
        ";
        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendWaitingListNotificationAsync(string toEmail, string userName, int packageId)
    {
        var subject = "Package Available - TripWings";
        var body = $@"
            <h2>Package Available!</h2>
            <p>Dear {userName},</p>
            <p>A package you were waiting for (ID: {packageId}) is now available.</p>
            <p>Please book soon as spots are limited!</p>
        ";
        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendPaymentConfirmationAsync(string toEmail, string userName, int paymentId)
    {
        var subject = "Payment Confirmation - TripWings";
        var body = $@"
            <h2>Payment Confirmed!</h2>
            <p>Dear {userName},</p>
            <p>Your payment (ID: {paymentId}) has been processed successfully.</p>
            <p>Thank you for your payment!</p>
        ";
        await SendEmailAsync(toEmail, subject, body);
    }
}
