using centrny.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;

namespace centrny.Controllers
{
    public class LoginController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<LoginController> _logger;

        public LoginController(CenterContext context, ILogger<LoginController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Unicode MD5 hasher to match SQL trigger
        public static string MD5hasher(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.Unicode.GetBytes(input ?? "");
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                    sb.Append(b.ToString("X2")); // Uppercase hex to match SQL output
                return sb.ToString();
            }
        }

        [AllowAnonymous]
        public IActionResult Index(string lang = null)
        {
            // Language switch logic
            if (!string.IsNullOrEmpty(lang))
            {
                var culture = lang == "ar" ? "ar-SA" : "en-US";
                CultureInfo ci = new CultureInfo(culture);

                System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
                System.Threading.Thread.CurrentThread.CurrentCulture = ci;

                // Optional: persist language in session/cookie for future requests
                HttpContext.Session.SetString("CurrentCulture", culture);
            }
            else
            {
                // If persisted, use the session value
                var sessionCulture = HttpContext.Session.GetString("CurrentCulture");
                if (!string.IsNullOrWhiteSpace(sessionCulture))
                {
                    CultureInfo ci = new CultureInfo(sessionCulture);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
                    System.Threading.Thread.CurrentThread.CurrentCulture = ci;
                }
            }

            // If already logged in, redirect to Root
            if (User.Identity.IsAuthenticated)
            {
                // If logged in but forced to update password, redirect to change password view
                if (IsDefaultPassword(User))
                {
                    return RedirectToAction("ForceChangePassword");
                }
                return RedirectToAction("Index", "Root");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string returnUrl = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    ViewBag.Error = "Username and password are required.";
                    return View("Index");
                }

                _logger.LogInformation("Login attempt for username: '{Username}'", username);

                // Find user with all necessary navigation properties
                var user = await _context.Users
                    .Include(u => u.GroupCodeNavigation)
                        .ThenInclude(g => g.RootCodeNavigation)
                    .Include(u => u.GroupCodeNavigation.GroupPages)
                        .ThenInclude(gp => gp.PageCodeNavigation)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower().Trim());

                if (user == null)
                {
                    ViewBag.Error = "Invalid username or password.";
                    ViewBag.Username = username;
                    return View("Index");
                }

                if (!user.IsActive)
                {
                    ViewBag.Error = "User account is inactive.";
                    ViewBag.Username = username;
                    return View("Index");
                }

                // Calculate the hashed password before comparison
                string hashedInputPassword = MD5hasher(password);

                if (hashedInputPassword != user.Password)
                {
                    _logger.LogWarning("Login attempt with invalid password for user: {Username}", username);
                    ViewBag.Error = "Invalid username or password.";
                    ViewBag.Username = username;
                    return View("Index");
                }

                // Update last login time
                user.LastUpdateTime = DateTime.Now;
                await _context.SaveChangesAsync();

                // ==== SESSION STORAGE LOGIC ====
                // Store user info in session
                HttpContext.Session.SetInt32("UserCode", user.UserCode);
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("Password", user.Password); // hashed password
                HttpContext.Session.SetInt32("GroupCode", user.GroupCode);

