using System.ComponentModel.DataAnnotations;

namespace TripWings.Models.ViewModels;

public class BookingViewModel
{
    public int TravelPackageId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Rooms count must be at least 1")]
    public int RoomsCount { get; set; } = 1;
}
