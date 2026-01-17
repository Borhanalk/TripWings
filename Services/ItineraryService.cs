using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TripWings.Models;

namespace TripWings.Services;

public class ItineraryService : IItineraryService
{
    private readonly ILogger<ItineraryService> _logger;

    public ItineraryService(ILogger<ItineraryService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateItineraryPdfAsync(Booking booking)
    {
        return await Task.Run(() =>
        {
            var itineraryNumber = $"ITN-{booking.Id:D6}";
            var itineraryDate = booking.CreatedAt.ToString("dd/MM/yyyy");
            var ownerName = "Borhan Kean";
            var duration = (booking.TravelPackage.EndDate - booking.TravelPackage.StartDate).Days;

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
                                    col.Item().Text("תוכנית נסיעה / Travel Itinerary")
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
                                    col.Item().Text($"מספר תוכנית / Itinerary Number: {itineraryNumber}")
                                        .Bold()
                                        .FontSize(12);
                                    col.Item().Text($"תאריך יצירה / Issue Date: {itineraryDate}")
                                        .FontSize(11);
                                });
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Black);

                            column.Item().Text("פרטי הנוסע / Traveler Information")
                                .Bold()
                                .FontSize(12);
                            column.Item().PaddingLeft(10).Column(col =>
                            {
                                col.Item().Text($"שם / Name: {booking.User.FirstName} {booking.User.LastName}");
                                col.Item().Text($"אימייל / Email: {booking.User.Email}");
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Black);

                            column.Item().Text("פרטי ההזמנה / Booking Information")
                                .Bold()
                                .FontSize(12);
                            column.Item().PaddingLeft(10).Column(col =>
                            {
                                col.Item().Text($"מספר הזמנה / Booking ID: #{booking.Id}");
                                col.Item().Text($"תאריך הזמנה / Booking Date: {booking.CreatedAt:dd/MM/yyyy HH:mm}");
                                var statusText = booking.Status switch
                                {
                                    BookingStatus.Active => "פעיל / Active",
                                    BookingStatus.Cancelled => "בוטל / Cancelled",
                                    BookingStatus.Completed => "הושלם / Completed",
                                    _ => "לא ידוע / Unknown"
                                };
                                col.Item().Text($"סטטוס / Status: {statusText}");
                                var paymentStatusText = booking.PaymentStatus switch
                                {
                                    PaymentStatus.Paid => "שולם / Paid",
                                    PaymentStatus.Pending => "ממתין / Pending",
                                    PaymentStatus.Refunded => "הוחזר / Refunded",
                                    PaymentStatus.Failed => "נכשל / Failed",
                                    _ => "לא ידוע / Unknown"
                                };
                                col.Item().Text($"סטטוס תשלום / Payment Status: {paymentStatusText}");
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Black);

                            column.Item().Text("פרטי החבילה / Package Details")
                                .Bold()
                                .FontSize(12);
                            column.Item().PaddingLeft(10).Column(col =>
                            {
                                col.Item().Text($"יעד / Destination: {booking.TravelPackage.Destination}");
                                col.Item().Text($"מדינה / Country: {booking.TravelPackage.Country}");
                                col.Item().Text($"סוג חבילה / Package Type: {booking.TravelPackage.PackageType}");
                                if (booking.TravelPackage.AgeLimit.HasValue)
                                {
                                    col.Item().Text($"הגבלת גיל / Age Limit: {booking.TravelPackage.AgeLimit}+ שנים / years");
                                }
                                col.Item().Text($"מספר חדרים / Rooms: {booking.RoomsCount}");
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Black);

                            column.Item().Text("לוח זמנים / Travel Schedule")
                                .Bold()
                                .FontSize(12);
                            column.Item().PaddingLeft(10).Column(col =>
                            {
                                col.Item().Text($"תאריך יציאה / Departure Date: {booking.TravelPackage.StartDate:dd/MM/yyyy}");
                                col.Item().Text($"תאריך חזרה / Return Date: {booking.TravelPackage.EndDate:dd/MM/yyyy}");
                                col.Item().Text($"משך הטיול / Duration: {duration} יום/ימים / day(s)");
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Black);

                            if (!string.IsNullOrEmpty(booking.TravelPackage.Description))
                            {
                                column.Item().Text("תיאור / Description")
                                    .Bold()
                                    .FontSize(12);
                                column.Item().PaddingLeft(10).Text(booking.TravelPackage.Description)
                                    .FontSize(10);
                                column.Item().LineHorizontal(1).LineColor(Colors.Black);
                            }

                            column.Item().Text("הערות חשובות / Important Notes")
                                .Bold()
                                .FontSize(12);
                            column.Item().PaddingLeft(10).Column(col =>
                            {
                                col.Item().Text("• אנא הגיעו לשדה התעופה לפחות שעתיים לפני היציאה");
                                col.Item().Text("• Please arrive at the airport at least 2 hours before departure");
                                col.Item().Text("• הביאו מסמכי זיהוי תקפים");
                                col.Item().Text("• Bring valid identification documents");
                                col.Item().Text("• צרו קשר עם תמיכת TripWings לכל שינוי או ביטול");
                                col.Item().Text("• Contact TripWings support for any changes or cancellations");
                                col.Item().Text("• שמרו מסמך זה לתיעוד");
                                col.Item().Text("• Keep this document for your records");
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

            return document.GeneratePdf();
        });
    }
}
