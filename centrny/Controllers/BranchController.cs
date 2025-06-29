using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;

namespace centrny.Controllers
{
    [Authorize]
    public class BranchController : Controller
    {
        private readonly CenterContext _context;

        public BranchController(CenterContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);

            if (user == null)
                return Unauthorized();

            int groupCode = user.GroupCode;

            // Now fetch the group using groupCode to get the root code
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null)
                return Unauthorized();

            int rootCode = group.RootCode;

            // For rootCode != 1, get center name for this root
            string centerName = null;
            if (rootCode != 1)
            {
                var center = await _context.Centers.FirstOrDefaultAsync(c => c.RootCode == rootCode);
                centerName = center?.CenterName ?? "Unknown Center";
            }

            ViewBag.UserRootCode = rootCode;
            ViewBag.UserGroupCode = groupCode;
            ViewBag.CurrentUserName = user.Username; // adjust as per your user model
            ViewBag.CenterName = centerName;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetRootsIsCenterTrue()
        {
            var roots = await _context.Roots
                .Where(r => r.IsCenter)
                .Select(r => new { r.RootCode, r.RootName })
                .ToListAsync();

            return Json(roots);
        }

        [HttpGet]
        public async Task<IActionResult> GetCentersByRoot(int rootCode)
        {
            var centers = await _context.Centers
                .Where(c => c.RootCode == rootCode)
                .Select(c => new { c.CenterCode, c.CenterName })
                .ToListAsync();

            return Json(centers);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByRootCode(int rootCode)
        {
            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode)
                .Select(b => new { b.BranchCode, b.BranchName, b.RootCode })
                .ToListAsync();

            return Json(branches);
        }

        [HttpGet]
        public async Task<IActionResult> GetHallsByBranch(int branchCode)
        {
            var halls = await _context.Halls
                .Where(h => h.BranchCode == branchCode)
                .Select(h => new { h.HallCode, h.HallName, h.HallCapacity })
                .ToListAsync();

            return Json(halls);
        }

        [HttpPost]
        public async Task<IActionResult> AddHall([FromBody] HallCreateDto dto)
        {
            if (dto == null)
                return BadRequest();

            var hall = new Hall
            {
                HallName = dto.HallName,
                HallCapacity = dto.HallCapacity,
                RootCode = dto.RootCode,
                BranchCode = dto.BranchCode,
                InsertUser = GetCurrentUserId(),
                InsertTime = DateTime.Now
            };

            _context.Halls.Add(hall);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> EditHall([FromBody] EditHallDto dto)
        {
            if (dto == null)
                return BadRequest();

            var hall = await _context.Halls.FindAsync(dto.HallCode);
            if (hall == null) return NotFound();

            hall.HallName = dto.HallName;
            hall.HallCapacity = dto.HallCapacity;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteHall(int hallCode)
        {
            var hall = await _context.Halls.FindAsync(hallCode);
            if (hall == null) return NotFound();

            _context.Halls.Remove(hall);
            await _context.SaveChangesAsync();
            return Ok();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }
    }

    public class HallCreateDto
    {
        public string HallName { get; set; }
        public int HallCapacity { get; set; }
        public int RootCode { get; set; }
        public int BranchCode { get; set; }
    }

    public class EditHallDto
    {
        public int HallCode { get; set; }
        public string HallName { get; set; }
        public int HallCapacity { get; set; }
    }
}