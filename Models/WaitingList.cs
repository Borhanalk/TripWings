namespace TripWings.Models;

public class WaitingList
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int PackageId { get; set; }
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public int NumberOfTravelers { get; set; } = 1;
    public bool IsNotified { get; set; } = false;
    public DateTime? NotificationDate { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Package Package { get; set; } = null!;
}
