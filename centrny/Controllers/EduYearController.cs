using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace centrny.Controllers
{
    [Authorize]
    public class EduYearController : Controller
    {
        private readonly CenterContext _context;

        public EduYearController(CenterContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetEduYears()
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

            var eduYears = await _context.EduYears
                .Where(e => e.RootCode == rootCode)
                .Select(e => new
                {
                    eduCode = e.EduCode,
                    eduName = e.EduName,
                    isActive = e.IsActive
                })
                .ToListAsync();

            return Json(eduYears);
        }

        [HttpGet]
        public async Task<IActionResult> GetLevelsAndYearsForActiveEduYear()
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

            var activeEduYear = await _context.EduYears
                .Where(e => e.RootCode == rootCode && e.IsActive)
                .FirstOrDefaultAsync();

            if (activeEduYear == null)
                return Json(new { activeEduYear = (object)null, levels = new List<object>() });

            var levels = await _context.Levels
                .Where(l => l.RootCode == rootCode)
                .OrderBy(l => l.LevelCode)
                .Select(l => new
                {
                    levelCode = l.LevelCode,
                    levelName = l.LevelName,
                })
                .ToListAsync();

            var years = await _context.Years
                .Where(y => y.EduYearCode == activeEduYear.EduCode)
                .Select(y => new
                {
                    yearCode = y.YearCode,
                    yearName = y.YearName,
                    yearSort = y.YearSort,
                    levelCode = y.LevelCode
                })
                .ToListAsync();

            // Group years by level code
            var levelsWithYears = levels.Select(l => new
            {
                levelCode = l.levelCode,
                levelName = l.levelName,
                years = years.Where(y => y.levelCode == l.levelCode)
                    .OrderBy(y => y.yearSort)
                    .ToList()
            }).ToList();

            return Json(new
            {
                activeEduYear = new
                {
                    eduCode = activeEduYear.EduCode,
                    eduName = activeEduYear.EduName
                },
                levels = levelsWithYears
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddEduYear([FromBody] AddEduYearDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.EduName))
                return BadRequest("Invalid data.");

            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            // Ensure only one active EduYear per root
            if (dto.IsActive)
            {
                var activeExists = await _context.EduYears.AnyAsync(e => e.RootCode == rootCode && e.IsActive);
                if (activeExists)
                    return BadRequest("There is already an active Edu Year for this root. Only one can be active.");
            }

            var eduYear = new EduYear
            {
                EduName = dto.EduName,
                IsActive = dto.IsActive,
                RootCode = rootCode
            };

            _context.EduYears.Add(eduYear);
            await _context.SaveChangesAsync();

            return Json(new
            {
                eduCode = eduYear.EduCode,
                eduName = eduYear.EduName,
                isActive = eduYear.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditEduYear([FromBody] EditEduYearDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.EduName))
                return BadRequest("Invalid data.");
            var eduYear = await _context.EduYears.FirstOrDefaultAsync(e => e.EduCode == dto.EduCode);
            if (eduYear == null)
                return NotFound("EduYear not found.");

            // Only check if setting to active
            if (dto.IsActive && !eduYear.IsActive)
            {
                var activeExists = await _context.EduYears
                    .AnyAsync(e => e.RootCode == eduYear.RootCode && e.IsActive && e.EduCode != eduYear.EduCode);
                if (activeExists)
                    return BadRequest("There is already an active Edu Year for this root. Only one can be active.");
            }

            eduYear.EduName = dto.EduName;
            eduYear.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Json(new
            {
                eduCode = eduYear.EduCode,
                eduName = eduYear.EduName,
                isActive = eduYear.IsActive
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEduYear([FromBody] DeleteEduYearDto dto)
        {
            if (dto == null || dto.EduCode == 0)
                return BadRequest("Invalid data.");

            var eduYear = await _context.EduYears.FirstOrDefaultAsync(e => e.EduCode == dto.EduCode);
            if (eduYear == null)
                return NotFound("EduYear not found.");

            _context.EduYears.Remove(eduYear);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AddYear([FromBody] AddYearDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.YearName) || dto.LevelCode == 0)
                return BadRequest("Invalid data.");

            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            var activeEduYear = await _context.EduYears
                .Where(e => e.RootCode == rootCode && e.IsActive)
                .FirstOrDefaultAsync();

            if (activeEduYear == null)
                return BadRequest("No active Edu Year for this root.");

            var year = new Year
            {
                YearName = dto.YearName,
                YearSort = dto.YearSort,
                LevelCode = dto.LevelCode,
                EduYearCode = activeEduYear.EduCode,
                InsertUser = userId,
                InsertTime = DateTime.Now
            };

            _context.Years.Add(year);
            await _context.SaveChangesAsync();

            return Json(new
            {
                yearCode = year.YearCode,
                yearName = year.YearName,
                yearSort = year.YearSort,
                levelCode = year.LevelCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditYear([FromBody] EditYearDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.YearName) || dto.LevelCode == 0)
                return BadRequest("Invalid data.");
            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode);
            if (year == null)
                return NotFound("Year not found.");

            year.YearName = dto.YearName;
            year.YearSort = dto.YearSort;
            year.LevelCode = dto.LevelCode;

            await _context.SaveChangesAsync();

            return Json(new
            {
                yearCode = year.YearCode,
                yearName = year.YearName,
                yearSort = year.YearSort,
                levelCode = year.LevelCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteYear([FromBody] DeleteYearDto dto)
        {
            if (dto == null || dto.YearCode == 0)
                return BadRequest("Invalid data.");

            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode);
            if (year == null)
                return NotFound("Year not found.");

            _context.Years.Remove(year);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AddLevel([FromBody] AddLevelDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.LevelName))
                return BadRequest("Invalid data.");

            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            // Optional: Check for duplicate level name under root
            var exists = await _context.Levels.AnyAsync(l => l.RootCode == rootCode && l.LevelName == dto.LevelName);
            if (exists)
                return BadRequest("Level name already exists for this root.");

            var level = new Level
            {
                LevelName = dto.LevelName,
                RootCode = rootCode,
                InsertUser = userId,
                InsertTime = DateTime.Now
            };

            _context.Levels.Add(level);
            await _context.SaveChangesAsync();

            return Json(new
            {
                levelCode = level.LevelCode,
                levelName = level.LevelName
            });
        }

        // DTOs
        public class AddEduYearDto
        {
            public string EduName { get; set; }
            public bool IsActive { get; set; }
        }
        public class EditEduYearDto
        {
            public int EduCode { get; set; }
            public string EduName { get; set; }
            public bool IsActive { get; set; }
        }
        public class DeleteEduYearDto
        {
            public int EduCode { get; set; }
        }

        public class AddYearDto
        {
            public string YearName { get; set; }
            public int YearSort { get; set; }
            public int LevelCode { get; set; }
        }
        public class EditYearDto
        {
            public int YearCode { get; set; }
            public string YearName { get; set; }
            public int YearSort { get; set; }
            public int LevelCode { get; set; }
        }
        public class DeleteYearDto
        {
            public int YearCode { get; set; }
        }

        public class AddLevelDto
        {
            public string LevelName { get; set; }
        }
    }
}