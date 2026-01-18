using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
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
        var useApi = emailSettings.GetValue<bool>("UseApi", false);

        _logger.LogInformation($"=== EMAIL SEND ATTEMPT ===");
        _logger.LogInformation($"To: {toEmail}, Subject: {subject}");
        _logger.LogInformation($"SendEmails setting: {sendEmails}, LogOnly setting: {logOnly}, UseApi: {useApi}");

        if (logOnly || !sendEmails)
        {
            _logger.LogWarning($"⚠️ EMAIL (Log Only Mode): Email will NOT be sent!");
            _logger.LogWarning($"  - To: {toEmail}");
            _logger.LogWarning($"  - Subject: {subject}");
            _logger.LogWarning($"  - SendEmails: {sendEmails}, LogOnly: {logOnly}");
            _logger.LogInformation($"EMAIL Body (first 200 chars): {body.Substring(0, Math.Min(200, body.Length))}...");
            if (attachmentBytes != null)
            {
                _logger.LogInformation($"EMAIL Attachment: {attachmentFileName} ({attachmentBytes.Length} bytes)");
            }
            _logger.LogWarning("⚠️ To actually send emails, set EmailSettings:SendEmails=true and EmailSettings:LogOnly=false in appsettings.json");
            return;
        }

        if (useApi)
        {
            try
            {
                await SendEmailViaApiAsync(toEmail, subject, body, attachmentBytes, attachmentFileName);
                return;
            }
            catch (Exception apiEx)
            {
                _logger.LogWarning(apiEx, $"API email sending failed, falling back to SMTP. Error: {apiEx.Message}");
                // Fall through to SMTP as fallback
            }
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

            // Validate all required email settings
            if (string.IsNullOrEmpty(smtpServer))
            {
                _logger.LogError("✗✗✗ Email settings error: SmtpServer is missing or empty. Email not sent.");
                return;
            }

            if (string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogError("✗✗✗ Email settings error: SenderEmail is missing or empty. Email not sent.");
                return;
            }

            if (string.IsNullOrEmpty(username))
            {
                _logger.LogError("✗✗✗ Email settings error: Username is missing or empty. Email not sent.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                _logger.LogError("✗✗✗ Email settings error: Password is missing or empty. Email not sent.");
                return;
            }

            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.LogError("✗✗✗ Email settings error: Recipient email address is empty. Email not sent.");
                return;
            }

            _logger.LogInformation($"✓ Email settings validated successfully");
            _logger.LogInformation($"  - SMTP Server: {smtpServer}");
            _logger.LogInformation($"  - SMTP Port: {smtpPort}");
            _logger.LogInformation($"  - Sender: {senderEmail} ({senderName})");
            _logger.LogInformation($"  - Username: {username}");
            _logger.LogInformation($"  - Enable SSL: {enableSsl}");

            _logger.LogInformation($"Sending email via SMTP: Server={smtpServer}, Port={smtpPort}, From={senderEmail}, To={toEmail}");

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password),
                Timeout = 30000 // 30 seconds timeout
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
                _logger.LogInformation($"Adding attachment: {attachmentFileName} ({attachmentBytes.Length} bytes)");
            }

            try
            {
                _logger.LogInformation($"📧 Attempting to send email via SMTP...");
                await client.SendMailAsync(message);
                _logger.LogInformation($"✓✓✓ Email sent successfully via SMTP to {toEmail}" + (attachmentBytes != null ? $" with attachment {attachmentFileName}" : ""));
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, $"✗✗✗ SMTP Error sending email to {toEmail}");
                _logger.LogError($"  - StatusCode: {smtpEx.StatusCode}");
                _logger.LogError($"  - Message: {smtpEx.Message}");
                _logger.LogError($"  - Inner Exception: {smtpEx.InnerException?.Message ?? "None"}");
                // Don't re-throw - log the error but don't crash
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"✗✗✗ Unexpected error sending email to {toEmail}");
                _logger.LogError($"  - Exception Type: {ex.GetType().Name}");
                _logger.LogError($"  - Message: {ex.Message}");
                _logger.LogError($"  - Stack Trace: {ex.StackTrace}");
                // Don't re-throw - log the error but don't crash
            }
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, $"✗✗✗ SMTP Exception caught in outer catch block for {toEmail}");
            _logger.LogError($"  - StatusCode: {smtpEx.StatusCode}");
            _logger.LogError($"  - Message: {smtpEx.Message}");
            // Don't re-throw here - we want to log but not crash the application
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"✗✗✗ General Exception caught in outer catch block for {toEmail}");
            _logger.LogError($"  - Exception Type: {ex.GetType().Name}");
            _logger.LogError($"  - Message: {ex.Message}");
            // Don't re-throw here - we want to log but not crash the application
        }
    }

    private async Task SendEmailViaApiAsync(string toEmail, string subject, string body, byte[]? attachmentBytes, string? attachmentFileName)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        var apiKey = emailSettings["ApiKey"];
        var apiUrl = emailSettings["ApiUrl"];
        var senderEmail = emailSettings["SenderEmail"];
        var senderName = emailSettings["SenderName"];

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("✗✗✗ Email API settings error: ApiKey is missing or empty. Email not sent.");
            throw new InvalidOperationException("API Key is missing");
        }

        if (string.IsNullOrEmpty(apiUrl))
        {
            _logger.LogError("✗✗✗ Email API settings error: ApiUrl is missing or empty. Email not sent.");
            throw new InvalidOperationException("API URL is missing");
        }

        if (string.IsNullOrEmpty(toEmail))
        {
            _logger.LogError("✗✗✗ Email API settings error: Recipient email address is empty. Email not sent.");
            throw new InvalidOperationException("Recipient email is empty");
        }

        _logger.LogInformation($"✓ Email API settings validated successfully");
        _logger.LogInformation($"  - API URL: {apiUrl}");
        _logger.LogInformation($"  - Sender: {senderEmail} ({senderName})");
        _logger.LogInformation($"  - To: {toEmail}");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Try different API authentication methods
        if (!string.IsNullOrEmpty(apiKey))
        {
            // Try Bearer token first
            if (!apiKey.Contains(" ") || apiKey.Length > 50)
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
            else
            {
                // If API key contains spaces, it might be a different format
                httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            }
        }

        var emailData = new
        {
            to = toEmail,
            from = senderEmail,
            fromName = senderName,
            subject = subject,
            html = body,
            text = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]*>", "")
        };

        var json = JsonSerializer.Serialize(emailData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation($"📧 Attempting to send email via API: {apiUrl}");

        var response = await httpClient.PostAsync(apiUrl, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation($"✓✓✓ Email sent successfully via API to {toEmail}");
            _logger.LogInformation($"  - Response: {responseContent}");
        }
        else
        {
            _logger.LogError($"✗✗✗ API Error sending email to {toEmail}");
            _logger.LogError($"  - Status Code: {response.StatusCode}");
            _logger.LogError($"  - Response: {responseContent}");
            throw new HttpRequestException($"API returned status code {response.StatusCode}: {responseContent}");
        }
    }

    public async Task SendWaitingListNotificationAsync(string userEmail, string userName, int travelPackageId, int position)
    {
        _logger.LogInformation($"=== SENDING WAITING LIST NOTIFICATION ===");
        _logger.LogInformation($"To: {userEmail}, User: {userName}, Package: {travelPackageId}, Position: #{position}");
        
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            _logger.LogError($"Cannot send waiting list notification: userEmail is null or empty");
            return;
        }
        
        var package = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);
        
        if (package == null)
        {
            _logger.LogError($"Package {travelPackageId} not found when trying to send waiting list notification");
            return;
        }
        
        var packageName = package.Destination ?? "Travel Package";
        var country = package.Country ?? "";
        var startDate = package.StartDate.ToString("dd/MM/yyyy");
        var endDate = package.EndDate.ToString("dd/MM/yyyy");
        var price = package.Price.ToString("C");

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
        var bookingUrl = $"{baseUrl}/Trips/Details/{travelPackageId}";
        
        var subject = "Room Available Now - Book Within 10 Minutes! / חדר זמין כעת - הזמן תוך 10 דקות!";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd;'>
                    <h2 style='color: #2563eb; text-align: center;'>Room Available Now! / חדר זמין כעת!</h2>
                    <p>Dear {userName},</p>
                    <p><strong>Great news! A room has become available for {packageName}, {country}. You can now book this room!</strong></p>
                    <p><strong>חדשות נהדרות! חדר הפך זמין עבור {packageName}, {country}. אתה יכול כעת להזמין את החדר הזה!</strong></p>
                    <div style='background-color: #dbeafe; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #2563eb;'>
                        <p style='font-size: 18px; font-weight: bold; color: #1e40af; margin: 0;'>
                            ✅ You can now book this room! Click the button below to proceed with your booking.
                        </p>
                        <p style='font-size: 18px; font-weight: bold; color: #1e40af; margin: 10px 0 0 0;'>
                            ✅ אתה יכול כעת להזמין את החדר הזה! לחץ על הכפתור למטה כדי להמשיך עם ההזמנה.
                        </p>
                    </div>
                    <div style='background-color: #fee2e2; padding: 20px; border-radius: 5px; margin: 20px 0; border-left: 5px solid #dc2626;'>
                        <h3 style='color: #dc2626; margin-top: 0; font-size: 24px;'>⚠️ URGENT: 10 Minutes Only! ⚠️</h3>
                        <h3 style='color: #dc2626; margin-top: 0; font-size: 24px;'>⚠️ דחוף: רק 10 דקות! ⚠️</h3>
                        <p style='font-size: 20px; font-weight: bold; color: #dc2626; margin: 15px 0;'>
                            ⏰ You have ONLY 10 MINUTES to book this room! ⏰
                        </p>
                        <p style='font-size: 20px; font-weight: bold; color: #dc2626; margin: 15px 0;'>
                            ⏰ יש לך רק 10 דקות להזמין את החדר הזה! ⏰
                        </p>
                        <p style='font-size: 16px; color: #991b1b; margin: 10px 0;'>
                            If you don't book within 10 minutes, you will be automatically removed from the waiting list and the next user will be notified.
                        </p>
                        <p style='font-size: 16px; color: #991b1b; margin: 10px 0;'>
                            אם לא תזמין תוך 10 דקות, תוסר אוטומטית מרשימת ההמתנה והמשתמש הבא יקבל הודעה.
                        </p>
                        <div style='background-color: #fff; padding: 15px; border-radius: 5px; margin-top: 15px; text-align: center;'>
                            <p style='font-size: 22px; font-weight: bold; color: #dc2626; margin: 0;'>
                                ⏱️ Time Limit: 10 Minutes / מגבלת זמן: 10 דקות ⏱️
                            </p>
                        </div>
                    </div>
                    <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <h3>Trip Details / פרטי הטיול:</h3>
                        <ul style='list-style: none; padding: 0;'>
                            <li><strong>Destination / יעד:</strong> {packageName}, {country}</li>
                            <li><strong>Travel Dates / תאריכי נסיעה:</strong> {startDate} - {endDate}</li>
                            <li><strong>Price / מחיר:</strong> {price}</li>
                            <li><strong>Your Position / המיקום שלך:</strong> #{position}</li>
                        </ul>
                    </div>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{bookingUrl}' style='background-color: #2563eb; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; font-size: 18px; display: inline-block;'>
                            Book Now / הזמן כעת
                        </a>
                    </div>
                    <div style='background-color: #fef3c7; padding: 15px; border-radius: 5px; margin: 20px 0; text-align: center; border: 2px solid #f59e0b;'>
                        <p style='color: #dc2626; font-weight: bold; font-size: 18px; margin: 0;'>
                            ⚠️ IMPORTANT: You must book within 10 minutes! ⚠️
                        </p>
                        <p style='color: #dc2626; font-weight: bold; font-size: 18px; margin: 10px 0 0 0;'>
                            ⚠️ חשוב: אתה חייב להזמין תוך 10 דקות! ⚠️
                        </p>
                    </div>
                    <p>Best regards,<br>TripWings Team</p>
                </div>
            </body>
            </html>";

        _logger.LogInformation($"Attempting to send email to {userEmail} for package {travelPackageId}");
        try
        {
            await SendEmailAsync(userEmail, subject, body);
            _logger.LogInformation($"✓✓✓ SendEmailAsync completed successfully for {userEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"✗✗✗ Exception in SendEmailAsync for {userEmail}: {ex.Message}");
            _logger.LogError($"  - Stack Trace: {ex.StackTrace}");
            // Don't re-throw - we want to log but not crash the application
            // The waiting list entry is already marked as notified
        }
        _logger.LogInformation($"=== WAITING LIST NOTIFICATION PROCESS COMPLETED ===");
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
