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
using Microsoft.AspNetCore.Http;

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

        // ------------------ SESSION HELPERS ------------------
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);

        // Keep rootCode by domain (consistent with original design)
        private (int? userCode, int? groupCode, int? rootCode, string username) GetSessionContext()
        {
            var rootRecord = _context.Roots
                .FirstOrDefault(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", ""));

            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                rootRecord?.RootCode,
                GetSessionString("Username")
            );
        }

        private int GetRootCode() =>
            _context.Roots.FirstOrDefault(x => x.RootDomain == HttpContext.Request.Host.Host.Replace("www.", ""))?.RootCode ?? 0;

        private void LogSessionIfInvalid(string scope, int? userCode, int? groupCode, string username)
        {
            if (!userCode.HasValue || !groupCode.HasValue || string.IsNullOrEmpty(username))
            {
                _logger.LogWarning($"[{scope}] Missing session values. UserCode={userCode?.ToString() ?? "null"}, GroupCode={groupCode?.ToString() ?? "null"}, Username={(username ?? "null")}. SessionId={HttpContext.Session.Id}");
            }
        }
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
        public async Task<IActionResult> PublicRegister(int root_code, [FromBody] PublicRegistrationRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation($"=== STARTING REGISTRATION FOR ROOT {root_code} ===");

                // Manual validation for required fields
                var validationErrors = new List<string>();

                if (string.IsNullOrWhiteSpace(request.StudentName))
                    validationErrors.Add("Student name is required.");
                if (string.IsNullOrWhiteSpace(request.StudentPhone))
                    validationErrors.Add("Student phone is required.");
                if (string.IsNullOrWhiteSpace(request.StudentFatherPhone))
                    validationErrors.Add("Father phone is required.");
                if (string.IsNullOrWhiteSpace(request.StudentMotherPhone))
                    validationErrors.Add("Mother phone is required.");
                if (string.IsNullOrWhiteSpace(request.StudentFatherJob))
                    validationErrors.Add("Father job is required.");
                if (string.IsNullOrWhiteSpace(request.StudentMotherJob))
                    validationErrors.Add("Mother job is required.");
                if (string.IsNullOrWhiteSpace(request.Mode))
                    validationErrors.Add("Mode is required.");

                // Custom validation based on registration mode
                if (request.Mode == "Online")
                {
                    if (string.IsNullOrWhiteSpace(request.PinCode))
                        validationErrors.Add("PIN code is required for online registration.");
                    if (string.IsNullOrWhiteSpace(request.Username))
                        validationErrors.Add("Username is required for online registration.");
                    if (string.IsNullOrWhiteSpace(request.Password))
                        validationErrors.Add("Password is required for online registration.");

                    // Validate PIN code
                    if (!string.IsNullOrWhiteSpace(request.PinCode))
                    {
                        var pinEntity = await _context.Pins
                            .FirstOrDefaultAsync(p => p.Watermark == request.PinCode && p.IsActive == 1);
                        if (pinEntity == null)
                            validationErrors.Add("Invalid or expired PIN code.");
                    }
                }
                else // Offline mode
                {
                    if (!request.BranchCode.HasValue)
                        validationErrors.Add("Branch is required for offline registration.");
                    if (!request.YearCode.HasValue)
                        validationErrors.Add("Academic year is required for offline registration.");
                }

                // Return validation errors if any
                if (validationErrors.Any())
                {
                    _logger.LogWarning($"Validation failed: {string.Join(", ", validationErrors)}");
                    return Json(new { success = false, errors = validationErrors });
                }

                // Rest of your existing logic...
                var rootEntity = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == root_code && r.IsActive);
                if (rootEntity == null)
                {
                    _logger.LogWarning($"Root entity {root_code} not found or inactive");
                    return Json(new { success = false, error = "Registration center not found or inactive." });
                }

                // Continue with the rest of your existing code...
                _logger.LogInformation("Creating student entity");
                var student = CreateStudentFromRequest(request, root_code);
                _context.Students.Add(student);

                _logger.LogInformation("About to save Student entity");
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Student saved successfully with ID: {student.StudentCode}");

                if (request.SelectedSubjects?.Any() == true)
                {
                    _logger.LogInformation("About to create Learn records");
                    await CreateLearnRecords(request, student);

                    _logger.LogInformation("About to save Learn records");

                    // Check what's in the context before saving
                    var pendingLearns = _context.ChangeTracker.Entries<Learn>()
                        .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                        .ToList();

                    _logger.LogInformation($"Number of Learn entities to save: {pendingLearns.Count}");

                    foreach (var entry in pendingLearns)
                    {
                        var learn = entry.Entity;
                        _logger.LogInformation($"Pending Learn: StudentCode={learn.StudentCode}, SubjectCode={learn.SubjectCode}, TeacherCode={learn.TeacherCode}, ScheduleCode={learn.ScheduleCode?.ToString() ?? "NULL"}, BranchCode={learn.BranchCode?.ToString() ?? "NULL"}");
                    }

                    _logger.LogInformation("Executing SaveChangesAsync for Learn records...");

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Learn records saved successfully");
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully");

                string successMessage = request.Mode == "Online"
                    ? $"Online registration successful! Welcome {student.StudentName}."
                    : $"Registration successful! Welcome {student.StudentName}.";

                _logger.LogInformation($"Registration completed for student ID {student.StudentCode}");

                return Json(new
                {
                    success = true,
                    message = successMessage,
                    redirectUrl = $"/Register/{root_code}/Success",
                    studentCode = student.StudentCode
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Registration failed with exception: {Message}", ex.Message);
                _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException?.Message);

                return Json(new
                {
                    success = false,
                    error = $"Registration failed: {ex.Message}",
                    details = ex.InnerException?.Message
                });
            }
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

                // Set BranchCode to null for online students
                BranchCode = request.Mode == "Online" ? null : request.BranchCode,

                YearCode = request.YearCode,
                RootCode = rootCode,
                IsActive = true,
                InsertUser = 1,
                InsertTime = DateTime.Now,
                SubscribtionTime = DateOnly.FromDateTime(DateTime.Today),

                // Add credentials for online students
                StudentUsername = request.Mode == "Online" ? request.Username?.Trim() : null,
                StudentPassword = request.Mode == "Online" ? request.Password : null,
                IsConfirmed = request.Mode == "Online" ? false : (bool?)null
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
            if (!ModelState.IsValid)
                return Json(new { success = false, error = "Invalid request payload." });

            // Get the root code for the branch
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchCode == req.BranchCode);
            var targetRootCode = branch?.RootCode ?? 0;

            var users = await (
                from u in _context.Users
                join g in _context.Groups on u.GroupCode equals g.GroupCode
                where g.RootCode == targetRootCode // Only match RootCode, ignore branch and group name
                select new
                {
                    u.UserCode,
                    u.Password,
                    u.Username,
                    u.GroupCode,
                    g.GroupName,
                    g.BranchCode,
                    g.RootCode
                }
            ).ToListAsync();

            foreach (var user in users)
            {
                if (user.Password == MD5hasher(req.Password))
                {
                    // Set session keys (no condition needed; they are ints)
                    HttpContext.Session.SetInt32("UserCode", user.UserCode);
                    HttpContext.Session.SetInt32("GroupCode", user.GroupCode);
                    HttpContext.Session.SetString("Username", user.Username);
                    if (user.BranchCode != null)
                        HttpContext.Session.SetInt32("BranchCode", (int)user.BranchCode);
                    else
                        HttpContext.Session.Remove("BranchCode");
                    HttpContext.Session.SetInt32("RootCode", user.RootCode);

                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("UserId", user.UserCode.ToString()),
                new Claim("GroupCode", user.GroupCode.ToString()),
                new Claim("GroupName", user.GroupName)
            };
                    var identity = new ClaimsIdentity(claims, "AttendancePassword");
                    await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

                    _logger.LogInformation(
                        "[CheckAttendancePassword] Session set: UserCode={UserCode}, GroupCode={GroupCode}, Branch={Branch}, Root={Root}, SessionId={SessionId}",
                        user.UserCode, user.GroupCode, user.BranchCode, user.RootCode, HttpContext.Session.Id);

                    return Json(new
                    {
                        success = true,
                        username = user.Username,
                        userCode = user.UserCode,
                        message = "User authorized. You may now mark attendance."
                    });
                }
            }

            _logger.LogWarning("[CheckAttendancePassword] Failed authorization attempt for BranchCode={BranchCode}", req.BranchCode);
            return Json(new { success = false, error = "Wrong password or not authorized for this branch." });
        }

        // Put this method inside the StudentController (private or public static)
        public static string MD5hasher(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.Unicode.GetBytes(input ?? "");
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                var sb = new System.Text.StringBuilder();
                foreach (var b in hashBytes)
                    sb.Append(b.ToString("X2")); // Uppercase hex
                return sb.ToString();
            }
        }
        [HttpPost]
        [Route("Student/MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (!userCode.HasValue || !groupCode.HasValue)
            {
                LogSessionIfInvalid("MarkAttendance", userCode, groupCode, username);
                return Json(new { success = false, error = "Session expired. Please authenticate again." });
            }

            if (!rootCode.HasValue)
            {
                return Json(new { success = false, error = "Invalid domain configuration." });
            }

            if (request == null || string.IsNullOrEmpty(request.ItemKey))
                return Json(new { success = false, error = "Invalid request." });

            if (!request.AttendanceType.HasValue)
                return Json(new { success = false, error = "Attendance type is required." });

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

            if (request.AttendanceType.Value == discountTypeCode &&
                request.SessionPrice.HasValue &&
                request.SessionPrice.Value > 0)
            {
                sessionPriceToSave = request.SessionPrice.Value;
            }

            var attendance = new Attend
            {
                TeacherCode = classEntity.TeacherCode,
                ScheduleCode = classEntity.ScheduleCode,
                ClassId = request.ClassCode,
                HallId = classEntity.HallCode,
                StudentId = student.StudentCode,
                AttendDate = DateTime.Now,
                SessionPrice = sessionPriceToSave,
                RootCode = student.RootCode,
                Type = request.AttendanceType.Value
            };

            _context.Attends.Add(attendance);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Attendance marked successfully.",
                attendanceDate = attendance.AttendDate.ToString("yyyy-MM-dd HH:mm"),
                markedBy = username
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
            int rootCode = GetRootCode();

            if (rootCode == 0)
                return Json(new { error = "Unable to determine root code from domain." });

            // Get current active education year
            var currentEduYear = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == rootCode)
                .OrderByDescending(e => e.EduCode)
                .FirstOrDefaultAsync();

            if (currentEduYear == null)
                return Json(new { error = "No active education year found." });

            // Build the query step by step
            IQueryable<Teach> subjectsQuery = _context.Teaches
                .Where(t => t.RootCode == rootCode
                           && t.EduYearCode == currentEduYear.EduCode
                           && t.IsActive);

            // For offline mode, filter by branch
            if (branchCode > 0)
                subjectsQuery = subjectsQuery.Where(t => t.BranchCode == branchCode);

            // Filter by year if provided
            if (yearCode.HasValue)
                subjectsQuery = subjectsQuery.Where(t => t.YearCode == yearCode.Value);

            var subjects = await subjectsQuery
                .Include(t => t.SubjectCodeNavigation)
                .Select(t => new
                {
                    SubjectCode = t.SubjectCode,
                    SubjectName = t.SubjectCodeNavigation.SubjectName
                })
                .Distinct()
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            return Json(subjects);
        }
        [HttpGet]
        [Route("Student/GetSchedulesForSubjectTeacher")]
        public async Task<IActionResult> GetSchedulesForSubjectTeacher(int subjectCode, int teacherCode, int branchCode, int? yearCode = null)
        {
            int rootCode = GetRootCode();

            // Get current active education year
            var currentEduYear = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == rootCode)
                .OrderByDescending(e => e.EduCode)
                .FirstOrDefaultAsync();

            if (currentEduYear == null)
                return Json(new { error = "No active education year found." });

            var schedulesQuery = _context.Schedules
                .Where(s => s.RootCode == rootCode
                           && s.BranchCode == branchCode
                           && s.SubjectCode == subjectCode
                           && s.TeacherCode == teacherCode
                           && s.EduYearCode == currentEduYear.EduCode);

            if (yearCode.HasValue)
                schedulesQuery = schedulesQuery.Where(s => s.YearCode == yearCode.Value);

            // First get the data from database, then format it
            var schedules = await schedulesQuery
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            // Format the data after retrieving from database
            var formattedSchedules = schedules.Select(s => new {
                ScheduleCode = s.ScheduleCode,
                ScheduleName = s.ScheduleName,
                DayOfWeek = s.DayOfWeek ?? "N/A",
                StartTime = s.StartTime.HasValue ? s.StartTime.Value.ToString("HH:mm") : "N/A",
                EndTime = s.EndTime.HasValue ? s.EndTime.Value.ToString("HH:mm") : "N/A",
                HallName = s.HallCodeNavigation != null ? s.HallCodeNavigation.HallName : "N/A",
                ScheduleAmount = s.ScheduleAmount,
                SubjectCode = s.SubjectCode,
                TeacherCode = s.TeacherCode,
                YearCode = s.YearCode,
                EduYearCode = s.EduYearCode
            }).ToList();

            return Json(formattedSchedules);
        }


        [HttpGet]
        [Route("Student/GetTeachersForSubjects")]
        public async Task<IActionResult> GetTeachersForSubjects(string subjectCodes, int branchCode, int? yearCode = null)
        {
            int rootCode = GetRootCode();

            if (string.IsNullOrEmpty(subjectCodes))
                return Json(new { error = "No subjects selected." });

            var subjectCodeList = subjectCodes.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(int.Parse).ToList();

            if (!subjectCodeList.Any())
                return Json(new { error = "Invalid subject codes." });

            // Get current active education year
            var currentEduYear = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == rootCode)
                .OrderByDescending(e => e.EduCode)
                .FirstOrDefaultAsync();

            if (currentEduYear == null)
                return Json(new { error = "No active education year found." });

            // Build the query - different logic for online vs offline
            IQueryable<Teach> teachersQuery;

            if (branchCode == 0) // Online mode
            {
                // For online: filter by RootCode, SubjectCodes, and YearCode (no branch filtering)
                teachersQuery = _context.Teaches
                    .Where(t => t.RootCode == rootCode
                               && t.EduYearCode == currentEduYear.EduCode
                               && subjectCodeList.Contains(t.SubjectCode)
                               && t.IsActive);

                // Include year filtering for online mode
                if (yearCode.HasValue)
                    teachersQuery = teachersQuery.Where(t => t.YearCode == yearCode.Value);
            }
            else // Offline mode
            {
                // For offline: filter by RootCode, BranchCode, EduYearCode, YearCode, and SubjectCodes
                teachersQuery = _context.Teaches
                    .Where(t => t.RootCode == rootCode
                               && t.BranchCode == branchCode
                               && t.EduYearCode == currentEduYear.EduCode
                               && subjectCodeList.Contains(t.SubjectCode)
                               && t.IsActive);

                if (yearCode.HasValue)
                    teachersQuery = teachersQuery.Where(t => t.YearCode == yearCode.Value);
            }

            var teachers = await teachersQuery
                .Include(t => t.TeacherCodeNavigation)
                .Include(t => t.SubjectCodeNavigation)
                .Select(t => new
                {
                    TeachId = $"{t.TeacherCode}_{t.SubjectCode}_{t.YearCode}_{t.EduYearCode}",
                    TeacherCode = t.TeacherCode,
                    TeacherName = t.TeacherCodeNavigation.TeacherName,
                    TeacherPhone = t.TeacherCodeNavigation.TeacherPhone,
                    SubjectCode = t.SubjectCode,
                    SubjectName = t.SubjectCodeNavigation.SubjectName,
                    YearCode = t.YearCode,
                    EduYearCode = t.EduYearCode
                })
                .OrderBy(t => t.SubjectName)
                .ThenBy(t => t.TeacherName)
                .ToListAsync();

            return Json(teachers);
        }


        [HttpGet]
        [Route("Student/CheckUsername")]
        public async Task<IActionResult> CheckUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Json(new { available = false, error = "Username is required." });

            try
            {
                var existingStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.StudentUsername.ToLower() == username.ToLower());

                return Json(new { available = existingStudent == null });
            }
            catch (Exception ex)
            {
                return Json(new { available = false, error = "Error checking username availability." });
            }
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

            // Get all classes the student has actually attended
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Select(a => a.ClassId)
                .Distinct()
                .ToListAsync();

            if (!attendedClassCodes.Any())
                return Json(new List<object>());

            // Get lessons linked to attended classes
            var attendedLessonCodes = await _context.Classes
                .Where(c => attendedClassCodes.Contains(c.ClassCode) && c.ClassLessonCode.HasValue)
                .Select(c => c.ClassLessonCode.Value)
                .Distinct()
                .ToListAsync();

            if (!attendedLessonCodes.Any())
                return Json(new List<object>());

            // Get exam files with ExamCode (FileType 3)
            var examFiles = await _context.Files
                .Where(f => attendedLessonCodes.Contains(f.LessonCode) &&
                           f.IsActive &&
                           (f.IsOnlineLesson == false || f.IsOnlineLesson == null) &&
                           f.FileType == 3 &&
                           f.ExamCode.HasValue)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .ToListAsync();

            var examCodes = examFiles.Select(f => f.ExamCode.Value).Distinct().ToList();

            var examsData = await _context.Exams
                .Where(e => examCodes.Contains(e.ExamCode) && e.IsActive == true)
                .ToListAsync();

            var examResults = examFiles.Select(f =>
            {
                var exam = examsData.FirstOrDefault(e => e.ExamCode == f.ExamCode);
                return new
                {
                    ExamCode = exam?.ExamCode ?? f.FileCode,
                    ExamName = exam?.ExamName ?? f.DisplayName ?? "Unknown Exam",
                    FileCode = f.FileCode,
                    DisplayName = exam?.ExamName ?? f.DisplayName,
                    FileType = f.FileType,
                    FileTypeName = "Exam",
                    SortOrder = f.SortOrder,
                    InsertTime = f.InsertTime,
                    IsActive = f.IsActive,
                    LessonCode = f.LessonCode,
                    LessonName = f.LessonCodeNavigation.LessonName,
                    SubjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                    TeacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName,
                    Duration = exam?.ExamTimer,
                    DurationFormatted = exam?.ExamTimer != null ? exam.ExamTimer.ToString(@"hh\:mm") : null,
                    ExamDegree = exam?.ExamDegree,
                    IsOnline = exam?.IsOnline,
                    IsDone = exam?.IsDone
                };
            }).ToList();

            return Json(examResults);
        }
        [HttpGet]
        [Route("Student/GetLatestStudentDownloads/{item_key}")]
        public async Task<IActionResult> GetLatestStudentDownloads(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new List<object>());

            // Get the last attended class
            var lastAttend = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .OrderByDescending(a => a.AttendDate)
                .FirstOrDefaultAsync();

            if (lastAttend == null)
                return Json(new List<object>());

            var lastClass = await _context.Classes.FirstOrDefaultAsync(c => c.ClassCode == lastAttend.ClassId);
            if (lastClass == null || !lastClass.ClassLessonCode.HasValue)
                return Json(new List<object>());

            var lessonCode = lastClass.ClassLessonCode.Value;

            // Get all FileType=2 files for this lesson (PDFs)
            var pdfFiles = await _context.Files
                .Where(f => f.LessonCode == lessonCode && f.IsActive && f.FileType == 2)
                .OrderByDescending(f => f.InsertTime)
                .Select(f => new {
                    fileCode = f.FileCode,
                    displayName = f.DisplayName ?? "Downloadable File",
                    fileLocation = f.FileLocation,
                    insertTime = f.InsertTime,
                    lessonName = f.LessonCodeNavigation.LessonName,
                    subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                    teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName
                })
                .ToListAsync();

            return Json(pdfFiles);
        }
        [HttpGet]
        [Route("Student/GetLatestStudentVideos/{item_key}")]
        public async Task<IActionResult> GetLatestStudentVideos(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new List<object>());

            // Get the last attended class
            var lastAttend = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .OrderByDescending(a => a.AttendDate)
                .FirstOrDefaultAsync();

            if (lastAttend == null)
                return Json(new List<object>());

            var lastClass = await _context.Classes.FirstOrDefaultAsync(c => c.ClassCode == lastAttend.ClassId);
            if (lastClass == null || !lastClass.ClassLessonCode.HasValue)
                return Json(new List<object>());

            var lessonCode = lastClass.ClassLessonCode.Value;

            // Get all FileType=1 files for this lesson (Videos)
            var videoFiles = await _context.Files
                .Where(f => f.LessonCode == lessonCode && f.IsActive && f.FileType == 1)
                .OrderByDescending(f => f.InsertTime)
                .Select(f => new {
                    fileCode = f.FileCode,
                    displayName = f.DisplayName ?? "Video",
                    fileLocation = f.FileLocation,
                    insertTime = f.InsertTime,
                    lessonName = f.LessonCodeNavigation.LessonName,
                    subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                    teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName,
                    duration = f.Duration
                })
                .ToListAsync();

            return Json(videoFiles);
        }

        [HttpGet]
        [Route("Student/GetStudentDownloads/{item_key}")]
        public async Task<IActionResult> GetStudentDownloads(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            // Get all classes the student has attended
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Select(a => a.ClassId)
                .Distinct()
                .ToListAsync();

            if (!attendedClassCodes.Any())
                return Json(new List<object>());

            // Get lessons linked to attended classes
            var attendedLessonCodes = await _context.Classes
                .Where(c => attendedClassCodes.Contains(c.ClassCode) && c.ClassLessonCode.HasValue)
                .Select(c => c.ClassLessonCode.Value)
                .Distinct()
                .ToListAsync();

            if (!attendedLessonCodes.Any())
                return Json(new List<object>());

            // Get downloadable files (FileType == 2)
            var downloadableFiles = await _context.Files
                .Where(f => attendedLessonCodes.Contains(f.LessonCode) &&
                            f.IsActive &&
                            f.FileType == 2)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .OrderByDescending(f => f.InsertTime)
                .Select(f => new
                {
                    fileCode = f.FileCode,
                    displayName = f.DisplayName ?? "Downloadable File",
                    fileLocation = f.FileLocation,
                    insertTime = f.InsertTime,
                    lessonName = f.LessonCodeNavigation.LessonName,
                    subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                    teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName
                })
                .ToListAsync();

            return Json(downloadableFiles);
        }
        [HttpGet]
        [Route("Student/DownloadFile/{fileCode}")]
        public async Task<IActionResult> DownloadFile(int fileCode)
        {
            var file = await _context.Files.FirstOrDefaultAsync(f => f.FileCode == fileCode);
            if (file == null || file.FileType != 2)
                return NotFound();

            // If stored as absolute/relative path on disk:
            var filePath = Path.Combine("C:\\YourFilesFolder", file.FileLocation.Replace("/", "\\").Replace("\\", Path.DirectorySeparatorChar.ToString()));

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var contentType = "application/octet-stream";
            var fileName = file.DisplayName ?? Path.GetFileName(filePath);
            return PhysicalFile(filePath, contentType, fileName);
        }

        [HttpGet]
        [Route("Student/GetStudentAssignments/{item_key}")]
        public async Task<IActionResult> GetStudentAssignments(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            // Get all classes the student has actually attended
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Select(a => a.ClassId)
                .Distinct()
                .ToListAsync();

            if (!attendedClassCodes.Any())
                return Json(new List<object>()); // No attended classes, return empty

            // Get lessons linked to attended classes
            var attendedLessonCodes = await _context.Classes
                .Where(c => attendedClassCodes.Contains(c.ClassCode) && c.ClassLessonCode.HasValue)
                .Select(c => c.ClassLessonCode.Value)
                .Distinct()
                .ToListAsync();

            if (!attendedLessonCodes.Any())
                return Json(new List<object>()); // No lessons linked to attended classes

            // Get assignment files (FileType 4 or 5, IsOnlineLesson = false)
            var assignmentFiles = await _context.Files
                .Where(f => attendedLessonCodes.Contains(f.LessonCode) &&
                            f.IsActive &&
                            (f.IsOnlineLesson == false || f.IsOnlineLesson == null) &&
                            (f.FileType == 4 || f.FileType == 5) &&
                            f.ExamCode.HasValue) // Use only files linked to an Exam/Assignment
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .OrderByDescending(f => f.InsertTime)
                .ToListAsync();

            var assignmentExamCodes = assignmentFiles.Select(f => f.ExamCode.Value).Distinct().ToList();

            // Get all assignments/exams data for those codes
            var assignmentsData = await _context.Exams
                .Where(e => assignmentExamCodes.Contains(e.ExamCode) && e.IsActive == true)
                .ToListAsync();

            // Get student assignment submissions (if you track them in StudentExams)
            var attendedAssignmentCodes = await _context.StudentExams
                .Where(se => se.StudentCode == student.StudentCode && assignmentExamCodes.Contains(se.ExamCode) && se.IsActive == true)
                .Select(se => se.ExamCode)
                .ToListAsync();

            // Compose result
            var result = assignmentFiles.Select(f =>
            {
                var assignment = assignmentsData.FirstOrDefault(a => a.ExamCode == f.ExamCode);
                bool attended = attendedAssignmentCodes.Contains(f.ExamCode.Value);

                return new
                {
                    examCode = f.ExamCode.Value, // <-- This is the assignment/exam code, NOT FileCode
                    examName = assignment?.ExamName ?? f.DisplayName ?? "Unknown Assignment",
                    fileCode = f.FileCode,
                    displayName = assignment?.ExamName ?? f.DisplayName,
                    fileType = f.FileType,
                    fileTypeName = f.FileType == 4 ? "Assignment" : "Assignment Video",
                    duration = f.Duration,
                    durationFormatted = f.Duration.HasValue ? f.Duration.Value.ToString(@"hh\:mm\:ss") : null,
                    sortOrder = f.SortOrder,
                    insertTime = f.InsertTime,
                    isActive = f.IsActive,
                    lessonCode = f.LessonCode,
                    lessonName = f.LessonCodeNavigation.LessonName,
                    subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                    teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName,
                    attended = attended
                };
            }).ToList();

            return Json(result);
        }


        [HttpGet]
        [Route("Student/GetStudentVideos/{item_key}")]
        public async Task<IActionResult> GetStudentVideos(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            // Get all classes the student has actually attended
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Select(a => a.ClassId)
                .Distinct()
                .ToListAsync();

            if (!attendedClassCodes.Any())
                return Json(new List<object>());

            // Get lessons linked to attended classes
            var attendedLessonCodes = await _context.Classes
                .Where(c => attendedClassCodes.Contains(c.ClassCode) && c.ClassLessonCode.HasValue)
                .Select(c => c.ClassLessonCode.Value)
                .Distinct()
                .ToListAsync();

            if (!attendedLessonCodes.Any())
                return Json(new List<object>());

            // Get video files that admin has marked as accessible (IsOnlineLesson = true)
            var videoFiles = await _context.Files
                .Where(f => attendedLessonCodes.Contains(f.LessonCode) &&
                           f.IsActive &&
                           (f.IsOnlineLesson == false || f.IsOnlineLesson == null) && 
                           f.FileType == 1) // Videos only
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .OrderByDescending(f => f.InsertTime)
                .Select(f => new
                {
                    fileCode = f.FileCode,  // camelCase
                    displayName = f.DisplayName ?? "Unknown Video",  // camelCase
                    videoProvider = f.VideoProvider,
                    videoProviderName = f.VideoProvider == 0 ? "YouTube" :
                      f.VideoProvider == 1 ? "Bunny CDN" : "Unknown",
                    duration = f.Duration,
                    durationFormatted = f.Duration.HasValue ? f.Duration.Value.ToString(@"hh\:mm\:ss") : null,
                    sortOrder = f.SortOrder,
                    insertTime = f.InsertTime,
                    lessonCode = f.LessonCode,
                    lessonName = f.LessonCodeNavigation.LessonName,
                    subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                    teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName
                })
                .ToListAsync();

            return Json(videoFiles);
        }

        [HttpGet]
        [Route("Student/WatchVideo/{item_key}/{fileCode}")]
        public async Task<IActionResult> WatchVideo(string item_key, int fileCode)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return NotFound("Student not found.");

            // Verify student has access to this video
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Select(a => a.ClassId)
                .ToListAsync();

            var hasAccess = await _context.Files
                .Where(f => f.FileCode == fileCode &&
                           f.IsActive &&
                           f.FileType == 1 &&
                           f.IsOnlineLesson == false) // Admin granted access
                .Join(_context.Classes.Where(c => attendedClassCodes.Contains(c.ClassCode)),
                      f => f.LessonCode,
                      c => c.ClassLessonCode,
                      (f, c) => f)
                .AnyAsync();

            if (!hasAccess)
                return Forbid("Access denied to this video.");

            var videoFile = await _context.Files
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .FirstOrDefaultAsync(f => f.FileCode == fileCode);

            if (videoFile == null)
                return NotFound("Video not found.");

            ViewBag.Student = student;
            ViewBag.VideoFile = videoFile;
            ViewBag.ItemKey = item_key;

            return View("WatchVideo", videoFile);
        }


        [HttpGet]
        [Route("Student/GetSecureVideoUrl")]
        public async Task<IActionResult> GetSecureVideoUrl(int fileCode, string itemKey)
        {
            var student = await GetStudentByItemKey(itemKey);
            if (student == null)
                return Unauthorized("Student not found");

            // Verify student has access to this video
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Select(a => a.ClassId)
                .ToListAsync();

            var videoFile = await _context.Files
                .Where(f => f.FileCode == fileCode &&
                           f.IsActive &&
                           f.FileType == 1 &&
                           (f.IsOnlineLesson == false || f.IsOnlineLesson == null))
                .Join(_context.Classes.Where(c => attendedClassCodes.Contains(c.ClassCode)),
                      f => f.LessonCode,
                      c => c.ClassLessonCode,
                      (f, c) => f)
                .Include(f => f.LessonCodeNavigation)
                .FirstOrDefaultAsync();

            if (videoFile == null)
                return NotFound("Video not found or access denied");

            // Decrypt the video URL (same logic as LessonContentController)
            var originalUrl = DecryptString(videoFile.FileLocation);
            var expiryHours = videoFile.LessonCodeNavigation?.LessonExpireDays.HasValue == true
                ? videoFile.LessonCodeNavigation.LessonExpireDays.Value * 24
                : 24;

            return Json(new
            {
                secureUrl = originalUrl,
                displayName = videoFile.DisplayName,
                duration = videoFile.Duration?.ToString(@"hh\:mm\:ss"),
                provider = videoFile.VideoProvider == 0 ? "YouTube" : "Bunny CDN",
                expiryTime = DateTime.Now.AddHours(expiryHours)
            });
        }

        // Add the decrypt method to StudentController
        private string DecryptString(string encryptedText)
        {
            var data = Convert.FromBase64String(encryptedText);
            return System.Text.Encoding.UTF8.GetString(data);
        }

        [HttpGet]
        [Route("Student/GetUpcomingExams/{item_key}")]
        public async Task<IActionResult> GetUpcomingExams(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            // Only exclude exams that have a StudentExam with IsActive == true (submitted)
            var submittedExamCodes = await _context.StudentExams
                .Where(se => se.StudentCode == student.StudentCode && se.IsActive == true)
                .Select(se => se.ExamCode)
                .Distinct()
                .ToListAsync();

            // Get all lessons the student attended (as before)
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == student.StudentCode)
                .Select(a => a.ClassId)
                .Distinct()
                .ToListAsync();

            if (!attendedClassCodes.Any())
                return Json(new List<object>());

            var attendedLessonCodes = await _context.Classes
                .Where(c => attendedClassCodes.Contains(c.ClassCode) && c.ClassLessonCode.HasValue)
                .Select(c => c.ClassLessonCode.Value)
                .Distinct()
                .ToListAsync();

            if (!attendedLessonCodes.Any())
                return Json(new List<object>());

            // All exam links for these lessons
            var examFileLinks = await _context.Files
                .Where(f => attendedLessonCodes.Contains(f.LessonCode) &&
                            f.IsActive &&
                            f.FileType == 3 &&
                            f.ExamCode.HasValue)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .ToListAsync();

            var allExamCodes = examFileLinks.Select(f => f.ExamCode.Value).Distinct().ToList();

            // Remove only submitted (IsActive == true) exams
            var upcomingExamCodes = allExamCodes.Except(submittedExamCodes).ToList();

            if (!upcomingExamCodes.Any())
                return Json(new List<object>());

            var exams = await _context.Exams
                .Where(e => upcomingExamCodes.Contains(e.ExamCode) && e.IsActive == true)
                .ToListAsync();

            // Also, check if there's a StudentExam row with IsActive == false for resume logic
            var inProgressExamCodes = await _context.StudentExams
                .Where(se => se.StudentCode == student.StudentCode && se.IsActive == false)
                .Select(se => se.ExamCode)
                .Distinct()
                .ToListAsync();

            var upcomingExams = exams.Select(exam =>
            {
                var file = examFileLinks.FirstOrDefault(f => f.ExamCode == exam.ExamCode);
                bool isInProgress = inProgressExamCodes.Contains(exam.ExamCode);

                return new
                {
                    ExamCode = exam.ExamCode,
                    ExamName = exam.ExamName,
                    ExamDegree = exam.ExamDegree,
                    ExamTimer = exam.ExamTimer != null ? exam.ExamTimer.ToString(@"hh\:mm") : null,
                    SubjectName = file?.LessonCodeNavigation?.SubjectCodeNavigation?.SubjectName,
                    TeacherName = file?.LessonCodeNavigation?.TeacherCodeNavigation?.TeacherName,
                    LessonName = file?.LessonCodeNavigation?.LessonName,
                    IsInProgress = isInProgress // for UI, to show "Resume" instead of "Attend"
                };
            }).ToList();

            return Json(upcomingExams);
        }

        [HttpGet]
        [Route("Student/GetAttendedExams/{item_key}")]
        public async Task<IActionResult> GetAttendedExams(string item_key)
        {
            var student = await GetStudentByItemKey(item_key);
            if (student == null)
                return Json(new { error = "Student not found." });

            var attendedExamEntries = await _context.StudentExams
                .Where(se => se.StudentCode == student.StudentCode)
                .ToListAsync();

            if (!attendedExamEntries.Any())
                return Json(new List<object>());

            var examCodes = attendedExamEntries.Select(se => se.ExamCode).Distinct().ToList();

            // Only fetch real exams (IsExam == true)
            var exams = await _context.Exams
                .Where(e => examCodes.Contains(e.ExamCode) && e.IsActive == true && e.IsExam)
                .ToListAsync();

            var fileLinks = await _context.Files
                .Where(f => f.ExamCode.HasValue && examCodes.Contains(f.ExamCode.Value))
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .ToListAsync();

            var attendedExams = attendedExamEntries
                .Where(se => exams.Any(e => e.ExamCode == se.ExamCode))
                .Select(se =>
                {
                    var exam = exams.FirstOrDefault(e => e.ExamCode == se.ExamCode);
                    var file = fileLinks.FirstOrDefault(f => f.ExamCode == se.ExamCode);

                    return new
                    {
                        examCode = se.ExamCode,
                        examName = exam?.ExamName ?? "Unknown Exam",
                        degree = se.StudentResult,
                        examDegree = exam?.ExamDegree,
                        percentage = se.StudentPercentage,
                        examDate = se.InsertTime,
                        subjectName = file?.LessonCodeNavigation?.SubjectCodeNavigation?.SubjectName,
                        teacherName = file?.LessonCodeNavigation?.TeacherCodeNavigation?.TeacherName,
                        lessonName = file?.LessonCodeNavigation?.LessonName
                    };
                }).ToList();

            return Json(attendedExams);
        }
        [HttpGet]
        [Route("Student/GetExamAnswers/{studentCode}/{examCode}")]
        public async Task<IActionResult> GetExamAnswers(int studentCode, int examCode)
        {
            // Get all questions for this exam
            var examQuestions = await _context.ExamQuestions
                .Where(eq => eq.ExamCode == examCode)
                .ToListAsync();

            var questionCodes = examQuestions.Select(eq => eq.QuestionCode).ToList();

            // Get all possible answers for these questions
            var allAnswers = await _context.Answers
                .Where(a => questionCodes.Contains(a.QuestionCode))
                .ToListAsync();

            // Get the actual question text/content
            var allQuestions = await _context.Questions
                .Where(q => questionCodes.Contains(q.QuestionCode))
                .ToListAsync();

            // Get student answers for these questions
            var studentAnswers = await _context.StudentAnswers
                .Where(sa => sa.StudentCode == studentCode && sa.ExamCode == examCode)
                .ToListAsync();

            var result = examQuestions.Select(q =>
            {
                var answersForQuestion = allAnswers
                    .Where(a => a.QuestionCode == q.QuestionCode)
                    .Select(a => new {
                        AnswerCode = a.AnswerCode,
                        AnswerText = a.AnswerContent, // or whatever field holds the choice text
                        IsCorrect = a.IsTrue // or whatever field is the correct flag
                    }).ToList();

                var studentAnswer = studentAnswers.FirstOrDefault(sa => sa.QuestionCode == q.QuestionCode);

                int? studentAnswerCode = studentAnswer?.StudentAnswerCode;
                int? rightAnswerCode = studentAnswer?.RightAnswerCode;
                double? questionDegree = studentAnswer?.QuestionDegree ?? q.QuestionDegree;
                double? studentDegree = studentAnswer?.StudentDegree;

                // Fetch question text/content from Questions table
                var questionEntity = allQuestions.FirstOrDefault(qq => qq.QuestionCode == q.QuestionCode);
                string questionText = questionEntity?.QuestionContent ?? $"Question {q.QuestionCode}";

                return new
                {
                    QuestionCode = q.QuestionCode,
                    QuestionText = questionText,
                    QuestionDegree = questionDegree,
                    StudentDegree = studentDegree,
                    Answers = answersForQuestion,
                    StudentAnswerCode = studentAnswerCode,
                    RightAnswerCode = rightAnswerCode,
                    IsStudentCorrect = (studentAnswerCode.HasValue && rightAnswerCode.HasValue && studentAnswerCode.Value == rightAnswerCode.Value)
                };
            });

            return Json(result);
        }


        [HttpGet]
        [Route("Student/GetAssignments/{item_key}")]
        public async Task<IActionResult> GetAssignments(string item_key)
        {
            // Redirect to existing method
            return await GetStudentAssignments(item_key);
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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

        private async Task<bool> IsCurrentUserAdmin(int rootCode, int? branchCode)
        {
            var username = GetSessionString("Username");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
            if (user == null) return false;

            // Allow any group with the same rootCode to mark attendance
            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.GroupCode == user.GroupCode && g.RootCode == rootCode);

            return group != null;
        }

        private async Task CreateLearnRecords(PublicRegistrationRequest request, Student student)
        {
            // Get current active education year
            var currentEduYear = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == student.RootCode)
                .OrderByDescending(e => e.EduCode)
                .FirstOrDefaultAsync();

            if (currentEduYear == null)
                throw new Exception("No active education year found.");

            _logger.LogInformation("=== CreateLearnRecords START ===");
            _logger.LogInformation($"Mode: {request.Mode}");
            _logger.LogInformation($"Student RootCode: {student.RootCode}");
            _logger.LogInformation($"Current EduYear: {currentEduYear.EduCode}");

            if (request.SelectedSubjects?.Any() == true)
            {
                for (int i = 0; i < request.SelectedSubjects.Count; i++)
                {
                    var subjectCode = request.SelectedSubjects[i];
                    var teacherCode = i < request.SelectedTeachers.Count ? request.SelectedTeachers[i] : 0;

                    _logger.LogInformation($"--- Processing Learn Record {i} ---");
                    _logger.LogInformation($"Raw SubjectCode: {subjectCode}");
                    _logger.LogInformation($"Raw TeacherCode: {teacherCode}");

                    // Validate that the foreign key references exist
                    var subjectExists = await _context.Subjects.AnyAsync(s => s.SubjectCode == subjectCode);
                    var teacherExists = await _context.Teachers.AnyAsync(t => t.TeacherCode == teacherCode);

                    _logger.LogInformation($"Subject {subjectCode} exists: {subjectExists}");
                    _logger.LogInformation($"Teacher {teacherCode} exists: {teacherExists}");

                    if (!subjectExists)
                        throw new Exception($"Subject with code {subjectCode} does not exist");
                    if (!teacherExists)
                        throw new Exception($"Teacher with code {teacherCode} does not exist");

                    // Handle schedule codes properly for online vs offline
                    int? scheduleCodeToUse = null;
                    if (request.Mode == "Offline")
                    {
                        if (request.SelectedSchedules != null && i < request.SelectedSchedules.Count)
                        {
                            var scheduleValue = request.SelectedSchedules[i];
                            scheduleCodeToUse = scheduleValue > 0 ? scheduleValue : null;

                            // Validate schedule exists if not null
                            if (scheduleCodeToUse.HasValue)
                            {
                                var scheduleExists = await _context.Schedules.AnyAsync(s => s.ScheduleCode == scheduleCodeToUse.Value);
                                _logger.LogInformation($"Schedule {scheduleCodeToUse} exists: {scheduleExists}");
                                if (!scheduleExists)
                                    throw new Exception($"Schedule with code {scheduleCodeToUse} does not exist");
                            }
                        }
                    }

                    _logger.LogInformation($"Final ScheduleCode to use: {scheduleCodeToUse?.ToString() ?? "NULL"}");

                    var learn = new Learn
                    {
                        StudentCode = student.StudentCode,
                        SubjectCode = subjectCode,
                        TeacherCode = teacherCode,
                        YearCode = request.YearCode,
                        EduYearCode = request.EduYearCode ?? currentEduYear.EduCode,
                        ScheduleCode = request.Mode == "Online" ? null :scheduleCodeToUse ,
                        BranchCode = request.Mode == "Online" ? null : request.BranchCode,
                        RootCode = student.RootCode,
                        IsOnline = request.Mode == "Online",
                        IsActive = true,
                        InsertUser = 1,
                        InsertTime = DateTime.Now,
                        LastUpdateUser = 1,
                        LastUpdateTime = DateTime.Now
                    };

                    _logger.LogInformation($"Learn entity details:");
                    _logger.LogInformation($"  StudentCode: {learn.StudentCode}");
                    _logger.LogInformation($"  SubjectCode: {learn.SubjectCode}");
                    _logger.LogInformation($"  TeacherCode: {learn.TeacherCode}");
                    _logger.LogInformation($"  ScheduleCode: {learn.ScheduleCode?.ToString() ?? "NULL"}");
                    _logger.LogInformation($"  BranchCode: {learn.BranchCode?.ToString() ?? "NULL"}");
                    _logger.LogInformation($"  YearCode: {learn.YearCode?.ToString() ?? "NULL"}");
                    _logger.LogInformation($"  EduYearCode: {learn.EduYearCode}");
                    _logger.LogInformation($"  RootCode: {learn.RootCode}");
                    _logger.LogInformation($"  IsOnline: {learn.IsOnline}");

                    _context.Learns.Add(learn);
                    _logger.LogInformation($"Added Learn entity to context");
                }
            }

            _logger.LogInformation("=== CreateLearnRecords END ===");
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
    public int? EduYearCode { get; set; }
    public List<SelectedSubjectData> SelectedSubjects { get; set; } = new List<SelectedSubjectData>();

    public string PinCode { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
public class SelectedSubjectData
{
    public int SubjectCode { get; set; }
    public int TeacherCode { get; set; }
    public int YearCode { get; set; }
    public int EduYearCode { get; set; }
    public int? ScheduleCode { get; set; }
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
    public int RootCode { get; set; }
    public string StudentName { get; set; }
    public string StudentPhone { get; set; }
    public string StudentFatherPhone { get; set; }
    public string StudentMotherPhone { get; set; }
    public string StudentFatherJob { get; set; }
    public string StudentMotherJob { get; set; }
    public DateOnly BirthDate { get; set; }
    public bool? Gender { get; set; }
    public string Mode { get; set; }
    public int? BranchCode { get; set; }
    public int? YearCode { get; set; }
    public int? EduYearCode { get; set; }
    public List<int> SelectedSubjects { get; set; } = new List<int>();
    public List<int> SelectedSchedules { get; set; } = new List<int>();
    public List<int> SelectedTeachers { get; set; } = new List<int>();

    // NO validation attributes on these fields
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