namespace TripWings.Models;

public class Review
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int PackageId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    public bool IsApproved { get; set; } = false; // Admin approval

    public ApplicationUser User { get; set; } = null!;
    public Package Package { get; set; } = null!;
}
