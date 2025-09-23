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
            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.Unicode.GetBytes(input ?? "");
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        private bool IsSessionInitialized()
        {
            return HttpContext.Session.GetInt32("UserCode").HasValue
                   && HttpContext.Session.GetInt32("GroupCode").HasValue
                   && HttpContext.Session.GetInt32("RootCode").HasValue;
        }

        /// <summary>
        /// Attempts to rebuild the session from existing authentication claims if possible.
        /// Returns true if it successfully set minimal required session keys.
        /// </summary>
        private async Task<bool> EnsureSessionFromClaimsAsync(ClaimsPrincipal user)
        {
            try
            {
                if (!user.Identity?.IsAuthenticated ?? true) return false;

                var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userIdClaim)) return false;

                if (!int.TryParse(userIdClaim, out int userCode)) return false;

                // Load user with navigation needed to reconstruct session
                var dbUser = await _context.Users
                    .Include(u => u.GroupCodeNavigation)
                        .ThenInclude(g => g.RootCodeNavigation)
                    .FirstOrDefaultAsync(u => u.UserCode == userCode);

                if (dbUser == null) return false;

                // Repopulate essential session keys
                HttpContext.Session.SetInt32("UserCode", dbUser.UserCode);
                HttpContext.Session.SetString("Username", dbUser.Username);
                HttpContext.Session.SetString("Password", dbUser.Password);
                HttpContext.Session.SetInt32("GroupCode", dbUser.GroupCode);
             
                if (dbUser.GroupCodeNavigation != null)
                {
                    HttpContext.Session.SetString("GroupName", dbUser.GroupCodeNavigation.GroupName ?? "");
                    if (dbUser.GroupCodeNavigation.RootCodeNavigation != null)
                    {
                        var root = dbUser.GroupCodeNavigation.RootCodeNavigation;
                        HttpContext.Session.SetInt32("RootCode", root.RootCode);
                        HttpContext.Session.SetString("RootName", root.RootName ?? "");
                        HttpContext.Session.SetString("RootDomain", root.RootDomain ?? "");
                        HttpContext.Session.SetString("RootIsCenter", root.IsCenter.ToString());
                        // Store root colors in session
                        HttpContext.Session.SetString("RootBodyColor", root.RootBodyColor ?? "#fff");
                        HttpContext.Session.SetString("RootButtonColor", root.RootButtonColor ?? "#007bff");
                        HttpContext.Session.SetString("RootBodyFont", root.RootBodyFont ?? "#000");
                        HttpContext.Session.SetString("RootButtonFontColor", root.RootButtonFontColor ?? "#000");
                        HttpContext.Session.SetString("RootButtonFontColor2", root.RootButtonFontColor2 ?? "#fff");
                        HttpContext.Session.SetString("RootBackgroundColor", root.RootBackgroundColor ?? "#eee");

                        if (!root.IsCenter)
                        {
                            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.RootCode == root.RootCode);
                            if (teacher != null)
                            {
                                HttpContext.Session.SetInt32("TeacherCode", teacher.TeacherCode);
                                HttpContext.Session.SetString("TeacherName", teacher.TeacherName ?? "");
                            }
                        }
                        else
                        {
                            var center = await _context.Centers.FirstOrDefaultAsync(c => c.RootCode == root.RootCode);
                            if (center != null)
                            {
                                HttpContext.Session.SetInt32("CenterCode", center.CenterCode);
                                HttpContext.Session.SetString("CenterName", center.CenterName ?? "");
                                var branches = await _context.Branches
                                    .Where(b => b.CenterCode == center.CenterCode)
                                    .Select(b => new { b.BranchCode, b.BranchName })
                                    .ToListAsync();
                                HttpContext.Session.SetString("Branches", JsonConvert.SerializeObject(branches));
                            }
                        }
                    }
                }

                // Restore language if claim exists (optional)
                if (HttpContext.Session.GetString("ResourceCulture") == null)
                {
                    // Fallback to English if not stored
                    HttpContext.Session.SetString("ResourceCulture", "en");
                }

                return IsSessionInitialized();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to rehydrate session from claims.");
                return false;
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(string lang = null)
        {
            // Language switch logic
            if (!string.IsNullOrEmpty(lang))
            {
                var culture = lang == "ar" ? "ar-SA" : "en-US";
                var ci = new CultureInfo(culture);
                CultureInfo.CurrentUICulture = ci;
                CultureInfo.CurrentCulture = ci;
                HttpContext.Session.SetString("ResourceCulture", lang == "ar" ? "ar" : "en");
            }
            else
            {
                var sessionCulture = HttpContext.Session.GetString("ResourceCulture");
                if (!string.IsNullOrWhiteSpace(sessionCulture))
                {
                    var ci = new CultureInfo(sessionCulture == "ar" ? "ar-SA" : "en-US");
                    CultureInfo.CurrentUICulture = ci;
                    CultureInfo.CurrentCulture = ci;
                }
            }

            // If already logged in, verify session or rehydrate
            if (User.Identity?.IsAuthenticated == true)
            {
                if (!IsSessionInitialized())
                {
                    var rebuilt = await EnsureSessionFromClaimsAsync(User);
                    if (!rebuilt)
                    {
                        // Claims seem stale or DB user missing—sign out to avoid redirect loop
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        _logger.LogInformation("Authenticated cookie present but session rebuild failed; forcing fresh login.");
                        return View(); // show login normally
                    }
                }

                // Now session is valid
                if (IsDefaultPassword(User))
                    return RedirectToAction("ForceChangePassword");

                return RedirectToAction("Index", "Reports");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string returnUrl = null, string lang = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    ViewBag.Error = "Username and password are required.";
                    return View("Index");
                }

                // Always clear any stale auth cookie before new login to avoid loops
                if (User.Identity?.IsAuthenticated == true)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }

                _logger.LogInformation("Login attempt for username: '{Username}'", username);

                var user = await _context.Users
                    .Include(u => u.GroupCodeNavigation)
                        .ThenInclude(g => g.RootCodeNavigation)
                    .Include(u => u.GroupCodeNavigation.GroupPages)
                        .ThenInclude(gp => gp.PageCodeNavigation)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower().Trim());

                if (user == null || !user.IsActive)
                {
                    ViewBag.Error = "Invalid username or password.";
                    ViewBag.Username = username;
                    return View("Index");
                }

                string hashedInputPassword = MD5hasher(password);
                if (hashedInputPassword != user.Password)
                {
                    _logger.LogWarning("Invalid password for user: {Username}", username);
                    ViewBag.Error = "Invalid username or password.";
                    ViewBag.Username = username;
                    return View("Index");
                }

                user.LastUpdateTime = DateTime.Now;
                await _context.SaveChangesAsync();

                // Populate session
                HttpContext.Session.SetInt32("UserCode", user.UserCode);
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("Password", user.Password);
                HttpContext.Session.SetInt32("GroupCode", user.GroupCode);

                if (user.GroupCodeNavigation != null)
                {
                    HttpContext.Session.SetString("GroupName", user.GroupCodeNavigation.GroupName ?? "");
                    if (user.GroupCodeNavigation.RootCodeNavigation != null)
                    {
                        var root = user.GroupCodeNavigation.RootCodeNavigation;
                        HttpContext.Session.SetInt32("RootCode", root.RootCode);
                        HttpContext.Session.SetString("RootName", root.RootName ?? "");
                        HttpContext.Session.SetString("RootDomain", root.RootDomain ?? "");
                        HttpContext.Session.SetString("RootIsCenter", root.IsCenter.ToString());

                        if (!root.IsCenter)
                        {
                            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.RootCode == root.RootCode);
                            if (teacher != null)
                            {
                                HttpContext.Session.SetInt32("TeacherCode", teacher.TeacherCode);
                                HttpContext.Session.SetString("TeacherName", teacher.TeacherName ?? "");
                            }
                        }
                        else
                        {
                            var center = await _context.Centers.FirstOrDefaultAsync(c => c.RootCode == root.RootCode);
                            if (center != null)
                            {
                                HttpContext.Session.SetInt32("CenterCode", center.CenterCode);
                                HttpContext.Session.SetString("CenterName", center.CenterName ?? "");
                                var branches = await _context.Branches
                                    .Where(b => b.CenterCode == center.CenterCode)
                                    .Select(b => new { b.BranchCode, b.BranchName })
                                    .ToListAsync();
                                HttpContext.Session.SetString("Branches", JsonConvert.SerializeObject(branches));
                            }
                        }
                    }
                }

                // Language
                if (!string.IsNullOrEmpty(lang))
                {
                    HttpContext.Session.SetString("ResourceCulture", lang == "ar" ? "ar" : "en");
                }
                else
                {
                    var prev = HttpContext.Session.GetString("ResourceCulture");
                    HttpContext.Session.SetString("ResourceCulture", prev ?? "en");
                }

                // Claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserCode.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("FullName", user.Name ?? ""),
                    new Claim("GroupCode", user.GroupCode.ToString())
                };

                if (user.GroupCodeNavigation != null)
                {
                    claims.Add(new Claim("GroupName", user.GroupCodeNavigation.GroupName ?? ""));
                    if (user.GroupCodeNavigation.RootCodeNavigation != null)
                    {
                        var root = user.GroupCodeNavigation.RootCodeNavigation;
                        claims.Add(new Claim("RootCode", root.RootCode.ToString()));
                        claims.Add(new Claim("RootName", root.RootName ?? ""));
                        claims.Add(new Claim("IsCenter", root.IsCenter.ToString()));
                    }

                    if (user.GroupCodeNavigation.GroupPages != null)
                    {
                        foreach (var gp in user.GroupCodeNavigation.GroupPages)
                        {
                            if (gp.InsertFlag || gp.UpdateFlag || gp.DeleteFlag)
                            {
                                claims.Add(new Claim("PageAccess", gp.PageCode.ToString()));
                                if (gp.InsertFlag) claims.Add(new Claim("PageInsert", gp.PageCode.ToString()));
                                if (gp.UpdateFlag) claims.Add(new Claim("PageUpdate", gp.PageCode.ToString()));
                                if (gp.DeleteFlag) claims.Add(new Claim("PageDelete", gp.PageCode.ToString()));
                            }
                        }
                    }
                }

                if (user.Password == MD5hasher("123456789"))
                {
                    claims.Add(new Claim("ForcePasswordChange", "true"));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                _logger.LogInformation("Successful login for user: {Username}", username);

                if (user.Password == MD5hasher("123456789"))
                {
                    return RedirectToAction("ForceChangePassword");
                }

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) &&
                    !returnUrl.Contains("/Login", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Reports");
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
            // Clear session keys
            var keys = new[]
            {
                "SidebarPages","UserCode","Username","Password","GroupCode","GroupName",
                "RootCode","RootName","RootDomain","RootIsCenter","TeacherCode","TeacherName",
                "CenterCode","CenterName","Branches","ResourceCulture"
            };
            foreach (var k in keys) HttpContext.Session.Remove(k);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out.");

            return RedirectToAction("Index");
        }

        private bool IsDefaultPassword(ClaimsPrincipal user)
        {
            try
            {
                if (user.Identity?.IsAuthenticated != true) return false;
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId)) return false;
                if (!int.TryParse(userId, out int id)) return false;
                var dbUser = _context.Users.Find(id);
                return dbUser != null && dbUser.Password == MD5hasher("123456789");
            }
            catch
            {
                return false;
            }
        }

        [Authorize]
        public IActionResult ForceChangePassword()
        {
            if (!IsDefaultPassword(User))
                return RedirectToAction("Index", "Reports");
            return View("ChangePassword");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceChangePassword(string newPassword, string confirmPassword)
        {
            if (!IsDefaultPassword(User))
                return RedirectToAction("Index", "Reports");

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
            if (!int.TryParse(userId, out int id))
                return RedirectToAction("Index", "Reports");

            var dbUser = await _context.Users.FindAsync(id);
            if (dbUser != null)
            {
                dbUser.Password = MD5hasher(newPassword);
                await _context.SaveChangesAsync();
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