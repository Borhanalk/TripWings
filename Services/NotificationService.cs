using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;
    private readonly IReceiptPdfService _receiptPdfService;

    public NotificationService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<NotificationService> logger,
        IReceiptPdfService receiptPdfService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _receiptPdfService = receiptPdfService;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        await SendEmailAsync(toEmail, subject, body, null, null);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, byte[]? attachmentBytes, string? attachmentFileName)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        var sendEmails = emailSettings.GetValue<bool>("SendEmails", false);
        var logOnly = emailSettings.GetValue<bool>("LogOnly", true);

        if (logOnly || !sendEmails)
        {
            _logger.LogInformation($"EMAIL (Log Only): To: {toEmail}, Subject: {subject}");
            _logger.LogInformation($"EMAIL Body: {body}");
            if (attachmentBytes != null)
            {
                _logger.LogInformation($"EMAIL Attachment: {attachmentFileName} ({attachmentBytes.Length} bytes)");
            }
            return;
        }

        try
        {
            var smtpServer = emailSettings["SmtpServer"];
            var smtpPort = emailSettings.GetValue<int>("SmtpPort", 587);
            var senderEmail = emailSettings["SenderEmail"];
            var senderName = emailSettings["SenderName"];
            var username = emailSettings["Username"];
            var password = emailSettings["Password"];
            var enableSsl = emailSettings.GetValue<bool>("EnableSsl", true);

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogWarning("Email settings not configured. Email not sent.");
                return;
            }

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            if (attachmentBytes != null && !string.IsNullOrEmpty(attachmentFileName))
            {
                using var attachmentStream = new MemoryStream(attachmentBytes);
                var attachment = new Attachment(attachmentStream, attachmentFileName, "application/pdf");
                message.Attachments.Add(attachment);
            }

            await client.SendMailAsync(message);
            _logger.LogInformation($"Email sent successfully to {toEmail}" + (attachmentBytes != null ? $" with attachment {attachmentFileName}" : ""));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to {toEmail}");
        }
    }

    public async Task SendWaitingListNotificationAsync(string userEmail, string userName, int travelPackageId, int position)
    {
        var package = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);
        
        var packageName = package?.Destination ?? "Travel Package";
        var country = package?.Country ?? "";
        var startDate = package?.StartDate.ToString("dd/MM/yyyy") ?? "";
        var endDate = package?.EndDate.ToString("dd/MM/yyyy") ?? "";
        var price = package?.Price.ToString("C") ?? "";

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
        var bookingUrl = $"{baseUrl}/Trips/Details/{travelPackageId}";
        
        var subject = "Room Available Now - TripWings";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd;'>
                    <h2 style='color: #2563eb; text-align: center;'>Room Available Now!</h2>
                    <p>Dear {userName},</p>
                    <p>Great news! A room has become available for <strong>{packageName}, {country}</strong>.</p>
                    <div style='background-color: #fef3c7; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #f59e0b;'>
                        <h3 style='color: #dc2626; margin-top: 0;'>⚠️ Limited Time ⚠️</h3>
                        <p style='font-size: 18px; font-weight: bold; color: #dc2626;'>
                            You have only 10 minutes to book! After that, you will be removed from the waiting list and the next user will be notified.
                        </p>
                    </div>
                    <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <h3>Trip Details:</h3>
                        <ul style='list-style: none; padding: 0;'>
                            <li><strong>Destination:</strong> {packageName}, {country}</li>
                            <li><strong>Travel Dates:</strong> {startDate} - {endDate}</li>
                            <li><strong>Price:</strong> {price}</li>
                            <li><strong>Your Position:</strong> #{position}</li>
                        </ul>
                    </div>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{bookingUrl}' style='background-color: #2563eb; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; font-size: 18px; display: inline-block;'>
                            Book Now
                        </a>
                    </div>
                    <p style='color: #dc2626; font-weight: bold; text-align: center;'>
                        Please book within 10 minutes before the deadline expires!
                    </p>
                    <p>Best regards,<br>TripWings Team</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(userEmail, subject, body);
    }

    public async Task SendWaitingListConfirmationAsync(string userEmail, string userName, int travelPackageId, int position)
    {
        var package = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);
        
        var packageName = package?.Destination ?? "Travel Package";
        var country = package?.Country ?? "";
        var startDate = package?.StartDate.ToString("dd/MM/yyyy") ?? "";
        var endDate = package?.EndDate.ToString("dd/MM/yyyy") ?? "";
        var price = package?.Price.ToString("C") ?? "";

        var subject = "Added to Waiting List - TripWings";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd;'>
                    <h2 style='color: #2563eb; text-align: center;'>You've been added to the waiting list!</h2>
                    <p>Dear {userName},</p>
                    <p>Thank you! You have been added to the waiting list for <strong>{packageName}, {country}</strong>.</p>
                    <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <h3>Trip Details:</h3>
                        <ul style='list-style: none; padding: 0;'>
                            <li><strong>Destination:</strong> {packageName}, {country}</li>
                            <li><strong>Travel Dates:</strong> {startDate} - {endDate}</li>
                            <li><strong>Price:</strong> {price}</li>
                            <li><strong>Your Position:</strong> #{position}</li>
                        </ul>
                    </div>
                    <div style='background-color: #dbeafe; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #2563eb;'>
                        <h3 style='color: #1e40af; margin-top: 0;'>What happens next?</h3>
                        <p>When a room becomes available, we will send you an email immediately. You will have 10 minutes to book before the notification is sent to the next user.</p>
                    </div>
                    <p>We'll keep you updated!</p>
                    <p>Best regards,<br>TripWings Team</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(userEmail, subject, body);
    }

    public async Task SendBookingConfirmationAsync(string userEmail, string userName, int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            _logger.LogWarning($"Booking {bookingId} not found for email notification");
            return;
        }

        var subject = "Booking Confirmation - TripWings";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #2563eb;'>Booking Confirmed!</h2>
                    <p>Dear {userName},</p>
                    <p>Your booking has been confirmed successfully.</p>
                    <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <h3>Booking Details:</h3>
                        <ul style='list-style: none; padding: 0;'>
                            <li><strong>Booking ID:</strong> {bookingId}</li>
                            <li><strong>Destination:</strong> {booking.TravelPackage.Destination}, {booking.TravelPackage.Country}</li>
                            <li><strong>Rooms:</strong> {booking.RoomsCount}</li>
                            <li><strong>Travel Dates:</strong> {booking.TravelPackage.StartDate:MMM dd, yyyy} - {booking.TravelPackage.EndDate:MMM dd, yyyy}</li>
                            <li><strong>Status:</strong> {booking.Status}</li>
                        </ul>
                    </div>
                    <p>You can download your itinerary from your dashboard.</p>
                    <p>Thank you for choosing TripWings!</p>
                    <p>Best regards,<br>TripWings Team</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(userEmail, subject, body);
    }

    public async Task SendBookingCancellationAsync(string userEmail, string userName, int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            _logger.LogWarning($"Booking {bookingId} not found for cancellation email");
            return;
        }

        var subject = "Booking Cancelled - TripWings";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #dc2626;'>Booking Cancelled</h2>
                    <p>Dear {userName},</p>
                    <p>Your booking (ID: {bookingId}) for <strong>{booking.TravelPackage.Destination}</strong> has been cancelled.</p>
                    <p>If you have any questions or need assistance, please contact our support team.</p>
                    <p>Best regards,<br>TripWings Team</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(userEmail, subject, body);
    }


    public async Task SendPaymentConfirmationAsync(string userEmail, string userName, int bookingId, decimal amount)
    {
        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            _logger.LogWarning($"Booking {bookingId} not found for payment confirmation email");
            return;
        }

        var payment = await _context.Payments
            .Where(p => p.BookingId == bookingId && p.Status == PaymentStatus.Paid)
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();

        if (payment == null)
        {
            _logger.LogWarning($"No paid payment found for booking {bookingId}");
            return;
        }

        byte[]? pdfBytes = null;
        string? pdfFileName = null;
        try
        {
            pdfBytes = await _receiptPdfService.GeneratePaymentReceiptAsync(payment.Id);
            pdfFileName = $"Receipt_{payment.Id:D6}.pdf";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to generate PDF receipt for payment {payment.Id}");
        }

        var subject = "Payment Confirmed - TripWings";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd;'>
                    <h2 style='color: #16a34a; text-align: center;'>Payment Confirmed!</h2>
                    <p>Dear {userName},</p>
                    <p>Your payment has been processed successfully.</p>
                    <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <h3>Payment Details:</h3>
                        <ul style='list-style: none; padding: 0;'>
                            <li><strong>Amount Paid:</strong> {amount:C}</li>
                            <li><strong>Booking ID:</strong> {bookingId}</li>
                            <li><strong>Destination:</strong> {booking.TravelPackage.Destination}, {booking.TravelPackage.Country}</li>
                            <li><strong>Payment Status:</strong> {payment.Status}</li>
                        </ul>
                    </div>
                    <p>Your booking is now confirmed. You will receive a separate confirmation email with your itinerary.</p>
                    <p style='color: #2563eb; font-weight: bold;'>Please see the attached PDF file as your payment receipt.</p>
                    <p>Thank you for choosing TripWings!</p>
                    <p>Best regards,<br>TripWings Team</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(userEmail, subject, body, pdfBytes, pdfFileName);
    }

    public async Task SendTripReminderAsync(string userEmail, string userName, int bookingId, DateTime departureDate)
    {
        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            _logger.LogWarning($"Booking {bookingId} not found for reminder email");
            return;
        }

        var daysUntilTrip = (departureDate.Date - DateTime.UtcNow.Date).Days;
        var subject = $"Reminder: Your Trip to {booking.TravelPackage.Destination} is in {daysUntilTrip} Days!";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #f59e0b; text-align: center;'>Trip Reminder</h2>
                    <p>Dear {userName},</p>
                    <p>This is a friendly reminder that your trip is coming up soon!</p>
                    <div style='background-color: #fef3c7; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #f59e0b;'>
                        <h3>Your Trip Details:</h3>
                        <ul style='list-style: none; padding: 0;'>
                            <li><strong>Destination:</strong> {booking.TravelPackage.Destination}, {booking.TravelPackage.Country}</li>
                            <li><strong>Departure Date:</strong> {departureDate:dddd, MMMM dd, yyyy}</li>
                            <li><strong>Return Date:</strong> {booking.TravelPackage.EndDate:dddd, MMMM dd, yyyy}</li>
                            <li><strong>Rooms:</strong> {booking.RoomsCount}</li>
                            <li><strong>Days Remaining:</strong> {daysUntilTrip} days</li>
                        </ul>
                    </div>
                    <div style='background-color: #fee2e2; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #dc2626;'>
                        <p><strong>Important:</strong></p>
                        <ul>
                            <li>Please arrive at the airport at least 2 hours before departure</li>
                            <li>Bring valid identification documents</li>
                            <li>Download your itinerary from your dashboard</li>
                        </ul>
                    </div>
                    <p style='text-align: center; color: #059669; font-weight: bold;'>We're excited to have you travel with us!</p>
                    <p style='text-align: center;'>Best regards,<br>TripWings Team</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(userEmail, subject, body);
    }
}
