using TripWings.Models;

namespace TripWings.Services;

public interface IItineraryService
{
    Task<byte[]> GenerateItineraryPdfAsync(Booking booking);
}
