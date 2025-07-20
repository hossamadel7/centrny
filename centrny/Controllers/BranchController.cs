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
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null)
                return Unauthorized();

            int rootCode = group.RootCode;
            bool isCenter = await _context.Roots.AnyAsync(r => r.RootCode == rootCode && r.IsCenter);

            string centerName = null;
            if (rootCode != 1)
            {
                var center = await _context.Centers.FirstOrDefaultAsync(c => c.RootCode == rootCode);
                centerName = center?.CenterName ?? "Unknown Center";
            }

            ViewBag.UserRootCode = rootCode;
            ViewBag.UserGroupCode = groupCode;
            ViewBag.CurrentUserName = user.Username;
            ViewBag.CenterName = centerName;
            ViewBag.IsCenter = isCenter;
            ViewBag.UserCode = user.UserCode; // <--- ADD THIS LINE

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> GetRootsIsCenterTrue()
        {
            var roots = await _context.Roots
                .Where(r => r.IsCenter)
                .Select(r => new { r.RootCode, r.RootName, r.RootOwner })
                .ToListAsync();

            return Json(roots);
        }

        [HttpGet]
        public async Task<IActionResult> GetRootConfig(int rootCode)
        {
            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == rootCode);
            if (root == null)
                return NotFound();
            return Json(new { rootCode = root.RootCode, no_Of_Center = root.NoOfCenter, isCenter = root.IsCenter, ownerName = root.RootOwner });
        }

        [HttpGet]
        public async Task<IActionResult> GetCentersByRoot(int rootCode)
        {
            var centers = await _context.Centers
                .Where(c => c.RootCode == rootCode)
                .Select(c => new { c.CenterCode, c.CenterName, c.CenterPhone, c.CenterAddress, c.OwnerName, c.IsActive })
                .ToListAsync();

            return Json(centers);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByRootCode(int rootCode)
        {
            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode)
                .Select(b => new { b.BranchCode, b.BranchName, b.Address, b.Phone, b.StartTime, b.CenterCode, b.IsActive })
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

        // --- Add Center ---
        [HttpPost]
        public async Task<IActionResult> AddCenter([FromBody] CenterCreateDto dto)
        {
            if (dto == null) return BadRequest();

            if (string.IsNullOrWhiteSpace(dto.CenterName))
                return BadRequest("Center Name is required.");
            if (string.IsNullOrWhiteSpace(dto.CenterAddress))
                return BadRequest("Center Address is required.");
            if (string.IsNullOrWhiteSpace(dto.CenterPhone))
                return BadRequest("Center Phone is required.");

            // Get OwnerName from root
            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == dto.RootCode);
            if (root == null) return BadRequest("Root not found.");

            var center = new Center
            {
                CenterName = dto.CenterName,
                CenterAddress = dto.CenterAddress,
                CenterPhone = dto.CenterPhone,
                OwnerName = root.RootOwner,
                RootCode = dto.RootCode,
                InsertUser = dto.InsertUser,
                InsertTime = dto.InsertTime,
                IsActive = true
            };

            _context.Centers.Add(center);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- Edit Center ---
        [HttpPut]
        public async Task<IActionResult> EditCenter([FromBody] CenterEditDto dto)
        {
            if (dto == null) return BadRequest();
            var center = await _context.Centers.FindAsync(dto.CenterCode);
            if (center == null) return NotFound();

            center.CenterName = dto.CenterName;
            center.CenterAddress = dto.CenterAddress;
            center.CenterPhone = dto.CenterPhone;
            // OwnerName and RootCode are not changed in edit
            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- Add Branch ---
        [HttpPost]
        public async Task<IActionResult> AddBranch([FromBody] BranchCreateDto dto)
        {
            if (dto == null) return BadRequest();

            if (string.IsNullOrWhiteSpace(dto.BranchName))
                return BadRequest("Branch Name is required.");
            if (string.IsNullOrWhiteSpace(dto.Address))
                return BadRequest("Address is required.");
            if (string.IsNullOrWhiteSpace(dto.Phone))
                return BadRequest("Phone is required.");

            var branch = new Branch
            {
                BranchName = dto.BranchName,
                Address = dto.Address,
                Phone = dto.Phone,
                StartTime = dto.StartTime,
                CenterCode = dto.CenterCode,
                RootCode = dto.RootCode,
                InsertUser = dto.InsertUser,
                InsertTime = dto.InsertTime,
                IsActive = true
            };

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- Edit Branch ---
        [HttpPut]
        public async Task<IActionResult> EditBranch([FromBody] BranchEditDto dto)
        {
            if (dto == null) return BadRequest();
            var branch = await _context.Branches.FindAsync(dto.BranchCode);
            if (branch == null) return NotFound();

            branch.BranchName = dto.BranchName;
            branch.Address = dto.Address;
            branch.Phone = dto.Phone;
            branch.StartTime = dto.StartTime;
            // CenterCode, RootCode, InsertUser, InsertTime, IsActive are not changed in edit
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteCenter(int centerCode)
        {
            var center = await _context.Centers.FindAsync(centerCode);
            if (center == null) return NotFound();

            _context.Centers.Remove(center);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteBranch(int branchCode)
        {
            var branch = await _context.Branches.FindAsync(branchCode);
            if (branch == null) return NotFound();

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            return Ok();
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

    public class CenterCreateDto
    {
        public string CenterName { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public string CenterPhone { get; set; } = null!;
        public string CenterAddress { get; set; } = null!;
        public string? OwnerName { get; set; } // set by server from root
        public int RootCode { get; set; }
        public int InsertUser { get; set; }
        public DateTime InsertTime { get; set; }
    }

    public class CenterEditDto
    {
        public int CenterCode { get; set; }
        public string CenterName { get; set; } = null!;
        public string CenterPhone { get; set; } = null!;
        public string CenterAddress { get; set; } = null!;
    }

    public class BranchCreateDto
    {
        public string BranchName { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateOnly StartTime { get; set; }
        public int CenterCode { get; set; }
        public int InsertUser { get; set; }
        public DateTime InsertTime { get; set; }
        public bool IsActive { get; set; } = true;
        public int RootCode { get; set; }
    }

    public class BranchEditDto
    {
        public int BranchCode { get; set; }
        public string BranchName { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateOnly StartTime { get; set; }
    }
}