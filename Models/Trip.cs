namespace TripWings.Models;

public class Trip
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Duration { get; set; } // in days
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MaxCapacity { get; set; }
    public int CurrentBookings { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public bool IsAvailable => IsActive && CurrentBookings < MaxCapacity;
    public int AvailableSpots => MaxCapacity - CurrentBookings;
}