                // Store Group info
                if (user.GroupCodeNavigation != null)
                {
                    HttpContext.Session.SetInt32("GroupCode", user.GroupCodeNavigation.GroupCode);
                    HttpContext.Session.SetString("GroupName", user.GroupCodeNavigation.GroupName ?? "");

                    // Store Root info
                    if (user.GroupCodeNavigation.RootCodeNavigation != null)
                    {
                        var root = user.GroupCodeNavigation.RootCodeNavigation;
                        HttpContext.Session.SetInt32("RootCode", root.RootCode);
                        HttpContext.Session.SetString("RootName", root.RootName ?? "");
                        HttpContext.Session.SetString("RootDomain", root.RootDomain ?? "");
                        HttpContext.Session.SetString("RootIsCenter", root.IsCenter.ToString());

                        if (!root.IsCenter)
                        {
                            // Get Teacher info by RootCode FK
                            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.RootCode == root.RootCode);
                            if (teacher != null)
                            {
                                HttpContext.Session.SetInt32("TeacherCode", teacher.TeacherCode);
                                HttpContext.Session.SetString("TeacherName", teacher.TeacherName ?? "");
                            }
                        }
                        else
                        {
                            // Get Center info
                            var center = await _context.Centers.FirstOrDefaultAsync(c => c.RootCode == root.RootCode);
                            if (center != null)
                            {
                                HttpContext.Session.SetInt32("CenterCode", center.CenterCode);
                                HttpContext.Session.SetString("CenterName", center.CenterName ?? "");

                                // Get branches for this center
                                var branches = await _context.Branches
                                    .Where(b => b.CenterCode == center.CenterCode)
                                    .Select(b => new { b.BranchCode, b.BranchName })
                                    .ToListAsync();

                                var branchJson = JsonConvert.SerializeObject(branches);
                                HttpContext.Session.SetString("Branches", branchJson);
                            }
                        }
                    }
                }
                // ==== END SESSION STORAGE LOGIC ====

                // Create authentication claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserCode.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("FullName", user.Name),
                    new Claim("GroupCode", user.GroupCode.ToString())
                };

                // Add group and root information if available
                if (user.GroupCodeNavigation != null)
                {
                    claims.Add(new Claim("GroupName", user.GroupCodeNavigation.GroupName ?? ""));

                    if (user.GroupCodeNavigation.RootCodeNavigation != null)
                    {
                        claims.Add(new Claim("RootCode", user.GroupCodeNavigation.RootCode.ToString()));
                        claims.Add(new Claim("RootName", user.GroupCodeNavigation.RootCodeNavigation.RootName ?? ""));
                        claims.Add(new Claim("IsCenter", user.GroupCodeNavigation.RootCodeNavigation.IsCenter.ToString()));
                    }

                    // Add page permissions as claims for easy access
                    if (user.GroupCodeNavigation.GroupPages != null)
                    {
                        foreach (var groupPage in user.GroupCodeNavigation.GroupPages)
                        {
                            if (groupPage.InsertFlag || groupPage.UpdateFlag || groupPage.DeleteFlag)
                            {
                                claims.Add(new Claim("PageAccess", groupPage.PageCode.ToString()));

                                if (groupPage.InsertFlag)
                                    claims.Add(new Claim("PageInsert", groupPage.PageCode.ToString()));
                                if (groupPage.UpdateFlag)
                                    claims.Add(new Claim("PageUpdate", groupPage.PageCode.ToString()));
                                if (groupPage.DeleteFlag)
                                    claims.Add(new Claim("PageDelete", groupPage.PageCode.ToString()));
                            }
                        }
                    }
                }

                // Add custom claim if user still has default password (hashed)
                if (user.Password == MD5hasher("123456789"))
                {
                    claims.Add(new Claim("ForcePasswordChange", "true"));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true, // Remember login
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) // 8 hour session
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                _logger.LogInformation("Successful login for user: {Username}", username);

                // After login, check for forced password change
                if (user.Password == MD5hasher("123456789"))
                {
                    return RedirectToAction("ForceChangePassword");
                }

                // Redirect to return URL or Root
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Root");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", username);
                ViewBag.Error = $"An error occurred during login: {ex.Message}";
                return View("Index");
            }
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            ViewBag.Message = "You don't have permission to access this page.";
            ViewBag.ReturnUrl = Request.Headers["Referer"].ToString();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Remove("SidebarPages");
            // Remove all custom session keys
            HttpContext.Session.Remove("UserCode");
            HttpContext.Session.Remove("Username");
            HttpContext.Session.Remove("Password");
            HttpContext.Session.Remove("GroupCode");
            HttpContext.Session.Remove("GroupName");
            HttpContext.Session.Remove("RootCode");
            HttpContext.Session.Remove("RootName");
            HttpContext.Session.Remove("RootDomain");
            HttpContext.Session.Remove("RootIsCenter");
            HttpContext.Session.Remove("TeacherCode");
            HttpContext.Session.Remove("TeacherName");
            HttpContext.Session.Remove("CenterCode");
            HttpContext.Session.Remove("CenterName");
            HttpContext.Session.Remove("Branches");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out: {Username}", User.Identity.Name);
            return RedirectToAction("Index");
        }

        // Helper method to check if the logged-in user has default password (hashed)
        private bool IsDefaultPassword(ClaimsPrincipal user)
        {
            if (user.Identity.IsAuthenticated)
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var dbUser = _context.Users.Find(int.Parse(userId));
                    if (dbUser != null && dbUser.Password == MD5hasher("123456789"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // View for forcing the user to change their password
        [Authorize]
        public IActionResult ForceChangePassword()
        {
            if (!IsDefaultPassword(User))
            {
                return RedirectToAction("Index", "Root");
            }
            return View("ChangePassword");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceChangePassword(string newPassword, string confirmPassword)
        {
            if (!IsDefaultPassword(User))
            {
                return RedirectToAction("Index", "Root");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error = "Please enter the new password and confirm it.";
                return View("ChangePassword");
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "The new password and confirmation do not match.";
                return View("ChangePassword");
            }

            if (MD5hasher(newPassword) == MD5hasher("123456789"))
            {
                ViewBag.Error = "You cannot use the default password as your new password.";
                return View("ChangePassword");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return RedirectToAction("Index", "Root");
            }

            var dbUser = await _context.Users.FindAsync(int.Parse(userId));
            if (dbUser != null)
            {
                dbUser.Password = MD5hasher(newPassword);
                await _context.SaveChangesAsync();

                // Log out & force re-login with new password
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Index");
            }

            ViewBag.Error = "Unable to update password.";
            return View("ChangePassword");
        }

        public static bool HasPageAccess(ClaimsPrincipal user, string pagePath)
        {
            var pageName = pagePath.TrimStart('/').Replace('/', '_');
            return user.HasClaim("PageAccess", pageName) || user.IsInRole("Admin");
        }
    }
}