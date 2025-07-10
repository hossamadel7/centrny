using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
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
                                      s.YearCode,
                                      EduYearCode = y != null ? y.EduYearCode : null
                                  })
                                  .ToListAsync();

            return Json(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersForSubject(int subjectCode)
        {
            var teachJoin = await (from t in _context.Teaches
                                   join teacher in _context.Teachers on t.TeacherCode equals teacher.TeacherCode
                                   where t.SubjectCode == subjectCode
                                   select new
                                   {
                                       teacher.TeacherCode,
                                       teacher.TeacherName
                                   }).ToListAsync();

            return Json(teachJoin);
        }

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

            var activeEduYearCodes = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == rootCode)
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
            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            var teachers = await _context.Teachers
                .Where(t => t.RootCode == rootCode && t.IsActive)
                .Select(t => new { t.TeacherCode, t.TeacherName })
                .ToListAsync();

            return Json(teachers);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByRoot()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode)
                .Select(b => new { b.BranchCode, b.BranchName })
                .ToListAsync();

            return Json(branches);
        }

        [HttpPost]
        public async Task<IActionResult> AddTeacherToSubject([FromBody] AddTeacherToSubjectDto dto)
        {
            if (dto == null || dto.TeacherCode == 0 || dto.SubjectCode == 0 || dto.BranchCode == 0 || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.TeacherCode == dto.TeacherCode && t.RootCode == rootCode);
            if (teacher == null) return BadRequest(SubjectRes.Subject_TeacherNotFound);

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchCode == dto.BranchCode && b.RootCode == rootCode);
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
                RootCode = rootCode,
                YearCode = dto.YearCode,
                CenterPercentage = dto.CenterPercentage,
                CenterAmount = dto.CenterAmount,
                InsertUser = userId,
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
            if (dto == null || string.IsNullOrWhiteSpace(dto.SubjectName) || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

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
                subject.YearCode,
                EduYearCode = year?.EduYearCode
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditSubject([FromBody] EditSubjectDto dto)
        {
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
    }
}