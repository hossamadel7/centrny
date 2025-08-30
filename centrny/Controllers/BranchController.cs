using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Http;

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

        // Helper to get session values
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);

        public async Task<IActionResult> Index()
        {
            var userCode = GetSessionInt("UserCode");
            var groupCode = GetSessionInt("GroupCode");
            var rootCode = GetSessionInt("RootCode");
            var username = GetSessionString("Username");
            var isCenterStr = GetSessionString("RootIsCenter");
            var centerName = GetSessionString("CenterName");

            if (userCode == null || groupCode == null || rootCode == null)
                return Unauthorized();

            bool isCenter = isCenterStr == "True";

            ViewBag.UserRootCode = rootCode.Value;
            ViewBag.UserGroupCode = groupCode.Value;
            ViewBag.CurrentUserName = username;
            ViewBag.CenterName = centerName ?? "Unknown Center";
            ViewBag.IsCenter = isCenter;
            ViewBag.UserCode = userCode.Value;

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
        public async Task<IActionResult> GetCentersByRoot(int? rootCode = null)
        {
            // Use session rootCode if not provided
            if (rootCode == null)
                rootCode = GetSessionInt("RootCode");
            if (rootCode == null)
                return Unauthorized();

            var centers = await _context.Centers
                .Where(c => c.RootCode == rootCode.Value)
                .Select(c => new { c.CenterCode, c.CenterName, c.CenterPhone, c.CenterAddress, c.OwnerName, c.IsActive })
                .ToListAsync();

            return Json(centers);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByRootCode(int? rootCode = null)
        {
            if (rootCode == null)
                rootCode = GetSessionInt("RootCode");
            if (rootCode == null)
                return Unauthorized();

            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode.Value)
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

            dto.InsertUser = GetSessionInt("UserCode") ?? 0;
            dto.InsertTime = DateTime.Now;

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

            dto.InsertUser = GetSessionInt("UserCode") ?? 0;
            dto.InsertTime = DateTime.Now;
            dto.RootCode = GetSessionInt("RootCode") ?? dto.RootCode;

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
                RootCode = GetSessionInt("RootCode") ?? dto.RootCode,
                BranchCode = dto.BranchCode,
                InsertUser = GetSessionInt("UserCode") ?? 0,
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
    }

    // DTOs remain unchanged
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
        public string? OwnerName { get; set; }
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