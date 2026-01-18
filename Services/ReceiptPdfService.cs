using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Services;

public interface IReceiptPdfService
{
    Task<byte[]> GeneratePaymentReceiptAsync(int paymentId);
}

public class ReceiptPdfService : IReceiptPdfService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReceiptPdfService> _logger;

    public ReceiptPdfService(
        ApplicationDbContext context,
        ILogger<ReceiptPdfService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePaymentReceiptAsync(int paymentId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var payment = await _context.Payments
            .Include(p => p.User)
            .Include(p => p.Booking)
                .ThenInclude(b => b.TravelPackage)
            .Include(p => p.Discount)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            throw new ArgumentException($"Payment {paymentId} not found");
        }

        var receiptNumber = $"REC-{paymentId:D6}";
        var receiptDate = payment.PaymentDate.ToString("dd/MM/yyyy");
        var ownerName = "Borhan Kean";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header()
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Padding(15)
                    .Column(column =>
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("TripWings")
                                    .FontSize(24)
                                    .Bold()
                                    .AlignRight();
                                col.Item().Text("קבלת תשלום / Receipt")
                                    .FontSize(18)
                                    .Bold()
                                    .AlignRight();
                            });
                        });
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(15);

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text($"מספר קבלה / Receipt Number: {receiptNumber}")
                                    .Bold()
                                    .FontSize(12);
                                col.Item().Text($"תאריך / Date: {receiptDate}")
                                    .FontSize(11);
                            });
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        column.Item().Text("פרטי הלקוח / Customer Information")
                            .Bold()
                            .FontSize(12);
                        column.Item().PaddingLeft(10).Column(col =>
                        {
                            col.Item().Text($"שם / Name: {payment.User.FirstName} {payment.User.LastName}");
                            col.Item().Text($"אימייל / Email: {payment.User.Email}");
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        column.Item().Text("פרטי התשלום / Payment Details")
                            .Bold()
                            .FontSize(12);
                        column.Item().PaddingLeft(10).Column(col =>
                        {
                            col.Item().Text($"מספר הזמנה / Booking ID: #{payment.BookingId}");
                            col.Item().Text($"יעד / Destination: {payment.Booking.TravelPackage.Destination}, {payment.Booking.TravelPackage.Country}");
                            col.Item().Text($"מספר חדרים / Rooms: {payment.Booking.RoomsCount}");
                            col.Item().Text($"תאריכי נסיעה / Travel Dates: {payment.Booking.TravelPackage.StartDate:dd/MM/yyyy} - {payment.Booking.TravelPackage.EndDate:dd/MM/yyyy}");
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        column.Item().Text("סיכום תשלום / Payment Summary")
                            .Bold()
                            .FontSize(12);
                        column.Item().PaddingLeft(10).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text("סכום מקורי / Original Amount:");
                                row.AutoItem().Text($"{payment.Amount:C}")
                                    .Bold();
                            });
                            if (payment.DiscountAmount > 0)
                            {
                                col.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("הנחה / Discount:");
                                    row.AutoItem().Text($"-{payment.DiscountAmount:C}");
                                });
                            }
                            col.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text("סכום סופי / Final Amount:")
                                    .Bold()
                                    .FontSize(13);
                                row.AutoItem().Text($"{payment.FinalAmount:C}")
                                    .Bold()
                                    .FontSize(13);
                            });
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        column.Item().Text("שיטת תשלום / Payment Method")
                            .Bold()
                            .FontSize(12);
                        column.Item().PaddingLeft(10).Column(col =>
                        {
                            var paymentMethodText = payment.PaymentMethod switch
                            {
                                PaymentMethod.CreditCard => "כרטיס אשראי / Credit Card",
                                PaymentMethod.DebitCard => "כרטיס חיוב / Debit Card",
                                PaymentMethod.BankTransfer => "העברה בנקאית / Bank Transfer",
                                PaymentMethod.Cash => "מזומן / Cash",
                                _ => "אחר / Other"
                            };
                            col.Item().Text(paymentMethodText);
                            if (payment.InstallmentsCount > 1)
                            {
                                col.Item().Text($"מספר תשלומים / Installments: {payment.InstallmentsCount}");
                                col.Item().Text($"סכום לכל תשלום / Installment Amount: {payment.InstallmentAmount:C}");
                            }
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text("סטטוס תשלום / Payment Status:")
                                .Bold();
                            var statusText = payment.Status switch
                            {
                                PaymentStatus.Paid => "שולם / Paid",
                                PaymentStatus.Pending => "ממתין / Pending",
                                PaymentStatus.Refunded => "הוחזר / Refunded",
                                PaymentStatus.Failed => "נכשל / Failed",
                                _ => "לא ידוע / Unknown"
                            };
                            row.AutoItem().Text(statusText)
                                .Bold();
                        });
                    });

                page.Footer()
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Padding(15)
                    .Column(column =>
                    {
                        column.Item().AlignCenter().Column(col =>
                        {

                            col.Item().Border(2)
                                .BorderColor(Colors.Black)
                                .Padding(10)
                                .Width(150)
                                .Height(80)
                                .AlignCenter()
                                .Column(sealCol =>
                                {
                                    sealCol.Item().Text("חתימה / Signature")
                                        .FontSize(10)
                                        .AlignCenter();
                                    sealCol.Item().Text(ownerName)
                                        .FontSize(12)
                                        .Bold()
                                        .AlignCenter();
                                    sealCol.Item().Text("TripWings")
                                        .FontSize(10)
                                        .AlignCenter();
                                });
                        });
                        column.Item().PaddingTop(5).AlignCenter().Text($"© {DateTime.Now.Year} TripWings. All rights reserved.")
                            .FontSize(9);
                    });
            });
        });

        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;
        return stream.ToArray();
    }
}
