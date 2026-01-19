using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Models.ViewModels;
using TripWings.Services;
using System.Text.Json;

namespace TripWings.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBookingService _bookingService;
    private readonly INotificationService _notificationService;
    private readonly IPayPalService _payPalService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IBookingService bookingService,
        INotificationService notificationService,
        IPayPalService payPalService,
        IConfiguration configuration,
        ILogger<PaymentController> logger)
    {
        _context = context;
        _userManager = userManager;
        _bookingService = bookingService;
        _notificationService = notificationService;
        _payPalService = payPalService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> ProcessPayment(int? travelPackageId, int? roomsCount, List<int>? cartItemIds, int? bookingId)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "מנהלים לא יכולים לבצע תשלומים. / Admins cannot make payments.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "יש להתחבר כדי לבצע תשלום. / Please login to proceed with payment.";
            return RedirectToAction("Login", "Account");
        }

        decimal totalAmount = 0;
        string paymentType = "cart";

        if (travelPackageId.HasValue)
        {

            var travelPackage = await _context.TravelPackages
                .Include(t => t.Discounts)
                .FirstOrDefaultAsync(t => t.Id == travelPackageId.Value);
            
            if (travelPackage == null)
            {
                TempData["Error"] = "החבילה לא נמצאה. / Travel package not found.";
                return RedirectToAction("Gallery", "Trips");
            }

            var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(travelPackageId.Value);
            
            // Check if there's an active waiting list notification for another user
            var (hasActiveNotification, notifiedUserId) = await _bookingService.HasActiveWaitingListNotificationAsync(travelPackageId.Value);
            
            if (hasActiveNotification && notifiedUserId != user.Id)
            {
                TempData["Error"] = "חדר זמין כרגע, אך משתמש אחר ברשימת ההמתנה (מיקום #1) קיבל עדיפות. אנא המתן לתורך. / A room is available, but another user in the waiting list (position #1) has priority. Please wait for your turn.";
                return RedirectToAction("Details", "Trips", new { id = travelPackageId });
            }
            
            // If package is full, check if user has valid waiting list notification
            if (isFull || remainingRooms <= 0)
            {
                // Check if user is first in waiting list with valid notification
                var waitingListEntry = await _context.WaitingListEntries
                    .Where(w => w.UserId == user.Id && 
                               w.TravelPackageId == travelPackageId.Value && 
                               w.IsActive &&
                               w.Position == 1 &&
                               w.NotifiedAt.HasValue &&
                               w.NotificationExpiresAt.HasValue &&
                               w.NotificationExpiresAt.Value > DateTime.UtcNow)
                    .FirstOrDefaultAsync();
                
                if (waitingListEntry == null)
                {
                    TempData["Error"] = "החבילה הזו כבר לא זמינה. / This package is no longer available.";
                    return RedirectToAction("Details", "Trips", new { id = travelPackageId });
                }
                
                // User has valid notification, allow payment
                _logger.LogInformation($"User {user.Id} proceeding with payment for full package {travelPackageId.Value} with valid waiting list notification (position #1)");
            }

            var requestedRooms = roomsCount ?? 1;
            if (requestedRooms > remainingRooms)
            {
                TempData["Error"] = $"רק {remainingRooms} חדר/ים זמינים. / Only {remainingRooms} room(s) available.";
                return RedirectToAction("Details", "Trips", new { id = travelPackageId });
            }

            var now = DateTime.UtcNow;
            var activeDiscount = travelPackage.Discounts
                .FirstOrDefault(d => d.StartAt <= now && d.EndAt >= now && (d.EndAt - d.StartAt).TotalDays <= 7);
            
            var price = activeDiscount?.NewPrice ?? travelPackage.Price;
            totalAmount = price * requestedRooms;
            paymentType = "direct";
        }
        else if (cartItemIds != null && cartItemIds.Any())
        {

            var cartItems = await _context.CartItems
                .Include(c => c.TravelPackage)
                    .ThenInclude(t => t.Discounts)
                .Where(c => cartItemIds.Contains(c.Id) && c.UserId == user.Id)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Error"] = "פריטי עגלה לא תקינים.";
                return RedirectToAction("Index", "Cart");
            }

            var cartTime = DateTime.UtcNow;
            foreach (var item in cartItems)
            {
                var activeDiscount = item.TravelPackage.Discounts
                    .FirstOrDefault(d => d.StartAt <= cartTime && d.EndAt >= cartTime && (d.EndAt - d.StartAt).TotalDays <= 7);
                var price = activeDiscount?.NewPrice ?? item.TravelPackage.Price;
                totalAmount += price * item.Quantity;
            }
        }
        else
        {
            _logger.LogWarning($"Invalid payment request. User: {user?.Id}, TravelPackageId: {travelPackageId}, CartItemIds: {cartItemIds?.Count ?? 0}");
            TempData["Error"] = "בקשת תשלום לא תקינה. אנא נסה שוב. / Invalid payment request. Please try again.";
            return RedirectToAction("Gallery", "Trips");
        }

        var viewModel = new PaymentViewModel
        {
            TravelPackageId = travelPackageId,
            RoomsCount = roomsCount ?? 1,
            CartItemIds = cartItemIds
        };

        ViewBag.TotalAmount = totalAmount;
        ViewBag.PaymentType = paymentType;
        ViewBag.PayPalClientId = _configuration["PayPalSettings:ClientId"];
        ViewBag.PayPalMode = _configuration["PayPalSettings:Mode"] ?? "sandbox";
        
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(PaymentViewModel model)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "מנהלים לא יכולים לבצע תשלומים. / Admins cannot make payments.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "יש להתחבר כדי לבצע תשלום. / Please login to proceed with payment.";
            return RedirectToAction("Login", "Account");
        }

        if (model.PaymentMethod == "PayPal")
        {

            decimal totalAmount = 0;
            string description = "TripWings Travel Package Payment";
            
            if (model.TravelPackageId.HasValue)
            {
                var travelPackage = await _context.TravelPackages
                    .Include(t => t.Discounts)
                    .FirstOrDefaultAsync(t => t.Id == model.TravelPackageId.Value);
                
                if (travelPackage != null)
                {
                    var now = DateTime.UtcNow;
                    var activeDiscount = travelPackage.Discounts
                        .FirstOrDefault(d => d.StartAt <= now && d.EndAt >= now && (d.EndAt - d.StartAt).TotalDays <= 7);
                    
                    var price = activeDiscount?.NewPrice ?? travelPackage.Price;
                    totalAmount = price * (model.RoomsCount ?? 1);
                    description = $"TripWings - {travelPackage.Destination}, {travelPackage.Country}";
                }
            }
            else if (model.CartItemIds != null && model.CartItemIds.Any())
            {
                var cartItems = await _context.CartItems
                    .Include(c => c.TravelPackage)
                        .ThenInclude(t => t.Discounts)
                    .Where(c => model.CartItemIds.Contains(c.Id) && c.UserId == user.Id)
                    .ToListAsync();
                
                var cartTime = DateTime.UtcNow;
                foreach (var item in cartItems)
                {
                    var activeDiscount = item.TravelPackage.Discounts
                        .FirstOrDefault(d => d.StartAt <= cartTime && d.EndAt >= cartTime && (d.EndAt - d.StartAt).TotalDays <= 7);
                    var price = activeDiscount?.NewPrice ?? item.TravelPackage.Price;
                    totalAmount += price * item.Quantity;
                }
                description = $"TripWings - Cart Checkout ({cartItems.Count} items)";
            }

            var returnUrl = _configuration["PayPalSettings:ReturnUrl"] ?? 
                $"{Request.Scheme}://{Request.Host}/Payment/PayPalSuccess";
            var cancelUrl = _configuration["PayPalSettings:CancelUrl"] ?? 
                $"{Request.Scheme}://{Request.Host}/Payment/PayPalCancel";

            var (success, paymentId, approvalUrl, payPalError) = await _payPalService.CreatePaymentAsync(
                totalAmount,
                "USD",
                description,
                returnUrl,
                cancelUrl);

            if (success && !string.IsNullOrEmpty(paymentId) && !string.IsNullOrEmpty(approvalUrl))
            {

                HttpContext.Session.SetString("PayPalPaymentId", paymentId);
                HttpContext.Session.SetString("PayPalUserId", user.Id);
                
                if (model.TravelPackageId.HasValue)
                {
                    HttpContext.Session.SetString("PayPalTravelPackageId", model.TravelPackageId.Value.ToString());
                    HttpContext.Session.SetString("PayPalRoomsCount", (model.RoomsCount ?? 1).ToString());
                }
                else if (model.CartItemIds != null)
                {
                    HttpContext.Session.SetString("PayPalCartItemIds", string.Join(",", model.CartItemIds));
                }

                _logger.LogInformation($"Redirecting user {user.Id} to PayPal approval URL. Payment ID: {paymentId}");

                return Redirect(approvalUrl);
            }
            else
            {
                TempData["PaymentError"] = payPalError ?? "תשלום PayPal נכשל. אנא נסה שוב.";
                return RedirectToAction("Index", "Home");
            }
        }

        if (!ModelState.IsValid)
        {

            decimal totalAmount = 0;
            if (model.TravelPackageId.HasValue)
            {
                var travelPackage = await _context.TravelPackages
                    .Include(t => t.Discounts)
                    .FirstOrDefaultAsync(t => t.Id == model.TravelPackageId.Value);
                
                if (travelPackage != null)
                {
                    var now = DateTime.UtcNow;
                    var activeDiscount = travelPackage.Discounts
                        .FirstOrDefault(d => d.StartAt <= now && d.EndAt >= now && (d.EndAt - d.StartAt).TotalDays <= 7);
                    var price = activeDiscount?.NewPrice ?? travelPackage.Price;
                    totalAmount = price * (model.RoomsCount ?? 1);
                }
            }
            else if (model.CartItemIds != null)
            {
                var cartItems = await _context.CartItems
                    .Include(c => c.TravelPackage)
                        .ThenInclude(t => t.Discounts)
                    .Where(c => model.CartItemIds.Contains(c.Id) && c.UserId == user.Id)
                    .ToListAsync();
                var cartTime = DateTime.UtcNow;
                foreach (var item in cartItems)
                {
                    var activeDiscount = item.TravelPackage.Discounts
                        .FirstOrDefault(d => d.StartAt <= cartTime && d.EndAt >= cartTime && (d.EndAt - d.StartAt).TotalDays <= 7);
                    var price = activeDiscount?.NewPrice ?? item.TravelPackage.Price;
                    totalAmount += price * item.Quantity;
                }
            }
            ViewBag.TotalAmount = totalAmount;
            ViewBag.PayPalClientId = _configuration["PayPalSettings:ClientId"];
            ViewBag.PayPalMode = _configuration["PayPalSettings:Mode"] ?? "sandbox";
            return View(model);
        }

        var paymentResult = await ProcessPaymentAsync(model, user);

        if (paymentResult.Success)
        {

            List<Booking> createdBookings = new();

            if (model.TravelPackageId.HasValue)
            {
                // التحقق من التوفر مرة أخرى قبل إنشاء الحجز (لضمان عدم حجز نفس الرحلة من مستخدمين في نفس الوقت)
                var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(model.TravelPackageId.Value);
                var requestedRooms = model.RoomsCount ?? 1;
                
                // التحقق من وجود إشعار صالح من قائمة الانتظار
                bool userHasValidNotification = false;
                if (isFull || remainingRooms <= 0)
                {
                    var waitingListEntry = await _context.WaitingListEntries
                        .Where(w => w.UserId == user.Id && 
                                   w.TravelPackageId == model.TravelPackageId.Value && 
                                   w.IsActive &&
                                   w.Position == 1 &&
                                   w.NotifiedAt.HasValue &&
                                   w.NotificationExpiresAt.HasValue &&
                                   w.NotificationExpiresAt.Value > DateTime.UtcNow)
                        .FirstOrDefaultAsync();
                    
                    userHasValidNotification = waitingListEntry != null;
                }
                
                // إذا كانت الرحلة ممتلئة ولا يوجد إشعار صالح، توجيه المستخدم إلى قائمة الانتظار
                if ((isFull || remainingRooms < requestedRooms) && !userHasValidNotification)
                {
                    var travelPackage = await _context.TravelPackages
                        .FirstOrDefaultAsync(t => t.Id == model.TravelPackageId.Value);
                    
                    if (travelPackage != null)
                    {
                        TempData["Error"] = $"הריילה הזו כבר נמכרה. רק {remainingRooms} חדר/ים זמינים, אך מישהו אחר כבר הזמין אותם. אתה יכול להצטרף לרשימת ההמתנה. / This trip has been sold out. Only {remainingRooms} room(s) available, but someone else has already booked them. You can join the waiting list.";
                        return RedirectToAction("JoinWaitingList", "Booking", new { travelPackageId = model.TravelPackageId.Value });
                    }
                    else
                    {
                        TempData["Error"] = "החבילה לא נמצאה. / Travel package not found.";
                        return RedirectToAction("Gallery", "Trips");
                    }
                }
                
                // التحقق من أن العدد المطلوب متاح
                if (remainingRooms < requestedRooms && !userHasValidNotification)
                {
                    TempData["Error"] = $"רק {remainingRooms} חדר/ים זמינים. / Only {remainingRooms} room(s) available.";
                    return RedirectToAction("Details", "Trips", new { id = model.TravelPackageId.Value });
                }

                var (success, errorMessage, booking) = await _bookingService.CreateBookingAsync(
                    user.Id,
                    model.TravelPackageId.Value,
                    requestedRooms);

                if (!success || booking == null)
                {
                    // إذا فشل إنشاء الحجز (مثلاً بسبب race condition)، توجيه المستخدم إلى قائمة الانتظار
                    var travelPackage = await _context.TravelPackages
                        .FirstOrDefaultAsync(t => t.Id == model.TravelPackageId.Value);
                    
                    if (travelPackage != null)
                    {
                        var (isFullNow, remainingRoomsNow) = await _bookingService.CheckAvailabilityAsync(model.TravelPackageId.Value);
                        if (isFullNow || remainingRoomsNow <= 0)
                        {
                            TempData["Error"] = $"הריילה הזו כבר נמכרה. מישהו אחר כבר הזמין אותה. אתה יכול להצטרף לרשימת ההמתנה. / This trip has been sold out. Someone else has already booked it. You can join the waiting list.";
                            return RedirectToAction("JoinWaitingList", "Booking", new { travelPackageId = model.TravelPackageId.Value });
                        }
                        else
                        {
                            TempData["Error"] = errorMessage ?? "לא ניתן ליצור הזמנה. אנא נסה שוב. / Unable to create booking. Please try again.";
                            return RedirectToAction("Details", "Trips", new { id = model.TravelPackageId.Value });
                        }
                    }
                    else
                    {
                        TempData["Error"] = errorMessage ?? "לא ניתן ליצור הזמנה. / Unable to create booking.";
                        return RedirectToAction("Gallery", "Trips");
                    }
                }

                createdBookings.Add(booking);

                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(c => c.UserId == user.Id && c.TravelPackageId == model.TravelPackageId.Value);
                
                if (cartItem != null)
                {
                    _context.CartItems.Remove(cartItem);
                    _logger.LogInformation($"Removed cart item for travel package {model.TravelPackageId.Value} after Buy Now payment for user {user.Id}");
                }
                
                await _context.SaveChangesAsync();
            }
            else if (model.CartItemIds != null && model.CartItemIds.Any())
            {

                var cartItems = await _context.CartItems
                    .Include(c => c.TravelPackage)
                    .Where(c => model.CartItemIds.Contains(c.Id) && c.UserId == user.Id)
                    .ToListAsync();

                foreach (var cartItem in cartItems)
                {
                    // التحقق من التوفر مرة أخرى قبل إنشاء الحجز
                    var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(cartItem.TravelPackageId);
                    
                    if (isFull || remainingRooms < cartItem.Quantity)
                    {
                        // الرحلة ممتلئة، إزالة من العربة وإعلام المستخدم
                        _context.CartItems.Remove(cartItem);
                        _logger.LogInformation($"Removed cart item {cartItem.Id} for package {cartItem.TravelPackageId} - trip sold out. User: {user.Id}");
                        
                        // تخطي هذا العنصر والمتابعة للعناصر الأخرى
                        continue;
                    }
                    
                    var (success, errorMessage, booking) = await _bookingService.CreateBookingAsync(
                        user.Id,
                        cartItem.TravelPackageId,
                        cartItem.Quantity);

                    if (success && booking != null)
                    {
                        createdBookings.Add(booking);
                        _context.CartItems.Remove(cartItem);
                    }
                    else
                    {
                        // إذا فشل إنشاء الحجز (مثلاً بسبب race condition)، إزالة من العربة
                        _context.CartItems.Remove(cartItem);
                        _logger.LogWarning($"Failed to create booking for cart item {cartItem.Id}: {errorMessage}");
                    }
                }

                await _context.SaveChangesAsync();
            }

            var payments = new List<Payment>();
            foreach (var booking in createdBookings)
            {

                var bookingWithDiscounts = await _context.Bookings
                    .Include(b => b.TravelPackage)
                        .ThenInclude(t => t.Discounts)
                    .FirstOrDefaultAsync(b => b.Id == booking.Id);
                
                if (bookingWithDiscounts == null) continue;
                
                var bookingPrice = bookingWithDiscounts.TravelPackage.Price * bookingWithDiscounts.RoomsCount;
                var now = DateTime.UtcNow;
                var activeDiscount = bookingWithDiscounts.TravelPackage.Discounts
                    .FirstOrDefault(d => d.StartAt <= now && d.EndAt >= now && (d.EndAt - d.StartAt).TotalDays <= 7);
                
                var discountAmount = activeDiscount != null ? (activeDiscount.OldPrice - activeDiscount.NewPrice) * bookingWithDiscounts.RoomsCount : 0;
                var finalAmount = bookingPrice - discountAmount;
                var installmentAmount = finalAmount / model.InstallmentsCount;
                
                // IMPORTANT SECURITY: Never store any card information (not even last 4 digits)
                // All card information (number, CVV, expiry date, cardholder name) is processed in memory and immediately discarded
                // No card data is ever saved to database
                
                var payment = new Payment
                {
                    BookingId = bookingWithDiscounts.Id,
                    UserId = user.Id,
                    Amount = finalAmount,
                    PaymentDate = DateTime.UtcNow,
                    PaymentMethod = PaymentMethod.CreditCard,
                    Status = PaymentStatus.Paid,
                    DiscountId = activeDiscount?.Id,
                    DiscountAmount = discountAmount,
                    FinalAmount = finalAmount,
                    InstallmentsCount = model.InstallmentsCount,
                    InstallmentAmount = installmentAmount
                };
                
                _context.Payments.Add(payment);
                payments.Add(payment);
                
                // Update booking payment status to Paid
                bookingWithDiscounts.PaymentStatus = PaymentStatus.Paid;
            }
            
            await _context.SaveChangesAsync();

            for (int i = 0; i < createdBookings.Count; i++)
            {
                var booking = createdBookings[i];
                var payment = payments[i];

                await _notificationService.SendPaymentConfirmationAsync(
                    user.Email!,
                    $"{user.FirstName} {user.LastName}",
                    booking.Id,
                    payment.FinalAmount);

                await _notificationService.SendBookingConfirmationAsync(
                    user.Email!,
                    $"{user.FirstName} {user.LastName}",
                    booking.Id);
            }

            var installmentsText = model.InstallmentsCount > 1 ? $" ב-{model.InstallmentsCount} תשלומים" : "";
            TempData["PaymentSuccess"] = $"תשלום בוצע בהצלחה באמצעות כרטיס אשראי/חיוב{installmentsText}! {createdBookings.Count} הזמנה/ות נוצרו. אימיילי אישור נשלחו.";
            _logger.LogInformation($"Payment processed successfully for user {user.Id}. Created {createdBookings.Count} booking(s). Payment method: Credit Card, Installments: {model.InstallmentsCount}");
            
            return RedirectToAction("MyBookings", "Booking");
        }
        else
        {
            TempData["PaymentError"] = paymentResult.ErrorMessage ?? "התשלום נכשל. אנא נסה שוב.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> PayPalSuccess(string paymentId, string PayerID)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "מנהלים לא יכולים לבצע תשלומים. / Admins cannot make payments.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "יש להתחבר כדי לבצע תשלום. / Please login to proceed with payment.";
            return RedirectToAction("Login", "Account");
        }

        var storedPaymentId = HttpContext.Session.GetString("PayPalPaymentId");
        var storedUserId = HttpContext.Session.GetString("PayPalUserId");

        if (string.IsNullOrEmpty(storedPaymentId) || storedUserId != user.Id || paymentId != storedPaymentId)
        {
            TempData["PaymentError"] = "שגיאה באימות תשלום PayPal.";
            return RedirectToAction("Index", "Home");
        }

        var (success, payPalExecuteError) = await _payPalService.ExecutePaymentAsync(paymentId, PayerID);

        if (success)
        {

            List<Booking> createdBookings = new();

            var travelPackageIdStr = HttpContext.Session.GetString("PayPalTravelPackageId");
            var roomsCountStr = HttpContext.Session.GetString("PayPalRoomsCount");
            var cartItemIdsStr = HttpContext.Session.GetString("PayPalCartItemIds");

            if (!string.IsNullOrEmpty(travelPackageIdStr) && int.TryParse(travelPackageIdStr, out var travelPackageId))
            {
                var roomsCount = int.TryParse(roomsCountStr, out var count) ? count : 1;
                
                // التحقق من التوفر مرة أخرى قبل إنشاء الحجز (لضمان عدم حجز نفس الرحلة من مستخدمين في نفس الوقت)
                var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(travelPackageId);
                
                // التحقق من وجود إشعار صالح من قائمة الانتظار
                bool userHasValidNotification = false;
                if (isFull || remainingRooms <= 0)
                {
                    var waitingListEntry = await _context.WaitingListEntries
                        .Where(w => w.UserId == user.Id && 
                                   w.TravelPackageId == travelPackageId && 
                                   w.IsActive &&
                                   w.Position == 1 &&
                                   w.NotifiedAt.HasValue &&
                                   w.NotificationExpiresAt.HasValue &&
                                   w.NotificationExpiresAt.Value > DateTime.UtcNow)
                        .FirstOrDefaultAsync();
                    
                    userHasValidNotification = waitingListEntry != null;
                }
                
                // إذا كانت الرحلة ممتلئة ولا يوجد إشعار صالح، توجيه المستخدم إلى قائمة الانتظار
                if ((isFull || remainingRooms < roomsCount) && !userHasValidNotification)
                {
                    var travelPackage = await _context.TravelPackages
                        .FirstOrDefaultAsync(t => t.Id == travelPackageId);
                    
                    HttpContext.Session.Remove("PayPalPaymentId");
                    HttpContext.Session.Remove("PayPalUserId");
                    HttpContext.Session.Remove("PayPalTravelPackageId");
                    HttpContext.Session.Remove("PayPalRoomsCount");
                    HttpContext.Session.Remove("PayPalCartItemIds");
                    
                    if (travelPackage != null)
                    {
                        TempData["Error"] = $"הריילה הזו כבר נמכרה. רק {remainingRooms} חדר/ים זמינים, אך מישהו אחר כבר הזמין אותם. אתה יכול להצטרף לרשימת ההמתנה. / This trip has been sold out. Only {remainingRooms} room(s) available, but someone else has already booked them. You can join the waiting list.";
                        return RedirectToAction("JoinWaitingList", "Booking", new { travelPackageId = travelPackageId });
                    }
                    else
                    {
                        TempData["Error"] = "החבילה לא נמצאה. / Travel package not found.";
                        return RedirectToAction("Gallery", "Trips");
                    }
                }
                
                // التحقق من أن العدد المطلوب متاح
                if (remainingRooms < roomsCount && !userHasValidNotification)
                {
                    HttpContext.Session.Remove("PayPalPaymentId");
                    HttpContext.Session.Remove("PayPalUserId");
                    HttpContext.Session.Remove("PayPalTravelPackageId");
                    HttpContext.Session.Remove("PayPalRoomsCount");
                    HttpContext.Session.Remove("PayPalCartItemIds");
                    
                    TempData["Error"] = $"רק {remainingRooms} חדר/ים זמינים. / Only {remainingRooms} room(s) available.";
                    return RedirectToAction("Details", "Trips", new { id = travelPackageId });
                }
                
                var (bookingSuccess, errorMessage, booking) = await _bookingService.CreateBookingAsync(
                    user.Id,
                    travelPackageId,
                    roomsCount);

                if (!bookingSuccess || booking == null)
                {
                    // إذا فشل إنشاء الحجز (مثلاً بسبب race condition)، توجيه المستخدم إلى قائمة الانتظار
                    var travelPackage = await _context.TravelPackages
                        .FirstOrDefaultAsync(t => t.Id == travelPackageId);
                    
                    HttpContext.Session.Remove("PayPalPaymentId");
                    HttpContext.Session.Remove("PayPalUserId");
                    HttpContext.Session.Remove("PayPalTravelPackageId");
                    HttpContext.Session.Remove("PayPalRoomsCount");
                    HttpContext.Session.Remove("PayPalCartItemIds");
                    
                    if (travelPackage != null)
                    {
                        var (isFullNow, remainingRoomsNow) = await _bookingService.CheckAvailabilityAsync(travelPackageId);
                        if (isFullNow || remainingRoomsNow <= 0)
                        {
                            TempData["Error"] = $"הריילה הזו כבר נמכרה. מישהו אחר כבר הזמין אותה. אתה יכול להצטרף לרשימת ההמתנה. / This trip has been sold out. Someone else has already booked it. You can join the waiting list.";
                            return RedirectToAction("JoinWaitingList", "Booking", new { travelPackageId = travelPackageId });
                        }
                        else
                        {
                            TempData["Error"] = errorMessage ?? "לא ניתן ליצור הזמנה. אנא נסה שוב. / Unable to create booking. Please try again.";
                            return RedirectToAction("Details", "Trips", new { id = travelPackageId });
                        }
                    }
                    else
                    {
                        TempData["Error"] = errorMessage ?? "לא ניתן ליצור הזמנה. / Unable to create booking.";
                        return RedirectToAction("Gallery", "Trips");
                    }
                }

                createdBookings.Add(booking);

                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(c => c.UserId == user.Id && c.TravelPackageId == travelPackageId);
                
                if (cartItem != null)
                {
                    _context.CartItems.Remove(cartItem);
                }
                
                await _context.SaveChangesAsync();
            }
            else if (!string.IsNullOrEmpty(cartItemIdsStr))
            {
                var cartItemIds = cartItemIdsStr.Split(',').Select(int.Parse).ToList();
                var cartItems = await _context.CartItems
                    .Include(c => c.TravelPackage)
                    .Where(c => cartItemIds.Contains(c.Id) && c.UserId == user.Id)
                    .ToListAsync();

                foreach (var cartItem in cartItems)
                {
                    // التحقق من التوفر مرة أخرى قبل إنشاء الحجز
                    var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(cartItem.TravelPackageId);
                    
                    if (isFull || remainingRooms < cartItem.Quantity)
                    {
                        // الرحلة ممتلئة، إزالة من العربة وإعلام المستخدم
                        _context.CartItems.Remove(cartItem);
                        _logger.LogInformation($"Removed cart item {cartItem.Id} for package {cartItem.TravelPackageId} - trip sold out. User: {user.Id}");
                        
                        // تخطي هذا العنصر والمتابعة للعناصر الأخرى
                        continue;
                    }
                    
                    var (bookingSuccess, errorMessage, booking) = await _bookingService.CreateBookingAsync(
                        user.Id,
                        cartItem.TravelPackageId,
                        cartItem.Quantity);

                    if (bookingSuccess && booking != null)
                    {
                        createdBookings.Add(booking);
                        _context.CartItems.Remove(cartItem);
                    }
                    else
                    {
                        // إذا فشل إنشاء الحجز (مثلاً بسبب race condition)، إزالة من العربة
                        _context.CartItems.Remove(cartItem);
                        _logger.LogWarning($"Failed to create booking for cart item {cartItem.Id}: {errorMessage}");
                    }
                }

                await _context.SaveChangesAsync();
            }

            var payments = new List<Payment>();
            foreach (var booking in createdBookings)
            {

                var bookingWithDiscounts = await _context.Bookings
                    .Include(b => b.TravelPackage)
                        .ThenInclude(t => t.Discounts)
                    .FirstOrDefaultAsync(b => b.Id == booking.Id);
                
                if (bookingWithDiscounts == null) continue;
                
                var bookingPrice = bookingWithDiscounts.TravelPackage.Price * bookingWithDiscounts.RoomsCount;
                var now = DateTime.UtcNow;
                var activeDiscount = bookingWithDiscounts.TravelPackage.Discounts
                    .FirstOrDefault(d => d.StartAt <= now && d.EndAt >= now && (d.EndAt - d.StartAt).TotalDays <= 7);
                
                var discountAmount = activeDiscount != null ? (activeDiscount.OldPrice - activeDiscount.NewPrice) * bookingWithDiscounts.RoomsCount : 0;
                var finalAmount = bookingPrice - discountAmount;
                
                var payment = new Payment
                {
                    BookingId = bookingWithDiscounts.Id,
                    UserId = user.Id,
                    Amount = finalAmount,
                    PaymentDate = DateTime.UtcNow,
                    PaymentMethod = PaymentMethod.BankTransfer, // PayPal is treated as bank transfer
                    Status = PaymentStatus.Paid,
                    DiscountId = activeDiscount?.Id,
                    DiscountAmount = discountAmount,
                    FinalAmount = finalAmount,
                    TransactionId = paymentId,
                    InstallmentsCount = 1,
                    InstallmentAmount = finalAmount
                };
                
                _context.Payments.Add(payment);
                payments.Add(payment);
                
                // Update booking payment status to Paid
                bookingWithDiscounts.PaymentStatus = PaymentStatus.Paid;
            }
            
            await _context.SaveChangesAsync();

            for (int i = 0; i < createdBookings.Count; i++)
            {
                var booking = createdBookings[i];
                var payment = payments[i];

                await _notificationService.SendPaymentConfirmationAsync(
                    user.Email!,
                    $"{user.FirstName} {user.LastName}",
                    booking.Id,
                    payment.FinalAmount);

                await _notificationService.SendBookingConfirmationAsync(
                    user.Email!,
                    $"{user.FirstName} {user.LastName}",
                    booking.Id);
            }

            HttpContext.Session.Remove("PayPalPaymentId");
            HttpContext.Session.Remove("PayPalUserId");
            HttpContext.Session.Remove("PayPalTravelPackageId");
            HttpContext.Session.Remove("PayPalRoomsCount");
            HttpContext.Session.Remove("PayPalCartItemIds");

            TempData["PaymentSuccess"] = $"תשלום PayPal בוצע בהצלחה! {createdBookings.Count} הזמנה/ות נוצרו. אימיילי אישור נשלחו.";
            return RedirectToAction("MyBookings", "Booking");
        }
        else
        {
            TempData["PaymentError"] = payPalExecuteError ?? "תשלום PayPal נכשל.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public IActionResult PayPalCancel()
    {

        HttpContext.Session.Remove("PayPalPaymentId");
        HttpContext.Session.Remove("PayPalUserId");
        HttpContext.Session.Remove("PayPalTravelPackageId");
        HttpContext.Session.Remove("PayPalRoomsCount");
        HttpContext.Session.Remove("PayPalCartItemIds");

        TempData["PaymentError"] = "תשלום PayPal בוטל.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Processes payment with credit card information.
    /// SECURITY: This method processes card information in memory only and NEVER stores ANY card data:
    /// - Full card number (NOT stored, NOT logged, NOT saved anywhere)
    /// - Last 4 digits (NOT stored, NOT logged, NOT saved anywhere)
    /// - CVV code (NOT stored, NOT logged, NOT saved anywhere)
    /// - Expiry date (NOT stored, NOT logged, NOT saved anywhere)
    /// - Cardholder name (NOT stored, NOT logged, NOT saved anywhere)
    /// All sensitive card data is discarded immediately after validation and processing.
    /// No card information is ever persisted to database, logs, or any storage.
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> ProcessPaymentAsync(PaymentViewModel model, ApplicationUser user)
    {

        try
        {
            // SECURITY: Card information is validated but never stored
            if (string.IsNullOrWhiteSpace(model.CardNumber))
            {
                return (false, "מספר כרטיס נדרש לתשלום בכרטיס אשראי/חיוב.");
            }

            if (!IsValidCardNumber(model.CardNumber))
            {
                return (false, "מספר כרטיס לא תקין. אנא בדוק את מספר הכרטיס.");
            }

            if (string.IsNullOrWhiteSpace(model.ExpiryDate))
            {
                return (false, "תאריך תפוגה נדרש.");
            }

            if (!IsValidExpiryDate(model.ExpiryDate))
            {
                return (false, "הכרטיס פג תוקף או תאריך תפוגה לא תקין.");
            }

            if (string.IsNullOrWhiteSpace(model.CVV) || model.CVV.Length < 3)
            {
                return (false, "CVV נדרש.");
            }

            await Task.Delay(500); // Simulate network delay

            var randomCard = new Random();
            if (randomCard.Next(100) < 90)
            {
                // SECURITY: Never log any card information (not even last 4 digits)
                // Clear sensitive data from memory immediately
                model.CardNumber = null;
                model.CVV = null;
                model.ExpiryDate = null;
                model.CardHolderName = null;
                
                _logger.LogInformation($"Payment processed successfully for user {user.Id}. Payment method: Credit Card");
                return (true, null);
            }
            else
            {
                // Clear sensitive data even on failure
                model.CardNumber = null;
                model.CVV = null;
                model.ExpiryDate = null;
                model.CardHolderName = null;
                
                return (false, "התשלום נדחה על ידי הבנק. אנא בדוק את פרטי הכרטיס.");
            }
        }
        catch (Exception ex)
        {
            // SECURITY: Never log card information in error logs
            _logger.LogError(ex, $"Error processing payment for user {user.Id}");
            
            // Clear sensitive data on error
            model.CardNumber = null;
            model.CVV = null;
            model.ExpiryDate = null;
            model.CardHolderName = null;
            
            return (false, "אירעה שגיאה בעת עיבוד התשלום. אנא נסה שוב.");
        }
    }

    private bool IsValidCardNumber(string cardNumber)
    {

        cardNumber = cardNumber.Replace(" ", "").Replace("-", "");

        if (string.IsNullOrWhiteSpace(cardNumber) || !cardNumber.All(char.IsDigit))
            return false;

        int sum = 0;
        bool alternate = false;
        for (int i = cardNumber.Length - 1; i >= 0; i--)
        {
            int n = int.Parse(cardNumber[i].ToString());
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private bool IsValidExpiryDate(string expiryDate)
    {
        if (string.IsNullOrWhiteSpace(expiryDate))
            return false;

        var parts = expiryDate.Split('/');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int year))
            return false;

        if (month < 1 || month > 12)
            return false;

        if (year < 100)
        {
            year += 2000;
        }

        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var currentMonth = now.Month;

        if (year < currentYear)
            return false;

        if (year == currentYear && month < currentMonth)
            return false;

        return true;
    }
}
