using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Services;

public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReminderBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public ReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Reminder Background Service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task SendRemindersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var reminderDate = now.AddDays(5).Date;
        var reminderDateEnd = reminderDate.AddDays(1);

        var bookingsToRemind = await context.Bookings
            .Include(b => b.TravelPackage)
            .Include(b => b.User)
            .Where(b => b.Status == BookingStatus.Active &&
                       b.TravelPackage.StartDate.Date >= reminderDate &&
                       b.TravelPackage.StartDate.Date < reminderDateEnd &&
                       b.TravelPackage.StartDate > now &&
                       !b.ReminderSent)
            .ToListAsync();

        _logger.LogInformation($"Found {bookingsToRemind.Count} bookings to send reminders for.");

        foreach (var booking in bookingsToRemind)
        {
            try
            {
                await notificationService.SendTripReminderAsync(
                    booking.User.Email!,
                    $"{booking.User.FirstName} {booking.User.LastName}",
                    booking.Id,
                    booking.TravelPackage.StartDate);

                booking.ReminderSent = true;
                await context.SaveChangesAsync();

                _logger.LogInformation($"Reminder sent for booking {booking.Id} to {booking.User.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send reminder for booking {booking.Id}");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reminder Background Service is stopping.");
        await base.StopAsync(cancellationToken);
    }
}
