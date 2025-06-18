using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using centrny.Models;
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
            // Skip if not authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.Result = new RedirectToActionResult("Index", "Login", new
                {
                    returnUrl = context.HttpContext.Request.Path
                });
                return;
            }

            // Get services
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<CenterContext>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequirePageAccessAttribute>>();

            try
            {
                // Get user's group code
                var groupCodeClaim = context.HttpContext.User.FindFirst("GroupCode");
                if (groupCodeClaim == null || !int.TryParse(groupCodeClaim.Value, out int groupCode))
                {
                    logger.LogWarning("User {Username} missing GroupCode claim", context.HttpContext.User.Identity.Name);
                    context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
                    return;
                }

                // Check page access
                var hasAccess = await CheckPageAccess(dbContext, groupCode, _pagePath, _permission);

                if (!hasAccess)
                {
                    logger.LogWarning("User {Username} (Group: {GroupCode}) denied {Permission} access to {Path}",
                        context.HttpContext.User.Identity.Name, groupCode, _permission, _pagePath);

                    context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
                    return;
                }

                logger.LogDebug("User {Username} granted {Permission} access to {Path}",
                    context.HttpContext.User.Identity.Name, _permission, _pagePath);

                await next();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking page authorization for user {Username} on path {Path}",
                    context.HttpContext.User.Identity.Name, _pagePath);

                context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
            }
        }

        private async Task<bool> CheckPageAccess(CenterContext context, int groupCode, string pagePath, string permission)
        {
            // Find the page by path
            var page = await context.Pages
                .FirstOrDefaultAsync(p => p.PagePath.ToLower().Contains(pagePath.ToLower()) ||
                                         pagePath.ToLower().Contains(p.PagePath.ToLower()));

            if (page == null)
            {
                // If page not found in system, allow access (for non-restricted pages)
                return true;
            }

            // Check group permission for this page
            var groupPage = await context.GroupPages
                .FirstOrDefaultAsync(gp => gp.GroupCode == groupCode && gp.PageCode == page.PageCode);

            if (groupPage == null)
            {
                // No permission record = no access
                return false;
            }

            // Check specific permission
            return permission switch
            {
                "view" => groupPage.InsertFlag || groupPage.UpdateFlag || groupPage.DeleteFlag, // Any permission = can view
                "insert" => groupPage.InsertFlag,
                "update" => groupPage.UpdateFlag,
                "delete" => groupPage.DeleteFlag,
                _ => false
            };
        }
    }
}