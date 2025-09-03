using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using SubjectRes = centrny.Resources.Subject;
using centrny.Attributes;

namespace centrny.Controllers
{
    [RequirePageAccess("Question")]
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
    _context.Roots.Where(x => x.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "")).FirstOrDefault().RootCode ,
    GetSessionString("Username")
            );
        }

        // --- Authority Check via Session ---
      

        public async Task<IActionResult> Index()
        {
          
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
           

            ViewBag.UserRootCode = rootCode.Value;
            ViewBag.UserGroupCode = groupCode.Value;
            ViewBag.CurrentUserName = username;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects()
        {
          
            var (userCode, groupCode, rootCode,username) = GetSessionContext();

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
                                      EduYearCode = y != null 
                                  })
                                  .ToListAsync();

            return Json(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersForSubject(int subjectCode)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();


            var teachJoin = await (from t in _context.Teaches
                                   join teacher in _context.Teachers on t.TeacherCode equals teacher.TeacherCode
                                   join branch in _context.Branches on t.BranchCode equals branch.BranchCode
                                   where t.SubjectCode == subjectCode && t.RootCode==rootCode
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

        [RequirePageAccess("Question", "Update")]
        [HttpPost]
        public async Task<IActionResult> EditTeachCenter([FromBody] EditTeachCenterDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (dto == null)
                return BadRequest("Invalid data");
            if (!rootCode.HasValue)
                return Unauthorized();

            var teach = await _context.Teaches.FirstOrDefaultAsync(t =>
                t.TeacherCode == dto.TeacherCode &&
                t.SubjectCode == dto.SubjectCode &&
                t.BranchCode == dto.BranchCode &&
                t.EduYearCode == dto.EduYearCode &&
                t.RootCode == rootCode.Value // <--- session rootCode only!
            );

            if (teach == null)
                return NotFound("Teach record not found or you do not have access.");

            teach.CenterPercentage = dto.CenterPercentage;
            teach.CenterAmount = dto.CenterAmount;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveYears()
        {
            // Get session context (adjust if your GetSessionContext returns a different tuple)
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (!rootCode.HasValue)
                return Json(new { success = false, message = "Root not resolved (no rootCode in session)." });

            // Query all years that belong to this root
            var query = _context.Years
                .AsNoTracking()
                .Where(y => y.RootCode == rootCode.Value);

         
            var yearList = await query
                .OrderBy(y => y.YearSort)          // If YearSort exists
                                                   // .OrderBy(y => y.YearName)       // Use this instead if you do NOT have YearSort
                .Select(y => new {
                    yearCode = y.YearCode,
                    yearName = y.YearName
                })
                .ToListAsync();

            return Json(new { success = true, data = yearList });
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersByRoot()
        {
         
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
           

            var teachers = await _context.Teachers
                .Where(t => t.RootCode == rootCode.Value && t.IsActive)
                .Select(t => new { t.TeacherCode, t.TeacherName })
                .ToListAsync();

            return Json(teachers);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByRoot()
        {
           
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
           

            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode.Value)
                .Select(b => new { b.BranchCode, b.BranchName })
                .ToListAsync();

            return Json(branches);
        }
        [RequirePageAccess("Question", "insert")]

        [HttpPost]
        public async Task<IActionResult> AddTeacherToSubject([FromBody] AddTeacherToSubjectDto dto)
        {
          
            if (dto == null || dto.TeacherCode == 0 || dto.SubjectCode == 0 || dto.BranchCode == 0 || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
         
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.TeacherCode == dto.TeacherCode && t.RootCode == rootCode.Value);
            if (teacher == null) return BadRequest(SubjectRes.Subject_TeacherNotFound);

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchCode == dto.BranchCode && b.RootCode == rootCode.Value);
            if (branch == null) return BadRequest(SubjectRes.Subject_BranchNotFound);

            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode);
          

            var teach = new Teach
            {
                TeacherCode = dto.TeacherCode,
                SubjectCode = dto.SubjectCode,
                EduYearCode = dto.EduYearCode,
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
        [RequirePageAccess("Question", "insert")]
        [HttpPost]
        public async Task<IActionResult> AddSubject([FromBody] AddSubjectDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SubjectName) || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            // Check the year belongs to root
            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == dto.YearCode && y.RootCode == rootCode.Value);
            if (year == null)
                return BadRequest("Selected year does not belong to your root or does not exist.");

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

            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == rootCode.Value);

            return Json(new
            {
                subject.SubjectCode,
                subject.SubjectName,
                subject.IsPrimary,
                RootName = root?.RootName,
                YearName = year?.YearName,
                subject.YearCode,
            });
        }

        [RequirePageAccess("Question", "update")]
        [HttpPost]
        public async Task<IActionResult> EditSubject([FromBody] EditSubjectDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (dto == null || string.IsNullOrWhiteSpace(dto.SubjectName) || dto.YearCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

          

            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == dto.SubjectCode && s.RootCode==rootCode);

            if (subject == null)
                return NotFound(SubjectRes.Subject_SubjectNotFound);

            // Root isolation check
           

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
            });
        }

        [RequirePageAccess("Question", "delete")]
        [HttpPost]
        public async Task<IActionResult> DeleteSubject([FromBody] DeleteSubjectDto dto)
        {
            if (dto == null || dto.SubjectCode == 0)
                return BadRequest(SubjectRes.Subject_InvalidData);

            var (userCode, groupCode, rootCode, username) = GetSessionContext();
      

            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == dto.SubjectCode && s.RootCode == rootCode);

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