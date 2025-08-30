using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using SubjectRes = centrny.Resources.Subject;

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

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int? userCode, int? groupCode, int? rootCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                GetSessionInt("RootCode"),
                GetSessionString("Username")
            );
        }

        // --- Authority Check via Session ---
        private bool UserHasSubjectPermission()
        {
            var groupCode = GetSessionInt("GroupCode");
            if (groupCode == null) return false;
            var page = _context.Pages.FirstOrDefault(p => p.PagePath == "Subject/Index");
            if (page == null) return false;
            return _context.GroupPages.Any(gp => gp.GroupCode == groupCode.Value && gp.PageCode == page.PageCode);
        }

        public async Task<IActionResult> Index()
        {
            if (!UserHasSubjectPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            ViewBag.UserRootCode = rootCode.Value;
            ViewBag.UserGroupCode = groupCode.Value;
            ViewBag.CurrentUserName = username;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects()
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var subjects = await (from s in _context.Subjects
                                  where s.RootCode == rootCode.Value
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
                                      s.YearCode,
                                      EduYearCode = y != null ? y.EduYearCode : null
                                  })
                                  .ToListAsync();

            return Json(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersForSubject(int subjectCode)
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            var teachJoin = await (from t in _context.Teaches
                                   join teacher in _context.Teachers on t.TeacherCode equals teacher.TeacherCode
                                   join branch in _context.Branches on t.BranchCode equals branch.BranchCode
                                   where t.SubjectCode == subjectCode
                                   select new
                                   {
                                       t.TeacherCode,
                                       t.SubjectCode,
                                       t.BranchCode,
                                       t.RootCode,
                                       t.EduYearCode,
                                       teacher.TeacherName,
                                       branch.BranchName,
                                       t.CenterAmount,
                                       t.CenterPercentage
                                   }).ToListAsync();

            return Json(teachJoin);
        }

        [HttpPost]
        public async Task<IActionResult> EditTeachCenter([FromBody] EditTeachCenterDto dto)
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null)
                return BadRequest("Invalid data");

            var teach = await _context.Teaches.FirstOrDefaultAsync(t =>
                t.TeacherCode == dto.TeacherCode &&
                t.SubjectCode == dto.SubjectCode &&
                t.BranchCode == dto.BranchCode &&
                t.RootCode == dto.RootCode &&
                t.EduYearCode == dto.EduYearCode);

            if (teach == null)
                return NotFound("Teach record not found.");

            teach.CenterPercentage = dto.CenterPercentage;
            teach.CenterAmount = dto.CenterAmount;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveYears()
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var activeEduYearCodes = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == rootCode.Value)
                .Select(e => e.EduCode)
                .ToListAsync();

            var yearList = await _context.Years
                .Where(y => activeEduYearCodes.Contains((int)y.EduYearCode))
                .Select(y => new { y.YearCode, y.YearName })
                .ToListAsync();

            return Json(yearList);
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersByRoot()
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var teachers = await _context.Teachers
                .Where(t => t.RootCode == rootCode.Value && t.IsActive)
                .Select(t => new { t.TeacherCode, t.TeacherName })
                .ToListAsync();

            return Json(teachers);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByRoot()
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode.Value)
                .Select(b => new { b.BranchCode, b.BranchName })
                .ToListAsync();

            return Json(branches);
        }

        [HttpPost]
        public async Task<IActionResult> AddTeacherToSubject([FromBody] AddTeacherToSubjectDto dto)
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null || dto.TeacherCode == 0 || dto.SubjectCode == 0 || dto.BranchCode == 0 || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.TeacherCode == dto.TeacherCode && t.RootCode == rootCode.Value);
            if (teacher == null) return BadRequest(SubjectRes.Subject_TeacherNotFound);

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchCode == dto.BranchCode && b.RootCode == rootCode.Value);
            if (branch == null) return BadRequest(SubjectRes.Subject_BranchNotFound);

            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode);
            if (year == null || year.EduYearCode == null)
                return BadRequest(SubjectRes.Subject_CouldNotDetermineEduYear);

            var teach = new Teach
            {
                TeacherCode = dto.TeacherCode,
                SubjectCode = dto.SubjectCode,
                EduYearCode = year.EduYearCode.Value,
                BranchCode = dto.BranchCode,
                RootCode = rootCode.Value,
                YearCode = dto.YearCode,
                CenterPercentage = dto.CenterPercentage,
                CenterAmount = dto.CenterAmount,
                InsertUser = userCode.Value,
                InsertTime = DateTime.Now,
                IsActive = true
            };
            _context.Teaches.Add(teach);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = SubjectRes.Subject_SuccessTeacherAssigned });
        }

        [HttpPost]
        public async Task<IActionResult> AddSubject([FromBody] AddSubjectDto dto)
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null || string.IsNullOrWhiteSpace(dto.SubjectName) || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var subject = new Subject
            {
                SubjectName = dto.SubjectName,
                IsPrimary = dto.IsPrimary,
                YearCode = dto.YearCode,
                RootCode = rootCode.Value,
                InsertUser = userCode.Value,
                InsertTime = DateTime.Now
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode);
            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == rootCode.Value);

            return Json(new
            {
                subject.SubjectCode,
                subject.SubjectName,
                subject.IsPrimary,
                RootName = root?.RootName,
                YearName = year?.YearName,
                subject.YearCode,
                EduYearCode = year?.EduYearCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditSubject([FromBody] EditSubjectDto dto)
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null || string.IsNullOrWhiteSpace(dto.SubjectName) || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == dto.SubjectCode);
            if (subject == null)
                return NotFound(SubjectRes.Subject_SubjectNotFound);

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
                subject.YearCode,
                EduYearCode = year?.EduYearCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSubject([FromBody] DeleteSubjectDto dto)
        {
            if (!UserHasSubjectPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null || dto.SubjectCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == dto.SubjectCode);
            if (subject == null)
                return NotFound(SubjectRes.Subject_SubjectNotFound);

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
        public class AddTeacherToSubjectDto
        {
            public int TeacherCode { get; set; }
            public int SubjectCode { get; set; }
            public int EduYearCode { get; set; }
            public int BranchCode { get; set; }
            public int RootCode { get; set; }
            public int YearCode { get; set; }
            public double? CenterPercentage { get; set; }
            public double? CenterAmount { get; set; }
        }

        public class EditTeachCenterDto
        {
            public int TeacherCode { get; set; }
            public int SubjectCode { get; set; }
            public int BranchCode { get; set; }
            public int RootCode { get; set; }
            public int EduYearCode { get; set; }
            public double? CenterPercentage { get; set; }
            public double? CenterAmount { get; set; }
        }
    }
}