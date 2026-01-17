namespace TripWings.Models.ViewModels;

public class WaitingListViewModel
{
    public int Id { get; set; }
    public int TravelPackageId { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public int Position { get; set; }
    public int TotalWaiting { get; set; }
    public TimeSpan? EstimatedWaitTime { get; set; }
    public bool IsActive { get; set; }
    public DateTime? NotifiedAt { get; set; }
}
