using TripWings.Models;

namespace TripWings.Models.ViewModels;

public class AdminUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsAdmin { get; set; }
    public int BookingsCount { get; set; }
    public int PaymentsCount { get; set; }
    public decimal TotalSpent { get; set; }
}

public class AdminUserDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsAdmin { get; set; }
    public List<Booking> Bookings { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public decimal TotalSpent { get; set; }
}
