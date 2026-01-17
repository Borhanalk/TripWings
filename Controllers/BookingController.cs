using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;
using TripWings.Services;

namespace TripWings.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBookingService _bookingService;
    private readonly IWaitingListService _waitingListService;
    private readonly ILogger<BookingController> _logger;

    public BookingController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IBookingService bookingService,
        IWaitingListService waitingListService,
        ILogger<BookingController> logger)
    {
        _context = context;
        _userManager = userManager;
        _bookingService = bookingService;
        _waitingListService = waitingListService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int travelPackageId)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم إنشاء حجوزات كمستخدمين.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var travelPackage = await _context.TravelPackages
            .Include(t => t.PackageImages)
            .Include(t => t.Discounts)
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);

        if (travelPackage == null) return NotFound();

        var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(travelPackageId);
        
        ViewBag.TravelPackage = travelPackage;
        ViewBag.IsFull = isFull;
        ViewBag.RemainingRooms = remainingRooms;
        ViewBag.IsInWaitingList = await _waitingListService.IsUserInWaitingListAsync(user.Id, travelPackageId);

        return View(new Models.ViewModels.BookingViewModel 
        { 
            TravelPackageId = travelPackageId,
            RoomsCount = 1
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Models.ViewModels.BookingViewModel model)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم إنشاء حجوزات كمستخدمين.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            var travelPackage = await _context.TravelPackages
                .Include(t => t.PackageImages)
                .FirstOrDefaultAsync(t => t.Id == model.TravelPackageId);

            if (travelPackage != null)
            {
                ViewBag.TravelPackage = travelPackage;
                var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(model.TravelPackageId);
                ViewBag.IsFull = isFull;
                ViewBag.RemainingRooms = remainingRooms;
            }

            return View(model);
        }

        var (success, errorMessage, booking) = await _bookingService.CreateBookingAsync(
            user.Id, 
            model.TravelPackageId, 
            model.RoomsCount);

        if (success && booking != null)
        {
            var travelPackage = await _context.TravelPackages
                .FirstOrDefaultAsync(t => t.Id == booking.TravelPackageId);

            if (travelPackage != null)
            {
                var daysUntilTrip = (travelPackage.StartDate.Date - DateTime.UtcNow.Date).Days;
                
                if (daysUntilTrip <= 5 && daysUntilTrip >= 0)
                {
                    try
                    {
                        var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
                        await notificationService.SendTripReminderAsync(
                            user.Email!,
                            $"{user.FirstName} {user.LastName}",
                            booking.Id,
                            travelPackage.StartDate);

                        booking.ReminderSent = true;
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send immediate reminder for booking {booking.Id}");
                    }
                }
            }
        }

        if (!success)
        {
            TempData["Error"] = errorMessage;

            var (isFull, _) = await _bookingService.CheckAvailabilityAsync(model.TravelPackageId);
            if (isFull)
            {
                return RedirectToAction("JoinWaitingList", new { travelPackageId = model.TravelPackageId });
            }

            return RedirectToAction("Create", new { travelPackageId = model.TravelPackageId });
        }

        TempData["Success"] = "הזמנה נוצרה בהצלחה! אנא השלם את התשלום.";
        return RedirectToAction("ProcessPayment", "Payment", new { 
            travelPackageId = booking!.TravelPackageId, 
            roomsCount = booking.RoomsCount,
            bookingId = booking.Id 
        });
    }

    [HttpGet]
    public async Task<IActionResult> JoinWaitingList(int travelPackageId)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الانضمام إلى قائمة الانتظار.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var travelPackage = await _context.TravelPackages
            .FirstOrDefaultAsync(t => t.Id == travelPackageId);

        if (travelPackage == null) return NotFound();

        var (isFull, _) = await _bookingService.CheckAvailabilityAsync(travelPackageId);
        if (!isFull)
        {
            TempData["Info"] = "Rooms are available. You can book directly.";
            return RedirectToAction("Create", new { travelPackageId });
        }

        var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
        var (success, errorMessage, entry) = await _waitingListService.JoinWaitingListWithNotificationAsync(user.Id, travelPackageId, notificationService);

        if (!success)
        {
            TempData["Error"] = errorMessage;
            return RedirectToAction("Create", new { travelPackageId });
        }

        var position = entry!.Position;
        var count = await _waitingListService.GetWaitingListCountAsync(travelPackageId);
        var estimatedWait = await _waitingListService.EstimateWaitTimeAsync(travelPackageId, position);

        ViewBag.TravelPackage = travelPackage;
        ViewBag.Position = position;
        ViewBag.TotalWaiting = count;
        ViewBag.EstimatedWait = estimatedWait;

        TempData["Success"] = $"You have been added to the waiting list at position {position}.";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> MyBookings()
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى حجوزات المستخدمين.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var bookings = await _context.Bookings
            .Include(b => b.TravelPackage)
            .Where(b => b.UserId == user.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(bookings);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى حجوزات المستخدمين.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
                .ThenInclude(t => t.PackageImages)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == user.Id);

        if (booking == null) return NotFound();

        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم إلغاء حجوزات المستخدمين من هنا. استخدم لوحة إدارة الحجوزات.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var booking = await _context.Bookings
            .Include(b => b.TravelPackage)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == user.Id);

        if (booking == null) return NotFound();

        if (booking.Status == BookingStatus.Cancelled)
        {
            TempData["Error"] = "ההזמנה כבר בוטלה.";
            return RedirectToAction("MyBookings");
        }

        if (booking.Status != BookingStatus.Active)
        {
            TempData["Error"] = "לא ניתן לבטל הזמנה זו.";
            return RedirectToAction("MyBookings");
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.BookingId == id && p.Status == PaymentStatus.Paid);

        if (payment != null)
        {

            var refundAmount = payment.FinalAmount;

            if (payment.InstallmentsCount > 1)
            {


                refundAmount = payment.FinalAmount;
            }

            payment.Status = PaymentStatus.Refunded;

            var walletService = HttpContext.RequestServices.GetRequiredService<IWalletService>();
            var (success, errorMessage) = await walletService.AddToWalletAsync(
                user.Id,
                refundAmount,
                $"החזר כספי עבור הזמנה #{id}",
                bookingId: id,
                paymentId: payment.Id);
            
            if (success)
            {
                _logger.LogInformation($"Refunded {refundAmount} to wallet for booking {id}, user {user.Id}");
            }
            else
            {
                _logger.LogError($"Failed to refund to wallet: {errorMessage}");
            }
        }

        booking.Status = BookingStatus.Cancelled;
        booking.PaymentStatus = PaymentStatus.Refunded;
        
        await _context.SaveChangesAsync();

        var (isFull, _) = await _bookingService.CheckAvailabilityAsync(booking.TravelPackageId);
        if (!isFull)
        {
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
            await _waitingListService.NotifyNextInQueueAsync(booking.TravelPackageId, notificationService);
        }

        var notificationService2 = HttpContext.RequestServices.GetRequiredService<INotificationService>();
        await notificationService2.SendBookingCancellationAsync(
            user.Email!,
            $"{user.FirstName} {user.LastName}",
            booking.Id);

        var refundMessage = payment != null ? " והחזר כספי בוצע." : "";
        TempData["Success"] = $"ההזמנה בוטלה בהצלחה{refundMessage}";
        return RedirectToAction("MyBookings");
    }

    [HttpGet]
    public async Task<IActionResult> MyWaitingList()
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى قائمة انتظار المستخدمين.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var waitingList = await _context.WaitingListEntries
            .Include(w => w.TravelPackage)
            .Where(w => w.UserId == user.Id && w.IsActive)
            .OrderBy(w => w.Position)
            .ToListAsync();

        var viewModel = new List<Models.ViewModels.WaitingListViewModel>();
        foreach (var entry in waitingList)
        {
            var position = entry.Position;
            var totalWaiting = await _waitingListService.GetWaitingListCountAsync(entry.TravelPackageId);
            var estimatedWait = await _waitingListService.EstimateWaitTimeAsync(entry.TravelPackageId, position);
            var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(entry.TravelPackageId);

            viewModel.Add(new Models.ViewModels.WaitingListViewModel
            {
                Id = entry.Id,
                TravelPackageId = entry.TravelPackageId,
                Destination = entry.TravelPackage.Destination,
                Country = entry.TravelPackage.Country,
                JoinedAt = entry.JoinedAt,
                Position = position,
                TotalWaiting = totalWaiting,
                EstimatedWaitTime = estimatedWait,
                IsActive = entry.IsActive,
                NotifiedAt = entry.NotifiedAt
            });
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromWaitingList(int id)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "المسؤولون لا يمكنهم الوصول إلى قائمة انتظار المستخدمين.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var (success, errorMessage) = await _waitingListService.RemoveFromWaitingListAsync(id, user.Id);

        if (success)
        {
            TempData["Success"] = "تم إزالتك من قائمة الانتظار بنجاح. / Successfully removed from waiting list.";
        }
        else
        {
            TempData["Error"] = errorMessage ?? "حدث خطأ أثناء إزالتك من قائمة الانتظار. / An error occurred while removing you from the waiting list.";
        }

        return RedirectToAction("MyWaitingList");
    }
}
