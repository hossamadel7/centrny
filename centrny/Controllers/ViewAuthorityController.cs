using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Security;

namespace centrny.Controllers
{
    public class ViewAuthorityController : Controller
    {
        private CenterContext db = new CenterContext();

        public IActionResult Index()
        {
            var roots = db.Roots.ToList();
            return View(roots);
        }

        // Get groups for root
        [HttpGet]
        public JsonResult GetGroupsForRoot(int rootId)
        {
            var groups = db.Groups
                .Where(g => g.RootCode == rootId)
                .Select(g => new { g.GroupCode, g.GroupName })
                .ToList();
            return Json(groups);
        }

        // Get pages for modules assigned to this root
        [HttpGet]
        public JsonResult GetPagesForRoot(int rootId)
        {
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

        // Save permission (group & page with flags)
        [HttpPost]
        public JsonResult SaveGroupPagePermission([FromBody] GroupPage dto)
        {
            // You may want to check for duplicates before adding
            var perm = new GroupPage
            {
                GroupCode = dto.GroupCode,
                PageCode = dto.PageCode,
                InsertFlag = dto.InsertFlag,
                UpdateFlag = dto.UpdateFlag,
                DeleteFlag = dto.DeleteFlag
            };

            db.GroupPages.Add(perm);
            db.SaveChanges();

            return Json(new { success = true });
        }

    }
}