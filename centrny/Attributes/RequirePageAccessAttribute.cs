using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

namespace centrny.Attributes
{
    public class RequirePageAccessAttribute : ActionFilterAttribute, IAsyncActionFilter
    {
        private readonly string _pagePath;
        private readonly string _permission;
        private readonly bool _allowAnonymous;

        // Primary constructor - just requires page path
        public RequirePageAccessAttribute(string pagePath)
            : this(pagePath, "view", false)
        {
        }

        // Secondary constructor for specific permissions
        public RequirePageAccessAttribute(string pagePath, string permission)
            : this(pagePath, permission, false)
        {
        }

        // Full constructor with all options
        public RequirePageAccessAttribute(string pagePath, string permission, bool allowAnonymous)
        {
            _pagePath = pagePath ?? throw new ArgumentNullException(nameof(pagePath));
            _permission = permission?.ToLower() ?? "view";
            _allowAnonymous = allowAnonymous;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<CenterContext>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequirePageAccessAttribute>>();

            logger.LogInformation("=== PAGE ACCESS CHECK ===");
            logger.LogInformation("Checking access for: Page='{PagePath}', Permission='{Permission}', User='{Username}'",
                _pagePath, _permission, context.HttpContext.User.Identity?.Name ?? "Anonymous");

            // Check authentication
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                logger.LogWarning("User not authenticated - redirecting to login");
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("AccessDenied", "Login", new { returnUrl = returnUrl });
                return;
            }

            try
            {
                // === DOMAIN-SPECIFIC AUTHENTICATION CHECK ===
                var domainValidationResult = await ValidateDomainSpecificAuth(context.HttpContext, dbContext, logger);
                if (domainValidationResult != null)
                {
                    context.Result = domainValidationResult;
                    return;
                }

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

        /// <summary>
        /// Validates domain-specific authentication to ensure users can only access their domain's content
        /// </summary>
        private async Task<IActionResult> ValidateDomainSpecificAuth(HttpContext httpContext, CenterContext dbContext, ILogger logger)
        {
            logger.LogInformation("=== DOMAIN-SPECIFIC AUTH CHECK ===");

            var host = httpContext.Request.Host.Host;

            // ALWAYS resolve current domain's RootCode (don't trust session)
            var currentDomainRootCode = await ResolveDomainRootCode(host, dbContext, logger);

            if (!currentDomainRootCode.HasValue)
            {
                logger.LogError("Could not resolve domain root code for host: {Host}", host);
                return new RedirectToActionResult("Index", "Login", new { error = "domain_not_recognized" });
            }

            logger.LogInformation("Current domain '{Host}' maps to RootCode: {RootCode}", host, currentDomainRootCode.Value);

            // Get user's root code from claims (from login)
            var userRootCodeClaim = httpContext.User.FindFirst("RootCode");
            if (userRootCodeClaim == null || !int.TryParse(userRootCodeClaim.Value, out int userRootCode))
            {
                logger.LogError("User '{Username}' missing or invalid RootCode claim", httpContext.User.Identity?.Name);
                return new RedirectToActionResult("Index", "Login", null);
            }

            // Get session root code (from login)
            var sessionRootCode = httpContext.Session.GetInt32("RootCode");
            if (!sessionRootCode.HasValue)
            {
                logger.LogError("No RootCode found in session for user '{Username}'", httpContext.User.Identity?.Name);
                return new RedirectToActionResult("Index", "Login", null);
            }

            // SECURITY CHECK: All three must match
            if (userRootCode != currentDomainRootCode.Value || sessionRootCode.Value != currentDomainRootCode.Value)
            {
                logger.LogWarning("SECURITY VIOLATION - Domain mismatch detected! Current domain: {Host}={CurrentDomainRoot}, User claim: {UserRoot}, Session: {SessionRoot}, User: '{Username}'",
                    host, currentDomainRootCode.Value, userRootCode, sessionRootCode.Value, httpContext.User.Identity?.Name);

                // Clear session and sign out user immediately
                await httpContext.SignOutAsync();
                httpContext.Session.Clear();

                return new RedirectToActionResult("Index", "Login", new { error = "access_denied_wrong_domain" });
            }

            // Update session with current domain for consistency (optional)
            httpContext.Session.SetInt32("DomainRootCode", currentDomainRootCode.Value);

            logger.LogInformation("Domain authentication validated successfully for user '{Username}'", httpContext.User.Identity?.Name);
            return null; // Validation passed
        }

        /// <summary>
        /// Resolves domain to root code mapping using database lookup
        /// </summary>
        private async Task<int?> ResolveDomainRootCode(string host, CenterContext dbContext, ILogger logger)
        {
            // Use database lookup for domain mapping
            var root = await dbContext.Roots
                .FirstOrDefaultAsync(r => r.RootDomain != null && r.RootDomain.ToLower() == host.ToLower() && r.IsActive);

            if (root != null)
            {
                logger.LogInformation("Domain '{Host}' resolved to RootCode: {RootCode}", host, root.RootCode);
                return root.RootCode;
            }

            // Fallback for development environments
            //if (host == "localhost" || host == "127.0.0.1")
            //{
            //    var devRoot = await dbContext.Roots.FirstOrDefaultAsync(r => r.IsActive);
            //    if (devRoot != null)
            //    {
            //        logger.LogInformation("Development domain '{Host}' using RootCode: {RootCode}", host, devRoot.RootCode);
            //        return devRoot.RootCode;
            //    }
            //}

            logger.LogError("No active root found for domain: {Host}", host);
            return null;
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
                page = await context.Pages
                    .FirstOrDefaultAsync(p =>
                        p.PagePath.ToLower().StartsWith(pagePath.ToLower() + "/") ||
                        p.PagePath.ToLower().EndsWith("/" + pagePath.ToLower()));

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
                foreach (var p in allPages.Take(20))
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