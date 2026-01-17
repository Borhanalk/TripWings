using TripWings.Models;

namespace TripWings.Models.ViewModels;

public class DashboardViewModel
{
    public string UserName { get; set; } = string.Empty;
    public List<Booking> CurrentBookings { get; set; } = new();
    public List<UpcomingBookingViewModel> UpcomingBookings { get; set; } = new();
    public int CartItemsCount { get; set; }
}

public class UpcomingBookingViewModel
{
    public Booking Booking { get; set; } = null!;
    public TimeSpan TimeUntilDeparture { get; set; }
    
    public string TimeUntilDepartureFormatted
    {
        get
        {
            if (TimeUntilDeparture.TotalDays >= 1)
            {
                return $"{TimeUntilDeparture.Days} day(s), {TimeUntilDeparture.Hours} hour(s)";
            }
            else if (TimeUntilDeparture.TotalHours >= 1)
            {
                return $"{TimeUntilDeparture.Hours} hour(s), {TimeUntilDeparture.Minutes} minute(s)";
            }
            else
            {
                return $"{TimeUntilDeparture.Minutes} minute(s)";
            }
        }
    }
}
