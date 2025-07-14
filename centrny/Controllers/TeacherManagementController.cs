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

        // --- Authority Check ---
        private bool UserHasTeacherManagementPermission()
        {
            var username = User.Identity?.Name;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return false;

            var userGroupCodes = _db.Users
                .Where(ug => ug.UserCode == user.UserCode)
                .Select(ug => ug.GroupCode)
                .ToList();

            var page = _db.Pages.FirstOrDefault(p => p.PagePath == "TeacherManagement/Index");
            if (page == null)
                return false;

            return _db.GroupPages.Any(gp => userGroupCodes.Contains(gp.GroupCode) && gp.PageCode == page.PageCode);
        }

        public int GetUserRootCode()
        {
            string username = User.Identity.Name;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return -1;
            var group = _db.Groups.FirstOrDefault(g => g.GroupCode == user.GroupCode);
            if (group == null) return -1;
            return group.RootCode;
        }

        public IActionResult Index()
        {
            if (!UserHasTeacherManagementPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }
            return View();
        }

        [HttpGet]
        public IActionResult GetUserRootInfo()
        {
            if (!UserHasTeacherManagementPermission())
            {
                return Json(new { error = "Access denied." });
            }
            string username = User.Identity.Name;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return Json(new { error = "User not found" });
            var group = _db.Groups.FirstOrDefault(g => g.GroupCode == user.GroupCode);
            if (group == null) return Json(new { error = "Group not found" });
            var root = _db.Roots.FirstOrDefault(r => r.RootCode == group.RootCode);
            if (root == null) return Json(new { error = "Root not found" });

            return Json(new
            {
                user_code = user.UserCode,
                user_name = user.Username,
                group_code = group.GroupCode,
                user_root_code = root.RootCode,
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

            var user = _db.Users.FirstOrDefault(u => u.UserCode == teacher.InsertUser);
            if (user == null) return BadRequest("User not found.");

            var root = _db.Roots.FirstOrDefault(r => r.RootCode == teacher.RootCode);
            if (root == null) return BadRequest("Root not found.");

            var newTeacher = new Teacher
            {
                TeacherName = teacher.TeacherName,
                TeacherPhone = teacher.TeacherPhone,
                TeacherAddress = teacher.TeacherAddress,
                IsActive = true,
                IsStaff = true,
                RootCode = teacher.RootCode,
                InsertUser = teacher.InsertUser,
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

            var eduYearCodes = _db.EduYears
                .Where(e => e.RootCode == rootCode)
                .Select(e => e.EduCode)
                .ToList();

            var years = _db.Years
                .Where(y => y.EduYearCode.HasValue && eduYearCodes.Contains(y.EduYearCode.Value))
                .Select(y => new { yearCode = y.YearCode, yearName = y.YearName, eduYearCode = y.EduYearCode })
                .ToList();

            return Json(years);
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

            // Join Teach, Subject, Year to get subject name and year name
            var teachRecords = (from t in _db.Teaches
                                join s in _db.Subjects on t.SubjectCode equals s.SubjectCode
                                join y in _db.Years on t.YearCode equals y.YearCode
                                where t.TeacherCode == teacherCode && t.RootCode == rootCode
                                select new
                                {
                                    teacherCode = t.TeacherCode,
                                    subjectCode = t.SubjectCode,
                                    subjectName = s.SubjectName,
                                    yearCode = y.YearCode,
                                    yearName = y.YearName
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

            // Validate year
            var year = _db.Years.FirstOrDefault(y => y.YearCode == model.YearCode);
            if (year == null)
                return BadRequest("Year not found!");

            if (year.EduYearCode == null)
                return BadRequest("Year does not have a valid EduYearCode.");

            // Validate subject
            var subject = _db.Subjects.FirstOrDefault(s => s.SubjectCode == model.SubjectCode && s.RootCode == model.RootCode);
            if (subject == null)
                return BadRequest("Subject not found for this root.");

            // Validate teacher
            var teacher = _db.Teachers.FirstOrDefault(t => t.TeacherCode == model.TeacherCode && t.RootCode == model.RootCode);
            if (teacher == null)
                return BadRequest("Teacher not found for this root.");

            // Find corresponding EduYear
            var eduYear = _db.EduYears.FirstOrDefault(e => e.EduCode == year.EduYearCode.Value && e.RootCode == model.RootCode);
            if (eduYear == null)
                return BadRequest("Education year not found for this year and root.");

            // Create Teach record
            var teach = new Teach
            {
                SubjectCode = model.SubjectCode,
                TeacherCode = model.TeacherCode,
                BranchCode = model.BranchCode,
                RootCode = model.RootCode,
                YearCode = model.YearCode,
                EduYearCode = eduYear.EduCode,
                InsertUser = model.InsertUser,
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