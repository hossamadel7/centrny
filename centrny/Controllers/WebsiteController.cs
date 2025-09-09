using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace centrny.Controllers
{
    // This controller is simplified to directly serve the existing lowercase Arabic view files you listed:
    //
    //   Views/Website/home-ar.cshtml
    //   Views/Website/about-ar.cshtml
    //   Views/Website/courses-ar.cshtml
    //   Views/Website/centres-ar.cshtml
    //   Views/Website/outstanding-students-ar.cshtml
    //
    // Supported URLs (examples):
    //   /                -> home-ar.cshtml
    //   /home            -> home-ar.cshtml
    //   /home-ar         -> home-ar.cshtml
    //   /about           -> about-ar.cshtml
    //   /about-ar        -> about-ar.cshtml
    //   /courses         -> courses-ar.cshtml
    //   /centres         -> centres-ar.cshtml
    //   /outstanding-students     -> outstanding-students-ar.cshtml
    //   /outstanding-students-ar  -> outstanding-students-ar.cshtml
    //
    // If you later add login-ar.cshtml or signup-ar.cshtml, just add "login" / "signup" to the AllowedPages set.

    public class WebsiteController : Controller
    {
        private static readonly HashSet<string> AllowedPages = new(StringComparer.OrdinalIgnoreCase)
        {
            "home",
            "about",
            "courses",
            "centres",
            "outstanding-students"
            // add "login", "signup" here ONLY if you create login-ar.cshtml / signup-ar.cshtml
        };

        [Route("")]
        [Route("{page}")]
        public IActionResult Page(string? page = "home")
        {
            if (string.IsNullOrWhiteSpace(page))
                page = "home";

            page = page.ToLowerInvariant();

            // If the requested segment already ends with -ar, strip the suffix to get the base
            if (page.EndsWith("-ar", StringComparison.Ordinal))
                page = page[..^3]; // remove the last 3 chars "-ar"

            if (!AllowedPages.Contains(page))
                return NotFound(); // 404 if it's not one of the known slugs

            // Our view file names follow: <slug>-ar.cshtml (all lowercase)
            var viewName = $"{page}-ar";

            // This will look for: /Views/Website/{viewName}.cshtml  (e.g. home-ar.cshtml)
            return View(viewName);
        }
    }
}