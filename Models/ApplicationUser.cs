using Microsoft.AspNetCore.Identity;

namespace TripWings.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitingListEntry> WaitingListEntries { get; set; } = new List<WaitingListEntry>();
    public ICollection<ReviewTrip> ReviewTrips { get; set; } = new List<ReviewTrip>();
    public ICollection<ReviewService> ReviewServices { get; set; } = new List<ReviewService>();
    public ICollection<SiteComment> SiteComments { get; set; } = new List<SiteComment>();
    public ICollection<CommentRating> CommentRatings { get; set; } = new List<CommentRating>();
}
