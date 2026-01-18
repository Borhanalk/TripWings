using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;
using TripWings.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TripWings.Controllers;

[Authorize]
public class WalletController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWalletService _walletService;
    private readonly ILogger<WalletController> _logger;

    public WalletController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IWalletService walletService,
        ILogger<WalletController> logger)
    {
        _context = context;
        _userManager = userManager;
        _walletService = walletService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "מנהלים לא יכולים לגשת לארנק. / Admins cannot access the wallet.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var balance = await _walletService.GetBalanceAsync(user.Id);
        var transactions = await _walletService.GetTransactionsAsync(user.Id, limit: 20);
        var withdrawals = await _walletService.GetWithdrawalsAsync(user.Id);

        ViewBag.Balance = balance;
        ViewBag.Transactions = transactions;
        ViewBag.Withdrawals = withdrawals;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw()
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "מנהלים לא יכולים לגשת לארנק. / Admins cannot access the wallet.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var balance = await _walletService.GetBalanceAsync(user.Id);
        ViewBag.Balance = balance;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(Models.ViewModels.BankWithdrawalRequestViewModel model)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "מנהלים לא יכולים לגשת לארנק. / Admins cannot access the wallet.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            var balance = await _walletService.GetBalanceAsync(user.Id);
            ViewBag.Balance = balance;
            return View(model);
        }

        var balance2 = await _walletService.GetBalanceAsync(user.Id);
        
        if (model.Amount > balance2)
        {
            ModelState.AddModelError("Amount", "אין מספיק כסף בארנק");
            ViewBag.Balance = balance2;
            return View(model);
        }

        var request = new BankWithdrawalRequest
        {
            AccountNumber = model.AccountNumber,
            AccountHolderName = model.AccountHolderName,
            BranchNumber = model.BranchNumber,
            BankName = model.BankName,
            IdNumber = model.IdNumber,
            AdditionalInfo = model.AdditionalInfo
        };

        var (success, errorMessage) = await _walletService.WithdrawToBankAsync(user.Id, model.Amount, request);

        if (success)
        {
            TempData["Success"] = $"בקשת משיכה בסך {model.Amount:C} נוצרה בהצלחה. הכסף יועבר לחשבון הבנק תוך 3-5 ימי עסקים.";
            return RedirectToAction("Index");
        }
        else
        {
            TempData["Error"] = errorMessage ?? "אירעה שגיאה בעת יצירת בקשת משיכה";
            ViewBag.Balance = balance2;
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadInvoice(int withdrawalId)
    {

        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "מנהלים לא יכולים לגשת לארנק. / Admins cannot access the wallet.";
            return RedirectToAction("Index", "AdminDashboard");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var withdrawal = await _context.BankWithdrawals
            .Include(w => w.Wallet)
                .ThenInclude(w => w.User)
            .Include(w => w.Transaction)
            .FirstOrDefaultAsync(w => w.Id == withdrawalId && w.Wallet.UserId == user.Id);

        if (withdrawal == null) return NotFound();

        QuestPDF.Settings.License = LicenseType.Community;
        
        var invoiceNumber = $"INV-{withdrawal.Id:D6}";
        var invoiceDate = withdrawal.RequestedAt.ToString("dd/MM/yyyy");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text("חשבונית מס / קבלה")
                        .SemiBold()
                        .FontSize(20)
                        .AlignRight();

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(20);

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text($"מספר חשבונית: {invoiceNumber}").SemiBold();
                                col.Item().Text($"תאריך: {invoiceDate}");
                                col.Item().Text($"סטטוס: {withdrawal.Status}");
                            });
                        });

                        column.Item().LineHorizontal(1);

                        column.Item().Text("פרטי הלקוח:").SemiBold();
                        column.Item().PaddingLeft(20).Column(col =>
                        {
                            col.Item().Text($"שם: {user.FirstName} {user.LastName}");
                            col.Item().Text($"אימייל: {user.Email}");
                        });

                        column.Item().LineHorizontal(1);

                        column.Item().Text("פרטי המשיכה:").SemiBold();
                        column.Item().PaddingLeft(20).Column(col =>
                        {
                            col.Item().Text($"סכום: {withdrawal.Amount:C}");
                            col.Item().Text($"בנק: {withdrawal.BankName}");
                            col.Item().Text($"מספר סניף: {withdrawal.BranchNumber}");
                            col.Item().Text($"מספר חשבון: {withdrawal.AccountNumber}");
                            col.Item().Text($"שם בעל החשבון: {withdrawal.AccountHolderName}");
                            if (!string.IsNullOrEmpty(withdrawal.IdNumber))
                            {
                                col.Item().Text($"מספר זהות: {withdrawal.IdNumber}");
                            }
                        });

                        column.Item().LineHorizontal(1);

                        if (!string.IsNullOrEmpty(withdrawal.Notes))
                        {
                            column.Item().Text("הערות:").SemiBold();
                            column.Item().PaddingLeft(20).Text(withdrawal.Notes);
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("תודה על שירותך! ");
                        x.Span("TripWings").SemiBold();
                    });
            });
        });

        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;

        return File(stream, "application/pdf", $"invoice_{invoiceNumber}.pdf");
    }
}
