using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using centrny.Attributes;
using System.Text.Json;

namespace centrny.Controllers
{
    [RequirePageAccess("DailyClass")]
    public class DailyClassController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<DailyClassController> _logger;

        public DailyClassController(CenterContext context, ILogger<DailyClassController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Gets the current user's RootCode from claims - REQUIRED for all users
        /// </summary>
        private int? GetCurrentUserRootCode()
        {
            var rootCodeClaim = User.FindFirst("RootCode");
            if (rootCodeClaim != null && int.TryParse(rootCodeClaim.Value, out int rootCode))
            {
                _logger.LogDebug("Current user RootCode: {RootCode}", rootCode);
                return rootCode;
            }

            _logger.LogWarning("User {Username} missing or invalid RootCode claim", User.Identity?.Name);
            return null;
        }

        /// <summary>
        /// Gets user context information for UI display
        /// </summary>
        private async Task<(int? rootCode, string rootName, bool isCenter)> GetUserContext()
        {
            var rootCode = GetCurrentUserRootCode();
            var rootName = User.FindFirst("RootName")?.Value ?? "Unknown";
            var isCenter = User.FindFirst("IsCenter")?.Value == "True";

            return (rootCode, rootName, isCenter);
        }

        // ==================== MAIN ACTIONS ====================

        /// <summary>
        /// GET: DailyClass - Shows daily classes view for today with auto-generation check
        /// </summary>
        public async Task<IActionResult> Index(DateTime? date = null)
        {
            var (rootCode, rootName, isCenter) = await GetUserContext();

            if (!rootCode.HasValue)
            {
                ViewBag.Error = "Unable to determine your root assignment. Please contact administrator.";
                return View();
            }

            var selectedDate = date ?? DateTime.Today;

            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;
            ViewBag.SelectedDate = selectedDate;
            ViewBag.SelectedDateFormatted = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.DayOfWeek = selectedDate.DayOfWeek.ToString();

            await PopulateDropDownsSafe();

            // Check if we should auto-generate weekly classes (only on Saturdays or if it's a new week)
            try
            {
                var (weekStart, weekEnd) = GetCurrentWeekRange();
                var shouldAutoGenerate = await ShouldAutoGenerateThisWeek(rootCode.Value, weekStart, weekEnd);

                if (shouldAutoGenerate)
                {
                    _logger.LogInformation("Auto-generating weekly classes for user {Username} (Root: {RootCode}) on week start",
                        User.Identity?.Name, rootCode);

                    // Auto-generate in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await GenerateClassesForCurrentWeek(rootCode.Value);
                            _logger.LogInformation("Background auto-generation completed for Root: {RootCode}", rootCode);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in background auto-generation for Root: {RootCode}", rootCode);
                        }
                    });

                    ViewBag.AutoGenerationTriggered = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking auto-generation for user {Username}", User.Identity?.Name);
                // Continue normally even if auto-generation check fails
            }

