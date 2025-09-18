using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using centrny.Attributes;

namespace centrny1.Controllers
{
    [RequirePageAccess("Root")]
 
    public class RootController : Controller
    {
        private readonly CenterContext _context;

        public RootController(CenterContext context)
        {
            _context = context;
        }

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int? userCode, int? groupCode, int? rootCode, string username, bool isCenter) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                GetSessionInt("RootCode"),
                GetSessionString("Username"),
                GetSessionString("RootIsCenter") == "True"
            );
        }

        // Default Index view
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Root/GetActiveRoots
        public async Task<IActionResult> GetActiveRoots(int page = 1, int pageSize = 10, bool? isCenter = null)
        {
            var query = _context.Roots.Where(r => r.IsActive);

            if (isCenter.HasValue)
                query = query.Where(r => r.IsCenter == isCenter.Value);

            var totalCount = await query.CountAsync();

            var roots = await query
                .OrderBy(r => r.RootCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.RootCode,
                    r.RootOwner,
                    r.RootName,
                    r.RootPhone,
                    r.RootEmail,
                    r.RootFees,
                    r.RootAddress,
                    r.NoOfCenter,
                    r.NoOfUser,
                    r.IsCenter
                })
                .ToListAsync();

            return Ok(new
            {
                data = roots,
                totalCount = totalCount
            });
        }

        // POST: /Root/AddRoot
        [HttpPost]
        public async Task<IActionResult> AddRoot([FromBody] Root model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => kvp.Value.Errors.Count > 0)
                        .Select(kvp => new
                        {
                            Field = kvp.Key,
                            Errors = kvp.Value.Errors.Select(e => e.ErrorMessage)
                        });
                    return BadRequest(errors);
                }

                var (userCode, groupCode, rootCode, username, isCenter) = GetSessionContext();
                model.InsertTime = DateTime.UtcNow;
                model.StartTime = DateTime.UtcNow;
                model.InsertUser = userCode ?? 0;

                _context.Roots.Add(model);
                await _context.SaveChangesAsync();

                return Ok(model.RootCode);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal Server Error: " + ex.Message);
            }
        }

        // GET: /Root/GetRoot
        public async Task<IActionResult> GetRoot(int rootCode)
        {
            var root = await _context.Roots.FindAsync(rootCode);
            if (root == null)
                return NotFound();

            return Ok(root);
        }

        // POST: /Root/EditRoot
        [HttpPost]
        public async Task<IActionResult> EditRoot([FromBody] Root updatedRoot)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var root = await _context.Roots.FindAsync(updatedRoot.RootCode);
            if (root == null)
                return NotFound();

            // Update fields
            root.RootOwner = updatedRoot.RootOwner;
            root.RootName = updatedRoot.RootName;
            root.RootPhone = updatedRoot.RootPhone;
            root.RootEmail = updatedRoot.RootEmail;
            root.RootFees = updatedRoot.RootFees;
            root.RootAddress = updatedRoot.RootAddress;
            root.NoOfCenter = updatedRoot.NoOfCenter;
            root.NoOfUser = updatedRoot.NoOfUser;
            root.IsCenter = updatedRoot.IsCenter;

            var (userCode, _, _, _, _) = GetSessionContext();
            root.LastUpdateUser = userCode ?? 0;
            root.LastUpdateTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST: /Root/DeleteRoot
        [HttpPost]
        public async Task<IActionResult> DeleteRoot([FromForm] int id)
        {
            var root = await _context.Roots.FindAsync(id);
            if (root == null)
                return NotFound();

            root.IsActive = false; // Soft delete
            var (userCode, _, _, _, _) = GetSessionContext();
            root.LastUpdateUser = userCode ?? 0;
            root.LastUpdateTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // GET: /Root/GetAssignedModules
        public async Task<IActionResult> GetAssignedModules(int rootCode)
        {
            var modules = await _context.RootModules
                .Where(rm => rm.RootCode == rootCode)
                .Join(_context.Modules,
                    rm => rm.ModuleCode,
                    m => m.ModuleCode,
                    (rm, m) => new { m.ModuleCode, m.ModuleName })
                .ToListAsync();

            return Ok(modules);
        }

        // GET: /Root/GetAvailableModules
        public async Task<IActionResult> GetAvailableModules(int rootCode)
        {
            var assignedModuleCodes = await _context.RootModules
                .Where(rm => rm.RootCode == rootCode)
                .Select(rm => rm.ModuleCode)
                .ToListAsync();

            var availableModules = await _context.Modules
                .Where(m => !assignedModuleCodes.Contains(m.ModuleCode))
                .Select(m => new { m.ModuleCode, m.ModuleName })
                .ToListAsync();

            return Ok(availableModules);
        }

        // POST: /Root/SaveModuleAssignments
        [HttpPost]
        public async Task<IActionResult> SaveModuleAssignments([FromBody] ModuleAssignmentDto dto)
        {
            var (userCode, _, _, _, _) = GetSessionContext();

            var existingAssignments = await _context.RootModules
                .Where(rm => rm.RootCode == dto.RootCode)
                .ToListAsync();
            _context.RootModules.RemoveRange(existingAssignments);

            foreach (var moduleCode in dto.ModuleCodes)
            {
                _context.RootModules.Add(new RootModule
                {
                    RootCode = dto.RootCode,
                    ModuleCode = moduleCode,
                   
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        public class ModuleAssignmentDto
        {
            public int RootCode { get; set; }
            public List<int> ModuleCodes { get; set; }
        }
    }
}