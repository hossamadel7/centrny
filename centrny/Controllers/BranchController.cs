using System;
using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace centrny.Controllers
{
    public class BranchController : Controller
    {
        private readonly CenterContext _context;

        public BranchController(CenterContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Get branches by center/teacher (based on RootCode)
        [HttpGet]
        public async Task<IActionResult> GetBranchesByRootCode(int rootCode)
        {
            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode)
                .Include(b => b.CenterCodeNavigation)
                .Select(b => new
                {
                    b.BranchCode,
                    b.BranchName,
                    b.Address,
                    b.Phone,
                    StartTime = b.StartTime.ToString("yyyy-MM-dd"),
                    CenterName = b.CenterCodeNavigation.CenterName,
                    b.CenterCode,
                    b.IsActive
                })
                .ToListAsync();

            return Json(branches);
        }

        
        [HttpGet]
        public async Task<IActionResult> GetBranch(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            return Json(new
            {
                branch.BranchCode,
                branch.BranchName,
                branch.Address,
                branch.Phone,
                StartTime = branch.StartTime.ToString("yyyy-MM-dd"),
                branch.CenterCode,
                branch.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Branch branch)
        {
            ModelState.Remove("RootCodeNavigation");
            ModelState.Remove("CenterCodeNavigation");
            ModelState.Remove("InsertUserNavigation");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            branch.InsertTime = DateTime.Now;
            branch.InsertUser = 1;
            branch.RootCode = 1;

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] Branch branch)
        {
            ModelState.Remove("RootCodeNavigation");
            ModelState.Remove("CenterCodeNavigation");
            ModelState.Remove("InsertUserNavigation");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _context.Branches.FindAsync(branch.BranchCode);
            if (existing == null)
                return NotFound();

            existing.BranchName = branch.BranchName;
            existing.Address = branch.Address;
            existing.Phone = branch.Phone;
            existing.StartTime = branch.StartTime;
            existing.CenterCode = branch.CenterCode;
            existing.IsActive = branch.IsActive;
            existing.LastUpdateTime = DateTime.Now;
            existing.LastUpdateUser = 1;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
                return Json(new { success = false });

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
