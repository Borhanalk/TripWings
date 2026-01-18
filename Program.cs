using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped<TripWings.Services.IBookingService, TripWings.Services.BookingService>();
builder.Services.AddScoped<TripWings.Services.IWaitingListService, TripWings.Services.WaitingListService>();
builder.Services.AddScoped<TripWings.Services.IReceiptPdfService, TripWings.Services.ReceiptPdfService>();
builder.Services.AddScoped<TripWings.Services.INotificationService, TripWings.Services.NotificationService>();
builder.Services.AddScoped<TripWings.Services.IItineraryService, TripWings.Services.ItineraryService>();
builder.Services.AddScoped<TripWings.Services.IPayPalService, TripWings.Services.PayPalService>();
builder.Services.AddScoped<TripWings.Services.IWalletService, TripWings.Services.WalletService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<TripWings.Services.NotificationService>();

builder.Services.AddHostedService<TripWings.Services.ReminderBackgroundService>();
builder.Services.AddHostedService<TripWings.Services.WaitingListBackgroundService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.Initialize(services);
}

app.Run();
