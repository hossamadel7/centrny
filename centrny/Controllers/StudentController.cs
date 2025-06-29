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

                // Find the item and validate it exists and is active
                var item = await _context.Items
                    .Include(i => i.RootCodeNavigation)
                    .Where(i => i.ItemKey == item_key && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return NotFound("Item not found or already registered.");
                }

                // Get available branches for this root
                var availableBranches = await _context.Branches
                    .Where(b => b.RootCode == item.RootCode && b.IsActive)
                    .Select(b => new SelectListItem { Value = b.BranchCode.ToString(), Text = b.BranchName })
                    .ToListAsync();

                // Get available years
                var availableYears = await _context.Years
                    .Select(y => new SelectListItem { Value = y.YearCode.ToString(), Text = y.YearName })
                    .ToListAsync();

                // Get available education years - Fixed property name from EduYearCode to EduCode
                var currentYear = DateTime.Now.Year;
                var availableEduYears = await _context.EduYears
                    .Where(e => e.IsActive && e.RootCode == item.RootCode)
                    .Select(e => new SelectListItem { Value = e.EduCode.ToString(), Text = e.EduName })
                    .ToListAsync();

                var viewModel = new StudentRegistrationViewModel
                {
                    ItemKey = item_key,
                    RootName = item.RootCodeNavigation?.RootName ?? "Unknown",
                    AvailableBranches = availableBranches,
                    AvailableYears = availableYears,
                    AvailableEduYears = availableEduYears
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading registration page for item key {ItemKey}", item_key);
                return NotFound("An error occurred while loading the registration page.");
            }
        }

        /// <summary>
        /// GET: Student/GetAvailableSubjects/{item_key} - Get available subjects for registration
        /// </summary>
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

                // Validate item exists
                var item = await _context.Items
                    .Where(i => i.ItemKey == item_key && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Json(new { error = "Invalid item key or already registered." });
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
                if (string.IsNullOrWhiteSpace(item_key) || string.IsNullOrWhiteSpace(subjectCodes))
                {
                    return Json(new { error = "Item key and subject codes are required." });
                }

                var subjectCodeList = subjectCodes.Split(',').Select(int.Parse).ToList();

                // Validate item exists
                var item = await _context.Items
                    .Where(i => i.ItemKey == item_key && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Json(new { error = "Invalid item key or already registered." });
                }

                // Get available teachers for the selected subjects
                var teachers = await _context.Teaches
                    .Where(t => subjectCodeList.Contains(t.SubjectCode) &&
                               t.BranchCode == branchCode &&
                               t.RootCode == item.RootCode &&
                               t.IsActive)
                    .Include(t => t.TeacherCodeNavigation)
                    .Select(t => new
                    {
                        TeacherCode = t.TeacherCode,
                        TeacherName = t.TeacherCodeNavigation.TeacherName,
                        TeacherPhone = t.TeacherCodeNavigation.TeacherPhone,
                        SubjectCode = t.SubjectCode
                    })
                    .OrderBy(t => t.TeacherName)
                    .ToListAsync();

                return Json(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available teachers for item key {ItemKey}", item_key);
                return Json(new { error = "Failed to load teachers. Please try again." });
            }
        }

        /// <summary>
        /// GET: Student/GetAvailableSchedules/{item_key} - Get available schedules for selected subjects
        /// </summary>
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
                if (string.IsNullOrWhiteSpace(subjectCodes) || string.IsNullOrWhiteSpace(teacherCodes))
                {
                    return Json(new { error = "Subject codes and teacher codes are required." });
                }

                var subjectCodeList = subjectCodes.Split(',').Select(int.Parse).ToList();
                var teacherCodeList = teacherCodes.Split(',').Select(int.Parse).ToList();

                // Validate item exists
                var item = await _context.Items
                    .Where(i => i.ItemKey == item_key && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Json(new { error = "Invalid item key or already registered." });
                }

                // Get current education year
                var currentEduYear = await _context.EduYears
                    .Where(e => e.IsActive && e.RootCode == item.RootCode)
                    .OrderByDescending(e => e.EduCode)
                    .FirstOrDefaultAsync();

                if (currentEduYear == null)
                {
                    return Json(new { error = "No active education year found." });
                }

                // Find available schedules - Fixed property name from EduYearCode to EduYearCode
                var schedules = await _context.Schedules
                    .Include(s => s.SubjectCodeNavigation)
                    .Include(s => s.TeacherCodeNavigation)
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
                        TeacherCode = s.TeacherCode,
                        DayName = s.DayOfWeek ?? "N/A",
                        StartTime = s.StartTime.HasValue ? s.StartTime.Value.ToString("HH:mm") : "N/A",
                        EndTime = s.EndTime.HasValue ? s.EndTime.Value.ToString("HH:mm") : "N/A",
                        Capacity = 30, // Default capacity
                        CurrentStudents = _context.Learns.Count(l => l.ScheduleCode == s.ScheduleCode && l.IsActive),
                        YearCode = s.YearCode,
                        BranchCode = s.BranchCode
                    })
                    .ToListAsync();

                // Filter schedules with available spots
                var availableSchedules = schedules
                    .Where(s => s.CurrentStudents < s.Capacity)
                    .OrderBy(s => s.SubjectCode)
                    .ThenBy(s => s.DayName)
                    .ThenBy(s => s.StartTime)
                    .ToList();

                return Json(availableSchedules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available schedules for item key {ItemKey}", item_key);
                return Json(new { error = "Failed to load schedules. Please try again." });
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

                // Validate item key
                var item = await _context.Items
                    .Include(i => i.RootCodeNavigation)
                    .Where(i => i.ItemKey == request.ItemKey && i.IsActive && !i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item == null)
                {
                    return Json(new { success = false, error = "Invalid item key or already registered." });
                }

                // Pre-validate all foreign key references
                await ValidateForeignKeyReferences(request, item);

                // Create student
                var student = new Student
                {
                    StudentName = request.StudentName?.Trim(),
                    StudentPhone = request.StudentPhone?.Trim(),
                    StudentParentPhone = request.StudentParentPhone?.Trim(),
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

                _logger.LogInformation("Adding student to context: {@Student}", new
                {
                    student.StudentName,
                    student.BranchCode,
                    student.YearCode,
                    student.RootCode,
                    student.StudentBirthdate,
                    student.StudentGender
                });

                _context.Students.Add(student);

                try
                {
                    await _context.SaveChangesAsync(); // Save to get StudentCode
                    _logger.LogInformation("Student saved successfully with code: {StudentCode}", student.StudentCode);
                }
                catch (Exception studentSaveEx)
                {
                    _logger.LogError(studentSaveEx, "Failed to save student entity");
                    var detailedError = GetDetailedEntityError(studentSaveEx);
                    return Json(new { success = false, error = $"Failed to create student: {detailedError}" });
                }

                // Link item to student
                item.StudentCode = student.StudentCode;
                item.LastUpdateUser = 1;
                item.LastUpdateTime = DateTime.Now;

                // Create Learn records with selected schedules
                if (request.SelectedSubjects?.Any() == true)
                {
                    await CreateLearnRecords(request, student);
                }

                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Registration completed successfully for student: {StudentCode}", student.StudentCode);

                    return Json(new
                    {
                        success = true,
                        message = "Registration successful! Welcome to our system.",
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

        // ==================== REGISTRATION HELPER METHODS ====================

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
                    EduYearCode = currentEduYear.EduCode, // Use the correct EduCode
                    BranchCode = student.BranchCode,
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

                if (item == null || item.StudentCodeNavigation == null)
                {
                    return NotFound("Student profile not found or access denied.");
                }

                var student = item.StudentCodeNavigation;

                // Create view model with basic student information
                var viewModel = new StudentProfileViewModel
                {
                    ItemKey = item_key,
                    StudentCode = student.StudentCode,
                    StudentName = student.StudentName,
                    StudentPhone = student.StudentPhone,
                    StudentParentPhone = student.StudentParentPhone,
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
                    CanMarkAttendance = await IsCurrentUserAdmin()
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
                var oneHourBefore = currentTime.AddHours(-1); // 1 hour before current time
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

                var classResults = new List<object>();
                var isAdmin = await IsCurrentUserAdmin();

                foreach (var c in availableClasses)
                {
                    var hasAttended = await _context.Attends
                        .AnyAsync(a => a.StudentId == student.StudentCode && a.ClassId == c.ClassCode);

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
                        hallCode = c.HallCode,
                        totalAmount = c.TotalAmount,
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
        public async Task<IActionResult> GetWeeklyClasses(string item_key, int subjectCode, int teacherCode, int yearCode, int branchCode)
        {
            try
            {
                var student = await GetStudentByItemKey(item_key);
                if (student == null)
                {
                    return Json(new { error = "Student not found." });
                }

                // Verify student has access to this subject/teacher combination
                var hasAccess = await _context.Learns
                    .AnyAsync(l => l.StudentCode == student.StudentCode &&
                                  l.SubjectCode == subjectCode &&
                                  l.TeacherCode == teacherCode &&
                                  l.IsActive);

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

                // Get all classes for this week matching the criteria
                var weeklyClasses = await _context.Classes
                    .Where(c => c.ClassDate.HasValue &&
                               c.ClassDate >= startDate &&
                               c.ClassDate <= endDate &&
                               c.SubjectCode == subjectCode &&
                               c.TeacherCode == teacherCode &&
                               c.BranchCode == branchCode &&
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
                        hallCode = c.HallCode,
                        totalAmount = c.TotalAmount,
                        isAttended = hasAttended,
                        isCurrentlyAvailable = isCurrentlyAvailable,
                        canAttend = !hasAttended && isCurrentlyAvailable
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
        [HttpPost]
        [Route("Student/MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            try
            {
                // Validate request
                if (request == null || string.IsNullOrEmpty(request.ItemKey))
                {
                    return Json(new { success = false, error = "Invalid request." });
                }

                var student = await GetStudentByItemKey(request.ItemKey);
                if (student == null)
                {
                    return Json(new { success = false, error = "Student not found." });
                }

                // Check authorization (admins only)
                if (!await IsCurrentUserAdmin())
                {
                    return Json(new { success = false, error = "Only administrators can mark attendance." });
                }

                // Get class details
                var classEntity = await _context.Classes
                    .Include(c => c.ScheduleCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.TeacherCodeNavigation)
                    .FirstOrDefaultAsync(c => c.ClassCode == request.ClassCode);

                if (classEntity == null)
                {
                    return Json(new { success = false, error = "Class not found." });
                }

                // Check for duplicate attendance
                var existingAttendance = await _context.Attends
                    .FirstOrDefaultAsync(a => a.StudentId == student.StudentCode &&
                                            a.ClassId == request.ClassCode);

                if (existingAttendance != null)
                {
                    return Json(new { success = false, error = "Student has already been marked as attended for this class." });
                }

                // Create attendance record
                var attendance = new Attend
                {
                    TeacherCode = classEntity.TeacherCode,
                    ScheduleCode = classEntity.ScheduleCode ?? 0, // Use 0 if null
                    ClassId = request.ClassCode,
                    HallId = classEntity.HallCode,
                    StudentId = student.StudentCode,
                    AttendDate = DateTime.Now,
                    SessionPrice = classEntity.TotalAmount ?? 0,
                    RootCode = student.RootCode,
                    Type = request.AttendanceType
                };

                _context.Attends.Add(attendance);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Marked attendance for student {StudentCode} in class {ClassCode} by user {Username}",
                    student.StudentCode, request.ClassCode, User.Identity.Name);

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
                return Json(new { success = false, error = "An error occurred while marking attendance." });
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

        private int CalculateAge(DateOnly birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;
            return age;
        }

        // Check if current user is an admin
        private async Task<bool> IsCurrentUserAdmin()
        {
            try
            {
                // Skip authentication check - allow access to anyone, but only admins can edit
                if (!User.Identity.IsAuthenticated)
                {
                    return false;
                }

                var groupCodeClaim = User.FindFirst("GroupCode");
                if (groupCodeClaim == null || !int.TryParse(groupCodeClaim.Value, out int groupCode))
                {
                    return false;
                }

                // Check if user is in Admins group
                var group = await _context.Groups
                    .FirstOrDefaultAsync(g => g.GroupCode == groupCode);

                return group != null && group.GroupName == "Admins";
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

    public class MarkAttendanceRequest
    {
        public string ItemKey { get; set; } = string.Empty;
        public int ClassCode { get; set; }
        public int AttendanceType { get; set; }
    }
}