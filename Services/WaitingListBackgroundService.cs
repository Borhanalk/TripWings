using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Services;

public class WaitingListBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WaitingListBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute

    public WaitingListBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WaitingListBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Waiting List Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredNotificationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Waiting List Background Service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredNotificationsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var waitingListService = scope.ServiceProvider.GetRequiredService<IWaitingListService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var packagesWithExpiredNotifications = await context.WaitingListEntries
            .Where(w => w.IsActive &&
                       w.NotifiedAt.HasValue &&
                       w.NotificationExpiresAt.HasValue &&
                       w.NotificationExpiresAt.Value <= DateTime.UtcNow)
            .Select(w => w.TravelPackageId)
            .Distinct()
            .ToListAsync();

        _logger.LogInformation($"Found {packagesWithExpiredNotifications.Count} packages with expired waiting list notifications.");

        foreach (var packageId in packagesWithExpiredNotifications)
        {
            try
            {
                await waitingListService.RemoveExpiredNotificationsAsync(packageId, notificationService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process expired notifications for package {packageId}");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waiting List Background Service is stopping.");
        await base.StopAsync(cancellationToken);
    }
}
