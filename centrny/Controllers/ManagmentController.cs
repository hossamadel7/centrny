using centrny.Attributes;
using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace centrny.Controllers
{
    [RequirePageAccess("Management")]
    public class ManagementController : Controller
    {
        private readonly CenterContext _db;
        public ManagementController(CenterContext db)
        {
            _db = db;
        }

        // Helper: True if the logged-in user's group root code == 1 (superuser)
        private bool IsSuperUser()
        {
            string username = User.Identity.Name;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return false;
            var group = _db.Groups.FirstOrDefault(g => g.GroupCode == user.GroupCode);
            if (group == null) return false;
            var root = _db.Roots.FirstOrDefault(r => r.RootCode == group.RootCode);
            if (root == null) return false;
            return root.RootCode == 1;
        }

        private int GetUserRootCode()
        {
            string username = User.Identity.Name;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return -1;
            var group = _db.Groups.FirstOrDefault(g => g.GroupCode == user.GroupCode);
            if (group == null) return -1;
            return group.RootCode;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult GetUserRootInfo()
        {
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
                user_root_code = root.RootCode, // for JS logic
                root_code = root.RootCode,      // selected root code (for regular users it's always their own)
                root_name = root.RootName,
                iscenter = root.IsCenter
            });
        }

        [HttpGet]
        public IActionResult GetRoots()
        {
            var roots = _db.Roots
                .Select(r => new { rootCode = r.RootCode, rootName = r.RootName, isCenter = r.IsCenter })
                .ToList();
            return Json(roots);
        }

        [HttpGet]
        public IActionResult GetCentersByRoot(int rootCode)
        {
            var centers = _db.Centers
                .Where(c => c.RootCode == rootCode)
                .Select(c => new
                {
                    centerCode = c.CenterCode,
                    centerName = c.CenterName,
                    centerPhone = c.CenterPhone,
                    centerAddress = c.CenterAddress,
                    isActive = c.IsActive
                })
                .ToList();
            return Json(centers);
        }

        [HttpGet]
        public IActionResult GetBranchesByRoot(int rootCode)
        {
            var branches = _db.Branches
                .Where(b => b.RootCode == rootCode)
                .Select(b => new
                {
                    branchCode = b.BranchCode,
                    branchName = b.BranchName,
                    address = b.Address,
                    phone = b.Phone,
                    startTime = b.StartTime,
                    isActive = b.IsActive
                })
                .ToList();
            return Json(branches);
        }

        [HttpGet]
        public IActionResult GetBranchesByCenter(int centerCode)
        {
            var branches = _db.Branches
                .Where(b => b.CenterCode == centerCode)
                .Select(b => new
                {
                    branchCode = b.BranchCode,
                    branchName = b.BranchName,
                    address = b.Address,
                    phone = b.Phone,
                    startTime = b.StartTime,
                    isActive = b.IsActive
                }).ToList();
            return Json(branches);
        }

        [HttpGet]
        public IActionResult GetBranchesForRootWithCenters(int rootCode)
        {
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
        public IActionResult GetTeachersByRoot(int rootCode)
        {
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

        [HttpPost]
        public IActionResult AddTeacher([FromBody] TeacherInputModel teacher)
        {
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
        public IActionResult DeleteTeacher([FromBody] int teacherCode)
        {
            var teacher = _db.Teachers.FirstOrDefault(t => t.TeacherCode == teacherCode);
            if (teacher == null) return BadRequest("Teacher not found.");
            var teaches = _db.Teaches.Where(t => t.TeacherCode == teacherCode).ToList();
            _db.Teaches.RemoveRange(teaches);
            _db.Teachers.Remove(teacher);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Teacher and associated teaching subjects deleted successfully." });
        }

        [HttpPost]
        public IActionResult AddCenter([FromBody] CenterInputModel center)
        {
            if (center == null) return BadRequest("Invalid data.");
            var user = _db.Users.FirstOrDefault(u => u.UserCode == center.InsertUser);
            if (user == null) return BadRequest("User not found.");
            var root = _db.Roots.FirstOrDefault(r => r.RootCode == center.RootCode);
            if (root == null) return BadRequest("Root not found.");

            if (!IsSuperUser())
            {
                return Forbid("You do not have permission to add centers.");
            }

            var newCenter = new Center
            {
                CenterName = center.CenterName,
                IsActive = true,
                CenterPhone = center.CenterPhone,
                CenterAddress = center.CenterAddress,
                OwnerName = root.IsCenter ? root.RootOwner : null,
                RootCode = center.RootCode,
                InsertUser = center.InsertUser,
                InsertTime = DateTime.Now
            };

            _db.Centers.Add(newCenter);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Center added successfully." });
        }

        [HttpPost]
        public IActionResult AddBranch([FromBody] BranchInputModel branch)
        {
            if (branch == null) return BadRequest("Invalid data.");
            var user = _db.Users.FirstOrDefault(u => u.UserCode == branch.InsertUser);
            if (user == null) return BadRequest("User not found.");
            var center = _db.Centers.FirstOrDefault(c => c.CenterCode == branch.CenterCode && c.RootCode == branch.RootCode);
            if (center == null) return BadRequest("Center not found for this root.");

            if (!IsSuperUser())
            {
                return Forbid("You do not have permission to add branches.");
            }

            var newBranch = new Branch
            {
                BranchName = branch.BranchName,
                Address = branch.Address,
                Phone = branch.Phone,
                StartTime = branch.StartTime,
                CenterCode = branch.CenterCode,
                InsertUser = branch.InsertUser,
                InsertTime = DateTime.Now,
                IsActive = true,
                RootCode = branch.RootCode
            };

            _db.Branches.Add(newBranch);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Branch added successfully." });
        }

        [HttpPost]
        public IActionResult EditCenter([FromBody] CenterEditModel model)
        {
            var center = _db.Centers.FirstOrDefault(c => c.CenterCode == model.CenterCode);
            if (center == null) return BadRequest("Center not found.");

            if (!IsSuperUser())
            {
                return Forbid("You do not have permission to edit centers.");
            }

            center.CenterName = model.CenterName;
            center.CenterPhone = model.CenterPhone;
            center.CenterAddress = model.CenterAddress;
            _db.SaveChanges();
            return Ok(new { success = true, message = "Center updated successfully." });
        }

        [HttpPost]
        public IActionResult DeleteCenter([FromBody] int centerCode)
        {
            var center = _db.Centers.FirstOrDefault(c => c.CenterCode == centerCode);
            if (center == null) return BadRequest("Center not found.");

            if (!IsSuperUser())
            {
                return Forbid("You do not have permission to delete centers.");
            }

            _db.Centers.Remove(center);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Center deleted successfully." });
        }

        [HttpPost]
        public IActionResult EditBranch([FromBody] BranchEditModel model)
        {
            var branch = _db.Branches.FirstOrDefault(b => b.BranchCode == model.BranchCode);
            if (branch == null) return BadRequest("Branch not found.");

            if (!IsSuperUser())
            {
                return Forbid("You do not have permission to edit branches.");
            }

            branch.BranchName = model.BranchName;
            branch.Address = model.Address;
            branch.Phone = model.Phone;
            branch.StartTime = model.StartTime;
            _db.SaveChanges();
            return Ok(new { success = true, message = "Branch updated successfully." });
        }

        [HttpPost]
        public IActionResult DeleteBranch([FromBody] int branchCode)
        {
            var branch = _db.Branches.FirstOrDefault(b => b.BranchCode == branchCode);
            if (branch == null) return BadRequest("Branch not found.");

            if (!IsSuperUser())
            {
                return Forbid("You do not have permission to delete branches.");
            }

            _db.Branches.Remove(branch);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Branch deleted successfully." });
        }

        // ----------- TEACHING SUBJECTS LOGIC ----------------

        [HttpGet]
        public IActionResult GetYearsByRoot(int rootCode)
        {
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
            var activeEduYear = _db.EduYears.FirstOrDefault(e => e.RootCode == rootCode && e.IsActive);
            if (activeEduYear == null)
                return Json(new { });

            var year = _db.Years.FirstOrDefault(y => y.EduYearCode == activeEduYear.EduCode);

            return Json(new
            {
                eduYearCode = activeEduYear.EduCode,
                yearCode = year?.YearCode,
                yearName = year?.YearName,
                eduYearName = activeEduYear.EduCode
            });
        }

        [HttpGet]
        public IActionResult GetSubjectsByTeacher(int teacherCode, int rootCode)
        {
            var teachRecords = (from t in _db.Teaches
                                join s in _db.Subjects on t.SubjectCode equals s.SubjectCode
                                where t.TeacherCode == teacherCode && t.RootCode == rootCode
                                select new
                                {
                                    teacherCode = t.TeacherCode,
                                    subjectCode = t.SubjectCode,
                                    subjectName = s.SubjectName
                                }).ToList();

            return Json(teachRecords);
        }

        [HttpPost]
        public IActionResult AddTeachingSubject([FromBody] AddTeachingSubjectInputModel model)
        {
            var year = _db.Years.FirstOrDefault(y => y.YearCode == model.YearCode);
            if (year == null)
                return BadRequest("Year not found!");

            if (year.EduYearCode == null)
                return BadRequest("Year does not have a valid EduYearCode.");

            var subject = new Subject
            {
                SubjectName = model.SubjectName,
                IsPrimary = model.IsPrimary,
                RootCode = model.RootCode,
                YearCode = model.YearCode,
                InsertUser = model.InsertUser,
                InsertTime = DateTime.Now
            };
            _db.Subjects.Add(subject);
            _db.SaveChanges();

            var eduYear = _db.EduYears.FirstOrDefault(e => e.EduCode == year.EduYearCode.Value && e.RootCode == model.RootCode);
            if (eduYear == null)
                return BadRequest("Education year not found for this year and root.");

            var teach = new Teach
            {
                SubjectCode = subject.SubjectCode,
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
            var teach = _db.Teaches.FirstOrDefault(t => t.TeacherCode == model.TeacherCode && t.SubjectCode == model.SubjectCode);
            if (teach == null) return BadRequest("Teach record not found.");
            _db.Teaches.Remove(teach);
            _db.SaveChanges();
            return Ok(new { success = true, message = "Teaching subject deleted successfully." });
        }

        // ----------- MODELS ---------------

        public class BranchInputModel
        {
            public string BranchName { get; set; }
            public string Address { get; set; }
            public string Phone { get; set; }
            public DateOnly StartTime { get; set; }
            public int CenterCode { get; set; }
            public int InsertUser { get; set; }
            public int RootCode { get; set; }
            public bool IsActive { get; set; } = true;
        }

        public class BranchEditModel
        {
            public int BranchCode { get; set; }
            public string BranchName { get; set; }
            public string Address { get; set; }
            public string Phone { get; set; }
            public DateOnly StartTime { get; set; }
        }

        public class CenterInputModel
        {
            public string CenterName { get; set; }
            public string CenterPhone { get; set; }
            public string? CenterAddress { get; set; }
            public int InsertUser { get; set; }
            public int RootCode { get; set; }
        }

        public class CenterEditModel
        {
            public int CenterCode { get; set; }
            public string CenterName { get; set; }
            public string CenterPhone { get; set; }
            public string? CenterAddress { get; set; }
        }

        public class TeacherInputModel
        {
            public string TeacherName { get; set; } = null!;
            public string TeacherPhone { get; set; } = null!;
            public string? TeacherAddress { get; set; }
            public int RootCode { get; set; }
            public int InsertUser { get; set; }
        }

        public class AddTeachingSubjectInputModel
        {
            public string SubjectName { get; set; }
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