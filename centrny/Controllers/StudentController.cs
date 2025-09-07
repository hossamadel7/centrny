using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using centrny.Attributes;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace centrny.Controllers
{
    public class StudentController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<StudentController> _logger;

        public StudentController(CenterContext context, ILogger<StudentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Session/context helpers
        private int GetSessionInt(string key) => (int)HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int userCode, int groupCode, int rootCode, string username) GetSessionContext() =>
            (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                _context.Roots.FirstOrDefault(x => x.RootDomain == HttpContext.Request.Host.Host.Replace("www.", ""))?.RootCode ?? 0,
                GetSessionString("Username")
            );
        private int GetRootCode() =>
            _context.Roots.FirstOrDefault(x => x.RootDomain == HttpContext.Request.Host.Host.Replace("www.", ""))?.RootCode ?? 0;
        // ==================== REGISTRATION METHODS ====================

        //[HttpGet]
        //[Route("Student/Register/{item_key}")]
        //public async Task<IActionResult> Register(string item_key)
        //{
        //    if (string.IsNullOrWhiteSpace(item_key))
        //        return NotFound("Item key is required.");

        //    var item = await GetItemWithNavigation(item_key, true);
        //    if (item == null)
        //        return NotFound("Item not found.");

        //    var viewModel = await BuildStudentRegistrationViewModel(item);
        //    return View(viewModel);
        //}

        //[HttpPost]
        //[Route("Student/Register")]
        //public async Task<IActionResult> Register([FromBody] StudentRegistrationRequest request)
        //{
        //    using var transaction = await _context.Database.BeginTransactionAsync();
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //            return Json(new { success = false, errors = GetModelErrors() });

        //        var item = await GetItemWithNavigation(request.ItemKey, true);
        //        if (item == null)
        //            return Json(new { success = false, error = "Invalid item key." });

        //        Student student;
        //        bool isUpdate = item.StudentCode.HasValue && item.StudentCodeNavigation != null;

        //        if (isUpdate)
        //        {
        //            student = item.StudentCodeNavigation;
        //            UpdateStudentWithRequest(student, request);
        //        }
        //        else
        //        {
        //            student = CreateStudentFromRequest(request, item.RootCode);
        //            _context.Students.Add(student);
        //        }

        //        await ValidateForeignKeyReferences(request, item);
        //        await _context.SaveChangesAsync();

        //        if (!isUpdate)
        //        {
        //            item.StudentCode = student.StudentCode;
        //            item.LastUpdateUser = 1;
        //            item.LastUpdateTime = DateTime.Now;
        //        }

        //        if (request.SelectedSubjects?.Any() == true)
        //            await CreateLearnRecords(request, student);

        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        return Json(new
        //        {
        //            success = true,
        //            message = isUpdate ? "Academic enrollment updated successfully!" : "Registration successful! Welcome.",
        //            redirectUrl = $"/Student/{request.ItemKey}"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        return Json(new { success = false, error = GetDetailedEntityError(ex) });
        //    }
        //}
        [HttpGet]
        [Route("Register/{root_code:int}")]
        public async Task<IActionResult> PublicRegister(int root_code)
        {
            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == root_code && r.IsActive);
            if (root == null)
                return NotFound("Registration center not found or inactive.");

            var viewModel = await BuildPublicRegistrationViewModel(root_code, root.RootName);
            return View(viewModel);
        }

        [HttpPost]
        [Route("Register/{root_code:int}")]
        public async Task<IActionResult> PublicRegister(int root_code, [FromBody] StudentRegistrationRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Optionally set RootCode if needed
                // request.RootCode = root_code;

                if (!ModelState.IsValid)
                    return View(await BuildPublicRegistrationViewModel(root_code));

                var rootEntity = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == root_code && r.IsActive);
                if (rootEntity == null)
                    return View(await BuildPublicRegistrationViewModel(root_code));

                if (!await _context.Branches.AnyAsync(b => b.BranchCode == request.BranchCode && b.RootCode == root_code && b.IsActive))
                    return View(await BuildPublicRegistrationViewModel(root_code));

                if (request.YearCode.HasValue && !await _context.Years.AnyAsync(y => y.YearCode == request.YearCode.Value))
                    return View(await BuildPublicRegistrationViewModel(root_code));

                var student = CreateStudentFromRequest(request, root_code);
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Registration successful! Welcome {student.StudentName}.";
                TempData["StudentCode"] = student.StudentCode;
                TempData["RootName"] = rootEntity.RootName;

                return RedirectToAction("PublicRegisterSuccess", new { root_code });
            }
            catch
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Registration failed. Please try again or contact support.");
                return View(await BuildPublicRegistrationViewModel(root_code));
            }
        }

        // This function creates a Student from the request (for both public and internal registration)
        private Student CreateStudentFromRequest(StudentRegistrationRequest request, int rootCode)
        {
            return new Student
            {
                StudentName = request.StudentName?.Trim(),
                StudentPhone = request.StudentPhone?.Trim(),
                StudentFatherPhone = request.StudentFatherPhone?.Trim(),
                StudentMotherPhone = request.StudentMotherPhone?.Trim(),
                StudentFatherJob = request.StudentFatherJob?.Trim(),
                StudentMotherJob = request.StudentMotherJob?.Trim(),
                StudentBirthdate = request.BirthDate,
                StudentGender = request.Gender,
                BranchCode = request.BranchCode ?? 0,
                YearCode = request.YearCode,
                RootCode = rootCode,
                IsActive = true,
                InsertUser = 1,
                InsertTime = DateTime.Now,
                SubscribtionTime = DateOnly.FromDateTime(DateTime.Today)

            };
        }

        [HttpGet]
        [Route("Register/{root_code:int}/Success")]
        public IActionResult PublicRegisterSuccess(int root_code)
        {
            var successMessage = TempData["SuccessMessage"] as string;
            var studentCode = TempData["StudentCode"] as int?;
            var rootName = TempData["RootName"] as string;

            if (string.IsNullOrEmpty(successMessage))
                return RedirectToAction("PublicRegister", new { root_code });

            ViewBag.SuccessMessage = successMessage;
            ViewBag.StudentCode = studentCode;
            ViewBag.RootName = rootName ?? "Registration Center";
            ViewBag.RootCode = root_code;

            return View();
        }

        // ==================== REGISTRATION HELPER METHODS ====================

        private async Task<Item> GetItemWithNavigation(string itemKey, bool includeStudent)
        {
            IQueryable<Item> query = _context.Items
                .Include(i => i.RootCodeNavigation);

            if (includeStudent)
                query = query.Include(i => i.StudentCodeNavigation);

            return await query.FirstOrDefaultAsync(i => i.ItemKey == itemKey && i.IsActive);
        }
        private async Task<StudentRegistrationViewModel> BuildStudentRegistrationViewModel(Item item)
        {
            var availableEduYears = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == item.RootCode)
                .Select(e => new SelectListItem { Value = e.EduCode.ToString(), Text = e.EduName })
                .ToListAsync();

            var availableBranches = await _context.Branches
                .Where(b => b.RootCode == item.RootCode && b.IsActive)
                .Select(b => new SelectListItem { Value = b.BranchCode.ToString(), Text = b.BranchName })
                .ToListAsync();

            int? selectedEduYearCode = availableEduYears.Count > 0
                ? int.Parse(availableEduYears[0].Value)
                : (int?)null;

            var availableYears = selectedEduYearCode.HasValue
                ? await _context.Years
                    .Where(y => y.RootCode == selectedEduYearCode)
                    .Select(y => new SelectListItem { Value = y.YearCode.ToString(), Text = y.YearName })
                    .ToListAsync()
                : new List<SelectListItem>();

            var viewModel = new StudentRegistrationViewModel
            {
                ItemKey = item.ItemKey,
                RootName = item.RootCodeNavigation?.RootName ?? "Unknown",
                AvailableBranches = availableBranches,
                AvailableYears = availableYears,
                AvailableEduYears = availableEduYears,
                HasExistingStudent = item.StudentCode.HasValue && item.StudentCodeNavigation != null
            };

            if (viewModel.HasExistingStudent && item.StudentCodeNavigation != null)
            {
                var student = item.StudentCodeNavigation;
                viewModel.ExistingStudentName = student.StudentName;
                viewModel.ExistingStudentPhone = student.StudentPhone;
                viewModel.ExistingStudentParentPhone = student.StudentFatherPhone;
                viewModel.ExistingBirthDate = student.StudentBirthdate;
                viewModel.ExistingGender = student.StudentGender;
                viewModel.ExistingBranchCode = student.BranchCode;
                viewModel.ExistingYearCode = student.YearCode;
            }

            return viewModel;
        }

        private async Task<PublicRegistrationViewModel> BuildPublicRegistrationViewModel(int root_code, string rootName = null)
        {
            var availableBranches = await _context.Branches
                .Where(b => b.RootCode == root_code && b.IsActive)
                .Select(b => new SelectListItem { Value = b.BranchCode.ToString(), Text = b.BranchName })
                .ToListAsync();

            var activeEduYear = await _context.EduYears
                .Where(e => e.RootCode == root_code && e.IsActive)
                .OrderByDescending(e => e.EduCode)
                .FirstOrDefaultAsync();

            var availableYears = activeEduYear != null
                ? await _context.Years
                    .Where(y => y.RootCode == root_code)
                    .Select(y => new SelectListItem { Value = y.YearCode.ToString(), Text = y.YearName })
                    .ToListAsync()
                : new List<SelectListItem>();

            // Add this:
            var availableEduYears = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == root_code)
                .Select(e => new SelectListItem { Value = e.EduCode.ToString(), Text = e.EduName })
                .ToListAsync();

            return new PublicRegistrationViewModel
            {
                RootCode = root_code,
                RootName = rootName ?? "",
                AvailableBranches = availableBranches,
                AvailableYears = availableYears,
                AvailableEduYears = availableEduYears // <-- set here
            };
        }


        private Student CreateStudentFromRequest(PublicRegistrationRequest request, int rootCode)
        {
            return new Student
            {
                StudentName = request.StudentName?.Trim(),
                StudentPhone = request.StudentPhone?.Trim(),
                StudentFatherPhone = request.StudentFatherPhone?.Trim(),
                StudentMotherPhone = request.StudentMotherPhone?.Trim(),
                StudentFatherJob = request.StudentFatherJob?.Trim(),
                StudentMotherJob = request.StudentMotherJob?.Trim(),
                StudentBirthdate = request.BirthDate,
                StudentGender = request.Gender,
                BranchCode = request.BranchCode ?? 0,
                YearCode = request.YearCode,
                RootCode = rootCode,
                IsActive = true,
                InsertUser = 1,
                InsertTime = DateTime.Now,
                SubscribtionTime = DateOnly.FromDateTime(DateTime.Today),
               
            };
        }


        private void UpdateStudentWithRequest(Student student, StudentRegistrationRequest request)
        {
            student.StudentName = request.StudentName?.Trim();
            student.StudentPhone = request.StudentPhone?.Trim();
            student.StudentFatherPhone = request.StudentFatherPhone?.Trim();
            student.StudentBirthdate = request.BirthDate;
            student.StudentGender = request.Gender;
            student.BranchCode = request.BranchCode ?? 0;
            student.YearCode = request.YearCode;
            student.LastInsertUser = 1;
            student.LastInsertTime = DateTime.Now;
        }

        private List<string> GetModelErrors()
        {
            return ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
        }
        // ==================== ATTENDANCE METHODS ====================

        [HttpPost]
        [Route("Student/CheckAttendancePassword")]
        public async Task<IActionResult> CheckAttendancePassword([FromBody] AttendancePasswordRequest req)
        {
            var allowedGroups = new[] { "Admins", "Attendance Officers" };

            var users = await _context.Users
                .Join(_context.Groups, u => u.GroupCode, g => g.GroupCode,
                    (u, g) => new { u, g })
                .Where(joined => joined.g.BranchCode == req.BranchCode && allowedGroups.Contains(joined.g.GroupName))
                .Select(joined => new { joined.u.UserCode, joined.u.Password, joined.u.Username })
                .ToListAsync();

            foreach (var user in users)
            {
                if (user.Password == req.Password)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim("UserId", user.UserCode.ToString())
                    };
                    var identity = new ClaimsIdentity(claims, "AttendancePassword");
                    var principal = new ClaimsPrincipal(identity);
                    await HttpContext.SignInAsync(principal);

                    return Json(new
                    {
                        success = true,
                        username = user.Username,
                        userCode = user.UserCode,
                        message = "User authorized and logged in. You may now mark attendance."
                    });
                }
            }

            return Json(new
            {
                success = false,
                error = "Wrong password or not authorized for this branch."
            });
        }

        [HttpPost]
        [Route("Student/MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (request == null || string.IsNullOrEmpty(request.ItemKey))
                return Json(new { success = false, error = "Invalid request." });

            var student = await GetStudentByItemKey(request.ItemKey);
            if (student == null)
                return Json(new { success = false, error = "Student not found." });

            var classEntity = await _context.Classes
                .Include(c => c.ScheduleCodeNavigation)
                .Include(c => c.HallCodeNavigation)
                .Include(c => c.TeacherCodeNavigation)
                .FirstOrDefaultAsync(c => c.ClassCode == request.ClassCode);

            if (classEntity == null)
                return Json(new { success = false, error = "Class not found." });

            if (!await IsCurrentUserAdmin(student.RootCode, classEntity.BranchCode))
                return Json(new { success = false, error = "Only administrators can mark attendance." });

            if (await _context.Attends.AnyAsync(a => a.StudentId == student.StudentCode && a.ClassId == request.ClassCode))
                return Json(new { success = false, error = "Student has already been marked as attended for this class." });

            int discountTypeCode = await GetDiscountTypeCode();
            decimal sessionPriceToSave = classEntity.ClassPrice ?? 0;

            if (request.AttendanceType == discountTypeCode && request.SessionPrice.HasValue && request.SessionPrice.Value > 0)
                sessionPriceToSave = request.SessionPrice.Value;

            var attendance = new Attend
            {
                TeacherCode = classEntity.TeacherCode,
                ScheduleCode = classEntity.ScheduleCode,
                ClassId = request.ClassCode,
                HallId = (int)classEntity.HallCode,
                StudentId = student.StudentCode,
                AttendDate = DateTime.Now,
                SessionPrice = sessionPriceToSave,
                RootCode = student.RootCode,
                Type = (int)request.AttendanceType
            };

            _context.Attends.Add(attendance);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Attendance marked successfully.",
                attendanceDate = attendance.AttendDate.ToString("yyyy-MM-dd HH:mm")
            });
        }

        [HttpGet]
        [Route("Student/GetAttendanceTypes")]
        public async Task<IActionResult> GetAttendanceTypes()
        {
            var attendanceTypes = await _context.Lockups
                .Where(l => l.PaymentCode > 0 && !string.IsNullOrEmpty(l.PaymentName))
                .Select(l => new { value = l.PaymentCode, text = l.PaymentName })
                .ToListAsync();

            return Json(attendanceTypes);
        }

        // Helper for discount type code
        private async Task<int> GetDiscountTypeCode()
        {
            var discountType = await _context.Lockups.FirstOrDefaultAsync(l =>
                l.PaymentName != null &&
                (l.PaymentName.ToLower().Contains("discount") || l.PaymentName.Contains("خصم")));
            return discountType?.PaymentCode ?? 0;
        }
        // ==================== STUDENT SEARCH AND LINKING METHODS ====================

        [HttpGet]
        [Route("Student/Search/{item_key}")]
        public async Task<IActionResult> SearchStudent(string item_key)
        {
            if (string.IsNullOrWhiteSpace(item_key))
                return NotFound("Item key is required.");

            var item = await _context.Items
                .Include(i => i.RootCodeNavigation)
                .Where(i => i.ItemKey == item_key && i.IsActive && !i.StudentCode.HasValue)
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound("Item not found, already linked, or access denied.");

            var viewModel = new StudentSearchViewModel
            {
                ItemKey = item_key,
                RootName = item.RootCodeNavigation?.RootName ?? "Unknown",
                RootCode = item.RootCode
            };
            return View("StudentSearch", viewModel);
        }

        [HttpPost]
        [Route("Student/SearchByPhone")]
        public async Task<IActionResult> SearchByPhone([FromBody] StudentSearchRequest request)
        {
            var debugInfo = new List<string>();
            if (!ModelState.IsValid)
                return Json(new { success = false, errors = GetModelErrors(), debug = debugInfo });

            var item = await _context.Items
                .Where(i => i.ItemKey == request.ItemKey && i.IsActive && !i.StudentCode.HasValue)
                .FirstOrDefaultAsync();

            if (item == null)
                return Json(new { success = false, error = "Invalid item key or already linked.", debug = debugInfo });

            var students = await _context.Students
                .Where(s => s.StudentPhone == request.StudentPhone.Trim() && s.RootCode == item.RootCode && s.IsActive)
                .Include(s => s.BranchCodeNavigation)
                .Include(s => s.YearCodeNavigation)
                .Select(s => new
                {
                    StudentCode = s.StudentCode,
                    StudentName = s.StudentName,
                    StudentPhone = s.StudentPhone,
                    StudentParentPhone = s.StudentFatherPhone,
                    BirthDate = s.StudentBirthdate.ToString("yyyy-MM-dd"),
                    Gender = s.StudentGender.HasValue ? (s.StudentGender.Value ? "Male" : "Female") : "Not specified",
                    BranchName = s.BranchCodeNavigation != null ? s.BranchCodeNavigation.BranchName : "N/A",
                    YearName = s.YearCodeNavigation != null ? s.YearCodeNavigation.YearName : "N/A",
                    SubscriptionDate = s.SubscribtionTime.ToString("yyyy-MM-dd"),
                })
                .ToListAsync();

            return Json(new { success = true, students, debug = debugInfo });
        }

        [HttpGet]
        [Route("Student/Debug/PhoneSearch")]
        public async Task<IActionResult> DebugPhoneSearch(string itemKey = "", string phone = "")
        {
            var logs = new List<string>();
            var canConnect = await _context.Database.CanConnectAsync();
            logs.Add($"Database connectivity: {canConnect}");

            if (!canConnect)
            {
                return Json(new
                {
                    databaseConnectivity = false,
                    error = "Database connection failed",
                    logs
                });
            }
            if (string.IsNullOrEmpty(itemKey) || string.IsNullOrEmpty(phone))
            {
                logs.Add("No itemKey or phone provided - returning connectivity test only");
                return Json(new
                {
                    databaseConnectivity = true,
                    error = "Please provide itemKey and phone parameters",
                    logs
                });
            }
            var item = await _context.Items
                .Where(i => i.ItemKey == itemKey && i.IsActive && !i.StudentCode.HasValue)
                .FirstOrDefaultAsync();

            logs.Add($"Item search for key '{itemKey}': {(item != null ? "Found" : "Not found")}");

            var students = new List<object>();
            if (item != null)
            {
                var phoneToSearch = phone.Trim();
                logs.Add($"Searching students with phone '{phoneToSearch}' in root {item.RootCode}");

                var foundStudents = await _context.Students
                    .Where(s => s.StudentPhone == phoneToSearch && s.RootCode == item.RootCode && s.IsActive)
                    .Include(s => s.BranchCodeNavigation)
                    .Include(s => s.YearCodeNavigation)
                    .ToListAsync();

                students = foundStudents.Select(s => new
                {
                    s.StudentCode,
                    s.StudentName,
                    s.StudentPhone,
                    s.StudentFatherPhone,
                    BirthDate = s.StudentBirthdate.ToString("yyyy-MM-dd"),
                    Gender = s.StudentGender.HasValue ? (s.StudentGender.Value ? "Male" : "Female") : "Not specified",
                    BranchName = s.BranchCodeNavigation?.BranchName ?? "N/A",
                    YearName = s.YearCodeNavigation?.YearName ?? "N/A",
                    SubscriptionDate = s.SubscribtionTime.ToString("yyyy-MM-dd"),
                    Age = CalculateAge(s.StudentBirthdate)
                }).ToList<object>();
                logs.Add($"Found {foundStudents.Count} students");
            }

            return Json(new
            {
                databaseConnectivity = true,
                itemExists = item != null,
                studentCount = students.Count,
                students,
                logs
            });
        }

        [HttpPost]
        [Route("Student/LinkStudent")]
        public async Task<IActionResult> LinkStudent([FromBody] LinkStudentRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            if (!ModelState.IsValid)
                return Json(new { success = false, errors = GetModelErrors() });

            var item = await _context.Items
                .Where(i => i.ItemKey == request.ItemKey && i.IsActive && !i.StudentCode.HasValue)
                .FirstOrDefaultAsync();

            if (item == null)
                return Json(new { success = false, error = "Invalid item key or already linked." });

            var student = await _context.Students
                .Where(s => s.StudentCode == request.StudentCode && s.RootCode == item.RootCode && s.IsActive)
                .FirstOrDefaultAsync();

            if (student == null)
                return Json(new { success = false, error = "Student not found or not available for linking." });

            if (await _context.Items.AnyAsync(i => i.StudentCode == student.StudentCode && i.IsActive))
                return Json(new { success = false, error = "Student is already linked to another item." });

            item.StudentCode = student.StudentCode;
            item.LastUpdateUser = 1;
            item.LastUpdateTime = DateTime.Now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Json(new
            {
                success = true,
                message = "Student linked successfully! Redirecting to complete your academic enrollment.",
                redirectUrl = $"/Student/{request.ItemKey}"
            });
        }
        // ==================== EXISTING PROFILE METHODS ====================

        [HttpGet]
        [Route("Student/{item_key}")]
        public async Task<IActionResult> Profile(string item_key)
        {
            if (string.IsNullOrWhiteSpace(item_key))
                return NotFound("Item key is required.");

            var item = await _context.Items
                .Include(i => i.StudentCodeNavigation)
                    .ThenInclude(s => s.RootCodeNavigation)
                .Include(i => i.StudentCodeNavigation)
                    .ThenInclude(s => s.BranchCodeNavigation)
                .Include(i => i.StudentCodeNavigation)
                    .ThenInclude(s => s.YearCodeNavigation)
                        .ThenInclude(y => y.LevelCodeNavigation)
                .Where(i => i.ItemKey == item_key && i.IsActive)
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound("Student profile not found or access denied.");

            if (item.StudentCodeNavigation == null)
                return RedirectToAction("SearchStudent", new { item_key });

            var student = item.StudentCodeNavigation;
            var canMarkAttendance = await IsCurrentUserAdmin(student.RootCode, (int)student.BranchCode);

            var viewModel = new StudentProfileViewModel
            {
                ItemKey = item_key,
                StudentCode = student.StudentCode,
                StudentName = student.StudentName,
                StudentPhone = student.StudentPhone,
                StudentParentPhone = student.StudentFatherPhone,
                StudentBirthdate = student.StudentBirthdate,
                StudentGender = student.StudentGender,
                SubscriptionTime = student.SubscribtionTime,
                IsActive = student.IsActive,
                BranchName = student.BranchCodeNavigation?.BranchName,
                YearName = student.YearCodeNavigation?.YearName,
                LevelName = student.YearCodeNavigation?.LevelCodeNavigation?.LevelName,
                RootName = student.RootCodeNavigation?.RootName,
                RootCode = student.RootCode,
                YearCode = student.YearCode,
                Age = CalculateAge(student.StudentBirthdate),
                CanMarkAttendance = canMarkAttendance
            };

            return View(viewModel);
        }

        [HttpGet]
        [Route("Student/StudentData/{item_key}")]
        public async Task<IActionResult> StudentData(string item_key)
        {
            var item = await _context.Items
                .Include(i => i.StudentCodeNavigation)
                    .ThenInclude(s => s.RootCodeNavigation)
                .Include(i => i.StudentCodeNavigation)
                    .ThenInclude(s => s.BranchCodeNavigation)
                .Include(i => i.StudentCodeNavigation)
                    .ThenInclude(s => s.YearCodeNavigation)
                        .ThenInclude(y => y.LevelCodeNavigation)
                .Where(i => i.ItemKey == item_key && i.IsActive)
                .FirstOrDefaultAsync();

            if (item == null || item.StudentCodeNavigation == null)
                return NotFound("Student profile not found or access denied.");

            var student = item.StudentCodeNavigation;

            var viewModel = new StudentProfileViewModel
            {
                ItemKey = item_key,
                StudentCode = student.StudentCode,
                StudentName = student.StudentName,
                StudentPhone = student.StudentPhone,
                StudentParentPhone = student.StudentFatherPhone,
                StudentBirthdate = student.StudentBirthdate,
                StudentGender = student.StudentGender,
                SubscriptionTime = student.SubscribtionTime,
                IsActive = student.IsActive,
                BranchName = student.BranchCodeNavigation?.BranchName,
                YearName = student.YearCodeNavigation?.YearName,
                LevelName = student.YearCodeNavigation?.LevelCodeNavigation?.LevelName,
                RootName = student.RootCodeNavigation?.RootName,
                RootCode = student.RootCode,
                YearCode = student.YearCode,
                Age = CalculateAge(student.StudentBirthdate),
                CanMarkAttendance = await IsCurrentUserAdmin(student.RootCode,(int) student.BranchCode)
            };

            return View("StudentData", viewModel);
        }
        // ==================== CLASSES, SUBJECTS, SCHEDULES ====================

        [HttpGet]
        [Route("Student/GetYearsForEduYear")]
        public async Task<IActionResult> GetYearsForEduYear()
        {
            var rootCode = GetRootCode();
            var years = await _context.Years
                .Where(y => y.RootCode == rootCode)
                .Select(y => new { Value = y.YearCode, Text = y.YearName })
                .ToListAsync();
            return Json(years);
        }

        [HttpGet]
        [Route("Student/GetAvailableSubjects")]
        public async Task<IActionResult> GetAvailableSubjects(int branchCode, int? yearCode = null)
        {
            int rootCode = GetRootCode(); // Use the method you already have

            if (rootCode == 0)
                return Json(new { error = "Unable to determine root code from domain.", debugRootCode = rootCode });

            IQueryable<Teach> query = _context.Teaches
      .Where(t => t.RootCode == rootCode && t.IsActive);

            if (branchCode > 0) // Only filter by branch if branchCode is positive/non-null/non-empty
                query = query.Where(t => t.BranchCode == branchCode);

            if (yearCode.HasValue)
                query = query.Where(t => t.YearCode == yearCode.Value);

            query = query.Include(t => t.SubjectCodeNavigation);
            var subjects = await query
                .Select(t => new
                {
                    SubjectCode = t.SubjectCode,
                    SubjectName = t.SubjectCodeNavigation.SubjectName,
                    YearCode = t.YearCode
                })
                .Distinct()
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            return Json(subjects);
        }

        [HttpGet]
        [Route("Student/GetAvailableTeachers")]
        public async Task<IActionResult> GetAvailableTeachers(string subjectCodes, int branchCode, int? yearCode, int? eduYearCode)
        {
            int rootCode = GetRootCode();
            var subjectCodeList = subjectCodes
                .Split(',')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(int.Parse)
                .ToList();

            IQueryable<Teach> teachesQuery = _context.Teaches
                .Include(t => t.TeacherCodeNavigation)
                .Where(t =>
                    t.IsActive &&
                    t.RootCode == rootCode &&
                    t.BranchCode == branchCode &&
                    subjectCodeList.Contains(t.SubjectCode) &&
                    (!yearCode.HasValue || t.YearCode == yearCode.Value) &&
                    (!eduYearCode.HasValue || t.EduYearCode == eduYearCode.Value)
                );

            var teaches = await teachesQuery
                .Select(t => new
                {

                    TeacherCode = t.TeacherCode,
                    TeacherName = t.TeacherCodeNavigation.TeacherName,
                    TeacherPhone = t.TeacherCodeNavigation.TeacherPhone,
                    SubjectCode = t.SubjectCode,
                    YearCode = t.YearCode,
                    EduYearCode = t.EduYearCode
                })
                .OrderBy(t => t.TeacherName)
                .ToListAsync();

            if (teaches.Any())
                return Json(teaches);

            return Json(new { error = "No teachers are currently assigned to teach these subjects at this branch/year/eduyear." });
        }

        [HttpGet]
        [Route("Student/GetAvailableSchedules")]
        public async Task<IActionResult> GetAvailableSchedules(string subjectCodes, string teacherCodes, int branchCode, int? yearCode = null)
        {
            int rootCode = GetRootCode();
            var subjectCodeList = subjectCodes.Split(',').Select(int.Parse).ToList();
            var teacherCodeList = teacherCodes.Split(',').Select(int.Parse).ToList();

            var currentEduYear = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == rootCode)
                .OrderByDescending(e => e.EduCode)
                .FirstOrDefaultAsync();
            if (currentEduYear == null)
                return Json(new { error = "No active education year found." });

            var schedules = await _context.Schedules
                .Include(s => s.SubjectCodeNavigation)
                .Include(s => s.TeacherCodeNavigation)
                .Include(s => s.HallCodeNavigation)
                .Where(s => subjectCodeList.Contains(s.SubjectCode.Value)
                    && teacherCodeList.Contains(s.TeacherCode.Value)
                    && s.BranchCode == branchCode
                    && s.RootCode == rootCode
                    && (yearCode == null || s.YearCode == yearCode)
                    && s.EduYearCode == currentEduYear.EduCode)
                .Select(s => new
                {
                    ScheduleCode = s.ScheduleCode,
                    SubjectCode = s.SubjectCode,
                    SubjectName = s.SubjectCodeNavigation.SubjectName,
                    TeacherCode = s.TeacherCode,
                    TeacherName = s.TeacherCodeNavigation.TeacherName,
                    HallCode = s.HallCode,
                    HallName = s.HallCodeNavigation.HallName,
                    DayName = s.DayOfWeek ?? "N/A",
                    StartTime = s.StartTime.HasValue ? s.StartTime.Value.ToString("HH:mm") : "N/A",
                    EndTime = s.EndTime.HasValue ? s.EndTime.Value.ToString("HH:mm") : "N/A",
                    YearCode = s.YearCode,
                    BranchCode = s.BranchCode,
                })
                .ToListAsync();

            return Json(schedules);
        }

        [HttpGet]
        [Route("Student/GetUpcomingClasses/{item_key}")]
        public async Task<IActionResult> GetUpcomingClasses(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            var currentTime = DateTime.Now;
            var today = DateOnly.FromDateTime(currentTime);

            var todaysClasses = await _context.Classes
                .Where(c => c.ClassDate.HasValue && c.ClassDate == today && c.ClassStartTime.HasValue && c.ClassEndTime.HasValue)
                .Include(c => c.SubjectCodeNavigation)
                .Include(c => c.TeacherCodeNavigation)
                .Include(c => c.HallCodeNavigation)
                .Include(c => c.BranchCodeNavigation)
                .Include(c => c.ScheduleCodeNavigation)
                .Where(c => _context.Learns.Any(l => l.StudentCode == student.StudentCode
                    && l.SubjectCode == c.SubjectCode
                    && l.TeacherCode == c.TeacherCode
                    && l.IsActive))
                .ToListAsync();

            var availableClasses = todaysClasses
                .Where(c =>
                {
                    var classStartTime = c.ClassStartTime.Value.ToTimeSpan();
                    var classEndTime = c.ClassEndTime.Value.ToTimeSpan();
                    var currentTimeSpan = currentTime.TimeOfDay;
                    var availableFromTime = classStartTime.Subtract(TimeSpan.FromHours(1));
                    return currentTimeSpan >= availableFromTime && currentTimeSpan <= classEndTime;
                })
                .OrderBy(c => c.ClassStartTime)
                .ToList();

            var isAdmin = await IsCurrentUserAdmin(student.RootCode, (int)student.BranchCode);

            var learns = await _context.Learns
                .Where(l => l.StudentCode == student.StudentCode && l.IsActive)
                .ToListAsync();

            var classResults = new List<object>();
            foreach (var c in availableClasses)
            {
                var hasAttended = await _context.Attends.AnyAsync(a => a.StudentId == student.StudentCode && a.ClassId == c.ClassCode);
                int? assignedScheduleCode = learns
                    .FirstOrDefault(l => l.SubjectCode == c.SubjectCode && l.TeacherCode == c.TeacherCode && l.YearCode == student.YearCode)?.ScheduleCode;

                classResults.Add(new
                {
                    classCode = c.ClassCode,
                    className = c.ClassName,
                    subjectCode = c.SubjectCode,
                    subjectName = c.SubjectCodeNavigation?.SubjectName ?? "N/A",
                    teacherCode = c.TeacherCode,
                    teacherName = c.TeacherCodeNavigation?.TeacherName ?? "N/A",
                    hallName = c.HallCodeNavigation?.HallName ?? "N/A",
                    branchCode = c.BranchCode,
                    branchName = c.BranchCodeNavigation?.BranchName ?? "N/A",
                    yearCode = student.YearCode,
                    rootCode = student.RootCode,
                    startTime = c.ClassStartTime?.ToString("HH:mm") ?? "N/A",
                    endTime = c.ClassEndTime?.ToString("HH:mm") ?? "N/A",
                    scheduleCode = c.ScheduleCode,
                    assignedScheduleCode,
                    hallCode = c.HallCode,
                    classPrice = c.ClassPrice,
                    canAttend = !hasAttended,
                    canMarkAttendance = isAdmin
                });
            }

            return Json(classResults);
        }

        [HttpGet]
        [Route("Student/GetWeeklyClasses/{item_key}")]
        public async Task<IActionResult> GetWeeklyClasses(string item_key, int subjectCode, int teacherCode, int yearCode)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            var assignedLearn = await _context.Learns
                .FirstOrDefaultAsync(l => l.StudentCode == student.StudentCode
                    && l.SubjectCode == subjectCode
                    && l.TeacherCode == teacherCode
                    && l.YearCode == yearCode
                    && l.IsActive);

            int? assignedScheduleCode = assignedLearn?.ScheduleCode;
            if (assignedLearn == null)
                return Json(new { error = "Student does not have access to this subject/teacher combination." });

            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(6);

            var startDate = DateOnly.FromDateTime(startOfWeek);
            var endDate = DateOnly.FromDateTime(endOfWeek);

            var weeklyClasses = await _context.Classes
                .Where(c => c.ClassDate.HasValue
                    && c.ClassDate >= startDate
                    && c.ClassDate <= endDate
                    && c.SubjectCode == subjectCode
                    && c.TeacherCode == teacherCode
                    && c.YearCode == yearCode
                    && c.ClassStartTime.HasValue
                    && c.ClassEndTime.HasValue)
                .Include(c => c.SubjectCodeNavigation)
                .Include(c => c.TeacherCodeNavigation)
                .Include(c => c.HallCodeNavigation)
                .Include(c => c.BranchCodeNavigation)
                .Include(c => c.ScheduleCodeNavigation)
                .OrderBy(c => c.ClassDate)
                .ThenBy(c => c.ClassStartTime)
                .ToListAsync();

            var isAdmin = await IsCurrentUserAdmin(student.RootCode, (int)student.BranchCode);

            var classResults = new List<object>();
            foreach (var c in weeklyClasses)
            {
                var hasAttended = await _context.Attends.AnyAsync(a => a.StudentId == student.StudentCode && a.ClassId == c.ClassCode);
                var isCurrentlyAvailable = false;
                if (c.ClassDate.HasValue && c.ClassDate == DateOnly.FromDateTime(DateTime.Today))
                {
                    var currentTime = DateTime.Now.TimeOfDay;
                    var classStartTime = c.ClassStartTime.Value.ToTimeSpan();
                    var classEndTime = c.ClassEndTime.Value.ToTimeSpan();
                    var availableFromTime = classStartTime.Subtract(TimeSpan.FromHours(1));
                    isCurrentlyAvailable = currentTime >= availableFromTime && currentTime <= classEndTime;
                }

                classResults.Add(new
                {
                    classCode = c.ClassCode,
                    className = c.ClassName,
                    subjectCode = c.SubjectCode,
                    subjectName = c.SubjectCodeNavigation?.SubjectName ?? "N/A",
                    teacherCode = c.TeacherCode,
                    teacherName = c.TeacherCodeNavigation?.TeacherName ?? "N/A",
                    hallName = c.HallCodeNavigation?.HallName ?? "N/A",
                    branchCode = c.BranchCode,
                    branchName = c.BranchCodeNavigation?.BranchName ?? "N/A",
                    yearCode = yearCode,
                    rootCode = student.RootCode,
                    classDate = c.ClassDate.HasValue ? c.ClassDate.Value.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd") : "N/A",
                    dayOfWeek = c.ClassDate.HasValue ? c.ClassDate.Value.ToDateTime(TimeOnly.MinValue).DayOfWeek.ToString() : "N/A",
                    startTime = c.ClassStartTime?.ToString("HH:mm") ?? "N/A",
                    endTime = c.ClassEndTime?.ToString("HH:mm") ?? "N/A",
                    scheduleCode = c.ScheduleCode,
                    assignedScheduleCode,
                    hallCode = c.HallCode,
                    totalAmount = c.TotalAmount,
                    isAttended = hasAttended,
                    isCurrentlyAvailable,
                    canAttend = !hasAttended && isCurrentlyAvailable,
                    canMarkAttendance = isAdmin
                });
            }

            return Json(classResults);
        }
        // ==================== EXAM, ASSIGNMENT, PLAN, STATS ====================

        [HttpGet]
        [Route("Student/GetStudentSubjects/{item_key}")]
        public async Task<IActionResult> GetStudentSubjects(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            var learns = await _context.Learns
                .Where(l => l.StudentCode == student.StudentCode && l.IsActive)
                .Include(l => l.SubjectCodeNavigation)
                .Include(l => l.TeacherCodeNavigation)
                .Include(l => l.YearCodeNavigation)
                .ToListAsync();

            var subjects = learns.Select(l => new
            {
                l.SubjectCode,
                SubjectName = l.SubjectCodeNavigation?.SubjectName,
                l.TeacherCode,
                TeacherName = l.TeacherCodeNavigation?.TeacherName,
                l.YearCode,
                YearName = l.YearCodeNavigation?.YearName,
                l.ScheduleCode
            }).ToList();

            return Json(subjects);
        }

        [HttpGet]
        [Route("Student/GetStudentPlans/{item_key}")]
        public async Task<IActionResult> GetStudentPlans(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            var plans = await _context.StudentPlans
                .Where(p => p.StudentCode == student.StudentCode)
                .OrderByDescending(p => p.InsertTime)
                .Select(p => new
                {
                    p.SubscriptionPlanCode,

                    p.SubDate,
                    p.Price,

                    p.InsertTime
                })
                .ToListAsync();

            return Json(plans);
        }

        [HttpGet]
        [Route("Student/GetStudentAttendance/{item_key}")]
        public async Task<IActionResult> GetStudentAttendance(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            var attendanceRecords = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Include(a => a.ClassId) // <-- Use the navigation property, not the int FK
                .OrderByDescending(a => a.AttendDate)
                .Select(a => new
                {
                    a.AttendDate,
                    a.SessionPrice,
                    a.Type,
                    a.ClassId,
                    ClassName = a.ClassId != null ? a.Class.ClassName : null
                })
                .ToListAsync();

            return Json(attendanceRecords);
        }

        [HttpGet]
        [Route("Student/GetStudentExams/{item_key}")]
        public async Task<IActionResult> GetStudentExams(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            // Join StudentExam with Exam, return only exams (ISExam == true)
            var exams = await _context.StudentExams
                .Where(se => se.StudentCode == student.StudentCode)
                .Join(_context.Exams,
                      se => se.ExamCode,
                      e => e.ExamCode,
                      (se, e) => new { StudentExam = se, Exam = e })
                .Where(joined => joined.Exam.IsExam == true)
                .OrderByDescending(joined => joined.StudentExam.InsertTime)
                .Select(joined => new
                {
                    joined.Exam.ExamCode,
                    ExamName = joined.Exam.ExamName,
                    joined.StudentExam.InsertTime,
                    joined.StudentExam.ExamDegree,
                    joined.StudentExam.StudentResult
                })
                .ToListAsync();

            return Json(exams);
        }

        [HttpGet]
        [Route("Student/GetStudentAssignments/{item_key}")]
        public async Task<IActionResult> GetStudentAssignments(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            // Join StudentExam with Exam, return only assignments (ISExam == false)
            var assignments = await _context.StudentExams
                .Where(se => se.StudentCode == student.StudentCode)
                .Join(_context.Exams,
                      se => se.ExamCode,
                      e => e.ExamCode,
                      (se, e) => new { StudentExam = se, Exam = e })
                .Where(joined => joined.Exam.IsExam == false)
                .OrderByDescending(joined => joined.StudentExam.InsertTime)
                .Select(joined => new
                {
                    joined.Exam.ExamCode,
                    AssignName = joined.Exam.ExamName,


                    joined.StudentExam.ExamDegree,
                    joined.StudentExam.StudentResult,
                    joined.StudentExam.IsActive,

                })
                .ToListAsync();

            return Json(assignments);
        }
        [HttpPost]
        [Route("Student/ValidatePin")]
        public async Task<IActionResult> ValidatePin([FromBody] PinValidationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Pin))
                return Json(new { valid = false, error = "PIN required" });

            // Add your real table name and entity here, e.g. OnlineRegistrationPins
            // Assume you have a DbSet<OnlineRegistrationPin> _context.OnlineRegistrationPins;

            var pinEntity = await _context.Pins
                .FirstOrDefaultAsync(p => p.Watermark == request.Pin && p.IsActive == 1); // and maybe not expired etc.

            if (pinEntity != null)
            {
                return Json(new { valid = true });
            }
            else
            {
                return Json(new { valid = false, error = "Invalid or expired PIN." });
            }
        }
        [HttpGet]
        [Route("Student/GetStudentStats/{item_key}")]
        public async Task<IActionResult> GetStudentStats(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            var totalAttendance = await _context.Attends.CountAsync(a => a.StudentId == student.StudentCode);
            var totalExams = await _context.StudentExams.CountAsync(e => e.StudentCode == student.StudentCode);
            var totalAssignments = await _context.StudentExams.CountAsync(a => a.StudentCode == student.StudentCode);

            return Json(new
            {
                totalAttendance,
                totalExams,
                totalAssignments
            });
        }
        // ==================== HELPER METHODS ====================

        private async Task<Student> GetStudentByItemKey(string itemKey)
        {
            var item = await _context.Items
                .Include(i => i.StudentCodeNavigation)
                .FirstOrDefaultAsync(i => i.ItemKey == itemKey && i.IsActive);
            return item?.StudentCodeNavigation;
        }

        private async Task ValidateForeignKeyReferences(StudentRegistrationRequest request, Item item)
        {
            if (!await _context.Branches.AnyAsync(b => b.BranchCode == request.BranchCode && b.RootCode == item.RootCode && b.IsActive))
                throw new Exception("Invalid branch code.");

            if (request.YearCode.HasValue && !await _context.Years.AnyAsync(y => y.YearCode == request.YearCode.Value))
                throw new Exception("Invalid year code.");
        }

        private async Task<bool> IsCurrentUserAdmin(int rootCode, int branchCode)
        {
            var username = GetSessionString("Username");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
            if (user == null) return false;

            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == user.GroupCode && g.BranchCode == branchCode);
            return group != null && (group.GroupName == "Admins" || group.GroupName == "Attendance Officers");
        }

        private async Task CreateLearnRecords(StudentRegistrationRequest request, Student student)
        {
            for (int i = 0; i < request.SelectedSubjects.Count; i++)
            {
                var learn = new Learn
                {
                    StudentCode = student.StudentCode,
                    SubjectCode = request.SelectedSubjects[i],
                    YearCode = request.YearCode,
                    EduYearCode = (int)request.EduYearCode, 
                    TeacherCode = request.SelectedTeachers?[i] ?? default,  // Only if you collect teacher per subject
                    ScheduleCode = request.SelectedSchedules?[i] ?? default, // Only for offline
                    BranchCode = request.BranchCode ?? 0,
                    RootCode = student.RootCode,
                    IsActive = true,
                    InsertUser = 1,
                    InsertTime = DateTime.Now
                };
                _context.Learns.Add(learn);
            }
        }

        private int CalculateAge(DateOnly birthdate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            int age = today.Year - birthdate.Year;
            if (birthdate > today.AddYears(-age)) age--;
            return age;
        }

        private string GetDetailedEntityError(Exception ex)
        {
            if (ex is DbUpdateException dbEx && dbEx.InnerException != null)
                return dbEx.InnerException.Message;
            return ex.Message;
        }
    }
}
// ==================== VIEWMODELS & REQUEST MODELS ====================
public class PinValidationRequest
{
    public string Pin { get; set; }
}

