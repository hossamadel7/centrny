using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using centrny.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

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

        public static List<SidebarPageViewModel> GetSidebarPagesForUser(CenterContext db, ClaimsPrincipal user, ISession session)
        {
            var sessionKey = "SidebarPages";
            if (session != null && session.TryGetValue(sessionKey, out var cachedBytes))
            {
                var cachedJson = System.Text.Encoding.UTF8.GetString(cachedBytes);
                var cached = System.Text.Json.JsonSerializer.Deserialize<List<SidebarPageViewModel>>(cachedJson);
                if (cached != null) return cached;
            }
            var username = user.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return new List<SidebarPageViewModel>();

            var currentUser = db.Users.FirstOrDefault(u => u.Username == username);
            if (currentUser == null) return new List<SidebarPageViewModel>();

            var group = db.Groups.FirstOrDefault(g => g.GroupCode == currentUser.GroupCode);
            if (group == null) return new List<SidebarPageViewModel>();

            var rootCode = group.RootCode;

            var assignedModuleCodes = db.RootModules
                .Where(rm => rm.RootCode == rootCode)
                .Select(rm => rm.ModuleCode)
                .Distinct()
                .ToList();

            if (!assignedModuleCodes.Any())
            {
                return new List<SidebarPageViewModel>();
            }

            // Fetch allowed pages based on updated Page table (with new URLs and removed deleted ones)
            var allowedPages = db.Pages
                .Where(p => assignedModuleCodes.Contains(p.ModuleCode))
                .OrderBy(p => p.PageSort)
                .Select(p => new SidebarPageViewModel
                {
                    Controller = GetControllerFromPath(p.PagePath),
                    Action = GetActionFromPath(p.PagePath),
                    Icon = GetIconForPage(p.PageName),
                    Text = p.PageName
                }).ToList();

            if (session != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(allowedPages);
                session.Set(sessionKey, System.Text.Encoding.UTF8.GetBytes(json));
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