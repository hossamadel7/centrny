
using centrny.Attributes;
using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;
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
        // ====== Private Fields and Constructor ======
        private readonly CenterContext _context;
        private readonly ILogger<DailyClassController> _logger;
        private readonly IMemoryCache _memoryCache;

        public DailyClassController(CenterContext context, ILogger<DailyClassController> logger, IMemoryCache memoryCache)
        {
            _context = context;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        #region Session & Security Helpers

        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);

        private (int userCode, int groupCode, int rootCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode") ?? 0,
                GetSessionInt("GroupCode") ?? 0,
                GetSessionInt("RootCode") ?? FetchRootCodeByDomain(),
                GetSessionString("Username")
            );
        }

        // Fallback: fetch and cache root code by domain if not in session
        private int FetchRootCodeByDomain()
        {
            var domain = HttpContext.Request.Host.Host.ToString().Replace("www.", "");
            var rootCode = _context.Roots.FirstOrDefault(x => x.RootDomain == domain)?.RootCode ?? 0;
            HttpContext.Session.SetInt32("RootCode", rootCode);
            return rootCode;
        }


        #endregion

            #region Index / Main Page

        public async Task<IActionResult> Index(DateTime? date = null)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            // Set ViewBag for UI
            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserGroupCode = groupCode;
            ViewBag.CurrentUserName = username;
            ViewBag.PageSubTitle = "Manage your daily classes";

            DateTime selectedDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = selectedDate;
            ViewBag.SelectedDateFormatted = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.DayOfWeek = selectedDate.ToString("dddd");

            return View();
        }
        // ========== CREATE (ADD) CLASS ==========
        [HttpPost]
        [RequirePageAccess("DailyClass", "insert")]
        public async Task<IActionResult> CreateClass([FromBody] DailyClassModel model)
        {
            try
            {
                var (userCode, groupCode, rootCode, username) = GetSessionContext();

                if (model == null)
                    return Json(new { success = false, error = "Invalid data received." });

                // Always enforce RootCode from session/domain
                model.RootCode = rootCode;

                // Basic required (common)
                if (string.IsNullOrWhiteSpace(model.ClassName) ||
                    string.IsNullOrWhiteSpace(model.StartTime) ||
                    string.IsNullOrWhiteSpace(model.EndTime))
                    return Json(new { success = false, error = "Class name, start time, and end time are required." });

                // Parse times
                if (!TimeOnly.TryParse(model.StartTime, out var startTime) ||
                    !TimeOnly.TryParse(model.EndTime, out var endTime))
                    return Json(new { success = false, error = "Invalid time format." });

                if (startTime >= endTime)
                    return Json(new { success = false, error = "End time must be after start time." });

                // Validate resources (ensuring all referenced objects belong to the same root)
                var resourcesOk = await ValidateClassResourcesOptimized(model, rootCode);
                if (!resourcesOk)
                    return Json(new { success = false, error = "One or more selected resources don't belong to your organization." });

                var classDate = model.ClassDate?.Date ?? DateTime.Today;
                var classDateOnly = DateOnly.FromDateTime(classDate);

                var newClass = new Class
                {
                    ClassName = model.ClassName.Trim(),
                    ClassStartTime = startTime,
                    ClassEndTime = endTime,
                    ClassDate = classDateOnly,
                    RootCode = rootCode,
                    TeacherCode = model.TeacherCode!.Value,
                    SubjectCode = model.SubjectCode!.Value,
                    BranchCode = model.BranchCode!.Value,
                    HallCode = model.HallCode,
                    ClassLessonCode = model.LessonCode,// may be null
                    EduYearCode = model.EduYearCode!.Value,
                    YearCode = model.YearCode,
                    NoOfStudents = 0,
                    ClassPrice = model.ClassPrice,
                    ScheduleCode = null,
                    ReservationCode = null,
                    InsertUser = userCode,
                    InsertTime = DateTime.Now
                };

                _context.Classes.Add(newClass);
                await _context.SaveChangesAsync();
                ClearRelevantCaches(rootCode);

                _logger.LogInformation("Created class {ClassCode} (Hall={Hall}) Root {Root} User {User}",
                    newClass.ClassCode, newClass.HallCode, rootCode, username);

                return Json(new { success = true, id = newClass.ClassCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating class. Inner: {Inner}", ex.InnerException?.Message);
                return Json(new { success = false, error = $"Unexpected error: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }


        [HttpPost]
        [RequirePageAccess("DailyClass", "update")]
        public async Task<IActionResult> EditClass(int id, [FromBody] JsonElement json)
        {
            try
            {
                var (userCode, groupCode, rootCode, username) = GetSessionContext();

                // Find class with root filtering (enforce session root)
                var existingClass = await _context.Classes
                    .Where(c => c.ClassCode == id && c.RootCode == rootCode)
                    .FirstOrDefaultAsync();

                if (existingClass == null)
                {
                    _logger.LogWarning("User {Username} attempted to edit class {ClassId} - not found or belongs to different root",
                        username, id);
                    return Json(new { success = false, error = "Class not found or you don't have permission to edit it." });
                }

                // Helper functions for safe JSON parsing
                string GetStringValue(string propertyName)
                {
                    if (json.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                        return prop.GetString()?.Trim();
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

                // Optionally update class date if sent (and your UI allows it)
                string classDateStr = GetStringValue("classDate");
                if (!string.IsNullOrWhiteSpace(classDateStr) && DateOnly.TryParse(classDateStr, out var classDateOnly))
                {
                    existingClass.ClassDate = classDateOnly;
                }

                // Update class properties
                existingClass.ClassName = className;
                existingClass.ClassStartTime = startTime;
                existingClass.ClassEndTime = endTime;

                // Only allow subject, teacher, branch, hall, eduYear, year update if they belong to same root
                var subjectCode = GetIntValue("subjectCode");
                if (subjectCode.HasValue && subjectCode > 0)
                {
                    var subjectOk = await _context.Subjects.AnyAsync(s => s.SubjectCode == subjectCode && s.RootCode == rootCode);
                    if (subjectOk)
                        existingClass.SubjectCode = subjectCode.Value;
                }

                var teacherCode = GetIntValue("teacherCode");
                if (teacherCode.HasValue && teacherCode > 0)
                {
                    var teacherOk = await _context.Teachers.AnyAsync(t => t.TeacherCode == teacherCode && t.RootCode == rootCode);
                    if (teacherOk)
                        existingClass.TeacherCode = teacherCode.Value;
                }

                var branchCode = GetIntValue("branchCode");
                if (branchCode.HasValue && branchCode > 0)
                {
                    var branchOk = await _context.Branches.AnyAsync(b => b.BranchCode == branchCode && b.RootCode == rootCode);
                    if (branchOk)
                        existingClass.BranchCode = branchCode.Value;
                }

                var hallCode = GetIntValue("hallCode");
                if (hallCode.HasValue && hallCode > 0)
                {
                    var hallOk = await _context.Halls.AnyAsync(h => h.HallCode == hallCode && h.RootCode == rootCode);
                    if (hallOk)
                        existingClass.HallCode = hallCode.Value;
                }

                var eduYearCode = GetIntValue("eduYearCode");
                if (eduYearCode.HasValue && eduYearCode > 0)
                {
                    var eduYearOk = await _context.EduYears.AnyAsync(e => e.EduCode == eduYearCode && e.RootCode == rootCode);
                    if (eduYearOk)
                        existingClass.EduYearCode = eduYearCode.Value;
                }

                var yearCode = GetIntValue("yearCode");
                if (yearCode.HasValue && yearCode > 0)
                {
                    existingClass.YearCode = yearCode.Value;
                }

                var classPrice = GetDecimalValue("classPrice");
                if (classPrice.HasValue)
                {
                    existingClass.ClassPrice = classPrice;
                }

                // Optional: update lesson code if present
                var lessonCode = GetIntValue("lessonCode");
                if (lessonCode.HasValue)
                {
                    existingClass.ClassLessonCode = lessonCode.Value;
                }

                // Update audit fields
                existingClass.LastUpdateUser = userCode;
                existingClass.LastUpdateTime = DateTime.Now;

                _context.Classes.Update(existingClass);
                await _context.SaveChangesAsync();

                ClearRelevantCaches(rootCode);

                _logger.LogInformation("Updated class {ClassId} by user {Username}", id, username);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing class {ClassId} for user {Username}", id, GetSessionString("Username"));
                return Json(new { success = false, error = "An error occurred while updating the class: " + ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("DailyClass", "delete")]
        public async Task<IActionResult> DeleteClass(int id)
        {
            try
            {
                var (userCode, groupCode, rootCode, username) = GetSessionContext();

                var existingClass = await _context.Classes
                    .Where(c => c.ClassCode == id && c.RootCode == rootCode)
                    .FirstOrDefaultAsync();

                if (existingClass == null)
                {
                    return Json(new { success = false, error = "Class not found or you don't have permission to delete it." });
                }

                _context.Classes.Remove(existingClass);
                await _context.SaveChangesAsync();

                ClearRelevantCaches(rootCode);

                _logger.LogInformation("Deleted class {ClassId} by user {Username}", id, username);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class {ClassId} for user {Username}", id, GetSessionString("Username"));
                return Json(new { success = false, error = "An error occurred while deleting the class: " + ex.Message });
            }
        }

        #endregion

        #region Data Retrieval - Read-only Endpoints

        // Example: Get user's branch (now using session context)
        [HttpGet]
        public async Task<IActionResult> GetUserBranch()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            var branch = await _context.Branches
                .AsNoTracking()
                .Where(b => b.RootCode == rootCode && b.BranchCode == groupCode) // Assuming GroupCode is BranchCode for user
                .Select(b => new { value = b.BranchCode, text = b.BranchName })
                .FirstOrDefaultAsync();

            if (branch == null)
                return Json(new { error = "Branch not found." });

            return Json(branch);
        }


        [HttpGet]
        public async Task<IActionResult> GetEduYearsForRoot()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var eduYears = await _context.EduYears
                .AsNoTracking()
                .Where(e => e.RootCode == rootCode && e.IsActive)
                .Select(e => new { value = e.EduCode, text = e.EduName })
                .ToListAsync();

            return Json(new { success = true, eduYears });
        }
        [HttpGet]
  
        public async Task<IActionResult> GetCentersForUserRoot()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var centers = await _context.Centers
                .AsNoTracking()
                .Where(c => c.RootCode == rootCode && c.IsActive)
                .Select(c => new { value = c.CenterCode, text = c.CenterName })
                .OrderBy(c => c.text)
                .ToListAsync();

            return Json(new { success = true, centers });
        }

        // ========== Example Read-only Helper (unchanged structure, now uses session for rootCode) ==========
        [HttpGet]
        public async Task<IActionResult> GetYearsForEduYear()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            var years = await _context.Years
                        .Where(y => y.RootCode == rootCode)
                        .Select(y => new { value = y.YearCode, text = y.YearName })
                        .ToListAsync();
            return Json(new { years });
        }
        [HttpGet]

        public async Task<IActionResult> GetDailyClasses(string date)
        {
            try
            {
                var (userCode, groupCode, rootCode, username) = GetSessionContext();

                if (rootCode == 0)
                {
                    _logger.LogWarning("User {Username} has no RootCode - returning empty daily classes", username);
                    return Json(new List<object>());
                }

                DateOnly dateOnly;
                bool parsed = DateOnly.TryParse(date, out dateOnly);
                if (!parsed)
                {
                    dateOnly = DateOnly.FromDateTime(DateTime.Today);
                }

                var allClasses = await _context.Classes
                    .AsNoTracking()
                    .Where(c => c.RootCode == rootCode && c.ClassDate == dateOnly)
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
                        c.ClassPrice,
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
                        teacherName = cls.TeacherName,
                        teacherCode = cls.TeacherCode,
                        hallName = cls.HallName,
                        hallCode = cls.HallCode,
                        centerName = cls.CenterName,
                        branchName = cls.BranchName,
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
                        classPrice = cls.ClassPrice?.ToString("F2"),
                        date = dateOnly.ToString("yyyy-MM-dd"),
                        classDate = cls.ClassDate?.ToString("yyyy-MM-dd")
                    };
                }).ToList();

                _logger.LogInformation("Loaded {Count} classes for date {Date} and user {Username} (Root: {RootCode})",
                    classViewModels.Count, dateOnly.ToString("yyyy-MM-dd"), username, rootCode);

                return Json(classViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily classes for date {Date} and user {Username}",
                    date ?? "", GetSessionString("Username"));
                return Json(new { error = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetLessonsForDropdown(int teacherCode, int yearCode, int subjectCode)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var lessons = await _context.Lessons
                .Where(l => l.TeacherCode == teacherCode
                         && l.YearCode == yearCode
                         && l.SubjectCode == subjectCode
                         && l.RootCode == rootCode)
                .Select(l => new {
                    l.LessonCode,
                    l.LessonName,
                    l.ChapterCode // null for chapters
                })
                .ToListAsync();

            var chapters = lessons.Where(l => l.ChapterCode == null).ToList();
            var lessonsByChapter = lessons.Where(l => l.ChapterCode != null)
                                          .GroupBy(l => l.ChapterCode)
                                          .ToDictionary(
                                              g => g.Key,
                                              g => g.Select(x => new LessonDropdownDto
                                              {
                                                  lessonCode = x.LessonCode,
                                                  lessonName = x.LessonName
                                              }).ToList()
                                          );

            var result = chapters.Select(ch => new {
                chapterCode = ch.LessonCode,
                chapterName = ch.LessonName,
                lessons = lessonsByChapter.ContainsKey(ch.LessonCode)
                    ? lessonsByChapter[ch.LessonCode]
                    : new List<LessonDropdownDto>()
            }).ToList();

            return Json(result);
        }


        [HttpGet]
        public async Task<IActionResult> GetWeekReservations()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var (weekStart, weekEnd) = GetCurrentWeekRange();
            var weekStartOnly = DateOnly.FromDateTime(weekStart);
            var weekEndOnly = DateOnly.FromDateTime(weekEnd);

            var reservations = await _context.Reservations
                .AsNoTracking()
                .Join(_context.Branches, r => r.BranchCode, b => b.BranchCode, (r, b) => new { r, b })
                .Where(x => x.b.RootCode == rootCode
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
                    BranchName = x.b.BranchName,
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
        [HttpGet]
        public async Task<IActionResult> GetDayReservations(string date)
        {
            try
            {
                var (userCode, groupCode, rootCode, username) = GetSessionContext();
                if (rootCode == 0)
                    return Json(new List<object>());

                DateOnly dateOnly;
                if (!DateOnly.TryParse(date, out dateOnly))
                {
                    dateOnly = DateOnly.FromDateTime(DateTime.Today);
                }

                var dbList = await _context.Reservations
                    .AsNoTracking()
                    .Join(_context.Branches, r => r.BranchCode, b => b.BranchCode, (r, b) => new { r, b })
                    .Where(x => x.b.RootCode == rootCode && x.r.RTime == dateOnly)
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
        public async Task<IActionResult> GetCurrentTeacherCode()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            // Replace this logic with how your users are linked to teachers
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.RootCode == rootCode);
            int? teacherCode = teacher?.TeacherCode;

            return Json(new { teacherCode });
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByCenter(int centerCode)
        {
            try
            {
                var (userCode, groupCode, rootCode, username) = GetSessionContext();

                if (rootCode == 0)
                {
                    return Json(new List<object>());
                }

                var cacheKey = $"branches_center_{centerCode}_{rootCode}";

                if (_memoryCache.TryGetValue(cacheKey, out var cachedBranches))
                {
                    return Json(cachedBranches);
                }

                var branches = await _context.Branches
                    .AsNoTracking()
                    .Where(b => b.CenterCode == centerCode && b.RootCode == rootCode && b.IsActive)
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
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var teachers = await _context.Teaches
                .Where(t => t.BranchCode == branchCode && t.RootCode == rootCode)
                .Select(t => new
                {
                    value = t.TeacherCode,
                    text = t.TeacherCodeNavigation.TeacherName,
                    isStaff = t.TeacherCodeNavigation.IsStaff
                })
                .Distinct()
                .ToListAsync();

            return Json(new { teachers });
        }



        [HttpGet]
        public async Task<IActionResult> GetHallsForBranch(int branchCode)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var halls = await _context.Halls
                .Where(h => h.BranchCode == branchCode && h.RootCode == rootCode)
                .Select(h => new { value = h.HallCode, text = h.HallName })
                .ToListAsync();

            return Json(new { halls });
        }

        [HttpGet]
        // ========== More: Example helpers/utility endpoints using session context ==========

        [HttpGet]
        public async Task<IActionResult> GetSubjectsForTeacherRootEduYearBranch(int teacherCode, int eduYearCode, int branchCode)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var subjects = await _context.Teaches
                .Where(t => t.TeacherCode == teacherCode
                         && t.EduYearCode == eduYearCode
                         && t.BranchCode == branchCode
                         && t.RootCode == rootCode)
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
        public async Task<IActionResult> GetYearsForTeach(int branchCode, int teacherCode, int subjectCode)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var years = await _context.Teaches
                .Where(t => t.BranchCode == branchCode && t.TeacherCode == teacherCode && t.SubjectCode == subjectCode && t.RootCode == rootCode)
                .Select(t => new { value = t.YearCode, text = t.YearCodeNavigation.YearName })
                .Distinct()
                .ToListAsync();

            return Json(new { years });
        }


        [HttpGet]
        public async Task<IActionResult> GetSubjectsForTeacherAndBranch(int teacherCode, int branchCode)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var subjects = await _context.Teaches
                .Where(t => t.TeacherCode == teacherCode && t.BranchCode == branchCode && t.RootCode == rootCode)
                .Select(t => new
                {
                    value = t.SubjectCode,
                    text = t.SubjectCodeNavigation.SubjectName
                })
                .Distinct()
                .ToListAsync();

            return Json(subjects);
        }

        #endregion

        #region Special Actions

        [HttpPost]
        [RequirePageAccess("DailyClass", "insert")]
        public async Task<IActionResult> GenerateWeeklyClasses()
        {
            try
            {
                var (userCode, groupCode, rootCode, username) = GetSessionContext();

                if (rootCode == 0)
                {
                    return Json(new { success = false, error = "Unable to determine your root assignment." });
                }

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
                        var hallCode = schedule.HallCode;
                        var branchCode = await GetDefaultBranchForRoot(rootCode);
                        var eduYearCode = schedule.EduYearCode ?? await GetDefaultEduYearForRoot(rootCode);

                        // Create new class with minimal data
                        var newClass = new Class
                        {
                            ClassName = $"{schedule.ScheduleName} - {date:MMM dd}",
                            ClassDate = dateOnly,
                            ScheduleCode = schedule.ScheduleCode,
                            RootCode = rootCode,
                            TeacherCode = teacherCode,
                            SubjectCode = subjectCode,
                            BranchCode = branchCode,
                            HallCode = hallCode,
                            EduYearCode = eduYearCode,
                            YearCode = schedule.YearCode,
                            ClassStartTime = schedule.StartTime.HasValue ? TimeOnly.FromDateTime(schedule.StartTime.Value) : null,
                            ClassEndTime = schedule.EndTime.HasValue ? TimeOnly.FromDateTime(schedule.EndTime.Value) : null,
                            NoOfStudents = 0,
                            ClassPrice = schedule.ScheduleAmount,
                            InsertUser = userCode,
                            InsertTime = DateTime.Now,
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

                ClearRelevantCaches(rootCode);

                _logger.LogInformation("Generated {CreatedCount} schedule-based classes for week {WeekStart} - {WeekEnd} by user {Username} (Root: {RootCode})",
                    result.CreatedCount, result.WeekStart.ToString("yyyy-MM-dd"), result.WeekEnd.ToString("yyyy-MM-dd"),
                    username, rootCode);

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
                var innerError = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error generating weekly classes for user {Username}. Inner: {Inner}", GetSessionString("Username"), innerError);
                return Json(new { success = false, error = $"Error generating classes: {innerError}" });
            }
        }


        [HttpPost]
        public async Task<IActionResult> CheckClassConflicts([FromBody] DailyClassModel model)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();
            if (rootCode == 0)
                return Json(new { success = false, error = "Unable to determine your root assignment." });

            model.RootCode = rootCode;

            // Parse times and date
            if (!TimeOnly.TryParse(model.StartTime, out TimeOnly startTime) ||
                !TimeOnly.TryParse(model.EndTime, out TimeOnly endTime))
            {
                return Json(new { success = false, error = "Invalid time format." });
            }
            if (startTime >= endTime)
            {
                return Json(new { success = false, error = "End time must be after start time." });
            }

            var classDate = model.ClassDate?.Date ?? DateTime.Today;
            var classDateOnly = DateOnly.FromDateTime(classDate);

            int? editingClassCode = model.ClassCode;

            // 1. Hall Conflict (EXCLUDE SELF)
            bool hallConflict = false;
            if (model.HallCode.HasValue)
            {
                var hallClasses = await _context.Classes
                    .AsNoTracking()
                    .Where(c =>
                        c.HallCode == model.HallCode &&
                        c.ClassDate == classDateOnly &&
                        c.RootCode == rootCode &&
                        (c.ClassStartTime < endTime && c.ClassEndTime > startTime) &&
                        (!editingClassCode.HasValue || c.ClassCode != editingClassCode.Value)
                    ).AnyAsync();

                var hallReservations = await _context.Reservations
                    .AsNoTracking()
                    .Where(r =>
                        r.HallCode == model.HallCode &&
                        r.RTime == classDateOnly &&
                        (r.ReservationStartTime < endTime && r.ReservationEndTime > startTime)
                    ).AnyAsync();

                hallConflict = hallClasses || hallReservations;
            }

            // 2. Teacher Conflict (EXCLUDE SELF)
            bool teacherConflict = false;
            if (model.TeacherCode.HasValue)
            {
                teacherConflict = await _context.Classes
                    .AsNoTracking()
                    .Where(c =>
                        c.TeacherCode == model.TeacherCode &&
                        c.ClassDate == classDateOnly &&
                        c.RootCode == rootCode &&
                        (c.ClassStartTime < endTime && c.ClassEndTime > startTime) &&
                        (!editingClassCode.HasValue || c.ClassCode != editingClassCode.Value)
                    ).AnyAsync();
            }

            // 3. Same Year Conflict (EXCLUDE SELF)
            bool sameYearConflict = false;
            string conflictingTeacherName = null;
            if (model.EduYearCode.HasValue && model.BranchCode.HasValue)
            {
                var yearConflicts = await _context.Classes
                    .Include(c => c.TeacherCodeNavigation)
                    .Where(c =>
                        c.EduYearCode == model.EduYearCode &&
                        c.BranchCode == model.BranchCode &&
                        c.ClassDate == classDateOnly &&
                        c.RootCode == rootCode &&
                        (c.ClassStartTime < endTime && c.ClassEndTime > startTime) &&
                        (!editingClassCode.HasValue || c.ClassCode != editingClassCode.Value)
                    ).ToListAsync();

                if (yearConflicts.Any(c => c.TeacherCode != model.TeacherCode))
                {
                    sameYearConflict = true;
                    conflictingTeacherName = string.Join(", ", yearConflicts
                        .Where(c => c.TeacherCode != model.TeacherCode)
                        .Select(c => c.TeacherCodeNavigation.TeacherName).Distinct());
                }
            }

            return Json(new
            {
                success = true,
                hallConflict,
                teacherConflict,
                sameYearConflict,
                conflictingTeacherName
            });
        }

        private async Task<int> GetDefaultTeacherForRoot(int rootCode)
        {
            var teacher = await _context.Teachers
                .Where(t => t.RootCode == rootCode)
                .OrderBy(t => t.TeacherCode)
                .Select(t => t.TeacherCode)
                .FirstOrDefaultAsync();

            if (teacher == 0)
                throw new Exception("No default teacher found for this organization.");
            return teacher;
        }

        #endregion

        #region Utilities & Private Helpers

        private async Task<bool> ValidateClassResourcesOptimized(DailyClassModel model, int rootCode)
        {
            // Use a single query to validate all resources at once (optimized)
            var teacherValid = !model.TeacherCode.HasValue || await _context.Teachers.AnyAsync(t => t.TeacherCode == model.TeacherCode && t.RootCode == rootCode);
            var branchValid = !model.BranchCode.HasValue || await _context.Branches.AnyAsync(b => b.BranchCode == model.BranchCode && b.RootCode == rootCode);
            var hallValid = !model.HallCode.HasValue || await _context.Halls.AnyAsync(h => h.HallCode == model.HallCode && h.RootCode == rootCode);
            var subjectValid = !model.SubjectCode.HasValue || await _context.Subjects.AnyAsync(s => s.SubjectCode == model.SubjectCode && s.RootCode == rootCode);
            var eduYearValid = !model.EduYearCode.HasValue || await _context.EduYears.AnyAsync(e => e.EduCode == model.EduYearCode && e.RootCode == rootCode);
            return teacherValid && branchValid && hallValid && subjectValid && eduYearValid;
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



        private (DateTime weekStart, DateTime weekEnd) GetCurrentWeekRange()
        {
            var today = DateTime.Today;
            var daysFromSaturday = ((int)today.DayOfWeek + 1) % 7;
            var weekStart = today.AddDays(-daysFromSaturday);
            var weekEnd = weekStart.AddDays(6);
            return (weekStart, weekEnd);
        }



        private async Task<int> GetDefaultSubjectForRoot(int rootCode)
        {
            var subject = await _context.Subjects
                .Where(s => s.RootCode == rootCode)
                .OrderBy(s => s.SubjectCode)
                .Select(s => s.SubjectCode)
                .FirstOrDefaultAsync();

            if (subject == 0)
                throw new Exception("No default subject found for this organization.");
            return subject;
        }

        private async Task<int> GetDefaultHallForRoot(int rootCode)
        {
            var hall = await _context.Halls
                .Where(h => h.RootCode == rootCode)
                .OrderBy(h => h.HallCode)
                .Select(h => h.HallCode)
                .FirstOrDefaultAsync();

            if (hall == 0)
                throw new Exception("No default hall found for this organization.");
            return hall;
        }
        private async Task<int> GetDefaultBranchForRoot(int rootCode)
        {
            var branch = await _context.Branches
                .Where(b => b.RootCode == rootCode)
                .OrderBy(b => b.BranchCode)
                .Select(b => b.BranchCode)
                .FirstOrDefaultAsync();

            if (branch == 0)
                throw new Exception("No default branch found for this organization.");
            return branch;
        }

        private async Task<int> GetDefaultEduYearForRoot(int rootCode)
        {
            var eduYear = await _context.EduYears
                .Where(e => e.RootCode == rootCode)
                .OrderBy(e => e.EduCode)
                .Select(e => e.EduCode)
                .FirstOrDefaultAsync();

            if (eduYear == 0)
                throw new Exception("No default education year found for this organization.");
            return eduYear;
        }


        #endregion

        #region ViewModels

        public class DailyClassModel
        {
            public string ClassName { get; set; } = string.Empty;
            public string StartTime { get; set; } = string.Empty;
            public string EndTime { get; set; } = string.Empty;
            public int? TeacherCode { get; set; }
            public int? CenterCode { get; set; }
            public int? SubjectCode { get; set; }
            public int? BranchCode { get; set; }
            public int? HallCode { get; set; }
            public int? EduYearCode { get; set; }
            public int? YearCode { get; set; }
            public decimal? ClassPrice { get; set; }
            public int RootCode { get; set; }
            public DateTime? ClassDate { get; set; }
            public int? ClassCode { get; set; } // <-- ADD THIS LINE
            public int? LessonCode { get; set; } // <-- if you want to support lessons editing
        }

        public class WeeklyClassGenerationResult
        {
            public DateTime WeekStart { get; set; }
            public DateTime WeekEnd { get; set; }
            public int CreatedCount { get; set; }
            public int SkippedCount { get; set; }
        }
        public class LessonDropdownDto
        {
            public int lessonCode { get; set; }
            public string lessonName { get; set; }
        }
        #endregion
    }
}
