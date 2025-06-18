using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.Authorization; // Commented out for now
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using centrny.Attributes;

namespace centrny.Controllers
{
    // [Authorize] // Commented out for testing - REMOVE THIS COMMENT IN PRODUCTION!    
    [RequirePageAccess("Admin")]
    public class AdminController : Controller
    {
        private readonly CenterContext _context;

        public AdminController(CenterContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var groups = await _context.Groups
                .Include(g => g.RootCodeNavigation)
                .Include(g => g.Users)
                .Include(g => g.GroupPages)
                .Where(g => g.RootCodeNavigation.IsActive)
                .OrderBy(g => g.GroupName)
                .ToListAsync();

            return View(groups);
        }

        public async Task<IActionResult> ManageGroupPermissions(int groupCode)
        {
            var group = await _context.Groups
                .Include(g => g.RootCodeNavigation)
                .Include(g => g.GroupPages)
                .ThenInclude(gp => gp.PageCodeNavigation)
                .ThenInclude(p => p.ModuleCodeNavigation)
                .FirstOrDefaultAsync(g => g.GroupCode == groupCode);

            if (group == null)
            {
                TempData["Error"] = "Group not found.";
                return RedirectToAction(nameof(Index));
            }

            // Get all pages available to this group's root (not just assigned ones)
            var allAvailablePages = await _context.Pages
                .Include(p => p.ModuleCodeNavigation)
                .Where(p => _context.RootModules
                    .Any(rm => rm.RootCode == group.RootCode && rm.ModuleCode == p.ModuleCode))
                .OrderBy(p => p.ModuleCodeNavigation.ModuleName)
                .ThenBy(p => p.PageSort)
                .ThenBy(p => p.PageName)
                .ToListAsync();

            // Create a new group object with all available pages and their current permissions
            var groupWithAllPages = new Group
            {
                GroupCode = group.GroupCode,
                GroupName = group.GroupName,
                GroupDesc = group.GroupDesc,
                RootCode = group.RootCode,
                RootCodeNavigation = group.RootCodeNavigation,
                GroupPages = allAvailablePages.Select(page =>
                {
                    var existingPermission = group.GroupPages
                        .FirstOrDefault(gp => gp.PageCode == page.PageCode);

                    return new GroupPage
                    {
                        GroupCode = group.GroupCode,
                        PageCode = page.PageCode,
                        InsertFlag = existingPermission?.InsertFlag ?? false,
                        UpdateFlag = existingPermission?.UpdateFlag ?? false,
                        DeleteFlag = existingPermission?.DeleteFlag ?? false,
                        PageCodeNavigation = page,
                        GroupCodeNavigation = group
                    };
                }).ToList()
            };

            return View(groupWithAllPages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePageAccess("Admin", "update")]
        public async Task<IActionResult> SaveGroupPermissions(int groupCode)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get form data for permissions
                var pagePermissions = new Dictionary<int, (bool insert, bool update, bool delete)>();

                // Parse form data to extract permission settings
                foreach (var key in Request.Form.Keys)
                {
                    if (key.StartsWith("access_"))
                    {
                        // Extract page code from key like "access_123"
                        if (int.TryParse(key.Replace("access_", ""), out int pageCode))
                        {
                            bool hasAccess = Request.Form[key].ToString().Contains("true");

                            if (hasAccess)
                            {
                                bool insertFlag = Request.Form[$"insert_{pageCode}"].ToString().Contains("true");
                                bool updateFlag = Request.Form[$"update_{pageCode}"].ToString().Contains("true");
                                bool deleteFlag = Request.Form[$"delete_{pageCode}"].ToString().Contains("true");

                                pagePermissions[pageCode] = (insertFlag, updateFlag, deleteFlag);
                            }
                        }
                    }
                }

                // Remove existing permissions
                var existingPermissions = await _context.GroupPages
                    .Where(gp => gp.GroupCode == groupCode)
                    .ToListAsync();

                _context.GroupPages.RemoveRange(existingPermissions);

                // Add new permissions
                var newPermissions = pagePermissions
                    .Where(kvp => kvp.Value.insert || kvp.Value.update || kvp.Value.delete)
                    .Select(kvp => new GroupPage
                    {
                        GroupCode = groupCode,
                        PageCode = kvp.Key,
                        InsertFlag = kvp.Value.insert,
                        UpdateFlag = kvp.Value.update,
                        DeleteFlag = kvp.Value.delete
                    }).ToList();

                await _context.GroupPages.AddRangeAsync(newPermissions);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Page permissions updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "An error occurred while updating permissions.";
                return RedirectToAction(nameof(ManageGroupPermissions), new { groupCode });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupPermissions(int groupCode)
        {
            var group = await _context.Groups
                .Include(g => g.RootCodeNavigation)
                .Include(g => g.GroupPages)
                .ThenInclude(gp => gp.PageCodeNavigation)
                .ThenInclude(p => p.ModuleCodeNavigation)
                .FirstOrDefaultAsync(g => g.GroupCode == groupCode);

            if (group == null)
            {
                return NotFound();
            }

            return Json(group);
        }
    }
}