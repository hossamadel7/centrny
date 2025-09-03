using centrny.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace centrny.Controllers
{
    [Authorize]
    public class TeacherManagementController : Controller
    {
        private readonly CenterContext _db;
        public TeacherManagementController(CenterContext db)
        {
            _db = db;
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
        private bool UserHasTeacherManagementPermission()
        {
            var groupCode = GetSessionInt("GroupCode");
            if (groupCode == null) return false;
            var page = _db.Pages.FirstOrDefault(p => p.PagePath == "TeacherManagement/Index");
            if (page == null) return false;
            return _db.GroupPages.Any(gp => gp.GroupCode == groupCode.Value && gp.PageCode == page.PageCode);
        }

        // --- Permission Helper ---
        public class PagePermission
        {
            public bool CanInsert { get; set; }
            public bool CanUpdate { get; set; }
            public bool CanDelete { get; set; }
        }

        // STATIC version for use in views (Razor)
        public static PagePermission GetPagePermissions(CenterContext db, System.Security.Claims.ClaimsPrincipal user, string pagePath)
        {
            var username = user.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return new PagePermission();

            var currentUser = db.Users.FirstOrDefault(u => u.Username == username);
            if (currentUser == null) return new PagePermission();
            var groupCode = currentUser.GroupCode;

            var page = db.Pages.FirstOrDefault(p => p.PagePath == pagePath);
            if (page == null) return new PagePermission();
            var gp = db.GroupPages.FirstOrDefault(g => g.GroupCode == groupCode && g.PageCode == page.PageCode);
            if (gp == null) return new PagePermission();
            return new PagePermission
            {
                CanInsert = gp.InsertFlag,
                CanUpdate = gp.UpdateFlag,
                CanDelete = gp.DeleteFlag
            };
        }

        // INSTANCE version for use in C# controller code
        public PagePermission GetPagePermissions(string pagePath)
        {
            var groupCode = GetSessionInt("GroupCode");
            if (groupCode == null) return new PagePermission();
            var page = _db.Pages.FirstOrDefault(p => p.PagePath == pagePath);
            if (page == null) return new PagePermission();
            var gp = _db.GroupPages.FirstOrDefault(g => g.GroupCode == groupCode.Value && g.PageCode == page.PageCode);
            if (gp == null) return new PagePermission();
            return new PagePermission
            {
                CanInsert = gp.InsertFlag,
                CanUpdate = gp.UpdateFlag,
                CanDelete = gp.DeleteFlag
            };
        }

        public int GetUserRootCode()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            return rootCode ?? -1;
        }

        public IActionResult Index()
        {
            if (!UserHasTeacherManagementPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }
            var perms = GetPagePermissions("TeacherManagement/Index");
            ViewBag.CanInsert = perms.CanInsert;
            ViewBag.CanUpdate = perms.CanUpdate;
            ViewBag.CanDelete = perms.CanDelete;
            return View();
        }

        [HttpGet]
        public IActionResult GetUserRootInfo()
        {
            if (!UserHasTeacherManagementPermission())
            {
                return Json(new { error = "Access denied." });
            }
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Json(new { error = "Session user/group/root missing" });

            var root = _db.Roots.FirstOrDefault(r => r.RootCode == rootCode.Value);
            if (root == null) return Json(new { error = "Root not found" });

            return Json(new
            {
                user_code = userCode,
                user_name = username,
                group_code = groupCode,
                user_root_code = rootCode,
                root_code = root.RootCode,
                root_name = root.RootName
            });
        }

        [HttpGet]
        public IActionResult GetTeachersByRoot(int rootCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var teachers = _db.Teachers
                .Where(t => t.RootCode == rootCode)
                .Select(t => new
                {
                    teacherCode = t.TeacherCode,
                    teacherName = t.TeacherName,
                    teacherPhone = t.TeacherPhone,
                    teacherAddress = t.TeacherAddress,
                    isActive = t.IsActive
                })
                .ToList();
            return Json(teachers);
        }

        [HttpGet]
        public IActionResult GetTeacherById(int teacherCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var t = _db.Teachers.FirstOrDefault(x => x.TeacherCode == teacherCode);
            if (t == null) return NotFound();
            return Json(new
            {
                teacherCode = t.TeacherCode,
                teacherName = t.TeacherName,
                teacherPhone = t.TeacherPhone,
                teacherAddress = t.TeacherAddress
            });
        }

        [HttpPost]
        public IActionResult AddTeacher([FromBody] TeacherInputModel teacher)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            if (teacher == null) return BadRequest("Invalid data.");

            var root = _db.Roots.FirstOrDefault(r => r.RootCode == teacher.RootCode);
            if (root == null) return BadRequest("Root not found.");

            var (userCode, _, _, _) = GetSessionContext();
            var newTeacher = new Teacher
            {
                TeacherName = teacher.TeacherName,
                TeacherPhone = teacher.TeacherPhone,
                TeacherAddress = teacher.TeacherAddress,
                IsActive = true,
                IsStaff = true,
                RootCode = teacher.RootCode,
                InsertUser = userCode ?? teacher.InsertUser,
                InsertTime = DateTime.Now
            };

            _db.Teachers.Add(newTeacher);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Teacher added successfully." });
        }

        [HttpPost]
        public IActionResult EditTeacher([FromBody] TeacherEditModel model)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var teacher = _db.Teachers.FirstOrDefault(t => t.TeacherCode == model.TeacherCode);
            if (teacher == null) return BadRequest("Teacher not found.");
            teacher.TeacherName = model.TeacherName;
            teacher.TeacherPhone = model.TeacherPhone;
            teacher.TeacherAddress = model.TeacherAddress;
            _db.SaveChanges();
            return Ok(new { success = true, message = "Teacher updated successfully." });
        }

        [HttpPost]
        public IActionResult DeleteTeacher([FromBody] int teacherCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var teacher = _db.Teachers.FirstOrDefault(t => t.TeacherCode == teacherCode);
            if (teacher == null) return BadRequest("Teacher not found.");
            var teaches = _db.Teaches.Where(t => t.TeacherCode == teacherCode).ToList();
            _db.Teaches.RemoveRange(teaches);
            _db.Teachers.Remove(teacher);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Teacher and associated teaching subjects deleted successfully." });
        }

        // ----------- TEACHING SUBJECTS LOGIC ----------------

        [HttpGet]
        public IActionResult GetYearsByRoot(int rootCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var Years = _db.Years
                .Where(e => e.RootCode == rootCode)
                .Select(e => new { e.YearCode, e.YearName })
                .ToList();

          
            return Json(Years);
        }

        [HttpGet]
        public IActionResult GetActiveEduYearByRoot(int rootCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var activeEduYear = _db.EduYears.FirstOrDefault(e => e.RootCode == rootCode && e.IsActive);
            if (activeEduYear == null)
                return Json(new { });

            return Json(new
            {
                eduYearCode = activeEduYear.EduCode,
                eduYearName = activeEduYear.EduName
            });
        }

        [HttpGet]
        public IActionResult GetBranchesForRootWithCenters(int rootCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var centers = _db.Centers.Where(c => c.RootCode == rootCode).ToList();
            var centerCodes = centers.Select(c => c.CenterCode).ToList();
            var branches = _db.Branches
                .Where(b => centerCodes.Contains(b.CenterCode))
                .Select(b => new
                {
                    branchCode = b.BranchCode,
                    branchName = b.BranchName
                }).ToList();
            return Json(branches);
        }

        [HttpGet]
        public IActionResult GetSubjectsByTeacher(int teacherCode, int rootCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var teachRecords = (from t in _db.Teaches
                                join s in _db.Subjects on t.SubjectCode equals s.SubjectCode
                                join y in _db.Years on t.YearCode equals y.YearCode
                                join b in _db.Branches on t.BranchCode equals b.BranchCode
                                where t.TeacherCode == teacherCode && t.RootCode == rootCode
                                select new
                                {
                                    teacherCode = t.TeacherCode,
                                    subjectCode = t.SubjectCode,
                                    subjectName = s.SubjectName,
                                    yearCode = y.YearCode,
                                    yearName = y.YearName,
                                    branchCode = b.BranchCode,
                                    branchName = b.BranchName
                                }).ToList();

            return Json(teachRecords);
        }

        [HttpGet]
        public IActionResult GetSubjectsByRoot(int rootCode)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var subjects = _db.Subjects
                .Where(s => s.RootCode == rootCode)
                .Select(s => new
                {
                    subjectCode = s.SubjectCode,
                    subjectName = s.SubjectName
                })
                .ToList();
            return Json(subjects);
        }

        [HttpPost]
        public IActionResult AddTeachingSubject([FromBody] AddTeachingSubjectInputModel model)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var year = _db.Years.FirstOrDefault(y => y.YearCode == model.YearCode);
            if (year == null)
                return BadRequest("Year not found!");

            

            var subject = _db.Subjects.FirstOrDefault(s => s.SubjectCode == model.SubjectCode && s.RootCode == model.RootCode);
            if (subject == null)
                return BadRequest("Subject not found for this root.");

            var teacher = _db.Teachers.FirstOrDefault(t => t.TeacherCode == model.TeacherCode && t.RootCode == model.RootCode);
            if (teacher == null)
                return BadRequest("Teacher not found for this root.");

            var eduYear = _db.EduYears.FirstOrDefault( year=> year.RootCode == model.RootCode);
            if (eduYear == null)
                return BadRequest("Education year not found for this year and root.");

            var (userCode, _, _, _) = GetSessionContext();

            var teach = new Teach
            {
                SubjectCode = model.SubjectCode,
                TeacherCode = model.TeacherCode,
                BranchCode = model.BranchCode,
                RootCode = model.RootCode,
                YearCode = model.YearCode,
                EduYearCode = eduYear.EduCode,
                InsertUser = userCode ?? model.InsertUser,
                InsertTime = DateTime.Now,
                CenterPercentage = null,
                CenterAmount = null
            };
            _db.Teaches.Add(teach);
            _db.SaveChanges();

            return Ok(new { success = true, message = "Teaching subject added successfully." });
        }

        [HttpPost]
        public IActionResult DeleteTeach([FromBody] DeleteTeachInputModel model)
        {
            if (!UserHasTeacherManagementPermission())
                return Json(new { error = "Access denied." });

            var teach = _db.Teaches.FirstOrDefault(t => t.TeacherCode == model.TeacherCode && t.SubjectCode == model.SubjectCode);
            if (teach == null) return BadRequest("Teach record not found.");
            _db.Teaches.Remove(teach);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Teaching subject deleted successfully." });
        }

        // ----------- MODELS ---------------

        public class TeacherInputModel
        {
            public string TeacherName { get; set; }
            public string TeacherPhone { get; set; }
            public string? TeacherAddress { get; set; }
            public int RootCode { get; set; }
            public int InsertUser { get; set; }
        }
        public class TeacherEditModel
        {
            public int TeacherCode { get; set; }
            public string TeacherName { get; set; }
            public string TeacherPhone { get; set; }
            public string? TeacherAddress { get; set; }
        }
        public class AddTeachingSubjectInputModel
        {
            public int SubjectCode { get; set; }
            public bool IsPrimary { get; set; }
            public int RootCode { get; set; }
            public int YearCode { get; set; }
            public int InsertUser { get; set; }
            public int BranchCode { get; set; }
            public int TeacherCode { get; set; }
        }
        public class DeleteTeachInputModel
        {
            public int TeacherCode { get; set; }
            public int SubjectCode { get; set; }
        }
    }
}