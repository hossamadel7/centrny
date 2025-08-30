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

        // ==================== REGISTRATION METHODS ====================

        /// <summary>
        /// GET: Student/Register/{item_key} - Show student registration page
        /// </summary>
        /// <summary>
        /// GET: Student/Register/{item_key} - Show student registration page
        /// </summary>
        [HttpGet]
        [Route("Student/Register/{item_key}")]
        public async Task<IActionResult> Register(string item_key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item_key))
                {
                    return NotFound("Item key is required.");
                }

                var item = await _context.Items
                    .Include(i => i.RootCodeNavigation)
                    .Include(i => i.StudentCodeNavigation)
                    .Where(i => i.ItemKey == item_key && i.IsActive)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return NotFound("Item not found.");
                }

                var hasExistingStudent = item.StudentCode.HasValue && item.StudentCodeNavigation != null;

                var availableEduYears = await _context.EduYears
                    .Where(e => e.IsActive && e.RootCode == item.RootCode)
                    .Select(e => new SelectListItem { Value = e.EduCode.ToString(), Text = e.EduName })
                    .ToListAsync();

                var availableBranches = await _context.Branches
                    .Where(b => b.RootCode == item.RootCode && b.IsActive)
                    .Select(b => new SelectListItem { Value = b.BranchCode.ToString(), Text = b.BranchName })
                    .ToListAsync();

                // Only use first available EduYear
                int? selectedEduYearCode = availableEduYears.Count > 0
                    ? int.Parse(availableEduYears[0].Value)
                    : (int?)null;

                var availableYears = selectedEduYearCode.HasValue
                    ? await _context.Years
                        .Where(y => y.EduYearCode == selectedEduYearCode)
                        .Select(y => new SelectListItem { Value = y.YearCode.ToString(), Text = y.YearName })
                        .ToListAsync()
                    : new List<SelectListItem>();

                var viewModel = new StudentRegistrationViewModel
                {
                    ItemKey = item_key,
                    RootName = item.RootCodeNavigation?.RootName ?? "Unknown",
                    AvailableBranches = availableBranches,
                    AvailableYears = availableYears,
                    AvailableEduYears = availableEduYears,
                    HasExistingStudent = hasExistingStudent
                };

                if (hasExistingStudent && item.StudentCodeNavigation != null)
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

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading registration page for item key {ItemKey}", item_key);
                return NotFound("An error occurred while loading the registration page.");
            }
        }
        [HttpGet]
        [Route("Student/GetYearsForEduYear/{eduYearCode}")]
        public async Task<IActionResult> GetYearsForEduYear(int eduYearCode)
        {
            try
            {
                var years = await _context.Years
                    .Where(y => y.EduYearCode == eduYearCode)
                    .Select(y => new { Value = y.YearCode, Text = y.YearName })
                    .ToListAsync();

                return Json(years);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading years for EduYear code {EduYearCode}", eduYearCode);
                return Json(new { error = "Failed to load years." });
            }
        }

        [HttpGet]
        [Route("Student/GetAvailableSubjects/{item_key}")]
        public async Task<IActionResult> GetAvailableSubjects(string item_key, int branchCode, int? yearCode = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item_key))
                {
                    return Json(new { error = "Item key is required." });
                }

                // Updated validation - Allow items with existing students
                var item = await _context.Items
                    .Where(i => i.ItemKey == item_key && i.IsActive)  // Removed !i.StudentCode.HasValue
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Json(new { error = "Invalid item key." });
                }

                // Get available subjects based on branch and year
                IQueryable<Teach> query = _context.Teaches
                    .Where(t => t.BranchCode == branchCode &&
                               t.RootCode == item.RootCode &&
                               t.IsActive)
                    .Include(t => t.SubjectCodeNavigation);

                if (yearCode.HasValue)
                {
                    query = query.Where(t => t.YearCode == yearCode.Value);
                }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available subjects for item key {ItemKey}", item_key);
                return Json(new { error = "Failed to load subjects. Please try again." });
            }
        }

        /// <summary>
        /// GET: Student/GetAvailableTeachers/{item_key} - Get available teachers for selected subjects
        /// </summary>
        [HttpGet]
        [Route("Student/GetAvailableTeachers/{item_key}")]
        public async Task<IActionResult> GetAvailableTeachers(string item_key, string subjectCodes, int branchCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item_key))
                {
                    return Json(new { error = "Item key is required." });
                }

                var subjectCodeList = subjectCodes.Split(',').Select(int.Parse).ToList();

                // Get the item and root info to determine user type
                var item = await _context.Items
                    .Include(i => i.RootCodeNavigation)
                    .Where(i => i.ItemKey == item_key && i.IsActive)
                    .FirstOrDefaultAsync();

                if (item == null || item.RootCodeNavigation == null)
                {
                    return Json(new { error = "Invalid item key or root not found." });
                }

                // Check if this is a center user or teacher user based on Root.IsCenter
                bool isCenterUser = item.RootCodeNavigation.IsCenter;

                if (!isCenterUser)
                {
                    // This is a teacher user - get the single teacher for this root
                    var teacherAssignments = await _context.Teaches
        .Include(t => t.TeacherCodeNavigation)
        .Where(t => t.IsActive &&
                   t.RootCode == item.RootCode &&
                   t.BranchCode == branchCode &&
                   subjectCodeList.Contains(t.SubjectCode))
        .ToListAsync();

                    if (teacherAssignments.Any())
                    {
                        return Json(teacherAssignments.Select(t => new
                        {
                            TeacherCode = t.TeacherCode,
                            TeacherName = t.TeacherCodeNavigation.TeacherName,
                            TeacherPhone = t.TeacherCodeNavigation.TeacherPhone,
                            SubjectCode = t.SubjectCode
                        }));
                    }
                }
                else
                {
                    // This is a center user - get all teachers assigned to these subjects
                    var teachersForSubjects = await _context.Teaches
                        .Include(t => t.TeacherCodeNavigation)
                        .Where(t => t.IsActive &&
                                   t.RootCode == item.RootCode &&
                                   t.BranchCode == branchCode &&
                                   subjectCodeList.Contains(t.SubjectCode))
                        .Select(t => new
                        {
                            TeacherCode = t.TeacherCode,
                            TeacherName = t.TeacherCodeNavigation.TeacherName,
                            TeacherPhone = t.TeacherCodeNavigation.TeacherPhone,
                            SubjectCode = t.SubjectCode
                        })
                        .Distinct()
                        .OrderBy(t => t.TeacherName)
                        .ToListAsync();

                    if (teachersForSubjects.Any())
                    {
                        return Json(teachersForSubjects);
                    }
                }

                return Json(new { error = "No teachers are currently assigned to teach these subjects at this branch." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available teachers for item key {ItemKey}", item_key);
                return Json(new { error = "Failed to load teachers. Please try again." });
            }
        }
        [HttpGet]
        [Route("Student/GetAvailableSchedules/{item_key}")]
        public async Task<IActionResult> GetAvailableSchedules(
    string item_key,
    string subjectCodes,
    string teacherCodes,
    int branchCode,
    int? yearCode = null)
        {
            try
            {
                _logger.LogInformation(
                    "GetAvailableSchedules called with parameters: itemKey={ItemKey}, subjectCodes={SubjectCodes}, teacherCodes={TeacherCodes}, branchCode={BranchCode}, yearCode={YearCode}",
                    item_key, subjectCodes, teacherCodes, branchCode, yearCode);

                if (string.IsNullOrWhiteSpace(subjectCodes))
                {
                    return Json(new { error = "Subject codes are required." });
                }

                // Get the item and root info
                var item = await _context.Items
                    .Include(i => i.RootCodeNavigation)
                    .Where(i => i.ItemKey == item_key && i.IsActive)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    _logger.LogWarning("Item not found or inactive for itemKey: {ItemKey}", item_key);
                    return Json(new { error = "Invalid item key." });
                }

                var subjectCodeList = subjectCodes.Split(',').Select(int.Parse).ToList();
                var teacherCodeList = teacherCodes.Split(',').Select(int.Parse).ToList();

                // Get current education year
                var currentEduYear = await _context.EduYears
                    .Where(e => e.IsActive && e.RootCode == item.RootCode)
                    .OrderByDescending(e => e.EduCode)
                    .FirstOrDefaultAsync();

                if (currentEduYear == null)
                {
                    _logger.LogWarning("No active education year found for root: {RootCode}", item.RootCode);
                    return Json(new { error = "No active education year found." });
                }

                // Get schedules with detailed includes
                var schedules = await _context.Schedules
                    .Include(s => s.SubjectCodeNavigation)
                    .Include(s => s.TeacherCodeNavigation)
                    .Include(s => s.HallCodeNavigation)
                    .Where(s => subjectCodeList.Contains(s.SubjectCode.Value) &&
                               teacherCodeList.Contains(s.TeacherCode.Value) &&
                               s.BranchCode == branchCode &&
                               s.RootCode == item.RootCode &&
                               (yearCode == null || s.YearCode == yearCode) &&
                               s.EduYearCode == currentEduYear.EduCode)
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

                _logger.LogInformation(
                    "Found {Count} schedules for itemKey={ItemKey}, subjects={Subjects}, teachers={Teachers}",
                    schedules.Count, item_key, string.Join(",", subjectCodeList), string.Join(",", teacherCodeList));

                return Json(schedules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting available schedules. ItemKey={ItemKey}, SubjectCodes={SubjectCodes}, TeacherCodes={TeacherCodes}, BranchCode={BranchCode}",
                    item_key, subjectCodes, teacherCodes, branchCode);
                return Json(new { error = $"Failed to load schedules: {ex.Message}" });
            }
        }
        /// <summary>
        /// POST: Student/Register - Process student registration
        /// </summary>
        [HttpPost]
        [Route("Student/Register")]
        public async Task<IActionResult> Register([FromBody] StudentRegistrationRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Starting registration for item key: {ItemKey}", request?.ItemKey);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return Json(new { success = false, errors = errors });
                }

                // Validate item key - allow both new and existing students
                var item = await _context.Items
                    .Include(i => i.RootCodeNavigation)
                    .Include(i => i.StudentCodeNavigation) // Include existing student if any
                    .Where(i => i.ItemKey == request.ItemKey && i.IsActive)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Json(new { success = false, error = "Invalid item key." });
                }

                Student student;
                bool isUpdate = item.StudentCode.HasValue && item.StudentCodeNavigation != null;

                if (isUpdate)
                {
                    // Update existing student
                    student = item.StudentCodeNavigation!;
                    _logger.LogInformation("Updating existing student: {StudentCode}", student.StudentCode);

                    // Update basic information
                    student.StudentName = request.StudentName?.Trim();
                    student.StudentPhone = request.StudentPhone?.Trim();
                    student.StudentFatherPhone = request.StudentParentPhone?.Trim();
                    student.StudentBirthdate = request.BirthDate;
                    student.StudentGender = request.Gender;
                    student.BranchCode = request.BranchCode;
                    student.YearCode = request.YearCode;
                    student.LastInsertUser = 1;
                    student.LastInsertTime = DateTime.Now;
                }
                else
                {
                    // Create new student (legacy path)
                    student = new Student
                    {
                        StudentName = request.StudentName?.Trim(),
                        StudentPhone = request.StudentPhone?.Trim(),
                        StudentFatherPhone = request.StudentParentPhone?.Trim(),
                        StudentBirthdate = request.BirthDate,
                        StudentGender = request.Gender,
                        BranchCode = request.BranchCode,
                        YearCode = request.YearCode,
                        RootCode = item.RootCode,
                        IsActive = true,
                        InsertUser = 1,
                        InsertTime = DateTime.Now,
                        SubscribtionTime = DateOnly.FromDateTime(DateTime.Today)
                    };

                    _context.Students.Add(student);
                }

                // Pre-validate all foreign key references
                await ValidateForeignKeyReferences(request, item);

                _logger.LogInformation("{Action} student: {@Student}", isUpdate ? "Updating" : "Creating", new
                {
                    student.StudentName,
                    student.BranchCode,
                    student.YearCode,
                    student.RootCode,
                    student.StudentBirthdate,
                    student.StudentGender
                });

                try
                {
                    await _context.SaveChangesAsync(); // Save to get StudentCode for new students
                    _logger.LogInformation("Student {Action} successfully with code: {StudentCode}",
                        isUpdate ? "updated" : "created", student.StudentCode);
                }
                catch (Exception studentSaveEx)
                {
                    _logger.LogError(studentSaveEx, "Failed to {Action} student entity", isUpdate ? "update" : "save");
                    var detailedError = GetDetailedEntityError(studentSaveEx);
                    return Json(new { success = false, error = $"Failed to {(isUpdate ? "update" : "create")} student: {detailedError}" });
                }

                // Link item to student if not already linked
                if (!isUpdate)
                {
                    item.StudentCode = student.StudentCode;
                    item.LastUpdateUser = 1;
                    item.LastUpdateTime = DateTime.Now;
                }

                // Create Learn records with selected schedules
                if (request.SelectedSubjects?.Any() == true)
                {
                    await CreateLearnRecords(request, student);
                }

                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Registration {Action} successfully for student: {StudentCode}",
                        isUpdate ? "update completed" : "completed", student.StudentCode);

                    return Json(new
                    {
                        success = true,
                        message = isUpdate ? "Academic enrollment updated successfully!" : "Registration successful! Welcome to our system.",
                        redirectUrl = $"/Student/{request.ItemKey}"
                    });
                }
                catch (Exception finalSaveEx)
                {
                    _logger.LogError(finalSaveEx, "Failed to save final changes");
                    var detailedError = GetDetailedEntityError(finalSaveEx);
                    return Json(new { success = false, error = $"Failed to complete registration: {detailedError}" });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Registration failed for item key {ItemKey}", request?.ItemKey);

                var detailedError = GetDetailedEntityError(ex);
                return Json(new { success = false, error = $"Registration failed: {detailedError}" });
            }
        }

        [HttpPost]
        [Route("Student/CheckAttendancePassword")]
        public async Task<IActionResult> CheckAttendancePassword([FromBody] AttendancePasswordRequest req)
        {
            var allowedGroups = new[] { "Admins", "Attendance Officers" };

            // Get allowed users in the branch
            var users = await (from u in _context.Users
                               join g in _context.Groups on u.GroupCode equals g.GroupCode
                               where g.BranchCode == req.BranchCode && allowedGroups.Contains(g.GroupName)
                               select new { u.UserCode, u.Password, u.Username }).ToListAsync();

            foreach (var user in users)
            {
                if (user.Password == req.Password) // Plain text
                {
                    // Simulate login: set user identity (you may need to adapt this for your auth system)
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("UserId", user.UserCode.ToString())
            };
                    var identity = new ClaimsIdentity(claims, "AttendancePassword");
                    var principal = new ClaimsPrincipal(identity);

                    // Set the user as logged in for this request
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

            // If no match
            return Json(new
            {
                success = false,
                error = "Wrong password or not authorized for this branch."
            });
        }

        // ==================== REGISTRATION HELPER METHODS ====================

        private string CreateMD5(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                    sb.Append(hashBytes[i].ToString("X2"));  // uppercase hex
                return sb.ToString();
            }
        }
        private string GetDetailedEntityError(Exception ex)
        {
            var message = ex.Message;

            if (ex.InnerException != null)
            {
                message += $" Inner Exception: {ex.InnerException.Message}";

                // Check for SQL Server specific errors
                if (ex.InnerException.Message.Contains("FOREIGN KEY constraint"))
                {
                    message += " - This indicates a reference to a non-existent record in a related table.";
                }
                else if (ex.InnerException.Message.Contains("PRIMARY KEY constraint"))
                {
                    message += " - This indicates a duplicate key violation.";
                }
                else if (ex.InnerException.Message.Contains("cannot be null"))
                {
                    message += " - This indicates a required field is missing.";
                }
            }

            return message;
        }

        private async Task ValidateForeignKeyReferences(StudentRegistrationRequest request, Item item)
        {
            // Validate Branch
            var branchExists = await _context.Branches
                .AnyAsync(b => b.BranchCode == request.BranchCode && b.IsActive);
            if (!branchExists)
            {
                throw new InvalidOperationException($"Branch with code {request.BranchCode} not found or inactive.");
            }

            // Validate Year if provided
            if (request.YearCode.HasValue)
            {
                var yearExists = await _context.Years
                    .AnyAsync(y => y.YearCode == request.YearCode.Value);
                if (!yearExists)
                {
                    throw new InvalidOperationException($"Year with code {request.YearCode} not found.");
                }
            }

            // Validate Root
            var rootExists = await _context.Roots
                .AnyAsync(r => r.RootCode == item.RootCode && r.IsActive);
            if (!rootExists)
            {
                throw new InvalidOperationException($"Root with code {item.RootCode} not found or inactive.");
            }

            // Validate selected subjects, teachers, and schedules
            if (request.SelectedSubjects?.Any() == true)
            {
                foreach (var selection in request.SelectedSubjects)
                {
                    // Validate teacher teaches subject at branch
                    var teachExists = await _context.Teaches
                        .AnyAsync(t => t.SubjectCode == selection.SubjectCode &&
                                     t.TeacherCode == selection.TeacherCode &&
                                     t.BranchCode == request.BranchCode &&
                                     t.RootCode == item.RootCode &&
                                     t.IsActive);

                    if (!teachExists)
                    {
                        throw new InvalidOperationException(
                            $"Teacher {selection.TeacherCode} does not teach subject {selection.SubjectCode} at branch {request.BranchCode}.");
                    }

                    // Validate schedule exists and matches
                    var scheduleExists = await _context.Schedules
                        .AnyAsync(s => s.ScheduleCode == selection.ScheduleCode &&
                                     s.SubjectCode == selection.SubjectCode &&
                                     s.TeacherCode == selection.TeacherCode &&
                                     s.BranchCode == request.BranchCode &&
                                     s.RootCode == item.RootCode);

                    if (!scheduleExists)
                    {
                        throw new InvalidOperationException(
                            $"Schedule {selection.ScheduleCode} is not valid for subject {selection.SubjectCode} with teacher {selection.TeacherCode}.");
                    }
                }
            }
        }

        private async Task CreateLearnRecords(StudentRegistrationRequest request, Student student)
        {
            // Get current education year
            var currentEduYear = await _context.EduYears
                .Where(e => e.IsActive && e.RootCode == student.RootCode)
                .OrderByDescending(e => e.EduCode)
                .FirstOrDefaultAsync();

            if (currentEduYear == null)
            {
                throw new InvalidOperationException("No active education year found for the student's root.");
            }

            foreach (var selection in request.SelectedSubjects)
            {
                // Validate that schedule exists and is available
                var schedule = await _context.Schedules
                    .FirstOrDefaultAsync(s => s.ScheduleCode == selection.ScheduleCode &&
                                            s.SubjectCode == selection.SubjectCode &&
                                            s.TeacherCode == selection.TeacherCode &&
                                            s.BranchCode == student.BranchCode);

                if (schedule == null)
                {
                    throw new InvalidOperationException($"Selected schedule is no longer available for {selection.SubjectName}.");
                }

                // Check capacity
                var currentStudents = await _context.Learns
                    .CountAsync(l => l.ScheduleCode == selection.ScheduleCode && l.IsActive);

                if (currentStudents >= 30) // Default capacity
                {
                    throw new InvalidOperationException($"Schedule for {selection.SubjectName} is full.");
                }

                var learn = new Learn
                {
                    StudentCode = student.StudentCode,
                    SubjectCode = selection.SubjectCode,
                    TeacherCode = selection.TeacherCode,
                    ScheduleCode = selection.ScheduleCode,
                    EduYearCode = currentEduYear.EduCode,
                    BranchCode = student.BranchCode, // <-- FIX: Use the student's actual branch code!
                    RootCode = student.RootCode,
                    YearCode = student.YearCode,
                    IsOnline = selection.IsOnline,
                    IsActive = true,
                    InsertUser = 1,
                    InsertTime = DateTime.Now,
                    LastUpdateUser = 1,
                    LastUpdateTime = DateTime.Now,
                    StudentFee = selection.StudentFee
                };

                _context.Learns.Add(learn);
            }
        }

        // ==================== PUBLIC REGISTRATION METHODS ====================

        /// <summary>
        /// GET: Register/{root_code} - Show public registration page
        /// </summary>
        [HttpGet]
        [Route("Register/{root_code:int}")]
        public async Task<IActionResult> PublicRegister(int root_code)
        {
            try
            {
                // Validate that the root exists and is active
                var root = await _context.Roots
                    .Where(r => r.RootCode == root_code && r.IsActive)
                    .FirstOrDefaultAsync();

                if (root == null)
                {
                    return NotFound("Registration center not found or inactive.");
                }

                // Get available branches for this root
                var availableBranches = await _context.Branches
                    .Where(b => b.RootCode == root_code && b.IsActive)
                    .Select(b => new SelectListItem { Value = b.BranchCode.ToString(), Text = b.BranchName })
                    .ToListAsync();

                // Get the active EduYear for this root
                var activeEduYear = await _context.EduYears
                    .Where(e => e.RootCode == root_code && e.IsActive)
                    .OrderByDescending(e => e.EduCode)
                    .FirstOrDefaultAsync();

                List<SelectListItem> availableYears = new();
                if (activeEduYear != null)
                {
                    // Only get years for the active EduYear
                    availableYears = await _context.Years
                        .Where(y => y.EduYearCode == activeEduYear.EduCode)
                        .Select(y => new SelectListItem { Value = y.YearCode.ToString(), Text = y.YearName })
                        .ToListAsync();
                }

                var viewModel = new PublicRegistrationViewModel
                {
                    RootCode = root_code,
                    RootName = root.RootName,
                    AvailableBranches = availableBranches,
                    AvailableYears = availableYears
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public registration page for root code {RootCode}", root_code);
                return NotFound("An error occurred while loading the registration page.");
            }
        }

        /// <summary>
        /// POST: Register/{root_code} - Process public registration
        /// </summary>
        [HttpPost]
        [Route("Register/{root_code:int}")]
        public async Task<IActionResult> PublicRegister(int root_code, [FromForm] PublicRegistrationRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Starting public registration for root code: {RootCode}", root_code);

                // Ensure root_code matches request
                request.RootCode = root_code;

                if (!ModelState.IsValid)
                {
                    // Reload the view with validation errors
                    var root = await _context.Roots
                        .Where(r => r.RootCode == root_code && r.IsActive)
                        .FirstOrDefaultAsync();

                    if (root == null)
                    {
                        return NotFound("Registration center not found.");
                    }

                    var availableBranches = await _context.Branches
                        .Where(b => b.RootCode == root_code && b.IsActive)
                        .Select(b => new SelectListItem { Value = b.BranchCode.ToString(), Text = b.BranchName })
                        .ToListAsync();

                    var availableYears = await _context.Years
                        .Select(y => new SelectListItem { Value = y.YearCode.ToString(), Text = y.YearName })
                        .ToListAsync();

                    var viewModel = new PublicRegistrationViewModel
                    {
                        RootCode = root_code,
                        RootName = root.RootName,
                        AvailableBranches = availableBranches,
                        AvailableYears = availableYears
                    };

                    return View(viewModel);
                }

                // Validate root exists and is active
                var rootEntity = await _context.Roots
                    .Where(r => r.RootCode == root_code && r.IsActive)
                    .FirstOrDefaultAsync();

                if (rootEntity == null)
                {
                    ModelState.AddModelError("", "Registration center not found or inactive.");
                    return await PublicRegister(root_code); // Reload view with error
                }

                // Validate branch belongs to this root and is active
                var branchExists = await _context.Branches
                    .AnyAsync(b => b.BranchCode == 27 && b.RootCode == 1 && b.IsActive);

                if (!branchExists)
                {
                    ModelState.AddModelError("BranchCode", "Selected branch is not available for this center.");
                    return await PublicRegister(root_code); // Reload view with error
                }

                // Validate year if provided
                if (request.YearCode.HasValue)
                {
                    var yearExists = await _context.Years
                        .AnyAsync(y => y.YearCode == request.YearCode.Value);
                    if (!yearExists)
                    {
                        ModelState.AddModelError("YearCode", "Selected year is not valid.");
                        return await PublicRegister(root_code); // Reload view with error
                    }
                }

                // Get default EduYear for this root
                var defaultEduYear = await GetDefaultEduYearForRoot(root_code);

                // Create student record immediately
                var student = new Student
                {
                    StudentName = request.StudentName?.Trim(),
                    StudentPhone = request.StudentPhone?.Trim(),
                    StudentFatherPhone = request.StudentParentPhone?.Trim(),
                    StudentBirthdate = request.BirthDate,
                    StudentGender = request.Gender,
                    BranchCode = 27,
                    YearCode = request.YearCode,
                    RootCode = root_code,
                    IsActive = true,
                    InsertUser = 1, // Default system user for public registration
                    InsertTime = DateTime.Now,
                    SubscribtionTime = DateOnly.FromDateTime(DateTime.Today)
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Public registration completed successfully for student: {StudentCode} in root: {RootCode}",
                    student.StudentCode, root_code);

                // Redirect to success page with student info
                TempData["SuccessMessage"] = $"Registration successful! Welcome {student.StudentName}. " +
                    "You will receive your access key from the center. Use it to complete your academic enrollment.";
                TempData["StudentCode"] = student.StudentCode;
                TempData["RootName"] = rootEntity.RootName;

                return RedirectToAction("PublicRegisterSuccess", new { root_code = root_code });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Public registration failed for root code {RootCode}", root_code);

                ModelState.AddModelError("", "Registration failed. Please try again or contact support.");
                return await PublicRegister(root_code); // Reload view with error
            }
        }

        /// <summary>
        /// GET: Register/{root_code}/Success - Show public registration success page
        /// </summary>
        [HttpGet]
        [Route("Register/{root_code:int}/Success")]
        public IActionResult PublicRegisterSuccess(int root_code)
        {
            var successMessage = TempData["SuccessMessage"] as string;
            var studentCode = TempData["StudentCode"] as int?;
            var rootName = TempData["RootName"] as string;

            if (string.IsNullOrEmpty(successMessage))
            {
                return RedirectToAction("PublicRegister", new { root_code = root_code });
            }

            ViewBag.SuccessMessage = successMessage;
            ViewBag.StudentCode = studentCode;
            ViewBag.RootName = rootName ?? "Registration Center";
            ViewBag.RootCode = root_code;

            return View();
        }

        // ==================== STUDENT SEARCH AND LINKING METHODS ====================

        /// <summary>
        /// GET: Student/Search/{item_key} - Show student search interface
        /// </summary>
        [HttpGet]
        [Route("Student/Search/{item_key}")]
        public async Task<IActionResult> SearchStudent(string item_key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item_key))
                {
                    return NotFound("Item key is required.");
                }

                // Find the item and validate it exists and is active but has no student linked
                var item = await _context.Items
                    .Include(i => i.RootCodeNavigation)
                    .Where(i => i.ItemKey == item_key && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return NotFound("Item not found, already linked, or access denied.");
                }

                var viewModel = new StudentSearchViewModel
                {
                    ItemKey = item_key,
                    RootName = item.RootCodeNavigation?.RootName ?? "Unknown",
                    RootCode = item.RootCode
                };

                return View("StudentSearch", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student search page for item key {ItemKey}", item_key);
                return NotFound("An error occurred while loading the search page.");
            }
        }

        /// <summary>
        /// POST: Student/SearchByPhone - Search students by phone number
        /// </summary>
        [HttpPost]
        [Route("Student/SearchByPhone")]
        public async Task<IActionResult> SearchByPhone([FromBody] StudentSearchRequest request)
        {
            var debugInfo = new List<string>();

            try
            {
                _logger.LogInformation("DEBUG: Starting SearchByPhone for request {@Request}", request);
                debugInfo.Add($"Request received: ItemKey={request?.ItemKey}, Phone={request?.StudentPhone}");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    _logger.LogWarning("DEBUG: ModelState validation failed: {Errors}", string.Join(", ", errors));
                    debugInfo.Add($"ModelState errors: {string.Join(", ", errors)}");
                    return Json(new { success = false, errors = errors, debug = debugInfo });
                }

                debugInfo.Add("ModelState validation passed");

                // Test database connectivity
                try
                {
                    var dbTest = await _context.Database.CanConnectAsync();
                    _logger.LogInformation("DEBUG: Database connectivity test result: {CanConnect}", dbTest);
                    debugInfo.Add($"Database connectivity: {dbTest}");

                    if (!dbTest)
                    {
                        return Json(new { success = false, error = "Database connection failed.", debug = debugInfo });
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "DEBUG: Database connectivity test failed");
                    debugInfo.Add($"Database connectivity error: {dbEx.Message}");
                    return Json(new { success = false, error = "Database connection test failed.", debug = debugInfo });
                }

                // Validate item exists and has no student linked
                _logger.LogInformation("DEBUG: Searching for item with key {ItemKey}", request.ItemKey);
                debugInfo.Add($"Searching for item with key: {request.ItemKey}");

                var item = await _context.Items
                    .Where(i => i.ItemKey == request.ItemKey && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                _logger.LogInformation("DEBUG: Item search result: {Found}", item != null);
                debugInfo.Add($"Item found: {item != null}");

                if (item != null)
                {
                    debugInfo.Add($"Item details: RootCode={item.RootCode}, IsActive={item.IsActive}, HasStudent={item.StudentCode.HasValue}");
                    _logger.LogInformation("DEBUG: Item details - RootCode: {RootCode}, IsActive: {IsActive}, HasStudent: {HasStudent}",
                        item.RootCode, item.IsActive, item.StudentCode.HasValue);
                }

                if (item == null)
                {
                    _logger.LogWarning("DEBUG: Item not found or invalid state for key {ItemKey}", request.ItemKey);
                    debugInfo.Add("Item validation failed: not found or already linked");
                    return Json(new { success = false, error = "Invalid item key or already linked.", debug = debugInfo });
                }

                // Search for students with the same phone number in the same root
                var phoneToSearch = request.StudentPhone.Trim();
                _logger.LogInformation("DEBUG: Searching for students with phone {Phone} in root {RootCode}", phoneToSearch, item.RootCode);
                debugInfo.Add($"Searching students with phone: {phoneToSearch} in root: {item.RootCode}");

                var students = await _context.Students
                    .Where(s => s.StudentPhone == phoneToSearch &&
                               s.RootCode == item.RootCode &&
                               s.IsActive)
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

                _logger.LogInformation("DEBUG: Found {Count} students with phone {Phone} in root {RootCode}",
                    students.Count, phoneToSearch, item.RootCode);
                debugInfo.Add($"Students found: {students.Count}");

                if (students.Any())
                {
                    debugInfo.Add($"Student names: {string.Join(", ", students.Select(s => s.StudentName))}");
                }

                _logger.LogInformation("DEBUG: SearchByPhone completed successfully");
                debugInfo.Add("Search completed successfully");

                return Json(new { success = true, students = students, debug = debugInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: Error searching students by phone for request {@Request}. Stack trace: {StackTrace}",
                    request, ex.StackTrace);
                debugInfo.Add($"Exception occurred: {ex.Message}");
                debugInfo.Add($"Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    debugInfo.Add($"Inner exception: {ex.InnerException.Message}");
                }

                return Json(new
                {
                    success = false,
                    error = "An error occurred while searching for students.",
                    debug = debugInfo,
                    exception = ex.Message,
                    stackTrace = ex.StackTrace?.Split('\n')?.Take(5)?.ToArray() // First 5 lines only
                });
            }
        }

        /// <summary>
        /// GET: Student/Debug/PhoneSearch - Debug endpoint for phone search testing
        /// </summary>
        [HttpGet]
        [Route("Student/Debug/PhoneSearch")]
        public async Task<IActionResult> DebugPhoneSearch(string itemKey = "", string phone = "")
        {
            var debugResult = new
            {
                timestamp = DateTime.Now,
                itemKey = itemKey,
                phone = phone,
                databaseConnectivity = false,
                itemExists = false,
                itemDetails = (object?)null,
                studentCount = 0,
                students = new List<object>(),
                error = (string?)null,
                logs = new List<string>()
            };

            var logs = new List<string>();

            try
            {
                logs.Add($"Debug endpoint called at {DateTime.Now}");
                logs.Add($"Parameters: itemKey='{itemKey}', phone='{phone}'");

                // Test database connectivity
                var canConnect = await _context.Database.CanConnectAsync();
                logs.Add($"Database connectivity: {canConnect}");

                if (!canConnect)
                {
                    return Json(new
                    {
                        debugResult.timestamp,
                        debugResult.itemKey,
                        debugResult.phone,
                        databaseConnectivity = false,
                        debugResult.itemExists,
                        debugResult.itemDetails,
                        debugResult.studentCount,
                        debugResult.students,
                        error = "Database connection failed",
                        logs = logs
                    });
                }

                // If no parameters provided, just return connectivity test
                if (string.IsNullOrEmpty(itemKey) || string.IsNullOrEmpty(phone))
                {
                    logs.Add("No itemKey or phone provided - returning connectivity test only");
                    return Json(new
                    {
                        debugResult.timestamp,
                        debugResult.itemKey,
                        debugResult.phone,
                        databaseConnectivity = true,
                        debugResult.itemExists,
                        debugResult.itemDetails,
                        debugResult.studentCount,
                        debugResult.students,
                        error = "Please provide itemKey and phone parameters",
                        logs = logs
                    });
                }

                // Test item lookup
                var item = await _context.Items
                    .Where(i => i.ItemKey == itemKey && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                logs.Add($"Item search for key '{itemKey}': {(item != null ? "Found" : "Not found")}");

                object? itemDetails = null;
                if (item != null)
                {
                    itemDetails = new
                    {
                        item.ItemKey,
                        item.RootCode,
                        item.IsActive,
                        HasStudent = item.StudentCode.HasValue,
                        item.StudentCode
                    };
                    logs.Add($"Item details: RootCode={item.RootCode}, IsActive={item.IsActive}, HasStudent={item.StudentCode.HasValue}");
                }

                // Test student search if item found
                var students = new List<object>();
                var studentCount = 0;

                if (item != null)
                {
                    var phoneToSearch = phone.Trim();
                    logs.Add($"Searching students with phone '{phoneToSearch}' in root {item.RootCode}");

                    var foundStudents = await _context.Students
                        .Where(s => s.StudentPhone == phoneToSearch &&
                                   s.RootCode == item.RootCode &&
                                   s.IsActive)
                        .Include(s => s.BranchCodeNavigation)
                        .Include(s => s.YearCodeNavigation)
                        .ToListAsync();

                    studentCount = foundStudents.Count;
                    logs.Add($"Found {studentCount} students");

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

                    if (foundStudents.Any())
                    {
                        logs.Add($"Student names: {string.Join(", ", foundStudents.Select(s => s.StudentName))}");
                    }
                }

                return Json(new
                {
                    debugResult.timestamp,
                    debugResult.itemKey,
                    debugResult.phone,
                    databaseConnectivity = true,
                    itemExists = item != null,
                    itemDetails = itemDetails,
                    studentCount = studentCount,
                    students = students,
                    debugResult.error,
                    logs = logs
                });
            }
            catch (Exception ex)
            {
                logs.Add($"Exception: {ex.Message}");
                logs.Add($"Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    logs.Add($"Inner exception: {ex.InnerException.Message}");
                }

                return Json(new
                {
                    debugResult.timestamp,
                    debugResult.itemKey,
                    debugResult.phone,
                    debugResult.databaseConnectivity,
                    debugResult.itemExists,
                    debugResult.itemDetails,
                    debugResult.studentCount,
                    debugResult.students,
                    error = ex.Message,
                    logs = logs
                });
            }
        }

        /// <summary>
        /// POST: Student/LinkStudent - Link found student to item
        /// </summary>
        [HttpPost]
        [Route("Student/LinkStudent")]
        public async Task<IActionResult> LinkStudent([FromBody] LinkStudentRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    return Json(new { success = false, errors = errors });
                }

                // Validate item exists and has no student linked
                var item = await _context.Items
                    .Where(i => i.ItemKey == request.ItemKey && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Json(new { success = false, error = "Invalid item key or already linked." });
                }

                // Validate student exists and belongs to the same root
                var student = await _context.Students
                    .Where(s => s.StudentCode == request.StudentCode &&
                               s.RootCode == item.RootCode &&
                               s.IsActive)
                    .FirstOrDefaultAsync();

                if (student == null)
                {
                    return Json(new { success = false, error = "Student not found or not available for linking." });
                }

                // Check if student is already linked to another active item
                var existingItem = await _context.Items
                    .Where(i => i.StudentCode == student.StudentCode && i.IsActive)
                    .FirstOrDefaultAsync();

                if (existingItem != null)
                {
                    return Json(new
                    {
                        success = false,
                        error = $"Student is already linked to item key: {existingItem.ItemKey}"
                    });
                }

                // Link student to item
                item.StudentCode = student.StudentCode;
                item.LastUpdateUser = 1; // Default system user
                item.LastUpdateTime = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully linked student {StudentCode} to item {ItemKey}",
                    student.StudentCode, request.ItemKey);

                return Json(new
                {
                    success = true,
                    message = "Student linked successfully! Redirecting to complete your academic enrollment.",
                    redirectUrl = $"/Student/Register/{request.ItemKey}"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error linking student for request {@Request}", request);
                return Json(new { success = false, error = "An error occurred while linking the student." });
            }
        }

        // ==================== EXISTING PROFILE METHODS ====================

        /// <summary>
        /// GET: Student/Profile/{item_key} - Show student profile page
        /// </summary>
        [HttpGet]
        [Route("Student/{item_key}")]
        public async Task<IActionResult> Profile(string item_key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item_key))
                {
                    return NotFound("Item key is required.");
                }

                // Find the item and validate it exists and is active
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
                {
                    return NotFound("Student profile not found or access denied.");
                }
                if (item.StudentCodeNavigation == null)
                {
                    // Item exists but no student is linked - redirect to search interface
                    return RedirectToAction("SearchStudent", new { item_key = item_key });
                }

                var student = item.StudentCodeNavigation;

                // FIX: Pass both rootCode and branchCode from student
                var canMarkAttendance = await IsCurrentUserAdmin(student.RootCode, student.BranchCode);

                // Create view model with basic student information
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

                _logger.LogInformation("Loading profile for student {StudentName} with item key {ItemKey}",
                    student.StudentName, item_key);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student profile for item key {ItemKey}", item_key);
                return NotFound("An error occurred while loading the profile.");
            }
        }
        [HttpGet]
        [Route("Student/StudentData/{item_key}")]
        public async Task<IActionResult> StudentData(string item_key)
        {
            // You can reuse the same logic as your Profile action:
            // Get the student/profile data to show in the StudentData view
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
            {
                return NotFound("Student profile not found or access denied.");
            }

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
                CanMarkAttendance = await IsCurrentUserAdmin(student.RootCode, student.BranchCode)
            };

            return View("StudentData", viewModel);
        }

        /// <summary>
        /// GET: Student/GetUpcomingClasses/{item_key} - API endpoint to get upcoming classes for attendance
        /// Now accessible to everyone, but only admins can mark attendance
        /// </summary>
        [HttpGet]
        [Route("Student/GetUpcomingClasses/{item_key}")]
        public async Task<IActionResult> GetUpcomingClasses(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                var currentTime = DateTime.Now;
                var today = DateOnly.FromDateTime(currentTime);

                // Get all classes for today first, then filter by time
                var todaysClasses = await _context.Classes
                    .Where(c => c.ClassDate.HasValue && c.ClassDate == today &&
                               c.ClassStartTime.HasValue && c.ClassEndTime.HasValue)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.ScheduleCodeNavigation)
                    .Where(c => _context.Learns.Any(l => l.StudentCode == student.StudentCode &&
                                                        l.SubjectCode == c.SubjectCode &&
                                                        l.TeacherCode == c.TeacherCode &&
                                                        l.IsActive))
                    .ToListAsync();

                // Filter classes available from 1 hour before start until class ends
                var availableClasses = todaysClasses
                    .Where(c => c.ClassStartTime.HasValue && c.ClassEndTime.HasValue)
                    .Where(c =>
                    {
                        var classStartTime = c.ClassStartTime.Value.ToTimeSpan();
                        var classEndTime = c.ClassEndTime.Value.ToTimeSpan();
                        var currentTimeSpan = currentTime.TimeOfDay;

                        // Class is available from 1 hour before start until class ends
                        var availableFromTime = classStartTime.Subtract(TimeSpan.FromHours(1));
                        return currentTimeSpan >= availableFromTime && currentTimeSpan <= classEndTime;
                    })
                    .OrderBy(c => c.ClassStartTime)
                    .ToList();

                // Use root/branch from student for admin check
                var isAdmin = await IsCurrentUserAdmin(student.RootCode, student.BranchCode);

                var classResults = new List<object>();

                // Preload all active learns for this student (for efficiency)
                var learns = await _context.Learns
                    .Where(l => l.StudentCode == student.StudentCode && l.IsActive)
                    .ToListAsync();

                foreach (var c in availableClasses)
                {
                    var hasAttended = await _context.Attends
                        .AnyAsync(a => a.StudentId == student.StudentCode && a.ClassId == c.ClassCode);

                    // Find the assigned schedule for this (subject, teacher, year)
                    int? assignedScheduleCode = learns
                        .FirstOrDefault(l =>
                            l.SubjectCode == c.SubjectCode &&
                            l.TeacherCode == c.TeacherCode &&
                            l.YearCode == student.YearCode
                        )?.ScheduleCode;

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
                        assignedScheduleCode = assignedScheduleCode, // <-- This line added!
                        hallCode = c.HallCode,
                        classPrice = c.ClassPrice,
                        canAttend = !hasAttended,
                        canMarkAttendance = isAdmin // Only admins can mark attendance
                    });
                }

                _logger.LogInformation("Retrieved {Count} available classes for student {StudentCode}",
                    classResults.Count, student.StudentCode);

                return Json(classResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving upcoming classes for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Student/GetWeeklyClasses/{item_key} - API endpoint to get all weekly classes for a specific subject/teacher/year/branch
        /// </summary>
        [HttpGet]
        [Route("Student/GetWeeklyClasses/{item_key}")]
        public async Task<IActionResult> GetWeeklyClasses(string item_key, int subjectCode, int teacherCode, int yearCode)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                // Get the student's assigned schedule code for this subject/teacher/year
                var assignedLearn = await _context.Learns
                    .FirstOrDefaultAsync(l => l.StudentCode == student.StudentCode
                        && l.SubjectCode == subjectCode
                        && l.TeacherCode == teacherCode
                        && l.YearCode == yearCode
                        && l.IsActive);

                int? assignedScheduleCode = assignedLearn?.ScheduleCode;

                var hasAccess = assignedLearn != null;

                if (!hasAccess)
                {
                    return Json(new { error = "Student does not have access to this subject/teacher combination." });
                }

                // Get current week date range (Sunday to Saturday)
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek); // Sunday
                var endOfWeek = startOfWeek.AddDays(6); // Saturday

                var startDate = DateOnly.FromDateTime(startOfWeek);
                var endDate = DateOnly.FromDateTime(endOfWeek);

                var weeklyClasses = await _context.Classes
                    .Where(c => c.ClassDate.HasValue &&
                               c.ClassDate >= startDate &&
                               c.ClassDate <= endDate &&
                               c.SubjectCode == subjectCode &&
                               c.TeacherCode == teacherCode &&
                               c.YearCode == yearCode &&
                               c.ClassStartTime.HasValue &&
                               c.ClassEndTime.HasValue)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.ScheduleCodeNavigation)
                    .OrderBy(c => c.ClassDate)
                    .ThenBy(c => c.ClassStartTime)
                    .ToListAsync();

                var isAdmin = await IsCurrentUserAdmin(student.RootCode, student.BranchCode);

                var classResults = new List<object>();

                foreach (var c in weeklyClasses)
                {
                    var hasAttended = await _context.Attends
                        .AnyAsync(a => a.StudentId == student.StudentCode && a.ClassId == c.ClassCode);

                    // Check if class is currently available to attend (1 hour before start until end)
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
                        assignedScheduleCode = assignedScheduleCode, // <--- send this to frontend
                        hallCode = c.HallCode,
                        totalAmount = c.TotalAmount,
                        isAttended = hasAttended,
                        isCurrentlyAvailable = isCurrentlyAvailable,
                        canAttend = !hasAttended && isCurrentlyAvailable,
                        canMarkAttendance = isAdmin
                    });
                }

                _logger.LogInformation("Retrieved {Count} weekly classes for student {StudentCode}, subject {SubjectCode}, teacher {TeacherCode}",
                    classResults.Count, student.StudentCode, subjectCode, teacherCode);

                return Json(classResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving weekly classes for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST: Student/MarkAttendance - API endpoint to mark student attendance
        /// </summary>
        // Add this to your StudentController

        [HttpPost]
        [Route("Student/MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.ItemKey))
                {
                    return Json(new { success = false, error = "Invalid request." });
                }

                var student = await GetStudentByItemKey(request.ItemKey);
                if (student == null)
                {
                    return Json(new { success = false, error = "Student not found." });
                }

                var classEntity = await _context.Classes
                    .Include(c => c.ScheduleCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.TeacherCodeNavigation)
                    .FirstOrDefaultAsync(c => c.ClassCode == request.ClassCode);

                if (classEntity == null)
                {
                    return Json(new { success = false, error = "Class not found." });
                }

                if (!await IsCurrentUserAdmin(student.RootCode, classEntity.BranchCode))
                {
                    return Json(new { success = false, error = "Only administrators can mark attendance." });
                }

                var existingAttendance = await _context.Attends
                    .FirstOrDefaultAsync(a => a.StudentId == student.StudentCode &&
                                              a.ClassId == request.ClassCode);

                if (existingAttendance != null)
                {
                    return Json(new { success = false, error = "Student has already been marked as attended for this class." });
                }

                // Find Discount type code robustly (case insensitive, both Arabic and English)
                int discountTypeCode = 0;
                var discountType = await _context.Lockups.FirstOrDefaultAsync(l =>
                    l.PaymentName != null && (
                        l.PaymentName.ToLower().Contains("discount") ||
                        l.PaymentName.Contains("خصم")
                    )
                );
                if (discountType != null)
                    discountTypeCode = discountType.PaymentCode;

                // Log incoming values for debugging 
                _logger.LogInformation("MarkAttendance: Received AttendanceType={AttendanceType}, SessionPrice={SessionPrice}, DiscountTypeCode={DiscountTypeCode}",
                    request.AttendanceType, request.SessionPrice, discountTypeCode);

                // Use discount price if Discount type selected and SessionPrice is present and positive
                decimal sessionPriceToSave = classEntity.ClassPrice ?? 0;
                if (request.AttendanceType == discountTypeCode && request.SessionPrice.HasValue && request.SessionPrice.Value > 0)
                {
                    sessionPriceToSave = request.SessionPrice.Value;
                    _logger.LogInformation("MarkAttendance: Using DISCOUNT session price: {SessionPrice}", sessionPriceToSave);
                }
                else
                {
                    _logger.LogInformation("MarkAttendance: Using REGULAR session price: {SessionPrice}", sessionPriceToSave);
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
                    Type = request.AttendanceType
                };

                _context.Attends.Add(attendance);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Marked attendance for student {StudentCode} in class {ClassCode} by user {Username} with price {SessionPrice}",
                    student.StudentCode, request.ClassCode, User.Identity.Name, sessionPriceToSave);

                return Json(new
                {
                    success = true,
                    message = "Attendance marked successfully.",
                    attendanceDate = attendance.AttendDate.ToString("yyyy-MM-dd HH:mm")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking attendance for request {@Request}", request);
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                return Json(new { success = false, error = errorMessage, stackTrace = ex.StackTrace });
            }
        }



        /// <summary>
        /// GET: Student/GetAttendanceTypes - API endpoint to get available attendance types
        /// </summary>
        [HttpGet]
        [Route("Student/GetAttendanceTypes")]
        public async Task<IActionResult> GetAttendanceTypes()
        {
            try
            {
                var attendanceTypes = await _context.Lockups
                    .Where(l => l.PaymentCode > 0 && l.PaymentName != null && l.PaymentName != "")
                    .Select(l => new
                    {
                        value = l.PaymentCode,
                        text = l.PaymentName
                    })
                    .ToListAsync();

                return Json(attendanceTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance types");
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Student/GetStudentSubjects/{item_key} - API endpoint to get student's subjects with schedule info
        /// </summary>
        [HttpGet]
        [Route("Student/GetStudentSubjects/{item_key}")]
        public async Task<IActionResult> GetStudentSubjects(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                var subjects = await _context.Learns
                    .Where(l => l.StudentCode == student.StudentCode && l.IsActive)
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .Include(l => l.BranchCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Include(l => l.ScheduleCodeNavigation)
                        .ThenInclude(s => s.HallCodeNavigation)
                    .Select(l => new
                    {
                        subjectCode = l.SubjectCode,
                        subjectName = l.SubjectCodeNavigation.SubjectName,
                        teacherCode = l.TeacherCode,
                        teacherName = l.TeacherCodeNavigation.TeacherName,
                        teacherPhone = l.TeacherCodeNavigation.TeacherPhone,
                        branchCode = l.BranchCode,
                        branchName = l.BranchCodeNavigation.BranchName,
                        branchAddress = l.BranchCodeNavigation.Address,
                        branchPhone = l.BranchCodeNavigation.Phone,
                        eduYearName = l.EduYearCodeNavigation.EduName,
                        studentFee = l.StudentFee,
                        isOnline = l.IsOnline,
                        yearCode = l.YearCode,
                        insertTime = l.InsertTime.ToString("yyyy-MM-dd"),
                        rootCode = student.RootCode,
                        // Schedule information directly from ScheduleCodeNavigation
                        scheduleDay = l.ScheduleCodeNavigation != null ? l.ScheduleCodeNavigation.DayOfWeek : null,
                        scheduleStartTime = l.ScheduleCodeNavigation != null && l.ScheduleCodeNavigation.StartTime.HasValue
                            ? l.ScheduleCodeNavigation.StartTime.Value.ToString("HH:mm") : null,
                        scheduleEndTime = l.ScheduleCodeNavigation != null && l.ScheduleCodeNavigation.EndTime.HasValue
                            ? l.ScheduleCodeNavigation.EndTime.Value.ToString("HH:mm") : null,
                        hallName = l.ScheduleCodeNavigation != null && l.ScheduleCodeNavigation.HallCodeNavigation != null
                            ? l.ScheduleCodeNavigation.HallCodeNavigation.HallName : null,
                        scheduleAmount = l.ScheduleCodeNavigation != null ? l.ScheduleCodeNavigation.ScheduleAmount : null
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} subjects for student {StudentCode}",
                    subjects.Count, student.StudentCode);

                return Json(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subjects for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Student/GetStudentPlans/{item_key} - API endpoint to get subscription plans
        /// </summary>
        [HttpGet]
        [Route("Student/GetStudentPlans/{item_key}")]
        public async Task<IActionResult> GetStudentPlans(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                var plans = await _context.StudentPlans
                    .Where(sp => sp.StudentCode == student.StudentCode && sp.IsActive)
                    .Include(sp => sp.SubscriptionPlanCodeNavigation)
                    .Include(sp => sp.EduYearCodeNavigation)
                    .OrderByDescending(sp => sp.SubDate)
                    .Select(sp => new
                    {
                        planName = sp.SubscriptionPlanCodeNavigation.SubPlanName,
                        description = sp.SubscriptionPlanCodeNavigation.Description,
                        price = sp.Price,
                        subDate = sp.SubDate.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"),
                        expiryDate = sp.ExpiryDate.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"),
                        isExpired = sp.IsExpired,
                        eduYearName = sp.EduYearCodeNavigation.EduName,
                        totalCount = sp.SubscriptionPlanCodeNavigation.TotalCount,
                        expiryMonths = sp.SubscriptionPlanCodeNavigation.ExpiryMonths,
                        isActive = sp.IsActive,
                        rootCode = student.RootCode,
                        daysRemaining = sp.IsExpired ? 0 : (sp.ExpiryDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days
                    })
                    .ToListAsync();

                return Json(plans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving plans for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Student/GetStudentAttendance/{item_key} - API endpoint to get attendance history
        /// </summary>
        [HttpGet]
        [Route("Student/GetStudentAttendance/{item_key}")]
        public async Task<IActionResult> GetStudentAttendance(string item_key, int page = 1, int pageSize = 10)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                var totalCount = await _context.Attends
                    .Where(a => a.StudentId == student.StudentCode)
                    .CountAsync();

                var attendance = await _context.Attends
                    .Where(a => a.StudentId == student.StudentCode)
                    .Include(a => a.TeacherCodeNavigation)
                    .Include(a => a.Class)
                        .ThenInclude(c => c.SubjectCodeNavigation)
                    .Include(a => a.Hall)
                    .Include(a => a.ScheduleCodeNavigation)
                    .Include(a => a.TypeNavigation)
                    .OrderByDescending(a => a.AttendDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        attendDate = a.AttendDate.ToString("yyyy-MM-dd HH:mm"),
                        teacherName = a.TeacherCodeNavigation.TeacherName,
                        subjectName = a.Class.SubjectCodeNavigation != null ? a.Class.SubjectCodeNavigation.SubjectName : "N/A",
                        className = a.Class.ClassName,
                        hallName = a.Hall.HallName,
                        sessionPrice = a.SessionPrice,
                        type = a.TypeNavigation != null ? a.TypeNavigation.PaymentName : "Regular",
                        rootCode = a.RootCode,
                        scheduleTime = a.ScheduleCodeNavigation != null && a.ScheduleCodeNavigation.StartTime.HasValue
                            ? $"{a.ScheduleCodeNavigation.StartTime.Value:HH:mm} - {a.ScheduleCodeNavigation.EndTime.Value:HH:mm}"
                            : null
                    })
                    .ToListAsync();

                return Json(new
                {
                    attendance = attendance,
                    totalCount = totalCount,
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Student/GetStudentExams/{item_key} - API endpoint to get exam results
        /// </summary>
        [HttpGet]
        [Route("Student/GetStudentExams/{item_key}")]
        public async Task<IActionResult> GetStudentExams(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                var exams = await _context.StudentExams
                    .Where(se => se.StudentCode == student.StudentCode && se.IsActive == true)
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.SubjectCodeNavigation)
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.TeacherCodeNavigation)
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.EduYearCodeNavigation)
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.LessonCodeNavigation)
                    .OrderByDescending(se => se.InsertTime)
                    .Select(se => new
                    {
                        examName = se.ExamCodeNavigation.ExamName,
                        subjectName = se.ExamCodeNavigation.SubjectCodeNavigation != null ? se.ExamCodeNavigation.SubjectCodeNavigation.SubjectName : "N/A",
                        teacherName = se.ExamCodeNavigation.TeacherCodeNavigation != null ? se.ExamCodeNavigation.TeacherCodeNavigation.TeacherName : "N/A",
                        eduYearName = se.ExamCodeNavigation.EduYearCodeNavigation != null ? se.ExamCodeNavigation.EduYearCodeNavigation.EduName : "N/A",
                        lessonName = se.ExamCodeNavigation.LessonCodeNavigation != null ? se.ExamCodeNavigation.LessonCodeNavigation.LessonName : "N/A",
                        examDegree = se.ExamDegree,
                        studentResult = se.StudentResult,
                        studentPercentage = se.StudentPercentage,
                        examDate = se.InsertTime.HasValue ? se.InsertTime.Value.ToString("yyyy-MM-dd") : "N/A",
                        examTime = se.ExamCodeNavigation.ExamTimer.ToString(@"hh\:mm"),
                        isOnline = se.ExamCodeNavigation.IsOnline,
                        examType = se.ExamCodeNavigation.IsExam ? "Exam" : "Quiz",
                        passed = se.StudentPercentage >= 50,
                        grade = GetGrade(se.StudentPercentage),
                        rootCode = student.RootCode
                    })
                    .ToListAsync();

                return Json(exams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving exams for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Student/GetStudentStats/{item_key} - API endpoint to get student statistics
        /// </summary>
        [HttpGet]
        [Route("Student/GetStudentStats/{item_key}")]
        public async Task<IActionResult> GetStudentStats(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                // Get various statistics
                var subjectsCount = await _context.Learns
                    .Where(l => l.StudentCode == student.StudentCode && l.IsActive)
                    .CountAsync();

                var totalAttendance = await _context.Attends
                    .Where(a => a.StudentId == student.StudentCode)
                    .CountAsync();

                var examsCount = await _context.StudentExams
                    .Where(se => se.StudentCode == student.StudentCode && se.IsActive == true)
                    .CountAsync();

                var averageGrade = await _context.StudentExams
                    .Where(se => se.StudentCode == student.StudentCode && se.IsActive == true && se.StudentPercentage.HasValue)
                    .AverageAsync(se => se.StudentPercentage ?? 0);

                var activePlansCount = await _context.StudentPlans
                    .Where(sp => sp.StudentCode == student.StudentCode && sp.IsActive && !sp.IsExpired)
                    .CountAsync();

                var recentAttendance = await _context.Attends
                    .Where(a => a.StudentId == student.StudentCode && a.AttendDate >= DateTime.Today.AddDays(-30))
                    .CountAsync();

                return Json(new
                {
                    subjectsCount = subjectsCount,
                    totalAttendance = totalAttendance,
                    examsCount = examsCount,
                    averageGrade = Math.Round(averageGrade, 1),
                    activePlansCount = activePlansCount,
                    recentAttendance = recentAttendance,
                    memberSince = student.SubscribtionTime.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy"),
                    rootCode = student.RootCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stats for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }
        [HttpGet]
        [Route("Student/GetUpcomingExams/{item_key}")]
        public async Task<IActionResult> GetUpcomingExams(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                    return Json(new { error = "Student not found." });

                var learns = await _context.Learns
                    .Where(l => l.StudentCode == student.StudentCode && l.IsActive == true)
                    .ToListAsync();

                var exams = await _context.Exams
                    .Where(ex => ex.IsActive == true && ex.IsExam == true)
                    .Include(ex => ex.SubjectCodeNavigation)
                    .Include(ex => ex.TeacherCodeNavigation)
                    .ToListAsync();

                var attendedExamCodes = await _context.StudentExams
                    .Where(se => se.StudentCode == student.StudentCode && se.IsActive == true)
                    .Select(se => se.ExamCode)
                    .ToListAsync();

                var upcoming = new List<object>();
                foreach (var exam in exams)
                {
                    bool hasMatchingLearn = exam.BranchCode == null
                        ? learns.Any(learn =>
                            learn.EduYearCode == exam.EduYearCode &&
                            learn.TeacherCode == exam.TeacherCode &&
                            learn.SubjectCode == exam.SubjectCode &&
                            learn.YearCode == exam.YearCode)
                        : learns.Any(learn =>
                            learn.EduYearCode == exam.EduYearCode &&
                            learn.TeacherCode == exam.TeacherCode &&
                            learn.SubjectCode == exam.SubjectCode &&
                            learn.YearCode == exam.YearCode &&
                            learn.BranchCode == exam.BranchCode);

                    if (hasMatchingLearn)
                    {
                        upcoming.Add(new
                        {
                            examCode = exam.ExamCode,
                            examName = exam.ExamName,
                            subjectName = exam.SubjectCodeNavigation != null ? exam.SubjectCodeNavigation.SubjectName : "N/A",
                            teacherName = exam.TeacherCodeNavigation != null ? exam.TeacherCodeNavigation.TeacherName : "N/A",
                            attended = attendedExamCodes.Contains(exam.ExamCode),
                            isExam = exam.IsExam,      // <-- ADD THIS
                            isDone = exam.IsDone
                        });
                    }
                }

                return Json(upcoming);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving upcoming exams for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }
        [HttpGet]
        [Route("Student/GetAssignments/{item_key}")]
        public async Task<IActionResult> GetAssignments(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                    return Json(new { error = "Student not found." });

                var learns = await _context.Learns
                    .Where(l => l.StudentCode == student.StudentCode && l.IsActive == true)
                    .ToListAsync();

                var assignments = await _context.Exams
                    .Where(ex => ex.IsActive == true && ex.IsExam == false)
                    .Include(ex => ex.SubjectCodeNavigation)
                    .Include(ex => ex.TeacherCodeNavigation)
                    .ToListAsync();

                var attendedExamCodes = await _context.StudentExams
                    .Where(se => se.StudentCode == student.StudentCode && se.IsActive == true)
                    .Select(se => se.ExamCode)
                    .ToListAsync();

                var result = new List<object>();
                foreach (var exam in assignments)
                {
                    bool hasMatchingLearn = exam.BranchCode == null
                        ? learns.Any(learn =>
                            learn.EduYearCode == exam.EduYearCode &&
                            learn.TeacherCode == exam.TeacherCode &&
                            learn.SubjectCode == exam.SubjectCode &&
                            learn.YearCode == exam.YearCode)
                        : learns.Any(learn =>
                            learn.EduYearCode == exam.EduYearCode &&
                            learn.TeacherCode == exam.TeacherCode &&
                            learn.SubjectCode == exam.SubjectCode &&
                            learn.YearCode == exam.YearCode &&
                            learn.BranchCode == exam.BranchCode);

                    if (hasMatchingLearn)
                    {
                        result.Add(new
                        {
                            examCode = exam.ExamCode,
                            examName = exam.ExamName,
                            subjectName = exam.SubjectCodeNavigation != null ? exam.SubjectCodeNavigation.SubjectName : "N/A",
                            teacherName = exam.TeacherCodeNavigation != null ? exam.TeacherCodeNavigation.TeacherName : "N/A",
                            attended = attendedExamCodes.Contains(exam.ExamCode),
                            isExam = exam.IsExam,      // <-- ADD THIS
                            isDone = exam.IsDone
                        });
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assignments for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }
        [HttpGet]
        [Route("Student/GetAttendedExams/{item_key}")]
        public async Task<IActionResult> GetAttendedExams(string item_key)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                    return Json(new { error = "Student not found." });

                var attended = await _context.StudentExams
                    .Where(se => se.StudentCode == student.StudentCode
                                 && se.IsActive == true
                                 && se.ExamCodeNavigation.IsExam == true) // <-- Only real exams!
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.SubjectCodeNavigation)
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.TeacherCodeNavigation)
                    .OrderByDescending(se => se.InsertTime)
                    .Select(se => new
                    {
                        examName = se.ExamCodeNavigation.ExamName,
                        subjectName = se.ExamCodeNavigation.SubjectCodeNavigation != null ? se.ExamCodeNavigation.SubjectCodeNavigation.SubjectName : "N/A",
                        teacherName = se.ExamCodeNavigation.TeacherCodeNavigation != null ? se.ExamCodeNavigation.TeacherCodeNavigation.TeacherName : "N/A",
                        degree = se.StudentResult,
                        examDegree = se.ExamDegree,
                        examDate = se.InsertTime.HasValue ? se.InsertTime.Value.ToString("yyyy-MM-dd") : null,
                    })
                    .ToListAsync();

                return Json(attended);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attended exams for item key {ItemKey}", item_key);
                return Json(new { error = ex.Message });
            }
        }

        // ==================== HELPER METHODS ====================

        private async Task<Student> GetStudentByItemKey(string item_key)
        {
            if (string.IsNullOrWhiteSpace(item_key))
                return null;

            var item = await _context.Items
                .Include(i => i.StudentCodeNavigation)
                .Where(i => i.ItemKey == item_key && i.IsActive)
                .FirstOrDefaultAsync();

            return item?.StudentCodeNavigation;
        }

        private async Task<int> GetDefaultEduYearForRoot(int rootCode)
        {
            var firstEduYear = await _context.EduYears
                .AsNoTracking()
                .Where(e => e.RootCode == rootCode && e.IsActive)
                .Select(e => e.EduCode)
                .FirstOrDefaultAsync();

            return firstEduYear > 0 ? firstEduYear : 1; // Fallback to 1 if no edu year found
        }

        private int CalculateAge(DateOnly birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;
            return age;
        }

        // Check if current user is an admin
        private async Task<bool> IsCurrentUserAdmin(int rootCode, int branchCode)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                    return false;

                // Get UserId from claims (adapt claim type as necessary)
                var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                    return false;

                // Get the user entity (with GroupCode FK)
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
                if (user == null)
                    return false;

                // Find the group the user belongs to
                var group = await _context.Groups
                    .FirstOrDefaultAsync(g =>
                        g.GroupCode == user.GroupCode &&
                        g.GroupName == "Admins" &&
                        g.RootCode == rootCode &&
                        (g.BranchCode == branchCode || g.BranchCode == null)
                    );

                return group != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin permission for user {Username}", User.Identity?.Name ?? "Anonymous");
                return false;
            }
        }

        // Made static to fix Entity Framework memory leak warning
        private static string GetGrade(double? percentage)
        {
            if (!percentage.HasValue) return "N/A";

            return percentage.Value switch
            {
                >= 90 => "A+",
                >= 85 => "A",
                >= 80 => "B+",
                >= 75 => "B",
                >= 70 => "C+",
                >= 65 => "C",
                >= 60 => "D+",
                >= 50 => "D",
                _ => "F"
            };
        }

        // Helper method to convert day names to numbers for sorting
        private static int GetDayOfWeekNumber(string dayOfWeek)
        {
            return dayOfWeek?.ToLower() switch
            {
                "sunday" => 0,
                "monday" => 1,
                "tuesday" => 2,
                "wednesday" => 3,
                "thursday" => 4,
                "friday" => 5,
                "saturday" => 6,
                _ => 7 // Unknown days go to the end
            };
        }
    }

    // ==================== VIEW MODELS & REQUEST CLASSES ====================

    public class StudentProfileViewModel
    {
        public string ItemKey { get; set; } = string.Empty;
        public int StudentCode { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentPhone { get; set; } = string.Empty;
        public string StudentParentPhone { get; set; } = string.Empty;
        public DateOnly StudentBirthdate { get; set; }
        public bool? StudentGender { get; set; }
        public DateOnly SubscriptionTime { get; set; }
        public bool IsActive { get; set; }
        public string? BranchName { get; set; }
        public string? YearName { get; set; }
        public string? LevelName { get; set; }
        public string? RootName { get; set; }
        public int RootCode { get; set; }
        public int? YearCode { get; set; }
        public int Age { get; set; }
        public bool CanMarkAttendance { get; set; }
    }

    public class StudentRegistrationViewModel
    {
        public string ItemKey { get; set; } = string.Empty;
        public string RootName { get; set; } = string.Empty;
        public List<SelectListItem> AvailableBranches { get; set; } = new();
        public List<SelectListItem> AvailableYears { get; set; } = new();
        public List<SelectListItem> AvailableEduYears { get; set; } = new();

        // Properties for pre-existing student
        public bool HasExistingStudent { get; set; }
        public string? ExistingStudentName { get; set; }
        public string? ExistingStudentPhone { get; set; }
        public string? ExistingStudentParentPhone { get; set; }
        public DateOnly? ExistingBirthDate { get; set; }
        public bool? ExistingGender { get; set; }
        public int? ExistingBranchCode { get; set; }
        public int? ExistingYearCode { get; set; }
    }

    public class StudentRegistrationRequest
    {
        [Required]
        public string ItemKey { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string StudentName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string StudentPhone { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string StudentParentPhone { get; set; } = string.Empty;

        [Required]
        public DateOnly BirthDate { get; set; }

        public bool? Gender { get; set; }

        [Required]
        public int BranchCode { get; set; }

        public int? YearCode { get; set; }

        public List<SubjectSelectionRequest> SelectedSubjects { get; set; } = new();
    }

    public class SubjectSelectionRequest
    {
        public int SubjectCode { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int TeacherCode { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public int ScheduleCode { get; set; }
        public string ScheduleName { get; set; } = string.Empty;
        public int? EduYearCode { get; set; }
        public bool IsOnline { get; set; }
        public int? StudentFee { get; set; }
    }

    // Your request class:
    public class MarkAttendanceRequest
    {
        public string ItemKey { get; set; } = string.Empty;
        public int ClassCode { get; set; }
        public int AttendanceType { get; set; }
        public int? SessionPrice { get; set; }
    }
    // ==================== PUBLIC REGISTRATION MODELS ====================

    public class PublicRegistrationViewModel
    {
        public int RootCode { get; set; }
        public string RootName { get; set; } = string.Empty;
        public List<SelectListItem> AvailableBranches { get; set; } = new();
        public List<SelectListItem> AvailableYears { get; set; } = new();
    }

    public class PublicRegistrationRequest
    {
        [Required]
        public int RootCode { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string StudentName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string StudentPhone { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string StudentParentPhone { get; set; } = string.Empty;

        [Required]
        public DateOnly BirthDate { get; set; }

        public bool? Gender { get; set; }

        [Required]
        public int BranchCode { get; set; }

        public int? YearCode { get; set; }
    }

    // ==================== STUDENT SEARCH MODELS ====================

    public class StudentSearchViewModel
    {
        public string ItemKey { get; set; } = string.Empty;
        public string RootName { get; set; } = string.Empty;
        public int RootCode { get; set; }
    }

    public class StudentSearchRequest
    {
        [Required]
        public string ItemKey { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string StudentPhone { get; set; } = string.Empty;
    }
    public class AttendancePasswordRequest
    {
        public int BranchCode { get; set; }
        public string Password { get; set; }
    }

    public class LinkStudentRequest
    {
        [Required]
        public string ItemKey { get; set; } = string.Empty;

        [Required]
        public int StudentCode { get; set; }
    }
}