using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using centrny.Attributes;

namespace centrny.Controllers
{
    [RequirePageAccess("EduYear")]
    [Authorize]
    public class EduYearController : Controller
    {
        private readonly CenterContext _context;

        public EduYearController(CenterContext context)
        {
            _context = context;
        }

        // --- SESSION HELPERS ---
        private int GetSessionInt(string key) => (int)HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int userCode, int groupCode, int rootCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
    _context.Roots.Where(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", "")).FirstOrDefault().RootCode,
    GetSessionString("Username")
            );
        }

        // --- Authority Check via Session ---
        private bool UserHasEduYearPermission()
        {
            var groupCode = GetSessionInt("GroupCode");
            if (groupCode == null) return false;
            var page = _context.Pages.FirstOrDefault(p => p.PagePath == "EduYear/Index");
            if (page == null) return false;
            return _context.GroupPages.Any(gp => gp.GroupCode == groupCode && gp.PageCode == page.PageCode);
        }

        private int GetCurrentUserId() => GetSessionInt("UserCode");

        public IActionResult Index()
        {
            if (!UserHasEduYearPermission())
                return View("~/Views/Login/AccessDenied.cshtml");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetEduYears()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!UserHasEduYearPermission())
                return Json(new { success = false, message = "Access denied." });

           

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
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

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
                .Where(y => y.RootCode == activeEduYear.RootCode)
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
        [RequirePageAccess("EduYear", "insert")]
        [HttpPost]
        public async Task<IActionResult> AddEduYear([FromBody] AddEduYearDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (dto == null || string.IsNullOrWhiteSpace(dto.EduName))
                return Json(new { success = false, message = "Invalid data." });

         
            var eduYear = new EduYear
            {
                EduName = dto.EduName,
                IsActive = false,
                RootCode = rootCode
            };

            _context.EduYears.Add(eduYear);
            await _context.SaveChangesAsync();

            return Json(new { success = true, eduCode = eduYear.EduCode, eduName = eduYear.EduName, isActive = eduYear.IsActive });
        }
        [RequirePageAccess("EduYear", "update")]


        [HttpPost]
        public async Task<IActionResult> EditEduYear([FromBody] EditEduYearDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (dto == null || string.IsNullOrWhiteSpace(dto.EduName))
                return Json(new { success = false, message = "Invalid data." });
            var eduYear = await _context.EduYears.FirstOrDefaultAsync(e => e.EduCode == dto.EduCode);
            if (eduYear == null)
                return Json(new { success = false, message = "EduYear not found." });

            if (dto.IsActive && !eduYear.IsActive)
            {
                var activeExists = await _context.EduYears
                    .AnyAsync(e => e.RootCode == eduYear.RootCode && e.IsActive && e.EduCode != eduYear.EduCode);
                if (activeExists)
                    return Json(new { success = false, message = "There is already an active Edu Year for this root. Only one can be active." });
            }

            eduYear.EduName = dto.EduName;
            eduYear.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Json(new { success = true, eduCode = eduYear.EduCode, eduName = eduYear.EduName, isActive = eduYear.IsActive });
        }

        [RequirePageAccess("EduYear", "delete")]

        [HttpPost]
        public async Task<IActionResult> DeleteEduYear([FromBody] DeleteEduYearDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (dto == null || dto.EduCode == 0)
                return Json(new { success = false, message = "Invalid data." });

            var eduYear = await _context.EduYears.FirstOrDefaultAsync(e => e.EduCode == dto.EduCode && e.RootCode==rootCode);
            if (eduYear == null)
                return Json(new { success = false, message = "EduYear not found." });

            _context.EduYears.Remove(eduYear);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Education year deleted successfully." });
        }

        [HttpPost]
        [RequirePageAccess("EduYear", "insert")]

        public async Task<IActionResult> AddYear([FromBody] AddYearDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (dto == null || string.IsNullOrWhiteSpace(dto.YearName) || dto.LevelCode == 0)
                return Json(new { success = false, message = "Invalid data." });

         
           
           
            var year = new Year
            {
                YearName = dto.YearName,
                YearSort = dto.YearSort,
                LevelCode = dto.LevelCode,
                RootCode = rootCode,
                InsertUser = userCode ,
                InsertTime = DateTime.Now
            };

            _context.Years.Add(year);
            await _context.SaveChangesAsync();

            return Json(new { success = true, yearCode = year.YearCode, yearName = year.YearName, yearSort = year.YearSort, levelCode = year.LevelCode });
        }
        [RequirePageAccess("EduYear", "update")]

        [HttpPost]
        public async Task<IActionResult> EditYear([FromBody] EditYearDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (dto == null || string.IsNullOrWhiteSpace(dto.YearName) || dto.LevelCode == 0)
                return Json(new { success = false, message = "Invalid data." });
            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode && y.RootCode==rootCode);
            if (year == null)
                return Json(new { success = false, message = "Year not found." });

            year.YearName = dto.YearName;
            year.YearSort = dto.YearSort;
            year.LevelCode = dto.LevelCode;

            await _context.SaveChangesAsync();

            return Json(new { success = true, yearCode = year.YearCode, yearName = year.YearName, yearSort = year.YearSort, levelCode = year.LevelCode });
        }
        [RequirePageAccess("EduYear", "delete")]

        [HttpPost]
        public async Task<IActionResult> DeleteYear([FromBody] DeleteYearDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            try
            {
                if (dto == null || dto.YearCode == 0)
                    return Json(new { success = false, message = "Invalid data." });

                var year = await _context.Years.FirstOrDefaultAsync(y=>y.YearCode== dto.YearCode && y.RootCode==rootCode);
                if (year == null)
                    return Json(new { success = false, message = "Year not found." });

                _context.Years.Remove(year);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Year deleted successfully." });
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("REFERENCE constraint \"FK_Student_Year\"") == true)
            {
                return Json(new { success = false, message = "Cannot delete year: There are students assigned to this year." });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }
        [RequirePageAccess("EduYear", "insert")]

        [HttpPost]
        public async Task<IActionResult> AddLevel([FromBody] AddLevelDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (dto == null || string.IsNullOrWhiteSpace(dto.LevelName))
                return Json(new { success = false, message = "Invalid data." });

          
            var exists = await _context.Levels.AnyAsync(l => l.RootCode == rootCode && l.LevelName == dto.LevelName);
            if (exists)
                return Json(new { success = false, message = "Level name already exists for this root." });

            var level = new Level
            {
                LevelName = dto.LevelName,
                RootCode = rootCode,
                InsertUser = userCode ,
                InsertTime = DateTime.Now
            };

            _context.Levels.Add(level);
            await _context.SaveChangesAsync();

            return Json(new { success = true, levelCode = level.LevelCode, levelName = level.LevelName });
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