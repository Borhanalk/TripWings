namespace TripWings.Models;

public class CartItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int TravelPackageId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public TravelPackage TravelPackage { get; set; } = null!;
}
