using centrny.Attributes;
using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace centrny.Controllers
{
    [RequirePageAccess("DailyClass")]
    public class DailyClassController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<DailyClassController> _logger;
        private readonly IMemoryCache _memoryCache;

        public DailyClassController(CenterContext context, ILogger<DailyClassController> logger, IMemoryCache memoryCache)
        {
            _context = context;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        // ==================== HELPER METHODS ====================

        private async Task<int?> GetCurrentUserGroupBranchCode()
        {
            var username = User.Identity.Name;
            var user = await _context.Users
                .Include(u => u.GroupCodeNavigation)
                .FirstOrDefaultAsync(u => u.Username == username);
            return user?.GroupCodeNavigation?.BranchCode;
        }
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
            var username = User.Identity.Name;
            var user = await _context.Users
                .Include(u => u.GroupCodeNavigation)
                .ThenInclude(g => g.RootCodeNavigation)
                .FirstOrDefaultAsync(u => u.Username == username);

            int? rootCode = user?.GroupCodeNavigation?.RootCode;
            string rootName = user?.GroupCodeNavigation?.RootCodeNavigation?.RootName ?? "Unknown";
            bool isCenter = user?.GroupCodeNavigation?.RootCodeNavigation?.IsCenter ?? false;
 
            return (rootCode, rootName, isCenter);
        }
        // ==================== MAIN ACTIONS ====================

        /// <summary>
        /// GET: DailyClass - Shows daily classes view for today (OPTIMIZED - removed heavy operations)
        /// </summary>

        public async Task<IActionResult> Index(DateTime? date = null)
        {
            // Get current user context using navigation properties
            var username = User.Identity.Name;
            var user = await _context.Users
                .Include(u => u.GroupCodeNavigation)
                .ThenInclude(g => g.RootCodeNavigation)
                .FirstOrDefaultAsync(u => u.Username == username);

            // Retrieve root code, name, and IsCenter flag from navigation
            int? rootCode = user?.GroupCodeNavigation?.RootCode;
            string rootName = user?.GroupCodeNavigation?.RootCodeNavigation?.RootName ?? "Unknown";
            bool isCenter = user?.GroupCodeNavigation?.RootCodeNavigation?.IsCenter ?? false;
            // Set ViewBag values for use in the view
            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            // Example: set selected date and formatted date for the calendar
            DateTime selectedDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = selectedDate;
            ViewBag.SelectedDateFormatted = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.DayOfWeek = selectedDate.ToString("dddd");

            // Example: set error if user is not found
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View();
            }

            // Any additional data loading for classes, reservations, etc. can go here
            // Example: load classes for the selected date (optional)
            // var dailyClasses = await _context.Classes.Where(c => c.ClassDate == selectedDate && c.RootCode == rootCode).ToListAsync();
            // ViewBag.DailyClasses = dailyClasses;

            // Example: load reservations for the selected date (optional)
            // var reservations = await _context.Reservations.Where(r => r.ReservationDate == selectedDate && r.RootCode == rootCode).ToListAsync();
            // ViewBag.Reservations = reservations;

            // Page subtitle and banners (example)
            ViewBag.PageSubTitle = "Manage your daily classes";
            ViewBag.DayOfWeek = selectedDate.ToString("dddd");
            System.Diagnostics.Debug.WriteLine($"Userrrr: {user?.Username}, RootCode: {user?.GroupCodeNavigation?.RootCode}, branchcode: {user?.GroupCodeNavigation?.BranchCode}");
            return View();
        }

        [HttpGet]

        public async Task<IActionResult> GetUserBranch()
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();
            if (!userRootCode.HasValue || !isCenter)
                return Json(new { error = "Not a center user or missing root." });

            var groupBranchCode = await GetCurrentUserGroupBranchCode();
            if (!groupBranchCode.HasValue)
                return Json(new { error = "No group branch found for this user." });

            var branch = await _context.Branches
                .AsNoTracking()
                .Where(b => b.BranchCode == groupBranchCode.Value)
                .Select(b => new { value = b.BranchCode, text = b.BranchName })
                .FirstOrDefaultAsync();

            if (branch == null)
                return Json(new { error = "Branch not found." });

            return Json(branch);
        }

        [HttpGet]
        public async Task<IActionResult> GetEduYearsForRoot()
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();
            if (!userRootCode.HasValue)
                return Json(new { error = "No root assignment" });

            var eduYears = await _context.EduYears
                .AsNoTracking()
                .Where(e => e.RootCode == userRootCode.Value && e.IsActive)
                .Select(e => new { value = e.EduCode, text = e.EduName })
                .ToListAsync();

            return Json(new { success = true, eduYears });
        }
        // In DailyClassController.cs
        [HttpGet]
        public async Task<IActionResult> GetSubjectsForTeacher(int teacherCode)
        {
            // Assumes you have a Teach table with TeacherCode and SubjectCode
            var subjects = await _context.Teaches
                .Where(t => t.TeacherCode == teacherCode)
                .Select(t => new
                {
                    value = t.SubjectCode,
                    text = t.SubjectCodeNavigation.SubjectName    // or navigation property
                })
                .Distinct()
                .ToListAsync();

            return Json(subjects);
        }
        [HttpGet]
        public async Task<IActionResult> GetYearsForTeach(int branchCode, int teacherCode, int subjectCode)
        {
            var years = await _context.Teaches
                .Where(t => t.BranchCode == branchCode && t.TeacherCode == teacherCode && t.SubjectCode == subjectCode)
                .Select(t => new { value = t.YearCode, text = t.YearCodeNavigation.YearName })
                .Distinct()
                .ToListAsync();

            return Json(new { years });
        }
        [HttpGet]
        public async Task<IActionResult> GetCentersForUserRoot()
        {
            var userRootCode = GetCurrentUserRootCode();
            if (!userRootCode.HasValue)
                return Json(new { success = false, error = "Unable to determine your root assignment." });

            var centers = await _context.Centers
                .AsNoTracking()
                .Where(c => c.RootCode == userRootCode.Value && c.IsActive)
                .Select(c => new { value = c.CenterCode, text = c.CenterName })
                .OrderBy(c => c.text)
                .ToListAsync();

            return Json(new { success = true, centers });
        }
        /// <summary>
        /// GET: DailyClass/GetWeekReservations - Returns reservations for current week (READ ONLY)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetWeekReservations()
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();
            if (!userRootCode.HasValue)
                return Json(new List<object>());

            var (weekStart, weekEnd) = GetCurrentWeekRange();
            var weekStartOnly = DateOnly.FromDateTime(weekStart);
            var weekEndOnly = DateOnly.FromDateTime(weekEnd);

            // Get reservations for branches belonging to this root
            var reservations = await _context.Reservations
                .AsNoTracking()
                .Join(_context.Branches, r => r.BranchCode, b => b.BranchCode, (r, b) => new { r, b })
                .Where(x => x.b.RootCode == userRootCode.Value
                    && x.r.RTime >= weekStartOnly && x.r.RTime <= weekEndOnly)
                .Select(x => new
                {
                    x.r.ReservationCode,
                    x.r.Description,
                    x.r.RTime,
                    x.r.ReservationStartTime,
                    x.r.ReservationEndTime,
                    x.r.TeacherCode,
                    TeacherName = x.r.TeacherCodeNavigation != null ? x.r.TeacherCodeNavigation.TeacherName : "",
                    x.r.BranchCode,
                    BranchName = x.b.BranchName, // branch join always exists
                    x.r.HallCode,
                    HallName = x.r.HallCodeNavigation != null ? x.r.HallCodeNavigation.HallName : "",
                    x.r.Capacity,
                    x.r.Cost,
                    x.r.Period,
                    x.r.Deposit,
                    x.r.FinalCost
                })
                .OrderBy(x => x.RTime)
                .ThenBy(x => x.ReservationStartTime)
                .ToListAsync();

            return Json(reservations);
        }
        /// <summary>
        /// GET: DailyClass/GetDayReservations - Returns reservations for the selected day (for unified view)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDayReservations(string date)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();
                if (!userRootCode.HasValue)
                    return Json(new List<object>()); // always valid JSON

                // Defensive parsing: always treat as date-only
                DateOnly dateOnly;
                if (!DateOnly.TryParse(date, out dateOnly))
                {
                    dateOnly = DateOnly.FromDateTime(DateTime.Today);
                }

                var dbList = await _context.Reservations
                    .AsNoTracking()
                    .Join(_context.Branches, r => r.BranchCode, b => b.BranchCode, (r, b) => new { r, b })
                    .Where(x => x.b.RootCode == userRootCode.Value && x.r.RTime == dateOnly)
                    .OrderBy(x => x.r.ReservationStartTime)
                    .ToListAsync();

                var reservations = dbList.Select(x => new
                {
                    x.r.ReservationCode,
                    description = x.r.Description,
                    rTime = x.r.RTime.ToString("yyyy-MM-dd"),
                    reservationStartTime = x.r.ReservationStartTime != null ? x.r.ReservationStartTime.Value.ToString("HH:mm") : "",
                    reservationEndTime = x.r.ReservationEndTime != null ? x.r.ReservationEndTime.Value.ToString("HH:mm") : "",
                    teacherName = x.r.TeacherCodeNavigation != null ? x.r.TeacherCodeNavigation.TeacherName : "",
                    branchName = x.b.BranchName,
                    hallName = x.r.HallCodeNavigation != null ? x.r.HallCodeNavigation.HallName : "",
                    capacity = x.r.Capacity,
                    cost = x.r.Cost
                }).ToList();

                return Json(reservations);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetDailyClasses(string date)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    _logger.LogWarning("User {Username} has no RootCode - returning empty daily classes", User.Identity?.Name);
                    return Json(new List<object>());
                }

                // Defensive parsing: always treat as date-only, never UTC
                DateOnly dateOnly;
                if (!DateOnly.TryParse(date, out dateOnly))
                {
                    dateOnly = DateOnly.FromDateTime(DateTime.Today);
                }

                int? groupBranchCode = null;
                if (isCenter)
                {
                    groupBranchCode = await GetCurrentUserGroupBranchCode();
                }

                var classQuery = _context.Classes
                    .AsNoTracking()
                    .Where(c => c.RootCode == userRootCode.Value &&
                                c.ClassDate == dateOnly);

                if (isCenter && groupBranchCode != null)
                {
                    classQuery = classQuery.Where(c => c.BranchCode == groupBranchCode.Value);
                }

                var allClasses = await classQuery
                    .Select(c => new
                    {
                        c.ClassCode,
                        c.ClassName,
                        c.ClassStartTime,
                        c.ClassEndTime,
                        c.ClassDate,
                        c.TeacherCode,
                        c.SubjectCode,
                        c.BranchCode,
                        c.HallCode,
                        c.EduYearCode,
                        c.YearCode,
                        c.RootCode,
                        c.NoOfStudents,
                        c.TotalAmount,
                        c.TeacherAmount,
                        c.CenterAmount,
                        c.ScheduleCode,
                        TeacherName = c.TeacherCodeNavigation.TeacherName,
                        SubjectName = c.SubjectCodeNavigation.SubjectName,
                        HallName = c.HallCodeNavigation.HallName,
                        BranchName = c.BranchCodeNavigation.BranchName,
                        EduYearName = c.EduYearCodeNavigation.EduName,
                        YearName = c.YearCodeNavigation.YearName,
                        RootName = c.RootCodeNavigation.RootName,
                        CenterName = c.BranchCodeNavigation.CenterCodeNavigation.CenterName,
                        ScheduleStartTime = c.ScheduleCodeNavigation.StartTime,
                        ScheduleEndTime = c.ScheduleCodeNavigation.EndTime,
                        ScheduleCenterName = c.ScheduleCodeNavigation.CenterCodeNavigation.CenterName
                    })
                    .ToListAsync();

                var classViewModels = allClasses.Select(cls =>
                {
                    var classType = cls.ScheduleCode != null ? "schedule" : "direct";
                    TimeOnly? startTime = cls.ClassStartTime;
                    TimeOnly? endTime = cls.ClassEndTime;

                    if (!startTime.HasValue || !endTime.HasValue)
                    {
                        if (classType == "schedule" && cls.ScheduleStartTime != null)
                        {
                            startTime = TimeOnly.FromDateTime(cls.ScheduleStartTime.Value);
                            endTime = TimeOnly.FromDateTime(cls.ScheduleEndTime.Value);
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
                        teacherName = isCenter ? cls.TeacherName : null,
                        teacherCode = cls.TeacherCode,
                        hallName = isCenter ? cls.HallName : null,
                        hallCode = isCenter ? (int?)cls.HallCode : null,
                        centerName = !isCenter ? (classType == "schedule" ? cls.ScheduleCenterName : cls.CenterName) : null,
                        branchName = !isCenter ? cls.BranchName : null,
                        subjectName = cls.SubjectName,
                        subjectCode = cls.SubjectCode,
                        branchCode = cls.BranchCode,
                        eduYearName = cls.EduYearName,
                        eduYearCode = cls.EduYearCode,
                        yearName = cls.YearName,
                        yearCode = cls.YearCode,
                        rootName = cls.RootName,
                        rootCode = cls.RootCode,
                        noOfStudents = cls.NoOfStudents,
                        totalAmount = cls.TotalAmount?.ToString("F2"),
                        teacherAmount = cls.TeacherAmount?.ToString("F2"),
                        centerAmount = cls.CenterAmount?.ToString("F2"),
                        date = dateOnly.ToString("yyyy-MM-dd"),
                        classDate = cls.ClassDate?.ToString("yyyy-MM-dd"),
                        isCenter = isCenter
                    };
                }).ToList();

                _logger.LogInformation("Loaded {Count} classes for date {Date} and user {Username} (Root: {RootCode})",
                    classViewModels.Count, dateOnly.ToString("yyyy-MM-dd"), User.Identity?.Name, userRootCode);

                return Json(classViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily classes for date {Date} and user {Username}",
                    date ?? "", User.Identity?.Name);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: DailyClass/GetBranchesByCenter - Get branches filtered by center code (OPTIMIZED)
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

                var cacheKey = $"branches_center_{centerCode}_{userRootCode}";

                if (_memoryCache.TryGetValue(cacheKey, out var cachedBranches))
                {
                    return Json(cachedBranches);
                }

                var branches = await _context.Branches
                    .AsNoTracking()
                    .Where(b => b.CenterCode == centerCode && b.RootCode == userRootCode.Value && b.IsActive)
                    .Select(b => new { value = b.BranchCode, text = b.BranchName })
                    .ToListAsync();

                // Cache for 15 minutes
                _memoryCache.Set(cacheKey, branches, TimeSpan.FromMinutes(15));

                return Json(branches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branches for center {CenterCode}", centerCode);
                return Json(new List<object>());
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetTeachersForBranch(int branchCode)
        {
            // Join Teach and Teacher tables to get only teachers assigned to this branch
            var teachers = await _context.Teaches
                .Where(t => t.BranchCode == branchCode)
                .Select(t => new
                {
                    value = t.TeacherCode,
                    text = t.TeacherCodeNavigation.TeacherName // assumes navigation property
                })
                .Distinct()
                .ToListAsync();

            return Json(new { teachers });
        }
        /// <summary>
        /// POST: DailyClass/CreateClass - Create new class (OPTIMIZED)
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

                model.RootCode = userRootCode.Value;

                // ENFORCE: If center user has a group branch, override posted branch code with user's group branch
                if (isCenter)
                {
                    var groupBranchCode = await GetCurrentUserGroupBranchCode();
                    if (groupBranchCode != null)
                    {
                        model.BranchCode = groupBranchCode;
                    }
                }

                // Validate required fields
                if (string.IsNullOrEmpty(model.ClassName) ||
                    string.IsNullOrEmpty(model.StartTime) || string.IsNullOrEmpty(model.EndTime))
                {
                    return Json(new { success = false, error = "Class name, start time, and end time are required." });
                }

                // Different validation based on user type
                if (isCenter)
                {
                    // Center users must provide teacher and hall
                    if (!model.TeacherCode.HasValue || !model.SubjectCode.HasValue ||
                        !model.BranchCode.HasValue || !model.HallCode.HasValue || !model.EduYearCode.HasValue)
                    {
                        return Json(new { success = false, error = "Teacher, subject, branch, hall, and education year are required." });
                    }
                }
                else
                {
                    // Teacher users must provide center (hall is optional for teachers)
                    if (!model.CenterCode.HasValue || !model.SubjectCode.HasValue ||
                        !model.BranchCode.HasValue || !model.EduYearCode.HasValue)
                    {
                        return Json(new { success = false, error = "Center, subject, branch, and education year are required." });
                    }

                    // For teachers, set TeacherCode to the teacher belonging to this root
                    var teacherCode = await _context.Teachers
                        .AsNoTracking()
                        .Where(t => t.RootCode == userRootCode.Value && t.IsActive)
                        .Select(t => t.TeacherCode)
                        .FirstOrDefaultAsync();

                    if (teacherCode == 0)
                    {
                        return Json(new { success = false, error = "No teacher found for your account." });
                    }

                    model.TeacherCode = teacherCode;

                    // For teachers, if no hall is provided, use default hall
                    if (!model.HallCode.HasValue)
                    {
                        model.HallCode = await GetDefaultHallForRoot(userRootCode.Value);
                    }
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
                if (!await ValidateClassResourcesOptimized(model, userRootCode.Value))
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

                // Clear relevant caches
                ClearRelevantCaches(userRootCode.Value);

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
                        var teacherExists = await _context.Teachers
                            .AsNoTracking()
                            .AnyAsync(t => t.TeacherCode == teacherCode && t.RootCode == userRootCode.Value);
                        if (teacherExists)
                            existingClass.TeacherCode = teacherCode.Value;
                    }
                }
                else
                {
                    // For teacher users, ensure teacher is set to the teacher of this root (don't allow changing)
                    var rootTeacherCode = await _context.Teachers
                        .AsNoTracking()
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

                // --- Branch logic START ---
                // If center user with group branch, override posted branch with group branch
                if (isCenter)
                {
                    var groupBranchCode = await GetCurrentUserGroupBranchCode();
                    if (groupBranchCode != null)
                    {
                        existingClass.BranchCode = groupBranchCode.Value;
                    }
                    else
                    {
                        var branchCode = GetIntValue("branchCode");
                        if (branchCode.HasValue && branchCode > 0)
                        {
                            var branchExists = await _context.Branches
                                .AsNoTracking()
                                .AnyAsync(b => b.BranchCode == branchCode && b.RootCode == userRootCode.Value);
                            if (branchExists)
                                existingClass.BranchCode = branchCode.Value;
                        }
                    }
                }
                else
                {
                    var branchCode = GetIntValue("branchCode");
                    if (branchCode.HasValue && branchCode > 0)
                    {
                        var branchExists = await _context.Branches
                            .AsNoTracking()
                            .AnyAsync(b => b.BranchCode == branchCode && b.RootCode == userRootCode.Value);
                        if (branchExists)
                            existingClass.BranchCode = branchCode.Value;
                    }
                }
                // --- Branch logic END ---

                // Handle hall update based on user type
                var hallCode = GetIntValue("hallCode");
                if (isCenter)
                {
                    // Center users can update hall
                    if (hallCode.HasValue && hallCode > 0)
                    {
                        var hallExists = await _context.Halls
                            .AsNoTracking()
                            .AnyAsync(h => h.HallCode == hallCode && h.RootCode == userRootCode.Value);
                        if (hallExists)
                            existingClass.HallCode = hallCode.Value;
                    }
                }
                // For teacher users, don't update hall (they don't see/edit halls)

                var eduYearCode = GetIntValue("eduYearCode");
                if (eduYearCode.HasValue && eduYearCode > 0)
                {
                    var eduYearExists = await _context.EduYears
                        .AsNoTracking()
                        .AnyAsync(e => e.EduCode == eduYearCode && e.RootCode == userRootCode.Value);
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

                // Clear relevant caches
                ClearRelevantCaches(userRootCode.Value);

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
        /// POST: DailyClass/DeleteClass - Delete class (OPTIMIZED)
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

                // Clear relevant caches
                ClearRelevantCaches(userRootCode.Value);

                _logger.LogInformation("Deleted class {ClassId} by user {Username}", id, User.Identity?.Name);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class {ClassId} for user {Username}", id, User.Identity?.Name);
                return Json(new { success = false, error = "An error occurred while deleting the class: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjectsForTeacherAndBranch(int teacherCode, int branchCode)
        {
            var subjects = await _context.Teaches
                .Where(t => t.TeacherCode == teacherCode && t.BranchCode == branchCode)
                .Select(t => new
                {
                    value = t.SubjectCode,
                    text = t.SubjectCodeNavigation.SubjectName
                })
                .Distinct()
                .ToListAsync();

            return Json(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> GetHallsForBranch(int branchCode)
        {
            var halls = await _context.Halls
                .Where(h => h.BranchCode == branchCode)
                .Select(h => new { value = h.HallCode, text = h.HallName })
                .ToListAsync();

            return Json(new { halls });
        }
        // ==================== WEEKLY CLASS GENERATION METHODS ====================

        /// <summary>
        /// Generate classes for the current week (Saturday to Friday) from active schedules (OPTIMIZED) AND reservations
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

                var (weekStart, weekEnd) = GetCurrentWeekRange();

                // Get all active schedules for this root
                var activeSchedules = await _context.Schedules
                    .AsNoTracking()
                    .Where(s => s.RootCode == userRootCode.Value &&
                               !string.IsNullOrEmpty(s.DayOfWeek) &&
                               s.StartTime.HasValue &&
                               s.EndTime.HasValue)
                    .Select(s => new
                    {
                        s.ScheduleCode,
                        s.ScheduleName,
                        s.DayOfWeek,
                        s.StartTime,
                        s.EndTime,
                        s.TeacherCode,
                        s.SubjectCode,
                        s.HallCode,
                        s.EduYearCode,
                        s.YearCode,
                        s.ScheduleAmount
                    })
                    .ToListAsync();

                var result = new WeeklyClassGenerationResult
                {
                    WeekStart = weekStart,
                    WeekEnd = weekEnd,
                    CreatedCount = 0,
                    SkippedCount = 0
                };

                var classesToAdd = new List<Class>();

                // Generate classes for each day of the week based ONLY on schedule weekday
                for (var date = weekStart; date <= weekEnd; date = date.AddDays(1))
                {
                    var dayOfWeek = date.DayOfWeek.ToString();
                    var daySchedules = activeSchedules.Where(s => s.DayOfWeek == dayOfWeek).ToList();

                    foreach (var schedule in daySchedules)
                    {
                        var dateOnly = DateOnly.FromDateTime(date);

                        // Check if class already exists for this schedule and date
                        var existingClass = await _context.Classes
                            .AsNoTracking()
                            .AnyAsync(c => c.ScheduleCode == schedule.ScheduleCode &&
                                          c.ClassDate == dateOnly);

                        if (existingClass)
                        {
                            result.SkippedCount++;
                            continue;
                        }

                        // Get default values from schedule for required fields
                        var teacherCode = schedule.TeacherCode ?? await GetDefaultTeacherForRoot(userRootCode.Value);
                        var subjectCode = schedule.SubjectCode ?? await GetDefaultSubjectForRoot(userRootCode.Value);
                        var hallCode = schedule.HallCode ?? await GetDefaultHallForRoot(userRootCode.Value);
                        var branchCode = await GetDefaultBranchForRoot(userRootCode.Value);
                        var eduYearCode = schedule.EduYearCode ?? await GetDefaultEduYearForRoot(userRootCode.Value);

                        // Create new class with minimal data - let DB triggers handle the rest
                        var newClass = new Class
                        {
                            ClassName = $"{schedule.ScheduleName} - {date:MMM dd}", // Include date in name
                            ClassDate = dateOnly,
                            ScheduleCode = schedule.ScheduleCode,
                            RootCode = userRootCode.Value,

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

                        classesToAdd.Add(newClass);
                        result.CreatedCount++;
                    }
                }

                if (classesToAdd.Count > 0)
                {
                    _context.Classes.AddRange(classesToAdd);
                    await _context.SaveChangesAsync();
                }

                // Clear relevant caches
                ClearRelevantCaches(userRootCode.Value);

                _logger.LogInformation("Generated {CreatedCount} schedule-based classes for week {WeekStart} - {WeekEnd} by user {Username} (Root: {RootCode})",
                    result.CreatedCount, result.WeekStart.ToString("yyyy-MM-dd"), result.WeekEnd.ToString("yyyy-MM-dd"),
                    User.Identity?.Name, userRootCode);

                return Json(new
                {
                    success = true,
                    message = $"Generated {result.CreatedCount} schedule-based classes for this week ({result.WeekStart:MMM dd} - {result.WeekEnd:MMM dd}). Skipped {result.SkippedCount} existing classes.",
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
        }              /// Get weekly class generation status for the UI (OPTIMIZED with caching)
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

                var cacheKey = $"weekly_status_{userRootCode}_{DateTime.Today:yyyy-MM-dd}";

                if (_memoryCache.TryGetValue(cacheKey, out var cachedStatus))
                {
                    return Json(cachedStatus);
                }

                var (weekStart, weekEnd) = GetCurrentWeekRange();
                var weekStartOnly = DateOnly.FromDateTime(weekStart);
                var weekEndOnly = DateOnly.FromDateTime(weekEnd);

                // OPTIMIZATION: Single query with subqueries instead of multiple separate queries
                var weeklyData = await _context.Schedules
                    .AsNoTracking()
                    .Where(s => s.RootCode == userRootCode.Value &&
                               !string.IsNullOrEmpty(s.DayOfWeek) &&
                               s.StartTime.HasValue &&
                               s.EndTime.HasValue)
                    .GroupBy(s => 1) // Group all to get aggregate data
                    .Select(g => new
                    {
                        ActiveSchedulesCount = g.Count(),
                        SchedulesByDay = g.GroupBy(s => s.DayOfWeek).ToDictionary(x => x.Key, x => x.Count()),
                        ExistingClassesCount = _context.Classes.Count(c =>
                            c.RootCode == userRootCode.Value &&
                            c.ScheduleCode.HasValue &&
                            c.ClassDate >= weekStartOnly &&
                            c.ClassDate <= weekEndOnly),
                        ClassesByDay = _context.Classes
                            .Where(c => c.RootCode == userRootCode.Value &&
                                       c.ScheduleCode.HasValue &&
                                       c.ClassDate >= weekStartOnly &&
                                       c.ClassDate <= weekEndOnly &&
                                       c.ClassDate.HasValue)
                            .GroupBy(c => c.ClassDate.Value.DayOfWeek.ToString())
                            .ToDictionary(x => x.Key, x => x.Count())
                    })
                    .FirstOrDefaultAsync();

                var result = new
                {
                    weekStart = weekStart.ToString("yyyy-MM-dd"),
                    weekEnd = weekEnd.ToString("yyyy-MM-dd"),
                    weekStartFormatted = weekStart.ToString("MMM dd"),
                    weekEndFormatted = weekEnd.ToString("MMM dd"),
                    activeSchedulesCount = weeklyData?.ActiveSchedulesCount ?? 0,
                    existingClassesCount = weeklyData?.ExistingClassesCount ?? 0,
                    schedulesByDay = weeklyData?.SchedulesByDay ?? new Dictionary<string, int>(),
                    classesByDay = weeklyData?.ClassesByDay ?? new Dictionary<string, int>(),
                    needsGeneration = (weeklyData?.ExistingClassesCount ?? 0) < (weeklyData?.ActiveSchedulesCount ?? 0),
                    canGenerate = (weeklyData?.ActiveSchedulesCount ?? 0) > 0
                };

                // Cache for 5 minutes
                _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weekly generation status for user {Username}", User.Identity?.Name);
                return Json(new { error = ex.Message });
            }
        }

        // ==================== UTILITY METHODS ====================

        /// <summary>
        /// Creates optimized class view model with conditional display based on user type
        /// </summary>
        private object CreateClassViewModelOptimized(dynamic cls, DateTime date, string classType, bool isCenter)
        {
            // Get actual times based on class type
            TimeOnly? startTime = cls.ClassStartTime;
            TimeOnly? endTime = cls.ClassEndTime;

            if (!startTime.HasValue || !endTime.HasValue)
            {
                if (classType == "schedule" && cls.ScheduleStartTime != null)
                {
                    startTime = TimeOnly.FromDateTime(cls.ScheduleStartTime);
                    endTime = TimeOnly.FromDateTime(cls.ScheduleEndTime);
                }
                else if (classType == "reservation")
                {
                    startTime = cls.ReservationStartTime;
                    endTime = cls.ReservationEndTime;
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

                // CENTER USERS see: Teacher name and Hall
                teacherName = isCenter ? cls.TeacherName : null,
                teacherCode = cls.TeacherCode,
                hallName = isCenter ? cls.HallName : null, // Only show hall to center users
                hallCode = isCenter ? cls.HallCode : null, // Only include hall code for center users

                // TEACHER USERS see: Center name and Branch (NO HALL)
                centerName = !isCenter ? (classType == "schedule" ? cls.ScheduleCenterName : cls.CenterName) : null,
                branchName = !isCenter ? cls.BranchName : null,

                // Always include for form population (but display depends on user type)
                subjectName = cls.SubjectName,
                subjectCode = cls.SubjectCode,
                branchCode = cls.BranchCode,
                eduYearName = cls.EduYearName,
                eduYearCode = cls.EduYearCode,
                yearName = cls.YearName,
                yearCode = cls.YearCode,
                rootName = cls.RootName,
                rootCode = cls.RootCode,
                noOfStudents = cls.NoOfStudents,
                totalAmount = cls.TotalAmount?.ToString("F2"),
                teacherAmount = cls.TeacherAmount?.ToString("F2"),
                centerAmount = cls.CenterAmount?.ToString("F2"),
                date = date.ToString("yyyy-MM-dd"),
                classDate = cls.ClassDate?.ToString("yyyy-MM-dd"),
                isCenter = isCenter
            };
        }

        private async Task<bool> ValidateClassResourcesOptimized(DailyClassModel model, int rootCode)
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();

            // Use a single query to validate all resources at once
            var validationResult = await _context.Teachers
                .AsNoTracking()
                .Where(t => t.RootCode == rootCode)
                .Select(t => new
                {
                    // Validate teacher (only for center users)
                    TeacherValid = !isCenter || !model.TeacherCode.HasValue ||
                                  _context.Teachers.Any(x => x.TeacherCode == model.TeacherCode && x.RootCode == rootCode),

                    // Validate center (only for teacher users)
                    CenterValid = isCenter || !model.CenterCode.HasValue ||
                                 _context.Centers.Any(c => c.CenterCode == model.CenterCode && c.RootCode == rootCode),

                    // Validate branch
                    BranchValid = !model.BranchCode.HasValue ||
                                 _context.Branches.Any(b => b.BranchCode == model.BranchCode && b.RootCode == rootCode &&
                                                           (!isCenter || !model.CenterCode.HasValue || b.CenterCode == model.CenterCode)),

                    // Validate hall (only for center users, optional for teachers)
                    HallValid = !model.HallCode.HasValue ||
                               _context.Halls.Any(h => h.HallCode == model.HallCode && h.RootCode == rootCode),

                    // Validate education year
                    EduYearValid = !model.EduYearCode.HasValue ||
                                  _context.EduYears.Any(e => e.EduCode == model.EduYearCode && e.RootCode == rootCode)
                })
                .FirstOrDefaultAsync();

            return validationResult?.TeacherValid == true &&
                   validationResult?.CenterValid == true &&
                   validationResult?.BranchValid == true &&
                   validationResult?.HallValid == true &&
                   validationResult?.EduYearValid == true;
        }

        private async Task<object> BuildDropdownData(int rootCode, bool isCenter)
        {
            var baseData = new
            {
                subjects = await _context.Subjects
                    .AsNoTracking()
                    .Select(s => new { value = s.SubjectCode, text = s.SubjectName })
                    .ToListAsync(),

                years = await _context.Years
                    .AsNoTracking()
                    .Select(y => new { value = y.YearCode, text = y.YearName })
                    .ToListAsync(),

                eduYears = await _context.EduYears
                    .AsNoTracking()
                    .Where(e => e.RootCode == rootCode && e.IsActive)
                    .Select(e => new { value = e.EduCode, text = e.EduName })
                    .ToListAsync()
            };

            if (isCenter)
            {
                // CENTER USERS get: teachers, halls, and branches
                return new
                {
                    baseData.subjects,
                    baseData.years,
                    baseData.eduYears,
                    halls = await _context.Halls
                        .AsNoTracking()
                        .Where(h => h.RootCode == rootCode)
                        .Select(h => new { value = h.HallCode, text = h.HallName })
                        .ToListAsync(),
                    teachers = await _context.Teachers
                        .AsNoTracking()
                        .Where(t => t.RootCode == rootCode && t.IsActive)
                        .Select(t => new { value = t.TeacherCode, text = t.TeacherName })
                        .ToListAsync(),
                    branches = await _context.Branches
                        .AsNoTracking()
                        .Where(b => b.RootCode == rootCode && b.IsActive)
                        .Select(b => new { value = b.BranchCode, text = b.BranchName })
                        .ToListAsync()
                };
            }
            else
            {
                // TEACHER USERS get: centers and branches (NO HALLS)
                return new
                {
                    baseData.subjects,
                    baseData.years,
                    baseData.eduYears,
                    centers = await _context.Centers
                        .AsNoTracking()
                        .Where(c => c.RootCode == rootCode && c.IsActive)
                        .Select(c => new { value = c.CenterCode, text = c.CenterName })
                        .ToListAsync(),
                    branches = await _context.Branches
                        .AsNoTracking()
                        .Where(b => b.RootCode == rootCode && b.IsActive)
                        .Select(b => new { value = b.BranchCode, text = b.BranchName })
                        .ToListAsync()
                    // NO HALLS for teacher users
                };
            }
        }

        private void ClearRelevantCaches(int rootCode)
        {
            // Clear caches that might be affected by data changes
            var keysToRemove = new List<string>();

            // Find all cache keys related to this root
            if (_memoryCache is MemoryCache mc)
            {
                var field = typeof(MemoryCache).GetField("_coherentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(mc) is IDictionary coherentState)
                {
                    foreach (DictionaryEntry entry in coherentState)
                    {
                        var key = entry.Key?.ToString();
                        if (key != null && (key.Contains($"dropdown_data_{rootCode}") ||
                                           key.Contains($"weekly_status_{rootCode}") ||
                                           key.Contains($"branches_center_") && key.Contains($"_{rootCode}")))
                        {
                            keysToRemove.Add(key);
                        }
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
            }
        }

        // ==================== WEEKLY GENERATION HELPER METHODS ====================

        /// <summary>
        /// Core method to generate classes from schedules for the current week (OPTIMIZED)
        /// </summary>
        private async Task<WeeklyClassGenerationResult> GenerateClassesForCurrentWeek(int rootCode)
        {
            var (weekStart, weekEnd) = GetCurrentWeekRange();

            // Get all active schedules for this root
            var activeSchedules = await _context.Schedules
                .AsNoTracking()
                .Where(s => s.RootCode == rootCode &&
                           !string.IsNullOrEmpty(s.DayOfWeek) &&
                           s.StartTime.HasValue &&
                           s.EndTime.HasValue)
                .Select(s => new
                {
                    s.ScheduleCode,
                    s.ScheduleName,
                    s.DayOfWeek,
                    s.StartTime,
                    s.EndTime,
                    s.TeacherCode,
                    s.SubjectCode,
                    s.HallCode,
                    s.EduYearCode,
                    s.YearCode,
                    s.ScheduleAmount
                })
                .ToListAsync();

            var result = new WeeklyClassGenerationResult
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                CreatedCount = 0,
                SkippedCount = 0
            };

            var classesToAdd = new List<Class>();

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
                        .AsNoTracking()
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

                    classesToAdd.Add(newClass);
                    result.CreatedCount++;
                }
            }

            if (classesToAdd.Count > 0)
            {
                _context.Classes.AddRange(classesToAdd);
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
                .AsNoTracking()
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
                .AsNoTracking()
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
                .AsNoTracking()
                .AnyAsync(c => c.RootCode == rootCode &&
                              c.ScheduleCode.HasValue &&
                              c.ClassDate >= weekStartOnly &&
                              c.ClassDate <= weekEndOnly);

            // Check if we have active schedules
            var hasActiveSchedules = await _context.Schedules
                .AsNoTracking()
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
                .AsNoTracking()
                .Where(t => t.RootCode == rootCode && t.IsActive)
                .Select(t => t.TeacherCode)
                .FirstOrDefaultAsync();

            return firstTeacher > 0 ? firstTeacher : 1; // Fallback to 1 if no teacher found
        }

        private async Task<int> GetDefaultSubjectForRoot(int rootCode)
        {
            var firstSubject = await _context.Subjects
                .AsNoTracking()
                .Select(s => s.SubjectCode)
                .FirstOrDefaultAsync();

            return firstSubject > 0 ? firstSubject : 1; // Fallback to 1 if no subject found
        }

        private async Task<int> GetDefaultHallForRoot(int rootCode)
        {
            var firstHall = await _context.Halls
                .AsNoTracking()
                .Where(h => h.RootCode == rootCode)
                .Select(h => h.HallCode)
                .FirstOrDefaultAsync();

            return firstHall > 0 ? firstHall : 1; // Fallback to 1 if no hall found
        }

        private async Task<int> GetDefaultBranchForRoot(int rootCode)
        {
            var firstBranch = await _context.Branches
                .AsNoTracking()
                .Where(b => b.RootCode == rootCode && b.IsActive)
                .Select(b => b.BranchCode)
                .FirstOrDefaultAsync();

            return firstBranch > 0 ? firstBranch : 1; // Fallback to 1 if no branch found
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
        public int? HallCode { get; set; } // Optional for teachers, required for centers
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