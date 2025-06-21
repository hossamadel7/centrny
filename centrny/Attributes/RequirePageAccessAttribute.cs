using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace centrny.Attributes
{
    public class RequirePageAccessAttribute : ActionFilterAttribute, IAsyncActionFilter
    {
        private readonly string _pagePath;
        private readonly string _permission; // "view", "insert", "update", "delete"

        public RequirePageAccessAttribute(string pagePath, string permission = "view")
        {
            _pagePath = pagePath;
            _permission = permission.ToLower();
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<CenterContext>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequirePageAccessAttribute>>();

            logger.LogInformation("=== PAGE ACCESS CHECK ===");
            logger.LogInformation("Checking access for: Page='{PagePath}', Permission='{Permission}', User='{Username}'",
                _pagePath, _permission, context.HttpContext.User.Identity?.Name ?? "Anonymous");

            // Skip if not authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                logger.LogWarning("User not authenticated - redirecting to login");
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Index", "Login", new { returnUrl = returnUrl });
                return;
            }

            try
            {
                // Get user's group code
                var groupCodeClaim = context.HttpContext.User.FindFirst("GroupCode");
                if (groupCodeClaim == null || !int.TryParse(groupCodeClaim.Value, out int groupCode))
                {
                    logger.LogError("User '{Username}' missing or invalid GroupCode claim", context.HttpContext.User.Identity.Name);
                    context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
                    return;
                }

                logger.LogInformation("User GroupCode: {GroupCode}", groupCode);

                // Check page access with PRECISE matching
                var hasAccess = await CheckPageAccessPrecise(dbContext, groupCode, _pagePath, _permission, logger);

                if (!hasAccess)
                {
                    logger.LogWarning("Access DENIED for user '{Username}' (Group: {GroupCode}) to page '{PagePath}' with permission '{Permission}'",
                        context.HttpContext.User.Identity.Name, groupCode, _pagePath, _permission);
                    context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
                    return;
                }

                logger.LogInformation("Access GRANTED for user '{Username}' to page '{PagePath}' with permission '{Permission}'",
                    context.HttpContext.User.Identity.Name, _pagePath, _permission);

                await next();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ERROR in RequirePageAccessAttribute for user '{Username}' accessing page '{PagePath}'",
                    context.HttpContext.User.Identity?.Name ?? "Anonymous", _pagePath);
                context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
            }
        }

        private async Task<bool> CheckPageAccessPrecise(CenterContext context, int groupCode, string pagePath, string permission, ILogger logger)
        {
            logger.LogInformation("=== PRECISE PAGE LOOKUP ===");

            // STEP 1: Try EXACT matches first (most restrictive)
            var page = await context.Pages
                .FirstOrDefaultAsync(p =>
                    p.PagePath.ToLower() == pagePath.ToLower() ||
                    p.PageName.ToLower() == pagePath.ToLower());

            if (page != null)
            {
                logger.LogInformation("Found page by EXACT match: PageCode={PageCode}, PageName='{PageName}', PagePath='{PagePath}'",
                    page.PageCode, page.PageName, page.PagePath);
            }
            else
            {
                // STEP 2: Try partial matches ONLY if exact match fails
                // But be more restrictive - only match if the pagePath is a SUBSTRING of existing paths
                page = await context.Pages
                    .FirstOrDefaultAsync(p =>
                        p.PagePath.ToLower().StartsWith(pagePath.ToLower() + "/") ||  // e.g., "Exam/Create" matches "Exam"
                        p.PagePath.ToLower().EndsWith("/" + pagePath.ToLower()));     // e.g., "Admin/Exam" matches "Exam"

                if (page != null)
                {
                    logger.LogInformation("Found page by PARTIAL match: PageCode={PageCode}, PageName='{PageName}', PagePath='{PagePath}'",
                        page.PageCode, page.PageName, page.PagePath);
                }
            }

            // STEP 3: If still no match, LOG ALL AVAILABLE PAGES for debugging
            if (page == null)
            {
                var allPages = await context.Pages
                    .Select(p => new { p.PageCode, p.PageName, p.PagePath })
                    .OrderBy(p => p.PageName)
                    .ToListAsync();

                logger.LogError("Page NOT FOUND for path: '{PagePath}'. Available pages:", pagePath);
                foreach (var p in allPages.Take(20)) // Log first 20 for debugging
                {
                    logger.LogError("  - PageCode={PageCode}, Name='{PageName}', Path='{PagePath}'",
                        p.PageCode, p.PageName, p.PagePath);
                }
                if (allPages.Count > 20)
                {
                    logger.LogError("  ... and {Count} more pages", allPages.Count - 20);
                }

                return false;
            }

            // Check group permission for this page
            var groupPage = await context.GroupPages
                .FirstOrDefaultAsync(gp => gp.GroupCode == groupCode && gp.PageCode == page.PageCode);

            if (groupPage == null)
            {
                logger.LogError("No permission record found for GroupCode={GroupCode}, PageCode={PageCode}",
                    groupCode, page.PageCode);
                return false;
            }

            logger.LogInformation("Permission record found: Insert={Insert}, Update={Update}, Delete={Delete}",
                groupPage.InsertFlag, groupPage.UpdateFlag, groupPage.DeleteFlag);

            // Check specific permission
            var hasPermission = permission switch
            {
                "view" => groupPage.InsertFlag || groupPage.UpdateFlag || groupPage.DeleteFlag,
                "insert" => groupPage.InsertFlag,
                "update" => groupPage.UpdateFlag,
                "delete" => groupPage.DeleteFlag,
                _ => false
            };

            logger.LogInformation("Permission check result for '{Permission}': {HasPermission}", permission, hasPermission);
            return hasPermission;
        }
    }
}