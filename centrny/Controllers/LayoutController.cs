using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using centrny.Models;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace centrny.Controllers
{
    public class LayoutController : Controller
    {
        private readonly CenterContext _db;

        public LayoutController(CenterContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // Keep this — now the form will include the token.
        public IActionResult SetResourceLanguage(string resourceCulture, string returnUrl = null)
        {
            // Normalize to supported set
            var culture = (resourceCulture?.StartsWith("ar") ?? false) ? "ar" : "en";

            // Store in session
            HttpContext.Session.SetString("ResourceCulture", culture);

            // Also set RequestLocalization cookie so middleware picks it up on next request
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

            System.Diagnostics.Debug.WriteLine($"[SetResourceLanguage] culture set to {culture}");

            return Redirect(returnUrl ?? "/");
        }

        public static List<SidebarPageViewModel> GetSidebarPagesForUser(CenterContext db, ISession session)
        {
            const string sessionKey = "SidebarPages";
            string resourceCulture = session?.GetString("ResourceCulture") ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string cultureInSession = session?.GetString("SidebarPagesCulture");

            if (session != null && session.TryGetValue(sessionKey, out var cachedBytes) && cultureInSession == resourceCulture)
            {
                var cachedJson = System.Text.Encoding.UTF8.GetString(cachedBytes);
                var cached = System.Text.Json.JsonSerializer.Deserialize<List<SidebarPageViewModel>>(cachedJson);
                if (cached != null) return cached;
            }

            int? userCode = session?.GetInt32("UserCode");
            int? groupCode = session?.GetInt32("GroupCode");

            if (userCode == null || groupCode == null)
                return new List<SidebarPageViewModel>();

            var groupPageCodes = db.GroupPages
                .Where(gp => gp.GroupCode == groupCode.Value)
                .Select(gp => gp.PageCode)
                .Distinct()
                .ToList();

            if (!groupPageCodes.Any())
                return new List<SidebarPageViewModel>();

            bool isArabic = resourceCulture.StartsWith("ar");

            var allowedPages = db.Pages
                .Where(p => groupPageCodes.Contains(p.PageCode))
                .OrderBy(p => p.PageSort)
                .Select(p => new SidebarPageViewModel
                {
                    Controller = GetControllerFromPath(p.PagePath),
                    Action = GetActionFromPath(p.PagePath),
                    Icon = GetIconForPage(p.PageName),
                    Text = isArabic && !string.IsNullOrEmpty(p.PageNameAr) ? p.PageNameAr : p.PageName
                }).ToList();

            if (session != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(allowedPages);
                session.Set(sessionKey, System.Text.Encoding.UTF8.GetBytes(json));
                session.SetString("SidebarPagesCulture", resourceCulture);
            }

            return allowedPages;
        }

        public static string GetControllerFromPath(string pagePath)
        {
            if (string.IsNullOrEmpty(pagePath)) return "";
            var parts = pagePath.Split('/');
            return parts.Length > 0 ? parts[0] : "";
        }

        public static string GetActionFromPath(string pagePath)
        {
            if (string.IsNullOrEmpty(pagePath)) return "";
            var parts = pagePath.Split('/');
            return parts.Length > 1 ? parts[1] : "Index";
        }

        public static string GetIconForPage(string pageName)
        {
            if (string.IsNullOrEmpty(pageName)) return "fas fa-file";
            var lowerPageName = pageName.ToLower().Trim();

            switch (lowerPageName)
            {
                case "roots": return "fas fa-sitemap";
                case "branch management": return "fas fa-code-branch";
                case "groups management": return "fas fa-users";
                case "edu years & years":
                case "educational year": return "fas fa-calendar";
                case "expenses": return "fas fa-money-bill-wave";
                case "subjects": return "fas fa-book";
                case "students": return "fas fa-user-graduate";
                case "exams":
                case "exam": return "fas fa-file-alt";
                case "schedules":
                case "schedule list": return "fas fa-calendar-alt";
                case "wallet exam list": return "fas fa-wallet";
                case "security management": return "fas fa-user-shield";
                case "dailyclass": return "fas fa-calendar-day";
                case "reservations": return "fas fa-calendar-check";
                case "teacher management": return "fas fa-chalkboard-teacher";
                case "items": return "fas fa-boxes";
                case "classes": return "fas fa-chalkboard";
                case "questions":
                case "question": return "fas fa-question-circle";
                case "student exam": return "fas fa-user-graduate";
                case "page permissions": return "fas fa-users-cog";
                case "viewauthority": return "fas fa-user-lock";
                case "employee": return "fas fa-user-tie";
                case "item": return "fas fa-box";
                case "root list": return "fas fa-sitemap";
                case "reports": return "fas fa-chart-line";
                case "subscription": return "fas fa-receipt";
                case "subscription add": return "fas fa-plus-circle";
                default: return "fas fa-file";
            }
        }
    }

    public class SidebarPageViewModel
    {
        public string Controller { get; set; }
        public string Action { get; set; }
        public string Icon { get; set; }
        public string Text { get; set; }
    }
}