using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using System.Globalization;

namespace centrny.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly CenterContext _context;

        public ScheduleController(CenterContext context)
        {
            _context = context;
        }

        // GET: Schedule
        public async Task<IActionResult> Index()
        {
            var schedules = await _context.Schedules
                .Include(s => s.EduYearCodeNavigation)
                .Include(s => s.HallCodeNavigation)
                .Include(s => s.RootCodeNavigation)
                .Include(s => s.SubjectCodeNavigation)
                .Include(s => s.TeacherCodeNavigation)
                .ToListAsync();

            return View(schedules);
        }

        // GET: Schedule/Calendar
        public async Task<IActionResult> Calendar()
        {
            await PopulateDropDownsSafe();
            return View();
        }

        // GET: Schedule/GetCalendarEvents - FIXED VERSION
        public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
        {
            try
            {
                var schedules = await _context.Schedules
                    .Include(s => s.SubjectCodeNavigation)
                    .Include(s => s.TeacherCodeNavigation)
                    .Include(s => s.HallCodeNavigation)
                    .Include(s => s.RootCodeNavigation)
                    .Where(s => s.StartTime.HasValue && s.EndTime.HasValue && !string.IsNullOrEmpty(s.DayOfWeek))
                    .ToListAsync();

                var events = new List<object>();

                foreach (var schedule in schedules)
                {
                    // Generate recurring events for the date range
                    var recurringEvents = GenerateRecurringEvents(schedule, start, end);
                    events.AddRange(recurringEvents);
                }

                // Add debug logging
                System.Diagnostics.Debug.WriteLine($"Generated {events.Count} events for {schedules.Count} schedules");

                return Json(events);
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error in GetCalendarEvents: {ex.Message}");
                return Json(new { error = ex.Message });
            }
        }

        // GET: Schedule/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var schedule = await _context.Schedules
                .Include(s => s.EduYearCodeNavigation)
                .Include(s => s.HallCodeNavigation)
                .Include(s => s.RootCodeNavigation)
                .Include(s => s.SubjectCodeNavigation)
                .Include(s => s.TeacherCodeNavigation)
                .FirstOrDefaultAsync(m => m.ScheduleCode == id);

            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }

        // GET: Schedule/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropDownsSafe();
            return View();
        }

        // POST: Schedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ScheduleCode,ScheduleName,HallCode,ScheduleAmount,RootCode,EduYearCode,TeacherCode,SubjectCode,DayOfWeek,StartTime,EndTime")] Schedule schedule)
        {
            if (ModelState.IsValid)
            {
                _context.Add(schedule);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Calendar));
            }
            await PopulateDropDownsSafe(schedule);
            return View(schedule);
        }

        // GET: Schedule/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound("Schedule ID is required.");
            }

            try
            {
                var schedule = await _context.Schedules.FindAsync(id);
                if (schedule == null)
                {
                    return NotFound($"Schedule with ID {id} was not found.");
                }

                await PopulateDropDownsSafe(schedule);
                return View(schedule);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error loading schedule {id}: {ex.Message}";
                var errorSchedule = new Schedule
                {
                    ScheduleCode = id.Value,
                    ScheduleName = "Error Loading Schedule"
                };

                await PopulateDropDownsSafe(errorSchedule);
                ViewBag.ErrorMessage = errorMsg;
                return View(errorSchedule);
            }
        }

        // POST: Schedule/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ScheduleCode,ScheduleName,HallCode,ScheduleAmount,RootCode,EduYearCode,TeacherCode,SubjectCode,DayOfWeek,StartTime,EndTime")] Schedule schedule)
        {
            if (id != schedule.ScheduleCode)
            {
                return NotFound();
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
            await PopulateDropDownsSafe(schedule);
            return View(schedule);
        }

        // GET: Schedule/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var schedule = await _context.Schedules
                .Include(s => s.EduYearCodeNavigation)
                .Include(s => s.HallCodeNavigation)
                .Include(s => s.RootCodeNavigation)
                .Include(s => s.SubjectCodeNavigation)
                .Include(s => s.TeacherCodeNavigation)
                .FirstOrDefaultAsync(m => m.ScheduleCode == id);

            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }

        // POST: Schedule/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule != null)
            {
                _context.Schedules.Remove(schedule);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Calendar));
        }

        // API endpoint for creating schedule via calendar - FIXED VERSION
        [HttpPost]
        public async Task<IActionResult> CreateScheduleEvent([FromBody] ScheduleEventModel model)
        {
            try
            {
                // Parse the time strings and create DateTime objects with time only
                var startTimeParts = model.StartTime.TimeOfDay;
                var endTimeParts = model.EndTime.TimeOfDay;

                // Create a base date (we'll use a fixed date since we only care about time)
                var baseDate = new DateTime(2000, 1, 1);

                var schedule = new Schedule
                {
                    ScheduleName = model.Title,
                    DayOfWeek = model.DayOfWeek,
                    StartTime = baseDate.Add(startTimeParts),
                    EndTime = baseDate.Add(endTimeParts),
                    HallCode = model.HallCode,
                    RootCode = model.RootCode,
                    EduYearCode = model.EduYearCode,
                    TeacherCode = model.TeacherCode,
                    SubjectCode = model.SubjectCode,
                    ScheduleAmount = model.ScheduleAmount
                };

                _context.Schedules.Add(schedule);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = schedule.ScheduleCode });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating schedule: {ex.Message}");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Debug method to check schedules
        public async Task<IActionResult> DebugSchedules()
        {
            try
            {
                var schedules = await _context.Schedules
                    .Include(s => s.HallCodeNavigation)
                    .Include(s => s.TeacherCodeNavigation)
                    .Include(s => s.SubjectCodeNavigation)
                    .ToListAsync();

                var debugInfo = schedules.Select(s => new {
                    s.ScheduleCode,
                    s.ScheduleName,
                    s.DayOfWeek,
                    StartTime = s.StartTime?.ToString("HH:mm"),
                    EndTime = s.EndTime?.ToString("HH:mm"),
                    Hall = s.HallCodeNavigation?.HallName,
                    Teacher = s.TeacherCodeNavigation?.TeacherName,
                    Subject = s.SubjectCodeNavigation?.SubjectName,
                    HasValidTimes = s.StartTime.HasValue && s.EndTime.HasValue && !string.IsNullOrEmpty(s.DayOfWeek)
                }).ToList();

                return Json(new
                {
                    totalSchedules = schedules.Count,
                    validSchedules = debugInfo.Count(s => s.HasValidTimes),
                    schedules = debugInfo
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private bool ScheduleExists(int id)
        {
            return _context.Schedules.Any(e => e.ScheduleCode == id);
        }

        private async Task PopulateDropDownsSafe(Schedule schedule = null)
        {
            try
            {
                // EduYears
                try
                {
                    var eduYears = await _context.EduYears.ToListAsync();
                    ViewData["EduYearCode"] = new SelectList(eduYears, "EduCode", "EduName", schedule?.EduYearCode);
                }
                catch
                {
                    ViewData["EduYearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }

                // Halls
                try
                {
                    var halls = await _context.Halls.ToListAsync();
                    ViewData["HallCode"] = new SelectList(halls, "HallCode", "HallName", schedule?.HallCode);
                }
                catch
                {
                    ViewData["HallCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }

                // Roots
                try
                {
                    var roots = await _context.Roots.ToListAsync();
                    ViewData["RootCode"] = new SelectList(roots, "RootCode", "RootName", schedule?.RootCode);
                }
                catch
                {
                    ViewData["RootCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }

                // Subjects
                try
                {
                    var subjects = await _context.Subjects.ToListAsync();
                    ViewData["SubjectCode"] = new SelectList(subjects, "SubjectCode", "SubjectName", schedule?.SubjectCode);
                }
                catch
                {
                    ViewData["SubjectCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }

                // Teachers
                try
                {
                    var teachers = await _context.Teachers.ToListAsync();
                    ViewData["TeacherCode"] = new SelectList(teachers, "TeacherCode", "TeacherName", schedule?.TeacherCode);
                }
                catch
                {
                    ViewData["TeacherCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                }
            }
            catch (Exception ex)
            {
                // Ultimate fallback - empty dropdowns
                ViewData["EduYearCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["HallCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["RootCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["SubjectCode"] = new SelectList(new List<dynamic>(), "Value", "Text");
                ViewData["TeacherCode"] = new SelectList(new List<dynamic>(), "Value", "Text");

                ViewBag.DropdownError = $"Error loading dropdown data: {ex.Message}";
            }
        }

        private List<object> GenerateRecurringEvents(Schedule schedule, DateTime unusedStart, DateTime unusedEnd)
        {
            var events = new List<object>();

            // Validation
            if (!schedule.StartTime.HasValue || !schedule.EndTime.HasValue || string.IsNullOrEmpty(schedule.DayOfWeek))
                return events;

            // Parse the DayOfWeek from string
            if (!Enum.TryParse<DayOfWeek>(schedule.DayOfWeek, out var dayOfWeek))
                return events;

            // Time of day
            var startTime = schedule.StartTime.Value.TimeOfDay;
            var endTime = schedule.EndTime.Value.TimeOfDay;

            // Start from today (or fixed point like start of academic year)
            var current = DateTime.Today;

            // Go to the first occurrence of the scheduled weekday
            while (current.DayOfWeek != dayOfWeek)
            {
                current = current.AddDays(1);
            }

            // Generate events for the next 365 days
            var endDate = current.AddDays(364); // 52 weeks
            while (current <= endDate)
            {
                var eventStart = current.Add(startTime);
                var eventEnd = current.Add(endTime);

                events.Add(new
                {
                    id = $"schedule_{schedule.ScheduleCode}_{current:yyyyMMdd}",
                    title = schedule.ScheduleName ?? "Untitled Schedule",
                    start = eventStart.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                    end = eventEnd.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                    backgroundColor = GetEventColor(schedule.RootCodeNavigation?.IsCenter),
                    borderColor = GetEventColor(schedule.RootCodeNavigation?.IsCenter),
                    textColor = "#ffffff",
                    extendedProps = new
                    {
                        scheduleCode = schedule.ScheduleCode,
                        hallName = schedule.HallCodeNavigation?.HallName,
                        teacherName = schedule.TeacherCodeNavigation?.TeacherName,
                        subjectName = schedule.SubjectCodeNavigation?.SubjectName,
                        amount = schedule.ScheduleAmount,
                        isCenter = schedule.RootCodeNavigation?.IsCenter ?? false
                    }
                });

                current = current.AddDays(7); // Repeat weekly
            }

            return events;
        }

        private string GetEventColor(bool? isCenter)
        {
            return isCenter == true ? "#3498db" : "#e74c3c"; // Blue for center, red for teacher
        }
    }

    public class ScheduleEventModel
    {
        public string Title { get; set; }
        public string DayOfWeek { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int? HallCode { get; set; }
        public int? RootCode { get; set; }
        public int? EduYearCode { get; set; }
        public int? TeacherCode { get; set; }
        public int? SubjectCode { get; set; }
        public decimal? ScheduleAmount { get; set; }
    }
}