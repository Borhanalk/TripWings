// C# code to add two trips with full rooms
// You can run this code in a controller action or console app

using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using Microsoft.AspNetCore.Identity;

public static class AddFullTripsHelper
{
    public static async Task AddFullTripsAsync(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager)
    {
        // Get admin user or first available user
        var adminUser = await userManager.FindByEmailAsync("admin@tripwings.com");
        if (adminUser == null)
        {
            adminUser = await userManager.Users.FirstOrDefaultAsync();
        }

        if (adminUser == null)
        {
            throw new Exception("No users found in database. Please create a user first.");
        }

        // Check if trips already exist
        var existingMaldives = await context.TravelPackages
            .FirstOrDefaultAsync(t => t.Destination == "Maldives" && t.Country == "Maldives");
        var existingSantorini = await context.TravelPackages
            .FirstOrDefaultAsync(t => t.Destination == "Santorini" && t.Country == "Greece");

        // Add Trip 1: Maldives (8 rooms, all booked)
        if (existingMaldives == null)
        {
            var maldivesTrip = new TravelPackage
            {
                Destination = "Maldives",
                Country = "Maldives",
                StartDate = DateTime.UtcNow.AddDays(35),
                EndDate = DateTime.UtcNow.AddDays(42),
                Price = 3500.00m,
                AvailableRooms = 8,
                PackageType = "Luxury",
                AgeLimit = null,
                Description = "Paradise islands with crystal clear waters",
                IsVisible = true,
                CreatedAt = DateTime.UtcNow
            };

            context.TravelPackages.Add(maldivesTrip);
            await context.SaveChangesAsync();

            // Add image
            context.PackageImages.Add(new PackageImage
            {
                TravelPackageId = maldivesTrip.Id,
                ImageUrl = "https://images.unsplash.com/photo-1512343879784-a960bf40e7f2?w=800"
            });

            // Create bookings to fill all 8 rooms
            context.Bookings.AddRange(new[]
            {
                new Booking
                {
                    UserId = adminUser.Id,
                    TravelPackageId = maldivesTrip.Id,
                    RoomsCount = 3,
                    Status = BookingStatus.Active,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = DateTime.UtcNow
                },
                new Booking
                {
                    UserId = adminUser.Id,
                    TravelPackageId = maldivesTrip.Id,
                    RoomsCount = 2,
                    Status = BookingStatus.Active,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = DateTime.UtcNow
                },
                new Booking
                {
                    UserId = adminUser.Id,
                    TravelPackageId = maldivesTrip.Id,
                    RoomsCount = 3,
                    Status = BookingStatus.Active,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = DateTime.UtcNow
                }
            }); // Total: 8 rooms

            await context.SaveChangesAsync();
            Console.WriteLine("Added Maldives trip with 8 rooms (all booked)");
        }

        // Add Trip 2: Santorini (12 rooms, all booked)
        if (existingSantorini == null)
        {
            var santoriniTrip = new TravelPackage
            {
                Destination = "Santorini",
                Country = "Greece",
                StartDate = DateTime.UtcNow.AddDays(50),
                EndDate = DateTime.UtcNow.AddDays(57),
                Price = 2800.00m,
                AvailableRooms = 12,
                PackageType = "Luxury",
                AgeLimit = null,
                Description = "Stunning Greek island with white buildings and blue domes",
                IsVisible = true,
                CreatedAt = DateTime.UtcNow
            };

            context.TravelPackages.Add(santoriniTrip);
            await context.SaveChangesAsync();

            // Add image
            context.PackageImages.Add(new PackageImage
            {
                TravelPackageId = santoriniTrip.Id,
                ImageUrl = "https://images.unsplash.com/photo-1570077188670-e3a8d69ac5ff?w=800"
            });

            // Create bookings to fill all 12 rooms
            context.Bookings.AddRange(new[]
            {
                new Booking
                {
                    UserId = adminUser.Id,
                    TravelPackageId = santoriniTrip.Id,
                    RoomsCount = 4,
                    Status = BookingStatus.Active,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = DateTime.UtcNow
                },
                new Booking
                {
                    UserId = adminUser.Id,
                    TravelPackageId = santoriniTrip.Id,
                    RoomsCount = 4,
                    Status = BookingStatus.Active,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = DateTime.UtcNow
                },
                new Booking
                {
                    UserId = adminUser.Id,
                    TravelPackageId = santoriniTrip.Id,
                    RoomsCount = 4,
                    Status = BookingStatus.Active,
                    PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = DateTime.UtcNow
                }
            }); // Total: 12 rooms

            await context.SaveChangesAsync();
            Console.WriteLine("Added Santorini trip with 12 rooms (all booked)");
        }
    }
}
