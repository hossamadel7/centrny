using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace centrny.Controllers
{
    public class WebsiteController : Controller
    {
        // Whitelist of allowed (base) page names (without language suffix)
        private static readonly HashSet<string> AllowedPages = new(StringComparer.OrdinalIgnoreCase)
        {
            "home",
            "about",
            "courses",
            "centres",
            "outstanding-students",
            "signup",
            "login"
        };

        // Routes supported:
        //   /home
        //   /home-ar
        //   /about
        //   /about-ar
        //   / (defaults to home)
        [Route("")]
        [Route("{page}")]
        [Route("{page}-{lang}")]
        public IActionResult Page(string page = "Home", string? lang = null)
        {
            if (!AllowedPages.Contains(page))
                return NotFound();

            // Build view name
            // Views/Website/Home.cshtml or Home-ar.cshtml
            var viewName = lang?.ToLower() == "ar"
                ? $"{Cap(page)}-ar"
                : Cap(page);

            return View(viewName);
        }

        private static string Cap(string slug)
        {
            // Convert slug like "outstanding-students" to "OutstandingStudents"
            if (string.IsNullOrWhiteSpace(slug)) return slug;
            var parts = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                parts[i] = char.ToUpperInvariant(p[0]) + p.Substring(1);
            }
            return string.Join("", parts);
        }
    }
}