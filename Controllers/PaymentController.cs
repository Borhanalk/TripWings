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
            TempData["Error"] = "المسؤولون لا يمكنهم إجراء عمليات دفع.";
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
            if (isFull || remainingRooms <= 0)
            {
                TempData["Error"] = "החבילה הזו כבר לא זמינה. / This package is no longer available.";
                return RedirectToAction("Details", "Trips", new { id = travelPackageId });
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
            TempData["Error"] = "المسؤولون لا يمكنهم إجراء عمليات دفع.";
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

            var (success, paymentId, approvalUrl, errorMessage) = await _payPalService.CreatePaymentAsync(
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
                TempData["PaymentError"] = errorMessage ?? "תשלום PayPal נכשל. אנא נסה שוב.";
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

                var (success, errorMessage, booking) = await _bookingService.CreateBookingAsync(
                    user.Id,
                    model.TravelPackageId.Value,
                    model.RoomsCount ?? 1);

                if (success && booking != null)
                {
                    createdBookings.Add(booking);

                    var cartItem = await _context.CartItems
                        .FirstOrDefaultAsync(c => c.UserId == user.Id && c.TravelPackageId == model.TravelPackageId.Value);
                    
                    if (cartItem != null)
                    {
                        _context.CartItems.Remove(cartItem);
                        _logger.LogInformation($"Removed cart item for travel package {model.TravelPackageId.Value} after Buy Now payment for user {user.Id}");
                    }
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

                    var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(cartItem.TravelPackageId);
                    if (!isFull && remainingRooms >= cartItem.Quantity)
                    {
                        var (success, _, booking) = await _bookingService.CreateBookingAsync(
                            user.Id,
                            cartItem.TravelPackageId,
                            cartItem.Quantity);

                        if (success && booking != null)
                        {
                            createdBookings.Add(booking);

                            _context.CartItems.Remove(cartItem);
                        }
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
                
                var cardNumber = model.CardNumber?.Replace(" ", "").Replace("-", "") ?? "";
                var lastFour = cardNumber.Length >= 4 ? cardNumber.Substring(cardNumber.Length - 4) : "****";
                
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
                    LastFourDigits = lastFour,
                    InstallmentsCount = model.InstallmentsCount,
                    InstallmentAmount = installmentAmount
                };
                
                _context.Payments.Add(payment);
                payments.Add(payment);
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
            TempData["Error"] = "المسؤولون لا يمكنهم إجراء عمليات دفع.";
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

        var (success, errorMessage) = await _payPalService.ExecutePaymentAsync(paymentId, PayerID);

        if (success)
        {

            List<Booking> createdBookings = new();

            var travelPackageIdStr = HttpContext.Session.GetString("PayPalTravelPackageId");
            var roomsCountStr = HttpContext.Session.GetString("PayPalRoomsCount");
            var cartItemIdsStr = HttpContext.Session.GetString("PayPalCartItemIds");

            if (!string.IsNullOrEmpty(travelPackageIdStr) && int.TryParse(travelPackageIdStr, out var travelPackageId))
            {
                var roomsCount = int.TryParse(roomsCountStr, out var count) ? count : 1;
                
                var (bookingSuccess, _, booking) = await _bookingService.CreateBookingAsync(
                    user.Id,
                    travelPackageId,
                    roomsCount);

                if (bookingSuccess && booking != null)
                {
                    createdBookings.Add(booking);

                    var cartItem = await _context.CartItems
                        .FirstOrDefaultAsync(c => c.UserId == user.Id && c.TravelPackageId == travelPackageId);
                    
                    if (cartItem != null)
                    {
                        _context.CartItems.Remove(cartItem);
                    }
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
                    var (isFull, remainingRooms) = await _bookingService.CheckAvailabilityAsync(cartItem.TravelPackageId);
                    if (!isFull && remainingRooms >= cartItem.Quantity)
                    {
                        var (bookingSuccess, _, booking) = await _bookingService.CreateBookingAsync(
                            user.Id,
                            cartItem.TravelPackageId,
                            cartItem.Quantity);

                        if (bookingSuccess && booking != null)
                        {
                            createdBookings.Add(booking);
                            _context.CartItems.Remove(cartItem);
                        }
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
            TempData["PaymentError"] = errorMessage ?? "תשלום PayPal נכשל.";
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

    private async Task<(bool Success, string? ErrorMessage)> ProcessPaymentAsync(PaymentViewModel model, ApplicationUser user)
    {

        try
        {

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
                var cardNumber = model.CardNumber.Replace(" ", "").Replace("-", "");
                var lastFour = cardNumber.Length >= 4 ? cardNumber.Substring(cardNumber.Length - 4) : "****";
                _logger.LogInformation($"Payment processed for user {user.Id}. Card: ****{lastFour}");
                return (true, null);
            }
            else
            {
                return (false, "התשלום נדחה על ידי הבנק. אנא בדוק את פרטי הכרטיס.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing payment for user {user.Id}");
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
