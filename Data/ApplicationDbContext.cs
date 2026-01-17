using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TripWings.Models;

namespace TripWings.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Trip> Trips { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<TravelPackage> TravelPackages { get; set; }
    public DbSet<PackageImage> PackageImages { get; set; }
    public DbSet<Discount> Discounts { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<WaitingListEntry> WaitingListEntries { get; set; }
    public DbSet<ReviewTrip> ReviewTrips { get; set; }
    public DbSet<ReviewService> ReviewServices { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<UserWallet> UserWallets { get; set; }
    public DbSet<WalletTransaction> WalletTransactions { get; set; }
    public DbSet<BankWithdrawal> BankWithdrawals { get; set; }
    public DbSet<SiteComment> SiteComments { get; set; }
    public DbSet<CommentRating> CommentRatings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Trip>(entity =>
        {
            entity.HasIndex(t => t.Name);
            entity.Property(t => t.Price).HasPrecision(18, 2);
        });

        builder.Entity<CartItem>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany(u => u.CartItems)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.TravelPackage)
                  .WithMany(t => t.CartItems)
                  .HasForeignKey(c => c.TravelPackageId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TravelPackage>(entity =>
        {
            entity.HasIndex(t => t.Destination);
            entity.HasIndex(t => t.Country);
            entity.Property(t => t.Price).HasPrecision(18, 2);
            entity.ToTable(t => 
            {
                t.HasCheckConstraint("CK_TravelPackage_EndDate", "EndDate > StartDate");
                t.HasCheckConstraint("CK_TravelPackage_AvailableRooms", "AvailableRooms >= 0 AND AvailableRooms <= TotalRooms");
            });
        });

        builder.Entity<PackageImage>(entity =>
        {
            entity.HasOne(p => p.TravelPackage)
                  .WithMany(t => t.PackageImages)
                  .HasForeignKey(p => p.TravelPackageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Discount>(entity =>
        {
            entity.HasOne(d => d.TravelPackage)
                  .WithMany(t => t.Discounts)
                  .HasForeignKey(d => d.TravelPackageId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(d => d.OldPrice).HasPrecision(18, 2);
            entity.Property(d => d.NewPrice).HasPrecision(18, 2);
            entity.ToTable(t => 
            {
                t.HasCheckConstraint("CK_Discount_NewPrice", "NewPrice < OldPrice");
                t.HasCheckConstraint("CK_Discount_EndAt", "EndAt > StartAt");
                t.HasCheckConstraint("CK_Discount_Duration", "DATEDIFF(day, StartAt, EndAt) <= 7");
            });
        });

        builder.Entity<Booking>(entity =>
        {
            entity.HasOne(b => b.User)
                  .WithMany(u => u.Bookings)
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.TravelPackage)
                  .WithMany(t => t.Bookings)
                  .HasForeignKey(b => b.TravelPackageId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(b => new { b.UserId, b.TravelPackageId });
        });

        builder.Entity<WaitingListEntry>(entity =>
        {
            entity.HasOne(w => w.User)
                  .WithMany(u => u.WaitingListEntries)
                  .HasForeignKey(w => w.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(w => w.TravelPackage)
                  .WithMany(t => t.WaitingListEntries)
                  .HasForeignKey(w => w.TravelPackageId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(w => new { w.UserId, w.TravelPackageId });
        });

        builder.Entity<ReviewTrip>(entity =>
        {
            entity.HasOne(r => r.User)
                  .WithMany(u => u.ReviewTrips)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.TravelPackage)
                  .WithMany(t => t.ReviewTrips)
                  .HasForeignKey(r => r.TravelPackageId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint("CK_ReviewTrip_Rating", "Rating >= 1 AND Rating <= 5"));
        });

        builder.Entity<ReviewService>(entity =>
        {
            entity.HasOne(r => r.User)
                  .WithMany(u => u.ReviewServices)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint("CK_ReviewService_Rating", "Rating >= 1 AND Rating <= 5"));
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasOne(p => p.Booking)
                  .WithMany()
                  .HasForeignKey(p => p.BookingId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Discount)
                  .WithMany()
                  .HasForeignKey(p => p.DiscountId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.DiscountAmount).HasPrecision(18, 2);
            entity.Property(p => p.FinalAmount).HasPrecision(18, 2);
            entity.Property(p => p.InstallmentAmount).HasPrecision(18, 2);
        });

        builder.Entity<SiteComment>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany(u => u.SiteComments)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint("CK_SiteComment_Rating", "Rating >= 1 AND Rating <= 5"));
            
            entity.HasIndex(c => c.CreatedAt);
            entity.HasIndex(c => c.IsApproved);
        });

        builder.Entity<CommentRating>(entity =>
        {
            entity.HasOne(r => r.SiteComment)
                  .WithMany(c => c.Ratings)
                  .HasForeignKey(r => r.SiteCommentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.User)
                  .WithMany(u => u.CommentRatings)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.SiteCommentId, r.UserId }).IsUnique();
        });
    }
}
