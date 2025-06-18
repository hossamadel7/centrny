//using centrny.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Filters;
//using Microsoft.EntityFrameworkCore;

//namespace centrny.Filters
//{
//    public class PageAuthorizationFilter : IAsyncAuthorizationFilter
//    {
//        private readonly CenterContext _context;
//        private readonly ILogger<PageAuthorizationFilter> _logger;

//        public PageAuthorizationFilter(CenterContext context, ILogger<PageAuthorizationFilter> logger)
//        {
//            _context = context;
//            _logger = logger;
//        }

//        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
//        {
//            // Skip authorization if AllowAnonymous is present
//            if (context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute))
//            {
//                return;
//            }

//            var user = context.HttpContext.User;

//            // If not authenticated, redirect to login
//            if (!user.Identity.IsAuthenticated)
//            {
//                context.Result = new RedirectToActionResult("Index", "Login", new
//                {
//                    returnUrl = context.HttpContext.Request.Path
//                });
//                return;
//            }

//            // Get current page path
//            var controller = context.RouteData.Values["controller"]?.ToString();
//            var action = context.RouteData.Values["action"]?.ToString();
//            var currentPath = $"/{controller}/{action}";

//            // Skip authorization for login-related pages
//            if (controller?.ToLower() == "login" ||
//                controller?.ToLower() == "home" ||
//                currentPath.ToLower().Contains("/login/"))
//            {
//                return;
//            }

//            try
//            {
//                // Get user's group code
//                var groupCodeClaim = user.FindFirst("GroupCode");
//                if (groupCodeClaim == null || !int.TryParse(groupCodeClaim.Value, out int groupCode))
//                {
//                    _logger.LogWarning("User {Username} missing GroupCode claim", user.Identity.Name);
//                    context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
//                    return;
//                }

//                // Check if user's group has access to current page
//                var hasAccess = await CheckPageAccess(groupCode, currentPath);

//                if (!hasAccess)
//                {
//                    _logger.LogWarning("User {Username} (Group: {GroupCode}) denied access to {Path}",
//                        user.Identity.Name, groupCode, currentPath);

//                    context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
//                    return;
//                }

//                _logger.LogDebug("User {Username} granted access to {Path}", user.Identity.Name, currentPath);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error checking page authorization for user {Username} on path {Path}",
//                    user.Identity.Name, currentPath);

//                context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
//            }
//        }

//        private async Task<bool> CheckPageAccess(int groupCode, string pagePath)
//        {
//            // Normalize page path for comparison
//            var normalizedPath = pagePath.TrimStart('/').ToLower();

//            // Check if group has access to this page
//            var hasAccess = await _context.GroupPages
//                .Include(gp => gp.PageCodeNavigation)
//                .AnyAsync(gp => gp.GroupCode == groupCode &&
//                               (gp.InsertFlag || gp.UpdateFlag || gp.DeleteFlag) &&
//                               (gp.PageCodeNavigation.PagePath.ToLower().Contains(normalizedPath) ||
//                                normalizedPath.Contains(gp.PageCodeNavigation.PagePath.ToLower())));

//            return hasAccess;
//        }
//    }

//    // Simpler attribute-based authorization for specific pages
//    public class RequirePageAccessAttribute : AuthorizeAttribute, IAuthorizationFilter
//    {
//        private readonly string _pageName;

//        public RequirePageAccessAttribute(string pageName)
//        {
//            _pageName = pageName;
//        }

//        public void OnAuthorization(AuthorizationFilterContext context)
//        {
//            var user = context.HttpContext.User;

//            if (!user.Identity.IsAuthenticated)
//            {
//                context.Result = new RedirectToActionResult("Index", "Login", null);
//                return;
//            }

//            // Check if user has access to the specific page
//            var hasAccess = user.HasClaim("PageAccess", _pageName);

//            if (!hasAccess)
//            {
//                context.Result = new RedirectToActionResult("AccessDenied", "Login", null);
//            }
//        }
//    }

//    // Extension methods for easier use
//    public static class UserExtensions
//    {
//        public static int GetGroupCode(this System.Security.Claims.ClaimsPrincipal user)
//        {
//            var groupCodeClaim = user.FindFirst("GroupCode");
//            return groupCodeClaim != null && int.TryParse(groupCodeClaim.Value, out int groupCode) ? groupCode : 0;
//        }

//        public static string GetFullName(this System.Security.Claims.ClaimsPrincipal user)
//        {
//            return user.FindFirst("FullName")?.Value ?? user.Identity.Name;
//        }

//        public static string GetGroupName(this System.Security.Claims.ClaimsPrincipal user)
//        {
//            return user.FindFirst("GroupName")?.Value ?? "";
//        }

//        public static bool HasPageAccess(this System.Security.Claims.ClaimsPrincipal user, string pageName)
//        {
//            return user.HasClaim("PageAccess", pageName);
//        }

//        public static bool CanInsert(this System.Security.Claims.ClaimsPrincipal user, string pageName)
//        {
//            return user.HasClaim("PageInsert", pageName);
//        }

//        public static bool CanUpdate(this System.Security.Claims.ClaimsPrincipal user, string pageName)
//        {
//            return user.HasClaim("PageUpdate", pageName);
//        }

//        public static bool CanDelete(this System.Security.Claims.ClaimsPrincipal user, string pageName)
//        {
//            return user.HasClaim("PageDelete", pageName);
//        }
//    }
//}