using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
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

        private int? GetCurrentUserRootCode()
        {
            try
            {
                var rootCodeClaim = User.FindFirst("RootCode");
                if (rootCodeClaim != null && int.TryParse(rootCodeClaim.Value, out int rootCode))
                {
                    return rootCode;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user root code");
                return null;
            }
        }

        private bool IsCurrentUserTeacher()
        {
            var isCenterClaim = User.FindFirst("IsCenter");
            return isCenterClaim?.Value != "True";
        }

        private bool IsCurrentUserCenter()
        {
            var isCenterClaim = User.FindFirst("IsCenter");
            return isCenterClaim?.Value == "True";
        }

        private async Task<int?> GetCurrentUserTeacherCode()
        {
            if (!IsCurrentUserTeacher()) return null;

            var userRootCode = GetCurrentUserRootCode();
            if (!userRootCode.HasValue) return null;

            var teacher = await _context.Teachers
                .Where(t => t.RootCode == userRootCode.Value)
                .FirstOrDefaultAsync();

            return teacher?.TeacherCode;
        }

        private async Task<int?> GetSingleCenterForCenterUser()
        {
            var userRootCode = GetCurrentUserRootCode();
            if (!userRootCode.HasValue || !IsCurrentUserCenter()) return null;

            try
            {
                var center = await _context.Centers
                    .Where(c => c.RootCode == userRootCode.Value && c.IsActive)
                    .FirstOrDefaultAsync();

                return center?.CenterCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting single center for center user");
                return null;
            }
        }

        private async Task<(int? rootCode, string rootName, bool isCenter, string branchName)> GetUserContext()
        {
            try
            {
                var rootCode = GetCurrentUserRootCode();
                var rootName = User.FindFirst("RootName")?.Value ?? "Unknown";
                var isCenter = User.FindFirst("IsCenter")?.Value == "True";

                string branchName = "Unknown Branch";
                if (rootCode.HasValue)
                {
                    var branch = await _context.Branches
                        .Where(b => b.RootCode == rootCode.Value)
                        .FirstOrDefaultAsync();

                    if (branch != null)
                    {
                        branchName = branch.BranchName;
                    }
                }

                return (rootCode, rootName, isCenter, branchName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserContext");
                return (null, "Error", false, "Error");
            }
        }

        private IQueryable<Schedule> GetFilteredScheduleQuery()
        {
            var userRootCode = GetCurrentUserRootCode();
            var query = _context.Schedules.AsQueryable();

            if (userRootCode.HasValue)
            {
                query = query.Where(s => s.RootCode == userRootCode.Value);
            }
            else
            {
                query = query.Where(s => false);
            }

            return query
                .Include(s => s.RootCodeNavigation)
                .Include(s => s.YearCodeNavigation)
                .Include(s => s.HallCodeNavigation)
                .Include(s => s.TeacherCodeNavigation)
                .Include(s => s.SubjectCodeNavigation)
                .Include(s => s.CenterCodeNavigation)
                .Include(s => s.BranchCodeNavigation)
                .Include(s => s.EduYearCodeNavigation);
        }

        // ==================== API ENDPOINTS ====================

        [HttpGet]
        public async Task<IActionResult> GetSubjectsForTeacher(int teacherCode, int? yearCode)
        {
            try
            {
                var userRootCode = GetCurrentUserRootCode();
                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                var query = _context.Teaches
                    .Where(t => t.TeacherCode == teacherCode && t.RootCode == userRootCode.Value && t.IsActive);

                if (yearCode.HasValue && yearCode > 0)
                {
                    query = query.Where(t => t.YearCode == yearCode.Value);
                }

                var subjects = await query
                    .Include(t => t.SubjectCodeNavigation)
                    .Select(t => new {
                        value = t.SubjectCode,
                        text = t.SubjectCodeNavigation.SubjectName
                    })
                    .Distinct()
                    .OrderBy(s => s.text)
                    .ToListAsync();

                return Json(new { success = true, subjects = subjects });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subjects for teacher {TeacherCode}", teacherCode);
                return Json(new { success = false, error = "Error retrieving subjects." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesForCenterUser()
        {
            try
            {
                var userRootCode = GetCurrentUserRootCode();
                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                if (!IsCurrentUserCenter())
                {
                    return Json(new { success = false, error = "This endpoint is only available for center users." });
                }

                var centerCode = await GetSingleCenterForCenterUser();
                if (!centerCode.HasValue)
                {
                    return Json(new { success = false, error = "No active center found for your account." });
                }

                var branches = await _context.Branches
                    .Where(b => b.CenterCode == centerCode.Value && b.RootCode == userRootCode.Value && b.IsActive)
                    .Select(b => new {
                        value = b.BranchCode,
                        text = b.BranchName
                    })
                    .OrderBy(b => b.text)
                    .ToListAsync();

                return Json(new { success = true, branches = branches, centerCode = centerCode.Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branches for center user");
                return Json(new { success = false, error = "Error retrieving branches." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersForCenterUser()
        {
            try
            {
                var userRootCode = GetCurrentUserRootCode();
                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                if (!IsCurrentUserCenter())
                {
                    return Json(new { success = false, error = "This endpoint is only available for center users." });
                }

                var teachers = await _context.Teachers
                    .Where(t => t.RootCode == userRootCode.Value && t.IsActive == true)
                    .OrderBy(t => t.TeacherName)
                    .Select(t => new {
                        value = t.TeacherCode,
                        text = t.TeacherName
                    })
                    .ToListAsync();

                return Json(new { success = true, teachers = teachers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teachers for center user");
                return Json(new { success = false, error = "Error retrieving teachers." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesForCenter(int centerCode)
        {
            try
            {
                var userRootCode = GetCurrentUserRootCode();
                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                var centerExists = await _context.Centers.AnyAsync(c => c.CenterCode == centerCode && c.RootCode == userRootCode.Value);
                if (!centerExists)
                {
                    return Json(new { success = false, error = "Selected center does not belong to your root." });
                }

                var branches = await _context.Branches
                    .Where(b => b.CenterCode == centerCode && b.RootCode == userRootCode.Value && b.IsActive)
                    .Select(b => new {
                        value = b.BranchCode,
                        text = b.BranchName
                    })
                    .OrderBy(b => b.text)
                    .ToListAsync();

                return Json(new { success = true, branches = branches });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branches for center {CenterCode}", centerCode);
                return Json(new { success = false, error = "Error retrieving branches." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHallsForBranch(int branchCode)
        {
            try
            {
                var userRootCode = GetCurrentUserRootCode();
                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                var branchExists = await _context.Branches.AnyAsync(b => b.BranchCode == branchCode && b.RootCode == userRootCode.Value);
                if (!branchExists)
                {
                    return Json(new { success = false, error = "Selected branch does not belong to your root." });
                }

                var halls = await _context.Halls
                    .Where(h => h.BranchCode == branchCode && h.RootCode == userRootCode.Value)
                    .Select(h => new {
                        value = h.HallCode,
                        text = h.HallName,
                        capacity = h.HallCapacity
                    })
                    .OrderBy(h => h.text)
                    .ToListAsync();

                return Json(new { success = true, halls = halls });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting halls for branch {BranchCode}", branchCode);
                return Json(new { success = false, error = "Error retrieving halls." });
            }
        }

        // ==================== MAIN ACTIONS ====================

        public async Task<IActionResult> Index()
        {
            try
            {
                var (rootCode, rootName, isCenter, branchName) = await GetUserContext();

                if (!rootCode.HasValue)
                {
                    ViewBag.Error = "Unable to determine your root assignment. Please contact administrator.";
                    return View(new List<Schedule>());
                }

                await PopulateDropDowns();

                var schedules = await _context.Schedules
                    .Where(s => s.RootCode == rootCode.Value)
                    .ToListAsync();

                ViewBag.CurrentUserRootCode = rootCode;
                ViewBag.UserRootName = rootName;
                ViewBag.IsCenter = isCenter;
                ViewBag.IsTeacher = IsCurrentUserTeacher();
                ViewBag.BranchName = branchName;
                ViewBag.ScheduleCount = schedules.Count;

                if (IsCurrentUserCenter())
                {
                    var centerCode = await _context.Centers
                        .Where(c => c.RootCode == rootCode.Value && c.IsActive)
                        .Select(c => c.CenterCode)
                        .FirstOrDefaultAsync();
                    ViewBag.SingleCenterCode = centerCode;
                }

                return View(schedules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Index action");
                ViewBag.Error = $"Error loading schedules: {ex.Message}";
                return View(new List<Schedule>());
            }
        }

        public async Task<IActionResult> Calendar()
        {
            var (rootCode, rootName, isCenter, branchName) = await GetUserContext();

            if (!rootCode.HasValue)
            {
                ViewBag.Error = "Unable to determine your root assignment. Please contact administrator.";
                ViewBag.CurrentUserRootCode = null;
                ViewBag.UserRootName = "Unknown";
                ViewBag.IsCenter = false;
                ViewBag.IsTeacher = true;
                ViewBag.BranchName = "Unknown Branch";
                return View();
            }

            await PopulateDropDowns();

            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;
            ViewBag.IsTeacher = IsCurrentUserTeacher();
            ViewBag.BranchName = branchName;

            if (IsCurrentUserCenter())
            {
                var centerCode = await _context.Centers
                    .Where(c => c.RootCode == rootCode.Value && c.IsActive)
                    .Select(c => c.CenterCode)
                    .FirstOrDefaultAsync();
                ViewBag.SingleCenterCode = centerCode;
            }

            return View();
        }

        [RequirePageAccess("Schedule", "insert")]
        public async Task<IActionResult> Create()
        {
            var (rootCode, rootName, isCenter, branchName) = await GetUserContext();

            if (!rootCode.HasValue)
            {
                TempData["Error"] = "Unable to determine your root assignment. Please contact administrator.";
                return RedirectToAction("Index");
            }

            await PopulateDropDowns();

            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;
            ViewBag.IsTeacher = IsCurrentUserTeacher();
            ViewBag.BranchName = branchName;

            if (IsCurrentUserCenter())
            {
                ViewBag.SingleCenterCode = await GetSingleCenterForCenterUser();
            }

            return View();
        }

        public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
        {
            try
            {
                var (rootCode, rootName, isCenter, branchName) = await GetUserContext();

                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var schedules = await GetFilteredScheduleQuery()
                    .Where(s => s.StartTime.HasValue && s.EndTime.HasValue && !string.IsNullOrEmpty(s.DayOfWeek))
                    .ToListAsync();

                var events = schedules.Select(schedule => new
                {
                    id = $"schedule_{schedule.ScheduleCode}",
                    title = schedule.ScheduleName ?? "Untitled Schedule",
                    start = DateTime.Today.ToString("yyyy-MM-dd"),
                    end = DateTime.Today.ToString("yyyy-MM-dd"),
                    allDay = false,
                    backgroundColor = GetEventColor(schedule.RootCodeNavigation?.IsCenter),
                    borderColor = GetEventBorderColor(schedule.RootCodeNavigation?.IsCenter),
                    textColor = "#ffffff",
                    extendedProps = new
                    {
                        scheduleCode = schedule.ScheduleCode,
                        dayOfWeek = schedule.DayOfWeek,
                        startTime = schedule.StartTime.Value.ToString("h:mm tt"),
                        endTime = schedule.EndTime.Value.ToString("h:mm tt"),
                        hallName = schedule.HallCodeNavigation?.HallName,
                        hallCode = schedule.HallCode,
                        teacherName = schedule.TeacherCodeNavigation?.TeacherName,
                        teacherCode = schedule.TeacherCode,
                        subjectName = schedule.SubjectCodeNavigation?.SubjectName,
                        subjectCode = schedule.SubjectCode,
                        rootName = schedule.RootCodeNavigation?.RootName,
                        rootCode = schedule.RootCode,
                        centerName = schedule.CenterCodeNavigation?.CenterName,
                        centerCode = schedule.CenterCode,
                        branchName = schedule.BranchCodeNavigation?.BranchName,
                        branchCode = schedule.BranchCode,
                        eduYearCode = schedule.EduYearCode,
                        yearCode = schedule.YearCode,
                        amount = schedule.ScheduleAmount?.ToString("F2"),
                        scheduleAmount = schedule.ScheduleAmount,
                        isCenter = schedule.RootCodeNavigation?.IsCenter ?? false
                    }
                }).ToList();

                return Json(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading calendar events");
                return Json(new { error = ex.Message });
            }
        }

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

                var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                model.RootCode = userRootCode.Value;

                if (IsCurrentUserCenter())
                {
                    var centerCode = await GetSingleCenterForCenterUser();
                    if (centerCode.HasValue)
                    {
                        model.CenterCode = centerCode.Value;
                    }
                    else
                    {
                        return Json(new { success = false, error = "No active center found for your account." });
                    }
                }

                if (IsCurrentUserTeacher())
                {
                    var teacherCode = await GetCurrentUserTeacherCode();
                    if (teacherCode.HasValue)
                    {
                        model.TeacherCode = teacherCode.Value;
                    }
                }

                // Validation
                if (string.IsNullOrEmpty(model.Title) || string.IsNullOrEmpty(model.DayOfWeek) ||
                    string.IsNullOrEmpty(model.StartTime) || string.IsNullOrEmpty(model.EndTime) ||
                    !model.YearCode.HasValue || model.YearCode <= 0)
                {
                    return Json(new { success = false, error = "Required fields are missing." });
                }

                if (IsCurrentUserCenter() && (!model.TeacherCode.HasValue || model.TeacherCode <= 0))
                {
                    return Json(new { success = false, error = "Teacher selection is required for center users." });
                }

                if (!TimeOnly.TryParse(model.StartTime, out TimeOnly startTime) ||
                    !TimeOnly.TryParse(model.EndTime, out TimeOnly endTime))
                {
                    return Json(new { success = false, error = "Invalid time format." });
                }

                if (startTime >= endTime)
                {
                    return Json(new { success = false, error = "End time must be after start time." });
                }

                var baseDate = new DateTime(2000, 1, 1);
                var schedule = new Schedule
                {
                    ScheduleName = model.Title.Trim(),
                    DayOfWeek = model.DayOfWeek,
                    StartTime = baseDate.Add(startTime.ToTimeSpan()),
                    EndTime = baseDate.Add(endTime.ToTimeSpan()),
                    YearCode = model.YearCode.Value,
                    RootCode = model.RootCode,
                    TeacherCode = model.TeacherCode,
                    CenterCode = model.CenterCode,
                    BranchCode = model.BranchCode,
                    HallCode = model.HallCode > 0 ? model.HallCode : null,
                    EduYearCode = model.EduYearCode > 0 ? model.EduYearCode : null,
                    SubjectCode = model.SubjectCode > 0 ? model.SubjectCode : null,
                    ScheduleAmount = model.ScheduleAmount > 0 ? model.ScheduleAmount : null,
                    InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1"),
                    InsertTime = DateTime.Now
                };

                _context.Schedules.Add(schedule);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = schedule.ScheduleCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating schedule");
                return Json(new { success = false, error = $"Unexpected error: {ex.Message}" });
            }
        }

        [HttpPost]
        [RequirePageAccess("Schedule", "update")]
        public async Task<IActionResult> EditScheduleEvent(int id, [FromBody] JsonElement json)
        {
            try
            {
                var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                var schedule = await _context.Schedules
                    .Where(s => s.ScheduleCode == id && s.RootCode == userRootCode.Value)
                    .FirstOrDefaultAsync();

                if (schedule == null)
                {
                    return Json(new { success = false, error = "Schedule not found or you don't have permission to edit it." });
                }

                // Helper functions for safe JSON parsing
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
                string title = GetStringValue("title");
                string dayOfWeek = GetStringValue("dayOfWeek");
                string startTimeStr = GetStringValue("startTime");
                string endTimeStr = GetStringValue("endTime");

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(dayOfWeek) ||
                    string.IsNullOrEmpty(startTimeStr) || string.IsNullOrEmpty(endTimeStr))
                {
                    return Json(new { success = false, error = "Required fields are missing." });
                }

                if (!TimeOnly.TryParse(startTimeStr, out TimeOnly startTime) ||
                    !TimeOnly.TryParse(endTimeStr, out TimeOnly endTime))
                {
                    return Json(new { success = false, error = "Invalid time format." });
                }

                if (startTime >= endTime)
                {
                    return Json(new { success = false, error = "End time must be after start time." });
                }

                // Update schedule
                var baseDate = new DateTime(2000, 1, 1);
                schedule.ScheduleName = title;
                schedule.DayOfWeek = dayOfWeek;
                schedule.StartTime = baseDate.Add(startTime.ToTimeSpan());
                schedule.EndTime = baseDate.Add(endTime.ToTimeSpan());

                // Update optional fields with validation
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

                // Handle center code based on user type
                if (IsCurrentUserCenter())
                {
                    var centerCode = await GetSingleCenterForCenterUser();
                    if (centerCode.HasValue)
                    {
                        schedule.CenterCode = centerCode.Value;
                    }
                }
                else
                {
                    var centerCode = GetIntValue("centerCode");
                    if (centerCode.HasValue && centerCode > 0)
                    {
                        var centerExists = await _context.Centers.AnyAsync(c => c.CenterCode == centerCode && c.RootCode == userRootCode.Value);
                        schedule.CenterCode = centerExists ? centerCode : null;
                    }
                    else
                    {
                        schedule.CenterCode = null;
                    }
                }

                var branchCode = GetIntValue("branchCode");
                if (branchCode.HasValue && branchCode > 0)
                {
                    var branchExists = await _context.Branches.AnyAsync(b => b.BranchCode == branchCode && b.RootCode == userRootCode.Value);
                    schedule.BranchCode = branchExists ? branchCode : null;
                }
                else
                {
                    schedule.BranchCode = null;
                }

                // Handle teacher code based on user type
                if (IsCurrentUserTeacher())
                {
                    var currentTeacherCode = await GetCurrentUserTeacherCode();
                    if (currentTeacherCode.HasValue)
                    {
                        schedule.TeacherCode = currentTeacherCode.Value;
                    }
                }
                else
                {
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

                _context.Schedules.Update(schedule);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing schedule {ScheduleId}", id);
                return Json(new { success = false, error = "An error occurred while updating the schedule." });
            }
        }

        [HttpPost]
        [RequirePageAccess("Schedule", "delete")]
        public async Task<IActionResult> DeleteScheduleEvent(int id)
        {
            try
            {
                var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

                if (!userRootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

                var schedule = await _context.Schedules
                    .Where(s => s.ScheduleCode == id && s.RootCode == userRootCode.Value)
                    .FirstOrDefaultAsync();

                if (schedule == null)
                {
                    return Json(new { success = false, error = "Schedule not found or you don't have permission to delete it." });
                }

                // Check for related data
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

                return Json(new { success = true, message = "Schedule deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedule {ScheduleId}", id);
                return Json(new { success = false, error = "An error occurred while deleting the schedule." });
            }
        }

        // ==================== EXISTING CRUD METHODS ====================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePageAccess("Schedule", "insert")]
        public async Task<IActionResult> Create([Bind("ScheduleCode,ScheduleName,HallCode,ScheduleAmount,EduYearCode,CenterCode,BranchCode,TeacherCode,SubjectCode,DayOfWeek,StartTime,EndTime,YearCode")] Schedule schedule)
        {
            var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                TempData["Error"] = "Unable to determine your root assignment. Please contact administrator.";
                return RedirectToAction("Index");
            }

            schedule.RootCode = userRootCode.Value;

            if (IsCurrentUserCenter())
            {
                var centerCode = await GetSingleCenterForCenterUser();
                if (centerCode.HasValue)
                {
                    schedule.CenterCode = centerCode.Value;
                }
            }

            if (IsCurrentUserTeacher())
            {
                var teacherCode = await GetCurrentUserTeacherCode();
                if (teacherCode.HasValue)
                {
                    schedule.TeacherCode = teacherCode.Value;
                }
            }

            schedule.InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1");
            schedule.InsertTime = DateTime.Now;

            if (ModelState.IsValid)
            {
                _context.Add(schedule);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Calendar));
            }

            await PopulateDropDowns(schedule);
            ViewBag.CurrentUserRootCode = userRootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;
            ViewBag.IsTeacher = IsCurrentUserTeacher();
            ViewBag.BranchName = branchName;

            if (IsCurrentUserCenter())
            {
                ViewBag.SingleCenterCode = await GetSingleCenterForCenterUser();
            }

            return View(schedule);
        }

        [RequirePageAccess("Schedule", "update")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound("Schedule ID is required.");
            }

            var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

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

                await PopulateDropDowns(schedule);
                ViewBag.CurrentUserRootCode = userRootCode;
                ViewBag.UserRootName = rootName;
                ViewBag.IsCenter = isCenter;
                ViewBag.IsTeacher = IsCurrentUserTeacher();
                ViewBag.BranchName = branchName;

                if (IsCurrentUserCenter())
                {
                    ViewBag.SingleCenterCode = await GetSingleCenterForCenterUser();
                }

                return View(schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading schedule {ScheduleId} for edit", id);
                return NotFound("Error loading schedule.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePageAccess("Schedule", "update")]
        public async Task<IActionResult> Edit(int id, [Bind("ScheduleCode,ScheduleName,HallCode,ScheduleAmount,EduYearCode,CenterCode,BranchCode,TeacherCode,SubjectCode,DayOfWeek,StartTime,EndTime,YearCode")] Schedule schedule)
        {
            if (id != schedule.ScheduleCode)
            {
                return NotFound();
            }

            var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

            if (!userRootCode.HasValue)
            {
                return RedirectToAction("Index");
            }

            var existingSchedule = await _context.Schedules
                .Where(s => s.ScheduleCode == id && s.RootCode == userRootCode.Value)
                .FirstOrDefaultAsync();

            if (existingSchedule == null)
            {
                return NotFound("Schedule not found or you don't have permission to edit it.");
            }

            schedule.RootCode = userRootCode.Value;

            if (IsCurrentUserCenter())
            {
                var centerCode = await GetSingleCenterForCenterUser();
                if (centerCode.HasValue)
                {
                    schedule.CenterCode = centerCode.Value;
                }
            }

            if (IsCurrentUserTeacher())
            {
                var teacherCode = await GetCurrentUserTeacherCode();
                if (teacherCode.HasValue)
                {
                    schedule.TeacherCode = teacherCode.Value;
                }
            }

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

            await PopulateDropDowns(schedule);
            ViewBag.CurrentUserRootCode = userRootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;
            ViewBag.IsTeacher = IsCurrentUserTeacher();
            ViewBag.BranchName = branchName;

            if (IsCurrentUserCenter())
            {
                ViewBag.SingleCenterCode = await GetSingleCenterForCenterUser();
            }

            return View(schedule);
        }

        [RequirePageAccess("Schedule", "delete")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

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

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequirePageAccess("Schedule", "delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

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

        private async Task PopulateDropDowns(Schedule schedule = null)
        {
            var (userRootCode, rootName, isCenter, branchName) = await GetUserContext();

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
                    var emptyList = new List<dynamic>();
                    ViewData["EduYearCode"] = new SelectList(emptyList, "Value", "Text");
                    ViewData["HallCode"] = new SelectList(emptyList, "Value", "Text");
                    ViewData["RootCode"] = new SelectList(emptyList, "Value", "Text");
                    ViewData["SubjectCode"] = new SelectList(emptyList, "Value", "Text");
                    ViewData["TeacherCode"] = new SelectList(emptyList, "Value", "Text");
                    ViewData["YearCode"] = new SelectList(emptyList, "Value", "Text");
                    ViewData["CenterCode"] = new SelectList(emptyList, "Value", "Text");
                    ViewData["BranchCode"] = new SelectList(emptyList, "Value", "Text");
                    return;
                }

                // Load common dropdowns
                var eduYears = await _context.EduYears
                    .Where(e => e.RootCode == userRootCode.Value)
                    .OrderBy(e => e.EduName)
                    .ToListAsync();
                ViewData["EduYearCode"] = new SelectList(eduYears, "EduCode", "EduName", schedule?.EduYearCode);

                var currentRoot = await _context.Roots
                    .Where(r => r.RootCode == userRootCode.Value)
                    .ToListAsync();
                ViewData["RootCode"] = new SelectList(currentRoot, "RootCode", "RootName", schedule?.RootCode);

                var years = await _context.Years
                    .OrderBy(y => y.YearSort)
                    .ToListAsync();
                ViewData["YearCode"] = new SelectList(years, "YearCode", "YearName", schedule?.YearCode);

                if (IsCurrentUserCenter())
                {
                    // For center users: empty center dropdown, load teachers
                    ViewData["CenterCode"] = new SelectList(new List<dynamic>(), "Value", "Text");

                    var teachers = await _context.Teachers
                        .Where(t => t.RootCode == userRootCode.Value && t.IsActive == true)
                        .OrderBy(t => t.TeacherName)
                        .ToListAsync();
                    ViewData["TeacherCode"] = new SelectList(teachers, "TeacherCode", "TeacherName", schedule?.TeacherCode);

                    ViewData["SubjectCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }
                else
                {
                    // For teacher users: show centers, pre-filter subjects
                    var centers = await _context.Centers
                        .Where(c => c.RootCode == userRootCode.Value && c.IsActive)
                        .OrderBy(c => c.CenterName)
                        .ToListAsync();
                    ViewData["CenterCode"] = new SelectList(centers, "CenterCode", "CenterName", schedule?.CenterCode);

                    var teacherSubjects = await _context.Teaches
                        .Where(t => t.RootCode == userRootCode.Value && t.IsActive)
                        .Include(t => t.SubjectCodeNavigation)
                        .Select(t => t.SubjectCodeNavigation)
                        .Distinct()
                        .OrderBy(s => s.SubjectName)
                        .ToListAsync();
                    ViewData["SubjectCode"] = new SelectList(teacherSubjects, "SubjectCode", "SubjectName", schedule?.SubjectCode);

                    var teachers = await _context.Teachers
                        .Where(t => t.RootCode == userRootCode.Value && t.IsActive)
                        .OrderBy(t => t.TeacherName)
                        .ToListAsync();
                    ViewData["TeacherCode"] = new SelectList(teachers, "TeacherCode", "TeacherName", schedule?.TeacherCode);
                }

                // Set empty dropdowns for dynamic loading
                ViewData["HallCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["BranchCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PopulateDropDowns");

                // Create empty dropdowns as fallback
                var emptyList = new List<dynamic>();
                ViewBag.DaysOfWeek = new SelectList(emptyList, "Value", "Text");
                ViewData["EduYearCode"] = new SelectList(emptyList, "Value", "Text");
                ViewData["HallCode"] = new SelectList(emptyList, "Value", "Text");
                ViewData["RootCode"] = new SelectList(emptyList, "Value", "Text");
                ViewData["SubjectCode"] = new SelectList(emptyList, "Value", "Text");
                ViewData["TeacherCode"] = new SelectList(emptyList, "Value", "Text");
                ViewData["YearCode"] = new SelectList(emptyList, "Value", "Text");
                ViewData["CenterCode"] = new SelectList(emptyList, "Value", "Text");
                ViewData["BranchCode"] = new SelectList(emptyList, "Value", "Text");

                ViewBag.DropdownError = $"Error loading dropdown data: {ex.Message}";
            }
        }

        private string GetEventColor(bool? isCenter)
        {
            return isCenter == true ? "#00b894" : "#e17055";
        }

        private string GetEventBorderColor(bool? isCenter)
        {
            return isCenter == true ? "#00a085" : "#d63031";
        }
    }

    public class ScheduleEventModel
    {
        public string Title { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int? HallCode { get; set; }
        public int? RootCode { get; set; }
        public int? EduYearCode { get; set; }
        public int? CenterCode { get; set; }
        public int? BranchCode { get; set; }
        public int? TeacherCode { get; set; }
        public int? SubjectCode { get; set; }
        public decimal? ScheduleAmount { get; set; }
        public int? YearCode { get; set; }
    }
}