using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using System.Globalization;
using System.Text.Json;
using centrny.Attributes;

namespace centrny.Controllers
{
    [RequirePageAccess("Schedule")]
    public class ScheduleController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<ScheduleController> _logger;

        public ScheduleController(CenterContext context, ILogger<ScheduleController> logger)
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

        /// <summary>
        /// Applies STRICT root-based filtering - ALL users must have a root and can only see their own data
        /// </summary>
        private IQueryable<Schedule> ApplyStrictRootFiltering(IQueryable<Schedule> query)
        {
            var userRootCode = GetCurrentUserRootCode();

            if (!userRootCode.HasValue)
            {
                // If user has no root, return empty result
                _logger.LogWarning("User {Username} has no RootCode - returning empty results", User.Identity?.Name);
                return query.Where(s => false); // Returns empty set
            }

            // Apply strict filtering - everyone sees only their root's data
            query = query.Where(s => s.RootCode == userRootCode.Value);
            _logger.LogDebug("Applied strict root filtering for RootCode: {RootCode}", userRootCode.Value);

            return query;
        }

        /// <summary>
        /// Gets a filtered schedule query with includes
        /// </summary>
        private IQueryable<Schedule> GetFilteredScheduleQuery()
        {
            var userRootCode = GetCurrentUserRootCode();

            var query = _context.Schedules.AsQueryable();

            // Apply root filtering FIRST
            if (userRootCode.HasValue)
            {
                query = query.Where(s => s.RootCode == userRootCode.Value);
                _logger.LogDebug("Applied strict root filtering for RootCode: {RootCode}", userRootCode.Value);
            }
            else
            {
                _logger.LogWarning("User {Username} has no RootCode - returning empty results", User.Identity?.Name);
                query = query.Where(s => false); // Returns empty set
            }

            // Then apply includes
            return query
                .Include(s => s.EduYearCodeNavigation)
                .Include(s => s.HallCodeNavigation)
                .Include(s => s.RootCodeNavigation)
                .Include(s => s.SubjectCodeNavigation)
                .Include(s => s.TeacherCodeNavigation);
        }

        // ==================== MAIN ACTIONS ====================

        /// <summary>
        /// GET: Schedule - Shows list view with strict root filtering
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var (rootCode, rootName, isCenter) = await GetUserContext();

            if (!rootCode.HasValue)
            {
                ViewBag.Error = "Unable to determine your root assignment. Please contact administrator.";
                return View(new List<Schedule>());
            }

            var schedules = await GetFilteredScheduleQuery()
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            // Pass user context to view
            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;
            ViewBag.ScheduleCount = schedules.Count;

            _logger.LogInformation("Loaded {Count} schedules for user {Username} (Root: {RootCode})",
                schedules.Count, User.Identity?.Name, rootCode);

