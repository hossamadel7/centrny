using centrny.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace centrny.Controllers
{
    public class GroupPageMultiDto
    {
        public int GroupCode { get; set; }
        public List<int> PageCodes { get; set; }
        public bool InsertFlag { get; set; }
        public bool UpdateFlag { get; set; }
        public bool DeleteFlag { get; set; }
    }

    [Authorize] // Require authentication for the whole controller
    public class ViewAuthorityController : Controller
    {
        private CenterContext db = new CenterContext();

        // Helper method to check authority permission based on group-page database permission
        private bool UserHasAuthorityPermission()
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return false;

            // Get all group codes for the user
            var userGroupCodes = db.Users
                .Where(ug => ug.UserCode == user.UserCode)
                .Select(ug => ug.GroupCode)
                .ToList();

            // Get the page code for ViewAuthority/Index
            var page = db.Pages.FirstOrDefault(p => p.PagePath == "ViewAuthority/Index");
            if (page == null)
                return false;

            // Check if any of the user's groups has permission for this page
            return db.GroupPages.Any(gp => userGroupCodes.Contains(gp.GroupCode) && gp.PageCode == page.PageCode);
        }

        public IActionResult Index()
        {
            // If user doesn't have permission, show the correct AccessDenied view
            if (!UserHasAuthorityPermission())
            {
                // Option 1: If you moved the view to Views/Account/AccessDenied.cshtml, use RedirectToAction
                // return RedirectToAction("AccessDenied", "Account");

                // Option 2: If you want to use the view from Views/Login/AccessDenied.cshtml:
                return View("~/Views/Login/AccessDenied.cshtml");
            }

            var roots = db.Roots.ToList();
            return View(roots);
        }

        [HttpGet]
        public JsonResult GetGroupsForRoot(int rootId)
        {
            if (!UserHasAuthorityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var groups = db.Groups
                .Where(g => g.RootCode == rootId)
                .Select(g => new { g.GroupCode, g.GroupName })
                .ToList();
            return Json(groups);
        }

        [HttpGet]
        public JsonResult GetPagesForRoot(int rootId)
        {
            if (!UserHasAuthorityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var assignedModuleCodes = db.RootModules
                .Where(rm => rm.RootCode == rootId)
                .Select(rm => rm.ModuleCode)
                .Distinct()
                .ToList();

            var pages = db.Pages
                .Where(p => assignedModuleCodes.Contains(p.ModuleCode))
                .Select(p => new { p.PageCode, p.PageName })
                .ToList();

            return Json(pages);
        }

        [HttpGet]
        public JsonResult GetGroupPagesForRoot(int rootId)
        {
            if (!UserHasAuthorityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var groupPages = db.GroupPages
                .Where(gp => db.Groups.Any(g => g.GroupCode == gp.GroupCode && g.RootCode == rootId))
                .Select(gp => new
                {
                    groupCode = gp.GroupCode,
                    groupName = gp.GroupCodeNavigation.GroupName,
                    pageCode = gp.PageCode,
                    pageName = gp.PageCodeNavigation.PageName,
                    insertFlag = gp.InsertFlag,
                    updateFlag = gp.UpdateFlag,
                    deleteFlag = gp.DeleteFlag
                })
                .ToList();

            return Json(groupPages);
        }

        [HttpGet]
        public JsonResult GetGroupPagesForGroup(int groupCode)
        {
            if (!UserHasAuthorityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var groupPages = db.GroupPages
                .Where(gp => gp.GroupCode == groupCode)
                .Select(gp => new
                {
                    groupCode = gp.GroupCode,
                    groupName = gp.GroupCodeNavigation.GroupName,
                    pageCode = gp.PageCode,
                    pageName = gp.PageCodeNavigation.PageName,
                    insertFlag = gp.InsertFlag,
                    updateFlag = gp.UpdateFlag,
                    deleteFlag = gp.DeleteFlag
                })
                .ToList();

            return Json(groupPages);
        }

        [HttpPost]
        public JsonResult SaveGroupPagePermission([FromBody] GroupPageMultiDto dto)
        {
            if (!UserHasAuthorityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            if (dto == null || dto.PageCodes == null || dto.PageCodes.Count == 0)
                return Json(new { success = false, message = "Invalid data." });

            foreach (var pageCode in dto.PageCodes)
            {
                var existing = db.GroupPages.FirstOrDefault(gp => gp.GroupCode == dto.GroupCode && gp.PageCode == pageCode);
                if (existing == null)
                {
                    existing = new GroupPage
                    {
                        GroupCode = dto.GroupCode,
                        PageCode = pageCode
                    };
                    db.GroupPages.Add(existing);
                }
                existing.InsertFlag = dto.InsertFlag;
                existing.UpdateFlag = dto.UpdateFlag;
                existing.DeleteFlag = dto.DeleteFlag;
            }
            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult DeleteGroupPage([FromBody] GroupPageMultiDto dto)
        {
            if (!UserHasAuthorityPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            if (dto == null || dto.PageCodes == null || dto.PageCodes.Count == 0)
                return Json(new { success = false, message = "Invalid data." });

            bool changed = false;
            foreach (var pageCode in dto.PageCodes)
            {
                var existing = db.GroupPages.FirstOrDefault(gp => gp.GroupCode == dto.GroupCode && gp.PageCode == pageCode);
                if (existing != null)
                {
                    db.GroupPages.Remove(existing);
                    changed = true;
                }
            }
            if (changed) db.SaveChanges();
            return Json(new { success = changed });
        }
    }
}