            return View();
        }

        /// <summary>
        /// GET: DailyClass/GetDailyClasses - API endpoint to get classes for a specific date
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDailyClasses(DateTime date)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    _logger.LogWarning("User {Username} has no RootCode - returning empty daily classes", User.Identity?.Name);
                    return Json(new List<object>());
                }

                var dayOfWeek = date.DayOfWeek.ToString();
                var dateOnly = DateOnly.FromDateTime(date);

                // Get reservation-based classes for this specific date
                var reservationClasses = await _context.Classes
                    .Where(c => c.ReservationCode != null &&
                               c.ReservationCodeNavigation != null &&
                               c.ReservationCodeNavigation.RTime == dateOnly &&
                               c.RootCode == userRootCode.Value)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                        .ThenInclude(b => b.CenterCodeNavigation)
                    .Include(c => c.EduYearCodeNavigation)
                    .Include(c => c.YearCodeNavigation)
                    .Include(c => c.ReservationCodeNavigation)
                    .Include(c => c.RootCodeNavigation)
                    .ToListAsync();

                // Get schedule-based classes for this day of week
                var scheduleClasses = await _context.Classes
                    .Where(c => c.ScheduleCode != null &&
                               c.ScheduleCodeNavigation != null &&
                               c.ScheduleCodeNavigation.DayOfWeek == dayOfWeek &&
                               c.RootCode == userRootCode.Value)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                        .ThenInclude(b => b.CenterCodeNavigation)
                    .Include(c => c.EduYearCodeNavigation)
                    .Include(c => c.YearCodeNavigation)
                    .Include(c => c.ScheduleCodeNavigation)
                        .ThenInclude(s => s.CenterCodeNavigation)
                    .Include(c => c.RootCodeNavigation)
                    .ToListAsync();

                // Get direct date-based classes for this specific date (new approach)
                var directDateClasses = await _context.Classes
                    .Where(c => c.ClassDate == dateOnly &&
                               c.ScheduleCode == null &&
                               c.ReservationCode == null &&
                               c.RootCode == userRootCode.Value)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                        .ThenInclude(b => b.CenterCodeNavigation)
                    .Include(c => c.EduYearCodeNavigation)
                    .Include(c => c.YearCodeNavigation)
                    .Include(c => c.RootCodeNavigation)
                    .ToListAsync();

                var allClasses = new List<object>();

                // Process reservation-based classes
                foreach (var cls in reservationClasses)
                {
                    allClasses.Add(CreateClassViewModel(cls, date, "reservation"));
                }

                // Process schedule-based classes (recurring)
                foreach (var cls in scheduleClasses)
                {
                    allClasses.Add(CreateClassViewModel(cls, date, "schedule"));
                }

                // Process direct date-based classes
                foreach (var cls in directDateClasses)
                {
                    allClasses.Add(CreateClassViewModel(cls, date, "direct"));
                }

                _logger.LogInformation("Loaded {Count} classes for date {Date} and user {Username} (Root: {RootCode}, Type: {UserType}) - Reservation: {ReservationCount}, Schedule: {ScheduleCount}, Direct: {DirectCount}",
                    allClasses.Count, date.ToString("yyyy-MM-dd"), User.Identity?.Name, userRootCode,
                    isCenter ? "Center" : "Teacher", reservationClasses.Count, scheduleClasses.Count, directDateClasses.Count);

                return Json(allClasses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily classes for date {Date} and user {Username}",
                    date.ToString("yyyy-MM-dd"), User.Identity?.Name);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: DailyClass/GetBranchesByCenter - Get branches filtered by center code
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBranchesByCenter(int centerCode)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var branches = await _context.Branches
                    .Where(b => b.CenterCode == centerCode && b.RootCode == userRootCode.Value && b.IsActive)
                    .Select(b => new { value = b.BranchCode, text = b.BranchName })
                    .ToListAsync();

                return Json(branches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branches for center {CenterCode}", centerCode);
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// POST: DailyClass/CreateClass - Create new class
        /// </summary>
        [HttpPost]
        [RequirePageAccess("DailyClass", "insert")]
        public async Task<IActionResult> CreateClass([FromBody] DailyClassModel model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { success = false, error = "Invalid data received." });
                }

                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                // Always set RootCode to current user's root
                model.RootCode = userRootCode.Value;

                // Validate required fields
                if (string.IsNullOrEmpty(model.ClassName) ||
                    string.IsNullOrEmpty(model.StartTime) || string.IsNullOrEmpty(model.EndTime))
                {
                    return Json(new { success = false, error = "Class name, start time, and end time are required." });
                }

                // Different validation based on user type
                if (isCenter)
                {
                    // Center users must provide teacher
                    if (!model.TeacherCode.HasValue || !model.SubjectCode.HasValue ||
                        !model.BranchCode.HasValue || !model.HallCode.HasValue || !model.EduYearCode.HasValue)
                    {
                        return Json(new { success = false, error = "Teacher, subject, branch, hall, and education year are required." });
                    }
                }
                else
                {
                    // Teacher users must provide center
                    if (!model.CenterCode.HasValue || !model.SubjectCode.HasValue ||
                        !model.BranchCode.HasValue || !model.HallCode.HasValue || !model.EduYearCode.HasValue)
                    {
                        return Json(new { success = false, error = "Center, subject, branch, hall, and education year are required." });
                    }

                    // For teachers, set TeacherCode to the teacher belonging to this root
                    var teacherCode = await _context.Teachers
                        .Where(t => t.RootCode == userRootCode.Value && t.IsActive)
                        .Select(t => t.TeacherCode)
                        .FirstOrDefaultAsync();

                    if (teacherCode == 0)
                    {
                        return Json(new { success = false, error = "No teacher found for your account." });
                    }

                    model.TeacherCode = teacherCode;
                }

                // Parse and validate times
                if (!TimeOnly.TryParse(model.StartTime, out TimeOnly startTime) ||
                    !TimeOnly.TryParse(model.EndTime, out TimeOnly endTime))
                {
                    return Json(new { success = false, error = "Invalid time format." });
                }

                if (startTime >= endTime)
                {
                    return Json(new { success = false, error = "End time must be after start time." });
                }

                // Validate resources belong to user's root
                if (!await ValidateClassResources(model, userRootCode.Value))
                {
                    return Json(new { success = false, error = "One or more selected resources don't belong to your organization." });
                }

                // Parse the class date from the model
                var classDate = model.ClassDate?.Date ?? DateTime.Today;
                var classDateOnly = DateOnly.FromDateTime(classDate);

                // Create new class
                var newClass = new Class
                {
                    ClassName = model.ClassName.Trim(),
                    ClassStartTime = startTime,
                    ClassEndTime = endTime,
                    ClassDate = classDateOnly,
                    RootCode = userRootCode.Value,
                    TeacherCode = model.TeacherCode.Value,
                    SubjectCode = model.SubjectCode.Value,
                    BranchCode = model.BranchCode.Value,
                    HallCode = model.HallCode.Value,
                    EduYearCode = model.EduYearCode.Value,
                    YearCode = model.YearCode,
                    NoOfStudents = 0, // Always start with 0, will be updated by attendance system
                    TotalAmount = model.TotalAmount,
                    TeacherAmount = model.TeacherAmount,
                    CenterAmount = model.CenterAmount,
                    InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1"),
                    InsertTime = DateTime.Now,
                    // Keep ScheduleCode and ReservationCode as null
                    ScheduleCode = null,
                    ReservationCode = null
                };

                _context.Classes.Add(newClass);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created class {ClassName} for RootCode {RootCode} by user {Username} (UserType: {UserType})",
                    newClass.ClassName, newClass.RootCode, User.Identity?.Name, isCenter ? "Center" : "Teacher");

                return Json(new { success = true, id = newClass.ClassCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating class for user {Username}", User.Identity?.Name);
                return Json(new { success = false, error = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// POST: DailyClass/EditClass - Edit existing class
        /// </summary>
        [HttpPost]
        [RequirePageAccess("DailyClass", "update")]
        public async Task<IActionResult> EditClass(int id, [FromBody] JsonElement json)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                // Find class with root filtering
                var existingClass = await _context.Classes
                    .Where(c => c.ClassCode == id && c.RootCode == userRootCode.Value)
                    .FirstOrDefaultAsync();

                if (existingClass == null)
                {
                    _logger.LogWarning("User {Username} attempted to edit class {ClassId} - not found or belongs to different root",
                        User.Identity?.Name, id);
                    return Json(new { success = false, error = "Class not found or you don't have permission to edit it." });
                }

                // Helper functions for safe JSON parsing
                string GetStringValue(string propertyName)
                {
                    if (json.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString()?.Trim();
                    }
                    return null;
                }

                int? GetIntValue(string propertyName)
                {
                    if (json.TryGetProperty(propertyName, out var prop))
                    {
                        return prop.ValueKind switch
                        {
                            JsonValueKind.Number => prop.GetInt32(),
                            JsonValueKind.String when int.TryParse(prop.GetString(), out var intVal) => intVal,
                            _ => null
                        };
                    }
                    return null;
                }

                decimal? GetDecimalValue(string propertyName)
                {
                    if (json.TryGetProperty(propertyName, out var prop))
                    {
                        return prop.ValueKind switch
                        {
                            JsonValueKind.Number => prop.GetDecimal(),
                            JsonValueKind.String when decimal.TryParse(prop.GetString(), out var decVal) => decVal,
                            _ => null
                        };
                    }
                    return null;
                }

                // Parse and validate input
                string className = GetStringValue("className");
                string startTimeStr = GetStringValue("startTime");
                string endTimeStr = GetStringValue("endTime");

                if (string.IsNullOrEmpty(className) || string.IsNullOrEmpty(startTimeStr) || string.IsNullOrEmpty(endTimeStr))
                {
                    return Json(new { success = false, error = "Class name, start time, and end time are required." });
                }

                // Parse times
                if (!TimeOnly.TryParse(startTimeStr, out TimeOnly startTime) ||
                    !TimeOnly.TryParse(endTimeStr, out TimeOnly endTime))
                {
                    return Json(new { success = false, error = "Invalid time format." });
                }

                if (startTime >= endTime)
                {
                    return Json(new { success = false, error = "End time must be after start time." });
                }

                // Update class properties
                existingClass.ClassName = className;
                existingClass.ClassStartTime = startTime;
                existingClass.ClassEndTime = endTime;

                // Handle teacher assignment based on user type
                var teacherCode = GetIntValue("teacherCode");
                if (isCenter)
                {
                    // Center users can change teacher
                    if (teacherCode.HasValue && teacherCode > 0)
                    {
                        var teacherExists = await _context.Teachers.AnyAsync(t => t.TeacherCode == teacherCode && t.RootCode == userRootCode.Value);
                        if (teacherExists)
                            existingClass.TeacherCode = teacherCode.Value;
                    }
                }
                else
                {
                    // For teacher users, ensure teacher is set to the teacher of this root (don't allow changing)
                    var rootTeacherCode = await _context.Teachers
                        .Where(t => t.RootCode == userRootCode.Value && t.IsActive)
                        .Select(t => t.TeacherCode)
                        .FirstOrDefaultAsync();

                    if (rootTeacherCode > 0)
                    {
                        existingClass.TeacherCode = rootTeacherCode;
                    }
                }

                // Update other fields with root validation
                var subjectCode = GetIntValue("subjectCode");
                if (subjectCode.HasValue && subjectCode > 0)
                {
                    existingClass.SubjectCode = subjectCode.Value;
                }

                var branchCode = GetIntValue("branchCode");
                if (branchCode.HasValue && branchCode > 0)
                {
                    var branchExists = await _context.Branches.AnyAsync(b => b.BranchCode == branchCode && b.RootCode == userRootCode.Value);
                    if (branchExists)
                        existingClass.BranchCode = branchCode.Value;
                }

                var hallCode = GetIntValue("hallCode");
                if (hallCode.HasValue && hallCode > 0)
                {
                    var hallExists = await _context.Halls.AnyAsync(h => h.HallCode == hallCode && h.RootCode == userRootCode.Value);
                    if (hallExists)
                        existingClass.HallCode = hallCode.Value;
                }

                var eduYearCode = GetIntValue("eduYearCode");
                if (eduYearCode.HasValue && eduYearCode > 0)
                {
                    var eduYearExists = await _context.EduYears.AnyAsync(e => e.EduCode == eduYearCode && e.RootCode == userRootCode.Value);
                    if (eduYearExists)
                        existingClass.EduYearCode = eduYearCode.Value;
                }

                var yearCode = GetIntValue("yearCode");
                if (yearCode.HasValue && yearCode > 0)
                {
                    existingClass.YearCode = yearCode;
                }

                // NoOfStudents is not updated here - it's managed by the attendance system

                var totalAmount = GetDecimalValue("totalAmount");
                if (totalAmount.HasValue)
                {
                    existingClass.TotalAmount = totalAmount;
                }

                var teacherAmount = GetDecimalValue("teacherAmount");
                if (teacherAmount.HasValue)
                {
                    existingClass.TeacherAmount = teacherAmount;
                }

                var centerAmount = GetDecimalValue("centerAmount");
                if (centerAmount.HasValue)
                {
                    existingClass.CenterAmount = centerAmount;
                }

                // Update audit fields
                existingClass.LastUpdateUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1");
                existingClass.LastUpdateTime = DateTime.Now;

                _context.Classes.Update(existingClass);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated class {ClassId} by user {Username} (UserType: {UserType})",
                    id, User.Identity?.Name, isCenter ? "Center" : "Teacher");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing class {ClassId} for user {Username}", id, User.Identity?.Name);
                return Json(new { success = false, error = "An error occurred while updating the class: " + ex.Message });
            }
        }

        /// <summary>
        /// POST: DailyClass/DeleteClass - Delete class
        /// </summary>
        [HttpPost]
        [RequirePageAccess("DailyClass", "delete")]
        public async Task<IActionResult> DeleteClass(int id)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                var existingClass = await _context.Classes
                    .Where(c => c.ClassCode == id && c.RootCode == userRootCode.Value)
                    .FirstOrDefaultAsync();

                if (existingClass == null)
                {
                    return Json(new { success = false, error = "Class not found or you don't have permission to delete it." });
                }

                _context.Classes.Remove(existingClass);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted class {ClassId} by user {Username}", id, User.Identity?.Name);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class {ClassId} for user {Username}", id, User.Identity?.Name);
                return Json(new { success = false, error = "An error occurred while deleting the class: " + ex.Message });
            }
        }

        // ==================== WEEKLY CLASS GENERATION METHODS ====================

        /// <summary>
        /// Generate classes for the current week (Saturday to Friday) from active schedules
        /// </summary>
        [HttpPost]
        [RequirePageAccess("DailyClass", "insert")]
        public async Task<IActionResult> GenerateWeeklyClasses()
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                var result = await GenerateClassesForCurrentWeek(userRootCode.Value);

                _logger.LogInformation("Generated {CreatedCount} classes for week {WeekStart} - {WeekEnd} by user {Username} (Root: {RootCode})",
                    result.CreatedCount, result.WeekStart.ToString("yyyy-MM-dd"), result.WeekEnd.ToString("yyyy-MM-dd"),
                    User.Identity?.Name, userRootCode);

                return Json(new
                {
                    success = true,
                    message = $"Generated {result.CreatedCount} classes for this week ({result.WeekStart:MMM dd} - {result.WeekEnd:MMM dd}). Skipped {result.SkippedCount} existing classes.",
                    createdCount = result.CreatedCount,
                    skippedCount = result.SkippedCount,
                    weekStart = result.WeekStart.ToString("yyyy-MM-dd"),
                    weekEnd = result.WeekEnd.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating weekly classes for user {Username}", User.Identity?.Name);
                return Json(new { success = false, error = $"Error generating classes: {ex.Message}" });
            }
        }

        /// <summary>
        /// Check if classes need to be generated automatically and generate them
        /// Called when user accesses the daily class view
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CheckAndGenerateWeeklyClasses()
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "No root assignment found." });
                }

                // Check if we need to auto-generate classes for this week
                var (weekStart, weekEnd) = GetCurrentWeekRange();
                var needsGeneration = await CheckIfWeekNeedsClassGeneration(userRootCode.Value, weekStart, weekEnd);

                if (needsGeneration)
                {
                    var result = await GenerateClassesForCurrentWeek(userRootCode.Value);

                    _logger.LogInformation("Auto-generated {CreatedCount} classes for week {WeekStart} - {WeekEnd} for user {Username} (Root: {RootCode})",
                        result.CreatedCount, result.WeekStart.ToString("yyyy-MM-dd"), result.WeekEnd.ToString("yyyy-MM-dd"),
                        User.Identity?.Name, userRootCode);

                    return Json(new
                    {
                        success = true,
                        autoGenerated = true,
                        message = $"Auto-generated {result.CreatedCount} classes for this week.",
                        createdCount = result.CreatedCount
                    });
                }

                return Json(new { success = true, autoGenerated = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-generation check for user {Username}", User.Identity?.Name);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get weekly class generation status for the UI
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetWeeklyGenerationStatus()
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { error = "No root assignment found." });
                }

                var (weekStart, weekEnd) = GetCurrentWeekRange();
                var weekStartOnly = DateOnly.FromDateTime(weekStart);
                var weekEndOnly = DateOnly.FromDateTime(weekEnd);

                // Count active schedules
                var activeSchedules = await _context.Schedules
                    .Where(s => s.RootCode == userRootCode.Value &&
                               !string.IsNullOrEmpty(s.DayOfWeek) &&
                               s.StartTime.HasValue &&
                               s.EndTime.HasValue)
                    .Select(s => new { s.ScheduleCode, s.ScheduleName, s.DayOfWeek })
                    .ToListAsync();

                // Count existing classes for this week (schedule-based)
                var existingClasses = await _context.Classes
                    .Where(c => c.RootCode == userRootCode.Value &&
                               c.ScheduleCode.HasValue &&
                               c.ClassDate >= weekStartOnly &&
                               c.ClassDate <= weekEndOnly)
                    .Select(c => new { c.ScheduleCode, c.ClassDate })
                    .ToListAsync();

                // Get schedule breakdown by day
                var schedulesByDay = activeSchedules
                    .GroupBy(s => s.DayOfWeek)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Get classes breakdown by day
                var classesByDay = existingClasses
                    .Where(c => c.ClassDate.HasValue)
                    .GroupBy(c => c.ClassDate.Value.DayOfWeek.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());

                return Json(new
                {
                    weekStart = weekStart.ToString("yyyy-MM-dd"),
                    weekEnd = weekEnd.ToString("yyyy-MM-dd"),
                    weekStartFormatted = weekStart.ToString("MMM dd"),
                    weekEndFormatted = weekEnd.ToString("MMM dd"),
                    activeSchedulesCount = activeSchedules.Count,
                    existingClassesCount = existingClasses.Count,
                    schedulesByDay = schedulesByDay,
                    classesByDay = classesByDay,
                    needsGeneration = existingClasses.Count < activeSchedules.Count,
                    canGenerate = activeSchedules.Count > 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weekly generation status for user {Username}", User.Identity?.Name);
                return Json(new { error = ex.Message });
            }
        }

        // ==================== UTILITY METHODS ====================

        private object CreateClassViewModel(Class cls, DateTime date, string classType)
        {
            // Get user context
            var (userRootCode, rootName, isCenter) = GetUserContext().Result;

            // Determine the actual times for the class based on type
            TimeOnly? startTime = cls.ClassStartTime;
            TimeOnly? endTime = cls.ClassEndTime;

            // If class doesn't have specific times, try to get from schedule or reservation
            if (!startTime.HasValue || !endTime.HasValue)
            {
                if (cls.ScheduleCode.HasValue && cls.ScheduleCodeNavigation != null)
                {
                    if (cls.ScheduleCodeNavigation.StartTime.HasValue)
                        startTime = TimeOnly.FromDateTime(cls.ScheduleCodeNavigation.StartTime.Value);
                    if (cls.ScheduleCodeNavigation.EndTime.HasValue)
                        endTime = TimeOnly.FromDateTime(cls.ScheduleCodeNavigation.EndTime.Value);
                }
                else if (cls.ReservationCode.HasValue && cls.ReservationCodeNavigation != null)
                {
                    startTime = cls.ReservationCodeNavigation.ReservationStartTime;
                    endTime = cls.ReservationCodeNavigation.ReservationEndTime;
                }
            }

            // Get center and branch information based on user type and class type
            string centerName = null;
            string branchName = null;
            string teacherName = null;

            if (isCenter)
            {
                // For center users, show teacher name only
                teacherName = cls.TeacherCodeNavigation?.TeacherName;
            }
            else
            {
                // For teacher users, show center and branch names
                branchName = cls.BranchCodeNavigation?.BranchName;

                // Get center name based on class type and relationships
                if (cls.ScheduleCode.HasValue && cls.ScheduleCodeNavigation?.CenterCodeNavigation != null)
                {
                    // For schedule-based classes, get center from schedule
                    centerName = cls.ScheduleCodeNavigation.CenterCodeNavigation.CenterName;
                }
                else if (cls.BranchCodeNavigation?.CenterCodeNavigation != null)
                {
                    // For other classes, get center through branch relationship
                    centerName = cls.BranchCodeNavigation.CenterCodeNavigation.CenterName;
                }
            }

            return new
            {
                id = $"class_{cls.ClassCode}",
                classCode = cls.ClassCode,
                title = cls.ClassName,
                startTime = startTime?.ToString("HH:mm") ?? "00:00",
                endTime = endTime?.ToString("HH:mm") ?? "23:59",
                startTime12 = startTime?.ToString("h:mm tt") ?? "",
                endTime12 = endTime?.ToString("h:mm tt") ?? "",
                classType = classType,
                backgroundColor = "#6c5ce7",
                borderColor = "#5a4fcf",
                textColor = "#ffffff",

                // Conditional display based on user type
                teacherName = teacherName,
                teacherCode = cls.TeacherCode,
                centerName = centerName,
                branchName = branchName,

                // Always include for form population
                subjectName = cls.SubjectCodeNavigation?.SubjectName,
                subjectCode = cls.SubjectCode,
                hallName = cls.HallCodeNavigation?.HallName,
                hallCode = cls.HallCode,
                branchCode = cls.BranchCode,
                eduYearName = cls.EduYearCodeNavigation?.EduName,
                eduYearCode = cls.EduYearCode,
                yearName = cls.YearCodeNavigation?.YearName,
                yearCode = cls.YearCode,
                rootName = cls.RootCodeNavigation?.RootName,
                rootCode = cls.RootCode,
                noOfStudents = cls.NoOfStudents,
                totalAmount = cls.TotalAmount?.ToString("F2"),
                teacherAmount = cls.TeacherAmount?.ToString("F2"),
                centerAmount = cls.CenterAmount?.ToString("F2"),
                date = date.ToString("yyyy-MM-dd"),
                classDate = cls.ClassDate?.ToString("yyyy-MM-dd"),

                // User context for display logic
                isCenter = isCenter
            };
        }

        private async Task<bool> ValidateClassResources(DailyClassModel model, int rootCode)
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();

            // Validate teacher belongs to root (only for center users)
            if (isCenter && model.TeacherCode.HasValue)
            {
                var teacherExists = await _context.Teachers.AnyAsync(t => t.TeacherCode == model.TeacherCode && t.RootCode == rootCode);
                if (!teacherExists) return false;
            }

            // Validate center belongs to root (only for teacher users)
            if (!isCenter && model.CenterCode.HasValue)
            {
                var centerExists = await _context.Centers.AnyAsync(c => c.CenterCode == model.CenterCode && c.RootCode == rootCode);
                if (!centerExists) return false;
            }

            // Validate branch belongs to root and center (if specified)
            if (model.BranchCode.HasValue)
            {
                var branchQuery = _context.Branches.Where(b => b.BranchCode == model.BranchCode && b.RootCode == rootCode);

                // For teacher users, also validate branch belongs to selected center
                if (!isCenter && model.CenterCode.HasValue)
                {
                    branchQuery = branchQuery.Where(b => b.CenterCode == model.CenterCode);
                }

                var branchExists = await branchQuery.AnyAsync();
                if (!branchExists) return false;
            }

            // Validate hall belongs to root
            if (model.HallCode.HasValue)
            {
                var hallExists = await _context.Halls.AnyAsync(h => h.HallCode == model.HallCode && h.RootCode == rootCode);
                if (!hallExists) return false;
            }

            // Validate education year belongs to root
            if (model.EduYearCode.HasValue)
            {
                var eduYearExists = await _context.EduYears.AnyAsync(e => e.EduCode == model.EduYearCode && e.RootCode == rootCode);
                if (!eduYearExists) return false;
            }

            return true;
        }

        private async Task PopulateDropDownsSafe()
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();

            try
            {
                if (!userRootCode.HasValue)
                {
                    // Provide empty dropdowns if no root code
                    ViewData["TeacherCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["SubjectCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["BranchCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["HallCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["EduYearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["YearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["CenterCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    return;
                }

                // Teachers - filter by user's root (only for centers, teachers won't see this)
                if (isCenter)
                {
                    var teachers = await _context.Teachers
                        .Where(t => t.RootCode == userRootCode.Value && t.IsActive)
                        .ToListAsync();
                    ViewData["TeacherCode"] = new SelectList(teachers, "TeacherCode", "TeacherName");
                }
                else
                {
                    ViewData["TeacherCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }

                // Subjects - global (no filtering needed)
                var subjects = await _context.Subjects.ToListAsync();
                ViewData["SubjectCode"] = new SelectList(subjects, "SubjectCode", "SubjectName");

                // Centers - for teachers only, filtered by root code
                if (!isCenter)
                {
                    var centers = await _context.Centers
                        .Where(c => c.RootCode == userRootCode.Value && c.IsActive)
                        .ToListAsync();
                    ViewData["CenterCode"] = new SelectList(centers, "CenterCode", "CenterName");
                }
                else
                {
                    ViewData["CenterCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }

                // Branches - filter by user's root (initial load, will be filtered by center via AJAX)
                var branches = await _context.Branches
                    .Where(b => b.RootCode == userRootCode.Value && b.IsActive)
                    .ToListAsync();
                ViewData["BranchCode"] = new SelectList(branches, "BranchCode", "BranchName");

                // Halls - filter by user's root
                var halls = await _context.Halls
                    .Where(h => h.RootCode == userRootCode.Value)
                    .ToListAsync();
                ViewData["HallCode"] = new SelectList(halls, "HallCode", "HallName");

                // EduYears - filter by user's root
                var eduYears = await _context.EduYears
                    .Where(e => e.RootCode == userRootCode.Value && e.IsActive)
                    .ToListAsync();
                ViewData["EduYearCode"] = new SelectList(eduYears, "EduCode", "EduName");

                // Years - global (no filtering needed)
                var years = await _context.Years.ToListAsync();
                ViewData["YearCode"] = new SelectList(years, "YearCode", "YearName");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PopulateDropDownsSafe for user {Username}", User.Identity?.Name);

                // Create empty dropdowns as fallback
                ViewData["TeacherCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["SubjectCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["BranchCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["HallCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["EduYearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["YearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["CenterCode"] = new SelectList(new List<dynamic>(), "Value", "Text");

                ViewBag.DropdownError = $"Error loading dropdown data: {ex.Message}";
            }
        }

        // ==================== WEEKLY GENERATION HELPER METHODS ====================

        /// <summary>
        /// Core method to generate classes from schedules for the current week
        /// </summary>
        private async Task<WeeklyClassGenerationResult> GenerateClassesForCurrentWeek(int rootCode)
        {
            var (weekStart, weekEnd) = GetCurrentWeekRange();

            // Get all active schedules for this root
            var activeSchedules = await _context.Schedules
                .Where(s => s.RootCode == rootCode &&
                           !string.IsNullOrEmpty(s.DayOfWeek) &&
                           s.StartTime.HasValue &&
                           s.EndTime.HasValue)
                .Include(s => s.TeacherCodeNavigation)
                .Include(s => s.SubjectCodeNavigation)
                .Include(s => s.HallCodeNavigation)
                .Include(s => s.EduYearCodeNavigation)
                .ToListAsync();

            var result = new WeeklyClassGenerationResult
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                CreatedCount = 0,
                SkippedCount = 0
            };

            // Generate classes for each day of the week
            for (var date = weekStart; date <= weekEnd; date = date.AddDays(1))
            {
                var dayOfWeek = date.DayOfWeek.ToString();
                var daySchedules = activeSchedules.Where(s => s.DayOfWeek == dayOfWeek).ToList();

                foreach (var schedule in daySchedules)
                {
                    var dateOnly = DateOnly.FromDateTime(date);

                    // Check if class already exists for this schedule and date
                    var existingClass = await _context.Classes
                        .AnyAsync(c => c.ScheduleCode == schedule.ScheduleCode &&
                                      c.ClassDate == dateOnly);

                    if (existingClass)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Get default values from schedule for required fields
                    var teacherCode = schedule.TeacherCode ?? await GetDefaultTeacherForRoot(rootCode);
                    var subjectCode = schedule.SubjectCode ?? await GetDefaultSubjectForRoot(rootCode);
                    var hallCode = schedule.HallCode ?? await GetDefaultHallForRoot(rootCode);
                    var branchCode = await GetDefaultBranchForRoot(rootCode);
                    var eduYearCode = schedule.EduYearCode ?? await GetDefaultEduYearForRoot(rootCode);

                    // Create new class with minimal data - let DB triggers handle the rest
                    var newClass = new Class
                    {
                        ClassName = $"{schedule.ScheduleName} - {date:MMM dd}", // Include date in name
                        ClassDate = dateOnly,
                        ScheduleCode = schedule.ScheduleCode,
                        RootCode = rootCode,

                        // Required fields - use schedule values or defaults
                        TeacherCode = teacherCode,
                        SubjectCode = subjectCode,
                        BranchCode = branchCode,
                        HallCode = hallCode,
                        EduYearCode = eduYearCode,
                        YearCode = schedule.YearCode,

                        // Set times from schedule
                        ClassStartTime = schedule.StartTime.HasValue ? TimeOnly.FromDateTime(schedule.StartTime.Value) : null,
                        ClassEndTime = schedule.EndTime.HasValue ? TimeOnly.FromDateTime(schedule.EndTime.Value) : null,

                        // Start with minimal values - triggers will populate as needed
                        NoOfStudents = 0,
                        TotalAmount = schedule.ScheduleAmount,
                        TeacherAmount = null,
                        CenterAmount = null,

                        // Audit fields
                        InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1"),
                        InsertTime = DateTime.Now,

                        // No reservation for schedule-based classes
                        ReservationCode = null
                    };

                    _context.Classes.Add(newClass);
                    result.CreatedCount++;
                }
            }

            if (result.CreatedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        /// <summary>
        /// Get the current week range (Saturday to Friday)
        /// </summary>
        private (DateTime weekStart, DateTime weekEnd) GetCurrentWeekRange()
        {
            var today = DateTime.Today;

            // Calculate days to go back to reach Saturday
            // Sunday = 0, Monday = 1, ..., Saturday = 6
            var daysFromSaturday = ((int)today.DayOfWeek + 1) % 7;

            var weekStart = today.AddDays(-daysFromSaturday);
            var weekEnd = weekStart.AddDays(6); // Friday

            return (weekStart, weekEnd);
        }

        /// <summary>
        /// Check if the current week needs class generation
        /// Returns true if it's a new week or if there are schedules without classes
        /// </summary>
        private async Task<bool> CheckIfWeekNeedsClassGeneration(int rootCode, DateTime weekStart, DateTime weekEnd)
        {
            // Get active schedules count
            var activeSchedulesCount = await _context.Schedules
                .Where(s => s.RootCode == rootCode &&
                           !string.IsNullOrEmpty(s.DayOfWeek) &&
                           s.StartTime.HasValue &&
                           s.EndTime.HasValue)
                .CountAsync();

            if (activeSchedulesCount == 0)
                return false;

            // Get existing classes count for this week that are based on schedules
            var weekStartOnly = DateOnly.FromDateTime(weekStart);
            var weekEndOnly = DateOnly.FromDateTime(weekEnd);

            var existingClassesCount = await _context.Classes
                .Where(c => c.RootCode == rootCode &&
                           c.ScheduleCode.HasValue &&
                           c.ClassDate >= weekStartOnly &&
                           c.ClassDate <= weekEndOnly)
                .CountAsync();

            // If we have schedules but no schedule-based classes for this week, we need generation
            return existingClassesCount == 0;
        }

        /// <summary>
        /// Determine if we should auto-generate classes for this week
        /// Only auto-generate on Saturday (week start) or if no classes exist for current week
        /// </summary>
        private async Task<bool> ShouldAutoGenerateThisWeek(int rootCode, DateTime weekStart, DateTime weekEnd)
        {
            var today = DateTime.Today;

            // Only auto-generate on Saturday (start of week) or if explicitly requested
            var isSaturday = today.DayOfWeek == DayOfWeek.Saturday;

            // Check if we have any schedule-based classes for this week
            var weekStartOnly = DateOnly.FromDateTime(weekStart);
            var weekEndOnly = DateOnly.FromDateTime(weekEnd);

            var hasScheduleBasedClasses = await _context.Classes
                .AnyAsync(c => c.RootCode == rootCode &&
                              c.ScheduleCode.HasValue &&
                              c.ClassDate >= weekStartOnly &&
                              c.ClassDate <= weekEndOnly);

            // Check if we have active schedules
            var hasActiveSchedules = await _context.Schedules
                .AnyAsync(s => s.RootCode == rootCode &&
                              !string.IsNullOrEmpty(s.DayOfWeek) &&
                              s.StartTime.HasValue &&
                              s.EndTime.HasValue);

            // Auto-generate if:
            // 1. It's Saturday (start of new week) AND we have schedules AND no schedule-based classes exist
            // 2. OR we have schedules but no classes for this week (catch-up generation)
            return hasActiveSchedules && (!hasScheduleBasedClasses && (isSaturday || !hasScheduleBasedClasses));
        }

        // ==================== HELPER METHODS FOR DEFAULTS ====================

        private async Task<int> GetDefaultTeacherForRoot(int rootCode)
        {
            var firstTeacher = await _context.Teachers
                .Where(t => t.RootCode == rootCode && t.IsActive)
                .Select(t => t.TeacherCode)
                .FirstOrDefaultAsync();

            return firstTeacher > 0 ? firstTeacher : 1; // Fallback to 1 if no teacher found
        }

        private async Task<int> GetDefaultSubjectForRoot(int rootCode)
        {
            var firstSubject = await _context.Subjects
                .Select(s => s.SubjectCode)
                .FirstOrDefaultAsync();

            return firstSubject > 0 ? firstSubject : 1; // Fallback to 1 if no subject found
        }

        private async Task<int> GetDefaultHallForRoot(int rootCode)
        {
            var firstHall = await _context.Halls
                .Where(h => h.RootCode == rootCode)
                .Select(h => h.HallCode)
                .FirstOrDefaultAsync();

            return firstHall > 0 ? firstHall : 1; // Fallback to 1 if no hall found
        }

        private async Task<int> GetDefaultBranchForRoot(int rootCode)
        {
            var firstBranch = await _context.Branches
                .Where(b => b.RootCode == rootCode && b.IsActive)
                .Select(b => b.BranchCode)
                .FirstOrDefaultAsync();

            return firstBranch > 0 ? firstBranch : 1; // Fallback to 1 if no branch found
        }

        private async Task<int> GetDefaultEduYearForRoot(int rootCode)
        {
            var firstEduYear = await _context.EduYears
                .Where(e => e.RootCode == rootCode && e.IsActive)
                .Select(e => e.EduCode)
                .FirstOrDefaultAsync();

            return firstEduYear > 0 ? firstEduYear : 1; // Fallback to 1 if no edu year found
        }
    }

    // ==================== VIEW MODELS ====================

    public class DailyClassModel
    {
        public string ClassName { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int? TeacherCode { get; set; }
        public int? CenterCode { get; set; } // New field for teachers to select center
        public int? SubjectCode { get; set; }
        public int? BranchCode { get; set; }
        public int? HallCode { get; set; }
        public int? EduYearCode { get; set; }
        public int? YearCode { get; set; }
        // NoOfStudents removed - managed by attendance system
        public decimal? TotalAmount { get; set; }
        public decimal? TeacherAmount { get; set; }
        public decimal? CenterAmount { get; set; }
        public int RootCode { get; set; } // Auto-set to user's root
        public DateTime? ClassDate { get; set; } // The specific date for this class
    }

    public class WeeklyClassGenerationResult
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
    }
}