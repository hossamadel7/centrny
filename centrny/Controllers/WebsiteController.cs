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

        // Pages that have distinct razor views per language (base + -ar)
        private static readonly HashSet<string> MultiViewPages = new(StringComparer.OrdinalIgnoreCase)
        {
            "home",
            "about",
            "courses",
            "centres",
            "outstanding-students"
        };

        // Pages localized via resources in a single view (no -ar variant file)
        // IMPORTANT: Do NOT include "register" here to avoid route conflicts with StudentController.
        private static readonly HashSet<string> SingleViewLocalizedPages = new(StringComparer.OrdinalIgnoreCase)
        {
            "studentlogin",
            // include "login" here only if it is also a single-view localized page
            "login"
        };

        // Union of allowed base slugs (collection first, then comparer)
        private static readonly HashSet<string> AllowedPages =
            new HashSet<string>(MultiViewPages.Concat(SingleViewLocalizedPages), StringComparer.OrdinalIgnoreCase);

        public WebsiteController(CenterContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Generic page handler (single segment slugs only, e.g., /home, /about, /studentlogin, /home-ar)
        [Route("")]
        [Route("{page}")]
        public IActionResult Page(string? page = "home")
        {
            if (string.IsNullOrWhiteSpace(page))
                page = "home";

            page = page.ToLowerInvariant();

            bool isArabic = false;
            string basePage = page;

            if (page.EndsWith("-ar", StringComparison.Ordinal))
            {
                isArabic = true;
                basePage = page[..^3];
            }

            if (!AllowedPages.Contains(basePage))
                return NotFound();

            string viewName;
            if (SingleViewLocalizedPages.Contains(basePage))
            {
                // Single-view pages: always use the base view name; localization via resources.
                viewName = basePage;

                // If someone hit e.g. /studentlogin-ar, optionally redirect to canonical without -ar.
                if (isArabic)
                {
                    return RedirectPermanent($"/{basePage}");
                }
            }
            else
            {
                // Multi-view pages have separate -ar file
                viewName = isArabic ? $"{basePage}-ar" : basePage;
            }

            // Domain-based RootCode (used to build links, including the register link)
            string domain = Request.Host.Host.ToLower();
            int? rootCode = _dbContext.Roots
                .AsNoTracking()
                .Where(r => r.RootDomain.ToLower() == domain)
                .Select(r => (int?)r.RootCode)
                .FirstOrDefault();

            ViewBag.RegisterUrl = !rootCode.HasValue ? "/register" : $"/register/{rootCode.Value}";
            ViewBag.LanguageToggleUrl = GetLanguageToggleUrl(basePage, isArabic);

            return View(viewName);
        }

        // ===== Canonicalization redirects for register/signup =====
        // NOTE: StudentController.PublicRegister owns /register and /register/{rootCode}.
        // These WebsiteController endpoints ONLY normalize non-canonical variants.

        // Redirect mis-formed Arabic variants to canonical /register
        [HttpGet("register-ar")]
        public IActionResult RegisterAr() => RedirectPermanent("/register");

        [HttpGet("register-ar/{rootCode:int}")]
        public IActionResult RegisterArWithCode(int rootCode) => RedirectPermanent($"/register/{rootCode}");

        // Catch patterns like /register/123-ar and normalize to /register/123 (regex constraint avoids overlap)
        [HttpGet("register/{root:regex(^\\d+-ar$)}")]
        public IActionResult RegisterRootWithSuffix(string root)
        {
            var trimmed = root[..^3]; // remove "-ar"
            if (int.TryParse(trimmed, out var parsed))
                return RedirectPermanent($"/register/{parsed}");
            return NotFound();
        }

        // SIGNUP should behave as an alias to register (no -ar variants)
        [HttpGet("signup")]
        public IActionResult Signup() => RedirectPermanent("/register");

        [HttpGet("signup/{rootCode:int}")]
        public IActionResult SignupWithCode(int rootCode) => RedirectPermanent($"/register/{rootCode}");

        [HttpGet("signup-ar")]
        public IActionResult SignupAr() => RedirectPermanent("/register");

        [HttpGet("signup-ar/{rootCode:int}")]
        public IActionResult SignupArWithCode(int rootCode) => RedirectPermanent($"/register/{rootCode}");

        // Handle /signup/123-ar and normalize
        [HttpGet("signup/{root:regex(^\\d+-ar$)}")]
        public IActionResult SignupRootWithSuffix(string root)
        {
            var trimmed = root[..^3];
            if (int.TryParse(trimmed, out var parsed))
                return RedirectPermanent($"/register/{parsed}");
            return NotFound();
        }

        // Toggle URL builder
        private string GetLanguageToggleUrl(string basePage, bool isArabic)
        {
            // For single-view pages (like studentlogin/login), never add -ar in the path.
            if (SingleViewLocalizedPages.Contains(basePage))
            {
                return $"/{basePage}";
            }

            // For multi-view pages, use -ar suffix convention
            return isArabic ? $"/{basePage}" : $"/{basePage}-ar";
        }

        // (Optional) helper; currently unused elsewhere
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