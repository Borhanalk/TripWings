using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Data;

public static class SeedData
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();



        try
        {
            await context.Database.EnsureCreatedAsync();
            
            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("SeedData");
            
            try
            {
                var sql = @"
                    IF EXISTS (
                        SELECT 1 
                        FROM sys.columns 
                        WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                        AND name = 'LastFourDigits'
                    )
                    BEGIN
                        ALTER TABLE [dbo].[Payments]
                        DROP COLUMN [LastFourDigits];
                    END";
                
                await context.Database.ExecuteSqlRawAsync(sql);
                logger?.LogInformation("LastFourDigits column removed from Payments table (if it existed).");
            }
            catch (Exception sqlEx)
            {
                logger?.LogWarning(sqlEx, "Could not remove LastFourDigits column. It may not exist.");
            }
        }
        catch (Exception ex)
        {
            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("SeedData");
            logger?.LogWarning(ex, "Warning: Could not ensure database is created. Continuing anyway...");
        }

        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = "admin@tripwings.com";
        var adminPassword = "Admin@123";

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        if (!context.TravelPackages.Any())
        {
            var packages = new List<TravelPackage>
            {
                new TravelPackage { Destination = "Paris", Country = "France", StartDate = DateTime.UtcNow.AddDays(30), EndDate = DateTime.UtcNow.AddDays(35), Price = 1500.00m, AvailableRooms = 20, PackageType = "Luxury", AgeLimit = null, Description = "Explore the City of Light", IsVisible = true },
                new TravelPackage { Destination = "Tokyo", Country = "Japan", StartDate = DateTime.UtcNow.AddDays(60), EndDate = DateTime.UtcNow.AddDays(67), Price = 2500.00m, AvailableRooms = 15, PackageType = "Cultural", AgeLimit = null, Description = "Experience vibrant Japanese culture", IsVisible = true },
                new TravelPackage { Destination = "Dubai", Country = "UAE", StartDate = DateTime.UtcNow.AddDays(45), EndDate = DateTime.UtcNow.AddDays(49), Price = 3000.00m, AvailableRooms = 10, PackageType = "Luxury", AgeLimit = null, Description = "Luxury experience in Dubai", IsVisible = true },
                new TravelPackage { Destination = "Bali", Country = "Indonesia", StartDate = DateTime.UtcNow.AddDays(75), EndDate = DateTime.UtcNow.AddDays(81), Price = 1800.00m, AvailableRooms = 25, PackageType = "Beach", AgeLimit = null, Description = "Tropical paradise", IsVisible = true },
                new TravelPackage { Destination = "New York", Country = "USA", StartDate = DateTime.UtcNow.AddDays(50), EndDate = DateTime.UtcNow.AddDays(55), Price = 2200.00m, AvailableRooms = 18, PackageType = "City", AgeLimit = null, Description = "The city that never sleeps", IsVisible = true },
                new TravelPackage { Destination = "London", Country = "UK", StartDate = DateTime.UtcNow.AddDays(40), EndDate = DateTime.UtcNow.AddDays(45), Price = 1900.00m, AvailableRooms = 22, PackageType = "Cultural", AgeLimit = null, Description = "Historic London experience", IsVisible = true },
                new TravelPackage { Destination = "Rome", Country = "Italy", StartDate = DateTime.UtcNow.AddDays(55), EndDate = DateTime.UtcNow.AddDays(60), Price = 1700.00m, AvailableRooms = 20, PackageType = "Historical", AgeLimit = null, Description = "Ancient Roman history", IsVisible = true },
                new TravelPackage { Destination = "Barcelona", Country = "Spain", StartDate = DateTime.UtcNow.AddDays(65), EndDate = DateTime.UtcNow.AddDays(70), Price = 1600.00m, AvailableRooms = 24, PackageType = "Beach", AgeLimit = null, Description = "Beautiful Spanish coast", IsVisible = true },
                new TravelPackage { Destination = "Sydney", Country = "Australia", StartDate = DateTime.UtcNow.AddDays(80), EndDate = DateTime.UtcNow.AddDays(87), Price = 2800.00m, AvailableRooms = 12, PackageType = "Adventure", AgeLimit = 18, Description = "Australian adventure", IsVisible = true },
                new TravelPackage { Destination = "Cairo", Country = "Egypt", StartDate = DateTime.UtcNow.AddDays(70), EndDate = DateTime.UtcNow.AddDays(75), Price = 1400.00m, AvailableRooms = 28, PackageType = "Historical", AgeLimit = null, Description = "Ancient Egyptian wonders", IsVisible = true },
                new TravelPackage { Destination = "Bangkok", Country = "Thailand", StartDate = DateTime.UtcNow.AddDays(85), EndDate = DateTime.UtcNow.AddDays(90), Price = 1200.00m, AvailableRooms = 30, PackageType = "Budget", AgeLimit = null, Description = "Thailand cultural tour", IsVisible = true },
                new TravelPackage { Destination = "Singapore", Country = "Singapore", StartDate = DateTime.UtcNow.AddDays(90), EndDate = DateTime.UtcNow.AddDays(94), Price = 2100.00m, AvailableRooms = 16, PackageType = "City", AgeLimit = null, Description = "Modern Singapore", IsVisible = true },
                new TravelPackage { Destination = "Istanbul", Country = "Turkey", StartDate = DateTime.UtcNow.AddDays(100), EndDate = DateTime.UtcNow.AddDays(105), Price = 1500.00m, AvailableRooms = 20, PackageType = "Cultural", AgeLimit = null, Description = "Bridge between continents", IsVisible = true },
                new TravelPackage { Destination = "Amsterdam", Country = "Netherlands", StartDate = DateTime.UtcNow.AddDays(110), EndDate = DateTime.UtcNow.AddDays(115), Price = 1700.00m, AvailableRooms = 18, PackageType = "City", AgeLimit = 18, Description = "Dutch capital experience", IsVisible = true },
                new TravelPackage { Destination = "Prague", Country = "Czech Republic", StartDate = DateTime.UtcNow.AddDays(120), EndDate = DateTime.UtcNow.AddDays(125), Price = 1300.00m, AvailableRooms = 25, PackageType = "Budget", AgeLimit = null, Description = "Medieval European city", IsVisible = true },
                new TravelPackage { Destination = "Vienna", Country = "Austria", StartDate = DateTime.UtcNow.AddDays(130), EndDate = DateTime.UtcNow.AddDays(135), Price = 1800.00m, AvailableRooms = 19, PackageType = "Cultural", AgeLimit = null, Description = "Classical music capital", IsVisible = true },
                new TravelPackage { Destination = "Athens", Country = "Greece", StartDate = DateTime.UtcNow.AddDays(140), EndDate = DateTime.UtcNow.AddDays(145), Price = 1600.00m, AvailableRooms = 21, PackageType = "Historical", AgeLimit = null, Description = "Ancient Greek history", IsVisible = true },
                new TravelPackage { Destination = "Lisbon", Country = "Portugal", StartDate = DateTime.UtcNow.AddDays(150), EndDate = DateTime.UtcNow.AddDays(155), Price = 1400.00m, AvailableRooms = 23, PackageType = "Beach", AgeLimit = null, Description = "Portuguese coastal city", IsVisible = true },
                new TravelPackage { Destination = "Berlin", Country = "Germany", StartDate = DateTime.UtcNow.AddDays(160), EndDate = DateTime.UtcNow.AddDays(165), Price = 1750.00m, AvailableRooms = 17, PackageType = "City", AgeLimit = null, Description = "Modern German capital", IsVisible = true },
                new TravelPackage { Destination = "Stockholm", Country = "Sweden", StartDate = DateTime.UtcNow.AddDays(170), EndDate = DateTime.UtcNow.AddDays(175), Price = 2000.00m, AvailableRooms = 14, PackageType = "Luxury", AgeLimit = null, Description = "Scandinavian beauty", IsVisible = true },
                new TravelPackage { Destination = "Oslo", Country = "Norway", StartDate = DateTime.UtcNow.AddDays(180), EndDate = DateTime.UtcNow.AddDays(185), Price = 2200.00m, AvailableRooms = 13, PackageType = "Adventure", AgeLimit = 16, Description = "Norwegian fjords", IsVisible = true },
                new TravelPackage { Destination = "Copenhagen", Country = "Denmark", StartDate = DateTime.UtcNow.AddDays(190), EndDate = DateTime.UtcNow.AddDays(195), Price = 1950.00m, AvailableRooms = 15, PackageType = "Family", AgeLimit = null, Description = "Hygge experience", IsVisible = true },
                new TravelPackage { Destination = "Zurich", Country = "Switzerland", StartDate = DateTime.UtcNow.AddDays(200), EndDate = DateTime.UtcNow.AddDays(205), Price = 2400.00m, AvailableRooms = 11, PackageType = "Luxury", AgeLimit = null, Description = "Swiss alpine experience", IsVisible = true },
                new TravelPackage { Destination = "Brussels", Country = "Belgium", StartDate = DateTime.UtcNow.AddDays(210), EndDate = DateTime.UtcNow.AddDays(215), Price = 1550.00m, AvailableRooms = 20, PackageType = "Cultural", AgeLimit = null, Description = "European capital", IsVisible = true },
                new TravelPackage { Destination = "Warsaw", Country = "Poland", StartDate = DateTime.UtcNow.AddDays(220), EndDate = DateTime.UtcNow.AddDays(225), Price = 1250.00m, AvailableRooms = 26, PackageType = "Budget", AgeLimit = null, Description = "Polish heritage", IsVisible = true },
                new TravelPackage { Destination = "Budapest", Country = "Hungary", StartDate = DateTime.UtcNow.AddDays(230), EndDate = DateTime.UtcNow.AddDays(235), Price = 1350.00m, AvailableRooms = 24, PackageType = "Cultural", AgeLimit = null, Description = "Danube river city", IsVisible = true },
                new TravelPackage { Destination = "Dublin", Country = "Ireland", StartDate = DateTime.UtcNow.AddDays(240), EndDate = DateTime.UtcNow.AddDays(245), Price = 1650.00m, AvailableRooms = 19, PackageType = "Family", AgeLimit = null, Description = "Irish charm", IsVisible = true },
                new TravelPackage { Destination = "Edinburgh", Country = "UK", StartDate = DateTime.UtcNow.AddDays(250), EndDate = DateTime.UtcNow.AddDays(255), Price = 1800.00m, AvailableRooms = 16, PackageType = "Historical", AgeLimit = null, Description = "Scottish heritage", IsVisible = true }
            };

            context.TravelPackages.AddRange(packages);
            await context.SaveChangesAsync();

            var packageImages = new List<PackageImage>();
            
            var destinationImages = new Dictionary<string, string>
            {
                { "Paris,France", "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800&q=80" },
                { "Tokyo,Japan", "https://images.unsplash.com/photo-1540959733332-eab4deabeeaf?w=800&q=80" },
                { "Dubai,UAE", "https://images.unsplash.com/photo-1512453979798-5ea266f8880c?w=800&q=80" },
                { "Bali,Indonesia", "https://images.unsplash.com/photo-1537996194471-e657df975ab4?w=800&q=80" },
                { "New York,USA", "https://images.unsplash.com/photo-1496442226666-8d4d0e62e6e9?w=800&q=80" },
                { "London,UK", "https://images.unsplash.com/photo-1513635269975-59663e0ac1ad?w=800&q=80" },
                { "Rome,Italy", "https://images.unsplash.com/photo-1529260830199-42c24126f198?w=800&q=80" },
                { "Barcelona,Spain", "https://images.unsplash.com/photo-1539037116277-4db20889f2d4?w=800&q=80" },
                { "Sydney,Australia", "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=800&q=80" },
                { "Cairo,Egypt", "https://images.unsplash.com/photo-1539650116574-75c0c6d73a6e?w=800&q=80" },
                { "Bangkok,Thailand", "https://images.unsplash.com/photo-1552465011-b4e21bf6e79a?w=800&q=80" },
                { "Singapore,Singapore", "https://images.unsplash.com/photo-1525625293386-3f8f99389edd?w=800&q=80" },
                { "Istanbul,Turkey", "https://images.unsplash.com/photo-1524231757912-21f4fe3a7200?w=800&q=80" },
                { "Amsterdam,Netherlands", "https://images.unsplash.com/photo-1534351590666-13e3e96b5017?w=800&q=80" },
                { "Prague,Czech Republic", "https://images.unsplash.com/photo-1541849546-216549ae216d?w=800&q=80" },
                { "Vienna,Austria", "https://images.unsplash.com/photo-1516550164669-4b0bb1b5b8ad?w=800&q=80" },
                { "Athens,Greece", "https://images.unsplash.com/photo-1605152276907-419341d0b4e2?w=800&q=80" },
                { "Lisbon,Portugal", "https://images.unsplash.com/photo-1555881400-74d7acaacd8b?w=800&q=80" },
                { "Berlin,Germany", "https://images.unsplash.com/photo-1587330979470-3585ac3d0c0e?w=800&q=80" },
                { "Stockholm,Sweden", "https://images.unsplash.com/photo-1508672019048-805c876b67e2?w=800&q=80" },
                { "Oslo,Norway", "https://images.unsplash.com/photo-1527004013197-933c4bb611b3?w=800&q=80" },
                { "Copenhagen,Denmark", "https://images.unsplash.com/photo-1513622470522-26c3c8a854bc?w=800&q=80" },
                { "Zurich,Switzerland", "https://images.unsplash.com/photo-1515488042361-ee00e0ddd4e4?w=800&q=80" },
                { "Brussels,Belgium", "https://images.unsplash.com/photo-1555993530-9c0e0e0b0b0b?w=800&q=80" },
                { "Warsaw,Poland", "https://images.unsplash.com/photo-1517154421773-0529f29ea451?w=800&q=80" },
                { "Budapest,Hungary", "https://images.unsplash.com/photo-1551867634-194f224b3f12?w=800&q=80" },
                { "Dublin,Ireland", "https://images.unsplash.com/photo-1561494653-744c43aed0c1?w=800&q=80" },
                { "Edinburgh,UK", "https://images.unsplash.com/photo-1483728642387-6c3bdd6c93e5?w=800&q=80" }
            };

            for (int i = 0; i < packages.Count; i++)
            {
                var package = packages[i];
                var key = $"{package.Destination},{package.Country}";
                
                string imageUrl;
                if (destinationImages.TryGetValue(key, out var url))
                {
                    imageUrl = url;
                }
                else
                {
                    imageUrl = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800";
                }
                
                packageImages.Add(new PackageImage
                {
                    TravelPackageId = package.Id,
                    ImageUrl = imageUrl
                });
            }

            context.PackageImages.AddRange(packageImages);
            await context.SaveChangesAsync();

            var discounts = new List<Discount>
            {
                new Discount { TravelPackageId = packages[0].Id, OldPrice = 1500.00m, NewPrice = 1200.00m, StartAt = DateTime.UtcNow.AddDays(-2), EndAt = DateTime.UtcNow.AddDays(5) },
                new Discount { TravelPackageId = packages[2].Id, OldPrice = 3000.00m, NewPrice = 2400.00m, StartAt = DateTime.UtcNow.AddDays(-1), EndAt = DateTime.UtcNow.AddDays(6) },
                new Discount { TravelPackageId = packages[4].Id, OldPrice = 2200.00m, NewPrice = 1760.00m, StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddDays(7) },
                new Discount { TravelPackageId = packages[6].Id, OldPrice = 1700.00m, NewPrice = 1360.00m, StartAt = DateTime.UtcNow.AddDays(-3), EndAt = DateTime.UtcNow.AddDays(4) }
            };

            context.Discounts.AddRange(discounts);
            await context.SaveChangesAsync();
        }
    }
}