public class StudentRegistrationViewModel
{
    public string? ItemKey { get; set; }
    public string RootName { get; set; }
    public bool HasExistingStudent { get; set; }
    public string ExistingStudentName { get; set; }
    public string ExistingStudentPhone { get; set; }
    public string ExistingStudentParentPhone { get; set; }
    public DateOnly? ExistingBirthDate { get; set; }
    public bool? ExistingGender { get; set; }
    public int? ExistingBranchCode { get; set; }
    public int? ExistingYearCode { get; set; }
    public List<SelectListItem> AvailableBranches { get; set; }
    public List<SelectListItem> AvailableYears { get; set; }
    public List<SelectListItem> AvailableEduYears { get; set; }
}

public class StudentRegistrationRequest
{
    [Required]
    public string StudentName { get; set; }
    [Required]
    public string StudentPhone { get; set; }
    [Required]
    public string StudentFatherPhone { get; set; }
    [Required]
    public string StudentMotherPhone { get; set; }
    [Required]
    public string StudentFatherJob { get; set; }
    [Required]
    public string StudentMotherJob { get; set; }
    public DateOnly BirthDate { get; set; }
    public bool? Gender { get; set; }
    [Required]
    public string Mode { get; set; }
    public int? BranchCode { get; set; }
    public int? YearCode { get; set; }
    public int? EduYearCode { get; set; } // <-- ADD THIS
    public List<int> SelectedSubjects { get; set; }
    public List<int> SelectedSchedules { get; set; }
    public List<int> SelectedTeachers { get; set; }
    public string PinCode { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}

public class PublicRegistrationViewModel
{
    public int RootCode { get; set; }
    public string RootName { get; set; }
    public List<SelectListItem> AvailableBranches { get; set; }
    public List<SelectListItem> AvailableYears { get; set; }
    public List<SelectListItem> AvailableEduYears { get; set; } // <-- Add this
}


public class PublicRegistrationRequest
{
    [Required]
    public int RootCode { get; set; }
    [Required]
    public string StudentName { get; set; }
    [Required]
    public string StudentPhone { get; set; }
    [Required]
    public string StudentFatherPhone { get; set; }
    [Required]
    public string StudentMotherPhone { get; set; }
    [Required]
    public string StudentFatherJob { get; set; }
    [Required]
    public string StudentMotherJob { get; set; }
    public DateOnly BirthDate { get; set; }
    public bool? Gender { get; set; }
    [Required]
    public string Mode { get; set; }
    public int? BranchCode { get; set; }
    public int? YearCode { get; set; }
    public int? EduYearCode { get; set; } // <-- ADD THIS
    public List<int> SelectedSubjects { get; set; }
    public List<int> SelectedSchedules { get; set; }
    public List<int> SelectedTeachers { get; set; }
    public string PinCode { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
public class StudentSearchViewModel
{
    public string ItemKey { get; set; }
    public string RootName { get; set; }
    public int RootCode { get; set; }
}

public class StudentSearchRequest
{
    [Required]
    public string ItemKey { get; set; }
    [Required]
    public string StudentPhone { get; set; }
}

public class LinkStudentRequest
{
    [Required]
    public string ItemKey { get; set; }
    [Required]
    public int StudentCode { get; set; }
}

public class AttendancePasswordRequest
{
    [Required]
    public int BranchCode { get; set; }
    [Required]
    public string Password { get; set; }
}

public class MarkAttendanceRequest
{
    [Required]
    public string ItemKey { get; set; }
    [Required]
    public int ClassCode { get; set; }
    public int? AttendanceType { get; set; }
    public decimal? SessionPrice { get; set; }
}

public class StudentProfileViewModel
{
    public string? ItemKey { get; set; }
    public int StudentCode { get; set; }
    public string StudentName { get; set; }
    public string StudentPhone { get; set; }
    public string StudentParentPhone { get; set; }
    public DateOnly StudentBirthdate { get; set; }
    public bool? StudentGender { get; set; }
    public DateOnly SubscriptionTime { get; set; }
    public bool IsActive { get; set; }
    public string? BranchName { get; set; }
    public string YearName { get; set; }
    public string LevelName { get; set; }
    public string RootName { get; set; }
    public int RootCode { get; set; }
    public int? YearCode { get; set; }
    public int Age { get; set; }
    public bool CanMarkAttendance { get; set; }
}