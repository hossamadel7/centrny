using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Linq;

namespace centrny.Controllers
{
    [Authorize]
    public class SubjectController : Controller
    {
        private readonly CenterContext _context;

        public SubjectController(CenterContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
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

            ViewBag.UserRootCode = rootCode;
            ViewBag.UserGroupCode = groupCode;
            ViewBag.CurrentUserName = user.Username;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects()
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

            var subjects = await (from s in _context.Subjects
                                  where s.RootCode == rootCode
                                  join r in _context.Roots on s.RootCode equals r.RootCode into rootJoin
                                  from r in rootJoin.DefaultIfEmpty()
                                  join y in _context.Years on s.YearCode equals y.YearCode into yearJoin
                                  from y in yearJoin.DefaultIfEmpty()
                                  select new
                                  {
                                      s.SubjectCode,
                                      s.SubjectName,
                                      s.IsPrimary,
                                      RootName = r != null ? r.RootName : null,
                                      YearName = y != null ? y.YearName : null,
                                      s.YearCode
                                  })
                                  .ToListAsync();

            return Json(subjects);
        }

        // Get all active years for the current user's root code (for dropdown)
        [HttpGet]
        public async Task<IActionResult> GetActiveYears()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            // Get all active EduYear codes for this root
            var activeEduYearCodes = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == rootCode)
                .Select(e => e.EduCode)
                .ToListAsync();

            // Now get all Years that reference those EduYear codes
            var yearList = await _context.Years
                .Where(y => activeEduYearCodes.Contains((int)y.EduYearCode))
                .Select(y => new { y.YearCode, y.YearName })
                .ToListAsync();

            return Json(yearList);
        }

        [HttpPost]
        public async Task<IActionResult> AddSubject([FromBody] AddSubjectDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SubjectName) || dto.YearCode == 0)
                return BadRequest("Invalid data.");

            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            var subject = new Subject
            {
                SubjectName = dto.SubjectName,
                IsPrimary = dto.IsPrimary,
                YearCode = dto.YearCode,
                RootCode = rootCode,
                InsertUser = userId,
                InsertTime = DateTime.Now
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode);
            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == rootCode);

            return Json(new
            {
                subject.SubjectCode,
                subject.SubjectName,
                subject.IsPrimary,
                RootName = root?.RootName,
                YearName = year?.YearName,
                subject.YearCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditSubject([FromBody] EditSubjectDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SubjectName) || dto.YearCode == 0)
                return BadRequest("Invalid data.");
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == dto.SubjectCode);
            if (subject == null)
                return NotFound("Subject not found.");

            subject.SubjectName = dto.SubjectName;
            subject.IsPrimary = dto.IsPrimary;
            subject.YearCode = dto.YearCode;
            subject.InsertTime = DateTime.Now;

            await _context.SaveChangesAsync();

            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode);
            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == subject.RootCode);

            return Json(new
            {
                subject.SubjectCode,
                subject.SubjectName,
                subject.IsPrimary,
                RootName = root?.RootName,
                YearName = year?.YearName,
                subject.YearCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSubject([FromBody] DeleteSubjectDto dto)
        {
            if (dto == null || dto.SubjectCode == 0)
                return BadRequest("Invalid data.");

            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == dto.SubjectCode);
            if (subject == null)
                return NotFound("Subject not found.");

            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();

            return Ok();
        }

        public class AddSubjectDto
        {
            public string SubjectName { get; set; }
            public bool IsPrimary { get; set; }
            public int YearCode { get; set; }
        }
        public class EditSubjectDto
        {
            public int SubjectCode { get; set; }
            public string SubjectName { get; set; }
            public bool IsPrimary { get; set; }
            public int YearCode { get; set; }
        }
        public class DeleteSubjectDto
        {
            public int SubjectCode { get; set; }
        }
    }
}