            return View(schedules);
        }

        /// <summary>
        /// GET: Schedule/Calendar - Shows calendar view with strict root filtering
        /// </summary>
        public async Task<IActionResult> Calendar()
        {
            var (rootCode, rootName, isCenter) = await GetUserContext();

            if (!rootCode.HasValue)
            {
                ViewBag.Error = "Unable to determine your root assignment. Please contact administrator.";
                ViewBag.CurrentUserRootCode = null;
                ViewBag.UserRootName = "Unknown";
                ViewBag.IsCenter = false;
                return View();
            }

            await PopulateDropDownsSafe();

            // Pass user context to view
            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            return View();
        }

        /// <summary>
        /// GET: Schedule/GetCalendarEvents - API endpoint with strict root filtering
        /// </summary>
        public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
        {
            try
            {
                var (rootCode, rootName, isCenter) = await GetUserContext();

                if (!rootCode.HasValue)
                {
                    _logger.LogWarning("User {Username} has no RootCode - returning empty calendar events", User.Identity?.Name);
                    return Json(new List<object>());
                }

                // Get filtered schedules with includes
                var schedules = await GetFilteredScheduleQuery()
                    .Where(s => s.StartTime.HasValue && s.EndTime.HasValue && !string.IsNullOrEmpty(s.DayOfWeek))
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} calendar events for user {Username} (Root: {RootCode})",
                    schedules.Count, User.Identity?.Name, rootCode);

                var events = new List<object>();

                foreach (var schedule in schedules)
                {
                    // Format times
                    var startTime = schedule.StartTime.Value.ToString("h:mm tt");
                    var endTime = schedule.EndTime.Value.ToString("h:mm tt");

                    // Determine if this schedule belongs to a center or individual
                    var scheduleBelongsToCenter = schedule.RootCodeNavigation?.IsCenter ?? false;

                    var eventObj = new
                    {
                        id = $"schedule_{schedule.ScheduleCode}",
                        title = schedule.ScheduleName ?? "Untitled Schedule",
                        start = DateTime.Today.ToString("yyyy-MM-dd"),
                        end = DateTime.Today.ToString("yyyy-MM-dd"),
                        allDay = false,
                        backgroundColor = GetEventColor(scheduleBelongsToCenter),
                        borderColor = GetEventBorderColor(scheduleBelongsToCenter),
                        textColor = "#ffffff",
                        extendedProps = new
                        {
                            scheduleCode = schedule.ScheduleCode,
                            dayOfWeek = schedule.DayOfWeek,
                            startTime = startTime,
                            endTime = endTime,
                            hallName = schedule.HallCodeNavigation?.HallName,
                            hallCode = schedule.HallCode,
                            teacherName = schedule.TeacherCodeNavigation?.TeacherName,
                            teacherCode = schedule.TeacherCode,
                            subjectName = schedule.SubjectCodeNavigation?.SubjectName,
                            subjectCode = schedule.SubjectCode,
                            rootName = schedule.RootCodeNavigation?.RootName,
                            rootCode = schedule.RootCode,
                            eduYearCode = schedule.EduYearCode,
                            yearCode = schedule.YearCode,
                            amount = schedule.ScheduleAmount?.ToString("F2"),
                            scheduleAmount = schedule.ScheduleAmount,
                            isCenter = scheduleBelongsToCenter
                        }
                    };

                    events.Add(eventObj);
                }

                return Json(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading calendar events for user {Username}", User.Identity?.Name);
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST: Schedule/CreateScheduleEvent - Create with automatic root assignment
        /// </summary>
        [HttpPost]
        [RequirePageAccess("Schedule", "insert")]
        public async Task<IActionResult> CreateScheduleEvent([FromBody] ScheduleEventModel model)
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
                    return Json(new { success = false, error = "Unable to determine your root assignment. Please contact administrator." });
                }

                // ALWAYS set RootCode to current user's root - no exceptions
                model.RootCode = userRootCode.Value;
                _logger.LogDebug("Auto-setting RootCode to {RootCode} for user {Username}",
                    userRootCode.Value, User.Identity?.Name);

                // Validate required fields
                if (string.IsNullOrEmpty(model.Title) || string.IsNullOrEmpty(model.DayOfWeek))
                {
                    return Json(new { success = false, error = "Schedule name and day of week are required." });
                }

                if (string.IsNullOrEmpty(model.StartTime) || string.IsNullOrEmpty(model.EndTime))
                {
                    return Json(new { success = false, error = "Start time and end time are required." });
                }

                if (!model.YearCode.HasValue || model.YearCode <= 0)
                {
                    return Json(new { success = false, error = "Year selection is required." });
                }

                // Validate year exists
                var yearExists = await _context.Years.AnyAsync(y => y.YearCode == model.YearCode);
                if (!yearExists)
                {
                    return Json(new { success = false, error = "Selected year does not exist." });
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

                // Create schedule
                var baseDate = new DateTime(2000, 1, 1);
                var schedule = new Schedule
                {
                    ScheduleName = model.Title.Trim(),
                    DayOfWeek = model.DayOfWeek,
                    StartTime = baseDate.Add(startTime.ToTimeSpan()),
                    EndTime = baseDate.Add(endTime.ToTimeSpan()),
                    YearCode = model.YearCode.Value,
                    RootCode = model.RootCode, // Always the user's root
                    InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1"),
                    InsertTime = DateTime.Now
                };

                _context.Schedules.Add(schedule);
                await _context.SaveChangesAsync();

                // Update with optional fields (validate they belong to user's root)
                bool needsUpdate = false;

                if (model.HallCode.HasValue && model.HallCode > 0)
                {
                    var hallExists = await _context.Halls.AnyAsync(h => h.HallCode == model.HallCode && h.RootCode == userRootCode.Value);
                    if (hallExists)
                    {
                        schedule.HallCode = model.HallCode;
                        needsUpdate = true;
                    }
                    else
                    {
                        _logger.LogWarning("User {Username} tried to use Hall {HallCode} not belonging to their root {RootCode}",
                            User.Identity?.Name, model.HallCode, userRootCode.Value);
                    }
                }

                if (model.EduYearCode.HasValue && model.EduYearCode > 0)
                {
                    var eduYearExists = await _context.EduYears.AnyAsync(e => e.EduCode == model.EduYearCode && e.RootCode == userRootCode.Value);
                    if (eduYearExists)
                    {
                        schedule.EduYearCode = model.EduYearCode;
                        needsUpdate = true;
                    }
                    else
                    {
                        _logger.LogWarning("User {Username} tried to use EduYear {EduYearCode} not belonging to their root {RootCode}",
                            User.Identity?.Name, model.EduYearCode, userRootCode.Value);
                    }
                }

                if (model.TeacherCode.HasValue && model.TeacherCode > 0)
                {
                    var teacherExists = await _context.Teachers.AnyAsync(t => t.TeacherCode == model.TeacherCode && t.RootCode == userRootCode.Value);
                    if (teacherExists)
                    {
                        schedule.TeacherCode = model.TeacherCode;
                        needsUpdate = true;
                    }
                    else
                    {
                        _logger.LogWarning("User {Username} tried to use Teacher {TeacherCode} not belonging to their root {RootCode}",
                            User.Identity?.Name, model.TeacherCode, userRootCode.Value);
                    }
                }

                if (model.SubjectCode.HasValue && model.SubjectCode > 0)
                {
                    var subjectExists = await _context.Subjects.AnyAsync(s => s.SubjectCode == model.SubjectCode);
                    if (subjectExists)
                    {
                        schedule.SubjectCode = model.SubjectCode;
                        needsUpdate = true;
                    }
                }

                if (model.ScheduleAmount.HasValue && model.ScheduleAmount > 0)
                {
                    schedule.ScheduleAmount = model.ScheduleAmount;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    _context.Schedules.Update(schedule);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Created schedule {ScheduleName} for RootCode {RootCode} by user {Username}",
                    schedule.ScheduleName, schedule.RootCode, User.Identity?.Name);

                return Json(new { success = true, id = schedule.ScheduleCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating schedule for user {Username}", User.Identity?.Name);
                return Json(new { success = false, error = $"Unexpected error: {ex.Message}" });
            }
        }

        /// <summary>
        /// POST: Schedule/EditScheduleEvent - Edit with strict root validation
        /// </summary>
        [HttpPost]
        [RequirePageAccess("Schedule", "update")]
        public async Task<IActionResult> EditScheduleEvent(int id, [FromBody] JsonElement json)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                // Find schedule with root filtering
                var schedule = await _context.Schedules
                    .Where(s => s.ScheduleCode == id && s.RootCode == userRootCode.Value)
                    .FirstOrDefaultAsync();

                if (schedule == null)
                {
                    _logger.LogWarning("User {Username} attempted to edit schedule {ScheduleId} - not found or belongs to different root",
                        User.Identity?.Name, id);
                    return Json(new { success = false, error = "Schedule not found or you don't have permission to edit it." });
                }

                // Helper function to safely get string value from JsonElement
                string GetStringValue(string propertyName)
                {
                    if (json.TryGetProperty(propertyName, out var prop))
                    {
                        return prop.ValueKind switch
                        {
                            JsonValueKind.String => prop.GetString()?.Trim(),
                            JsonValueKind.Number => prop.GetDecimal().ToString(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null => null,
                            _ => prop.ToString()?.Trim()
                        };
                    }
                    return null;
                }

                // Helper function to safely get integer value from JsonElement
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

                // Helper function to safely get decimal value from JsonElement
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

                // Parse and validate input using safe helpers
                string title = GetStringValue("title");
                string dayOfWeek = GetStringValue("dayOfWeek");
                string startTimeStr = GetStringValue("startTime");
                string endTimeStr = GetStringValue("endTime");

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(dayOfWeek))
                {
                    return Json(new { success = false, error = "Schedule name and day of week are required." });
                }

                if (string.IsNullOrEmpty(startTimeStr) || string.IsNullOrEmpty(endTimeStr))
                {
                    return Json(new { success = false, error = "Start time and end time are required." });
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

                // Update schedule - basic fields
                var baseDate = new DateTime(2000, 1, 1);
                schedule.ScheduleName = title;
                schedule.DayOfWeek = dayOfWeek;
                schedule.StartTime = baseDate.Add(startTime.ToTimeSpan());
                schedule.EndTime = baseDate.Add(endTime.ToTimeSpan());

                // Update optional fields with root validation using safe helpers
                var hallCode = GetIntValue("hallCode");
                if (hallCode.HasValue && hallCode > 0)
                {
                    var hallExists = await _context.Halls.AnyAsync(h => h.HallCode == hallCode && h.RootCode == userRootCode.Value);
                    schedule.HallCode = hallExists ? hallCode : null;
                }
                else
                {
                    schedule.HallCode = null;
                }

                var eduYearCode = GetIntValue("eduYearCode");
                if (eduYearCode.HasValue && eduYearCode > 0)
                {
                    var eduYearExists = await _context.EduYears.AnyAsync(e => e.EduCode == eduYearCode && e.RootCode == userRootCode.Value);
                    schedule.EduYearCode = eduYearExists ? eduYearCode : null;
                }
                else
                {
                    schedule.EduYearCode = null;
                }

                var teacherCode = GetIntValue("teacherCode");
                if (teacherCode.HasValue && teacherCode > 0)
                {
                    var teacherExists = await _context.Teachers.AnyAsync(t => t.TeacherCode == teacherCode && t.RootCode == userRootCode.Value);
                    schedule.TeacherCode = teacherExists ? teacherCode : null;
                }
                else
                {
                    schedule.TeacherCode = null;
                }

                var subjectCode = GetIntValue("subjectCode");
                schedule.SubjectCode = subjectCode > 0 ? subjectCode : null;

                var yearCode = GetIntValue("yearCode");
                if (yearCode.HasValue && yearCode > 0)
                {
                    schedule.YearCode = yearCode.Value;
                }

                var scheduleAmount = GetDecimalValue("scheduleAmount");
                schedule.ScheduleAmount = scheduleAmount > 0 ? scheduleAmount : null;

                // Never allow root change
                // schedule.RootCode stays the same

                _context.Schedules.Update(schedule);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated schedule {ScheduleId} by user {Username}", id, User.Identity?.Name);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing schedule {ScheduleId} for user {Username}", id, User.Identity?.Name);
                return Json(new { success = false, error = "An error occurred while updating the schedule: " + ex.Message });
            }
        }

        /// <summary>
        /// POST: Schedule/DeleteScheduleEvent - Delete API endpoint with strict root validation
        /// </summary>
        [HttpPost]
        [RequirePageAccess("Schedule", "delete")]
        public async Task<IActionResult> DeleteScheduleEvent(int id)
        {
            try
            {
                var (userRootCode, rootName, isCenter) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                // Find schedule with root filtering
                var schedule = await _context.Schedules
                    .Where(s => s.ScheduleCode == id && s.RootCode == userRootCode.Value)
                    .FirstOrDefaultAsync();

                if (schedule == null)
                {
                    _logger.LogWarning("User {Username} attempted to delete schedule {ScheduleId} - not found or belongs to different root",
                        User.Identity?.Name, id);
                    return Json(new { success = false, error = "Schedule not found or you don't have permission to delete it." });
                }

                // Check for related data before deletion
                var hasClasses = await _context.Classes.AnyAsync(c => c.ScheduleCode == id);
                var hasAttendance = await _context.Attends.AnyAsync(a => a.ScheduleCode == id);

                if (hasClasses || hasAttendance)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Cannot delete schedule. It has associated classes or attendance records."
                    });
                }

                _context.Schedules.Remove(schedule);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted schedule {ScheduleId} ({ScheduleName}) by user {Username}",
                    id, schedule.ScheduleName, User.Identity?.Name);

                return Json(new { success = true, message = "Schedule deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedule {ScheduleId} for user {Username}", id, User.Identity?.Name);
                return Json(new { success = false, error = "An error occurred while deleting the schedule: " + ex.Message });
            }
        }

        // ==================== EXISTING CRUD METHODS WITH STRICT ROOT FILTERING ====================

        /// <summary>
        /// GET: Schedule/Details/5 - With strict root validation
        /// </summary>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var (userRootCode, rootName, isCenter) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                return RedirectToAction("Index");
            }

            var schedule = await GetFilteredScheduleQuery()
                .Where(s => s.ScheduleCode == id.Value)
                .FirstOrDefaultAsync();

            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }

        /// <summary>
        /// GET: Schedule/Create
        /// </summary>
        [RequirePageAccess("Schedule", "insert")]
        public async Task<IActionResult> Create()
        {
            var (rootCode, rootName, isCenter) = await GetUserContext();

            if (!rootCode.HasValue)
            {
                TempData["Error"] = "Unable to determine your root assignment. Please contact administrator.";
                return RedirectToAction("Index");
            }

            await PopulateDropDownsSafe();

            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            return View();
        }

        /// <summary>
        /// POST: Schedule/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePageAccess("Schedule", "insert")]
        public async Task<IActionResult> Create([Bind("ScheduleCode,ScheduleName,HallCode,ScheduleAmount,EduYearCode,TeacherCode,SubjectCode,DayOfWeek,StartTime,EndTime,YearCode")] Schedule schedule)
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                TempData["Error"] = "Unable to determine your root assignment. Please contact administrator.";
                return RedirectToAction("Index");
            }

            // ALWAYS set RootCode to current user's root
            schedule.RootCode = userRootCode.Value;

            // Set audit fields
            schedule.InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1");
            schedule.InsertTime = DateTime.Now;

            if (ModelState.IsValid)
            {
                _context.Add(schedule);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Calendar));
            }

            await PopulateDropDownsSafe(schedule);
            ViewBag.CurrentUserRootCode = userRootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            return View(schedule);
        }

        /// <summary>
        /// GET: Schedule/Edit/5
        /// </summary>
        [RequirePageAccess("Schedule", "update")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound("Schedule ID is required.");
            }

            var (userRootCode, rootName, isCenter) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                return RedirectToAction("Index");
            }

            try
            {
                var schedule = await _context.Schedules
                    .Where(s => s.ScheduleCode == id.Value && s.RootCode == userRootCode.Value)
                    .FirstOrDefaultAsync();

                if (schedule == null)
                {
                    return NotFound($"Schedule with ID {id} was not found or you don't have permission to edit it.");
                }

                await PopulateDropDownsSafe(schedule);
                ViewBag.CurrentUserRootCode = userRootCode;
                ViewBag.UserRootName = rootName;
                ViewBag.IsCenter = isCenter;

                return View(schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schedule {ScheduleId} for edit by user {Username}", id, User.Identity?.Name);
                return NotFound("Error loading schedule.");
            }
        }

        /// <summary>
        /// POST: Schedule/Edit/5
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePageAccess("Schedule", "update")]
        public async Task<IActionResult> Edit(int id, [Bind("ScheduleCode,ScheduleName,HallCode,ScheduleAmount,EduYearCode,TeacherCode,SubjectCode,DayOfWeek,StartTime,EndTime,YearCode")] Schedule schedule)
        {
            if (id != schedule.ScheduleCode)
            {
                return NotFound();
            }

            var (userRootCode, rootName, isCenter) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                return RedirectToAction("Index");
            }

            // Ensure the schedule belongs to user's root
            var existingSchedule = await _context.Schedules
                .Where(s => s.ScheduleCode == id && s.RootCode == userRootCode.Value)
                .FirstOrDefaultAsync();

            if (existingSchedule == null)
            {
                return NotFound("Schedule not found or you don't have permission to edit it.");
            }

            // Keep the original RootCode
            schedule.RootCode = userRootCode.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(schedule);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ScheduleExists(schedule.ScheduleCode))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Calendar));
            }

            await PopulateDropDownsSafe(schedule);
            ViewBag.CurrentUserRootCode = userRootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            return View(schedule);
        }

        /// <summary>
        /// GET: Schedule/Delete/5
        /// </summary>
        [RequirePageAccess("Schedule", "delete")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var (userRootCode, rootName, isCenter) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                return RedirectToAction("Index");
            }

            var schedule = await GetFilteredScheduleQuery()
                .Where(s => s.ScheduleCode == id.Value)
                .FirstOrDefaultAsync();

            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }

        /// <summary>
        /// POST: Schedule/Delete/5
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequirePageAccess("Schedule", "delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                return RedirectToAction("Index");
            }

            var schedule = await _context.Schedules
                .Where(s => s.ScheduleCode == id && s.RootCode == userRootCode.Value)
                .FirstOrDefaultAsync();

            if (schedule != null)
            {
                _context.Schedules.Remove(schedule);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted schedule {ScheduleId} by user {Username}", id, User.Identity?.Name);
            }

            return RedirectToAction(nameof(Calendar));
        }

        // ==================== UTILITY METHODS ====================

        private bool ScheduleExists(int id)
        {
            var userRootCode = GetCurrentUserRootCode();
            if (!userRootCode.HasValue) return false;

            return _context.Schedules.Any(e => e.ScheduleCode == id && e.RootCode == userRootCode.Value);
        }

        private async Task PopulateDropDownsSafe(Schedule schedule = null)
        {
            var (userRootCode, rootName, isCenter) = await GetUserContext();

            try
            {
                // Days of week
                var daysOfWeek = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Sunday", Text = "Sunday" },
                    new SelectListItem { Value = "Monday", Text = "Monday" },
                    new SelectListItem { Value = "Tuesday", Text = "Tuesday" },
                    new SelectListItem { Value = "Wednesday", Text = "Wednesday" },
                    new SelectListItem { Value = "Thursday", Text = "Thursday" },
                    new SelectListItem { Value = "Friday", Text = "Friday" },
                    new SelectListItem { Value = "Saturday", Text = "Saturday" }
                };
                ViewBag.DaysOfWeek = new SelectList(daysOfWeek, "Value", "Text", schedule?.DayOfWeek);

                if (!userRootCode.HasValue)
                {
                    // If no root code, provide empty dropdowns
                    ViewData["EduYearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["HallCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["RootCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["SubjectCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["TeacherCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    ViewData["YearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                    return;
                }

                // EduYears - filter by user's root
                var eduYears = await _context.EduYears
                    .Where(e => e.RootCode == userRootCode.Value)
                    .ToListAsync();
                ViewData["EduYearCode"] = new SelectList(eduYears, "EduCode", "EduName", schedule?.EduYearCode);

                // Halls - filter by user's root
                var halls = await _context.Halls
                    .Where(h => h.RootCode == userRootCode.Value)
                    .ToListAsync();
                ViewData["HallCode"] = new SelectList(halls, "HallCode", "HallName", schedule?.HallCode);

                // Root - only current user's root
                var currentRoot = await _context.Roots
                    .Where(r => r.RootCode == userRootCode.Value)
                    .ToListAsync();
                ViewData["RootCode"] = new SelectList(currentRoot, "RootCode", "RootName", schedule?.RootCode);

                // Subjects - global (no filtering needed)
                var subjects = await _context.Subjects.ToListAsync();
                ViewData["SubjectCode"] = new SelectList(subjects, "SubjectCode", "SubjectName", schedule?.SubjectCode);

                // Teachers - filter by user's root
                var teachers = await _context.Teachers
                    .Where(t => t.RootCode == userRootCode.Value)
                    .ToListAsync();
                ViewData["TeacherCode"] = new SelectList(teachers, "TeacherCode", "TeacherName", schedule?.TeacherCode);

                // Years - global (no filtering needed)
                var years = await _context.Years.ToListAsync();
                ViewData["YearCode"] = new SelectList(years, "YearCode", "YearName", schedule?.YearCode);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PopulateDropDownsSafe for user {Username}", User.Identity?.Name);

                // Create empty dropdowns as fallback
                ViewBag.DaysOfWeek = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["EduYearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["HallCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["RootCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["SubjectCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["TeacherCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["YearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");

                ViewBag.DropdownError = $"Error loading dropdown data: {ex.Message}";
            }
        }

        private string GetEventColor(bool? isCenter)
        {
            return isCenter == true ? "#00b894" : "#e17055"; // Green for center, orange-red for teacher
        }

        private string GetEventBorderColor(bool? isCenter)
        {
            return isCenter == true ? "#00a085" : "#d63031"; // Darker green for center, red for teacher
        }
    }

    public class ScheduleEventModel
    {
        public string Title { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int? HallCode { get; set; }
        public int? RootCode { get; set; } // This will be auto-set to user's root
        public int? EduYearCode { get; set; }
        public int? TeacherCode { get; set; }
        public int? SubjectCode { get; set; }
        public decimal? ScheduleAmount { get; set; }
        public int? YearCode { get; set; }
    }
}