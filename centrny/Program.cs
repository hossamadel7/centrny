using centrny.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Log the connection string once (temporary debug)
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine("DEBUG ConnectionString: " + (cs ?? "<null>"));

// EF Core
builder.Services.AddDbContext<CenterContext>(options =>
    options.UseSqlServer(cs));

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Login/Logout";
        options.AccessDeniedPath = "/Login/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Configure file upload settings
builder.Services.Configure<FileUploadSettings>(options =>
{
    // Generic upload location - you can change this path as needed
    options.UploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "files");
    options.MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    options.AllowedExtensions = new[] {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".jpg", ".jpeg", ".png", ".gif", ".zip", ".rar",
        ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav"
    };
});

// Localization
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ar") };
builder.Services.Configure<RequestLocalizationOptions>(opts =>
{
    opts.DefaultRequestCulture = new RequestCulture("en");
    opts.SupportedCultures = supportedCultures;
    opts.SupportedUICultures = supportedCultures;
});

// MVC
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var app = builder.Build();

// Ensure upload directory exists
try
{
    var fileUploadSettings = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileUploadSettings>>().Value;
    Directory.CreateDirectory(fileUploadSettings.UploadPath);
    Console.WriteLine($"? Upload directory ensured: {fileUploadSettings.UploadPath}");
    Console.WriteLine($"?? Max file size: {fileUploadSettings.MaxFileSizeBytes / (1024 * 1024)}MB");
    Console.WriteLine($"?? Allowed extensions: {string.Join(", ", fileUploadSettings.AllowedExtensions)}");
}
catch (Exception ex)
{
    Console.WriteLine($"? Error setting up upload directory: {ex.Message}");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

var locOptions = app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(locOptions);

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",

 pattern: "{controller=Home}/{action=Index}/{id?}");
Console.WriteLine($"?? Application started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine($"?? System configured for user: hossamadel7");

app.Run();

// File upload configuration class
public class FileUploadSettings
{
    public string UploadPath { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; }
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}