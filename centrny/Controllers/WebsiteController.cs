using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace centrny.Controllers
{
    public class WebsiteController : Controller
    {
        private readonly CenterContext _dbContext;

        private static readonly HashSet<string> AllowedPages = new(StringComparer.OrdinalIgnoreCase)
        {
            "home",
            "about",
            "courses",
            "centres",
            "outstanding-students"
            // add "login", "signup" here if you add those pages
        };

        public WebsiteController(CenterContext dbContext)
        {
            _dbContext = dbContext;
        }

        [Route("")]
        [Route("{page}")]
        public IActionResult Page(string? page = "home")
        {
            if (string.IsNullOrWhiteSpace(page))
                page = "home";

            page = page.ToLowerInvariant();

            bool isArabic = false;
            string basePage = page;

            // If the requested segment ends with -ar, it's Arabic
            if (page.EndsWith("-ar", StringComparison.Ordinal))
            {
                isArabic = true;
                basePage = page[..^3]; // remove the last 3 chars "-ar"
            }

            if (!AllowedPages.Contains(basePage))
                return NotFound(); // 404 if it's not one of the known slugs

            // Serve the appropriate view
            var viewName = isArabic ? $"{basePage}-ar" : basePage;

            // === Domain/RootCode logic ===
            string domain = Request.Host.Host.ToLower();

            // Look up the root record by domain (case-insensitive)
            int? rootCode = _dbContext.Roots
                .AsNoTracking()
                .Where(r => r.RootDomain.ToLower() == domain)
                .Select(r => (int?)r.RootCode)
                .FirstOrDefault();

            // Pass the rootCode to the view via ViewBag (or ViewData)
            ViewBag.RegisterUrl = !rootCode.HasValue ? "/register" : $"/register/{rootCode.Value}";

            // This will look for: /Views/Website/{viewName}.cshtml
            return View(viewName);
        }

        private int? GetRootCodeForDomain(string domain)
        {
            return _dbContext.Roots
                .AsNoTracking()
                .Where(r => r.RootDomain.ToLower() == domain.ToLower())
                .Select(r => (int?)r.RootCode)
                .FirstOrDefault();
        }
    }
}