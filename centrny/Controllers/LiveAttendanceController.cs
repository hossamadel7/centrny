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

namespace centrny.Controllers
{
    [RequirePageAccess("LiveAttendance")]
    public class LiveAttendanceController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<LiveAttendanceController> _logger;

        public LiveAttendanceController(CenterContext context, ILogger<LiveAttendanceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET: LiveAttendance - Show all currently running classes
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var currentTime = DateTime.Now;
                var today = DateOnly.FromDateTime(currentTime);

                // Get all classes running today within the time window (1 hour before start until class ends)
                var runningClasses = await _context.Classes
                    .Where(c => c.ClassDate.HasValue && c.ClassDate == today &&
                               c.ClassStartTime.HasValue && c.ClassEndTime.HasValue)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.ScheduleCodeNavigation)
                    .ToListAsync();

                // Filter classes that are currently available (1 hour before start until class ends)
                var availableClasses = runningClasses
                    .Where(c => c.ClassStartTime.HasValue && c.ClassEndTime.HasValue)
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

                var classViewModels = new List<RunningClassViewModel>();

                foreach (var c in availableClasses)
                {
                    // Get enrolled students count for this class
                    var enrolledCount = await _context.Learns
                        .Where(l => l.IsActive &&
                                   l.SubjectCode == c.SubjectCode &&
                                   l.TeacherCode == c.TeacherCode &&
                                   (l.ScheduleCode == c.ScheduleCode || l.YearCode == c.YearCode))
                        .CountAsync();

                    // Get attended students count
                    var attendedCount = await _context.Attends
                        .Where(a => a.ClassId == c.ClassCode)
                        .CountAsync();

                    classViewModels.Add(new RunningClassViewModel
                    {
                        ClassCode = c.ClassCode,
                        ClassName = c.ClassName,
                        SubjectName = c.SubjectCodeNavigation?.SubjectName ?? "N/A",
                        TeacherName = c.TeacherCodeNavigation?.TeacherName ?? "N/A",
                        HallName = c.HallCodeNavigation?.HallName ?? "N/A",
                        BranchName = c.BranchCodeNavigation?.BranchName ?? "N/A",
                        StartTime = c.ClassStartTime?.ToString("HH:mm") ?? "N/A",
                        EndTime = c.ClassEndTime?.ToString("HH:mm") ?? "N/A",
                        ClassDate = c.ClassDate?.ToString("yyyy-MM-dd") ?? "N/A",
                        EnrolledStudentsCount = enrolledCount,
                        AttendedStudentsCount = attendedCount,
                        ScheduleCode = c.ScheduleCode
                    });
                }

                _logger.LogInformation("Retrieved {Count} running classes", classViewModels.Count);
                return View(classViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving running classes");
                TempData["Error"] = "An error occurred while loading running classes.";
                return View(new List<RunningClassViewModel>());
            }
        }

        /// <summary>
        /// GET: LiveAttendance/ClassDetail/{classCode} - Show students enrolled in a specific class
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ClassDetail(int classCode)
        {
            try
            {
                // Get class details
                var classEntity = await _context.Classes
                    .Where(c => c.ClassCode == classCode)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.ScheduleCodeNavigation)
                    .FirstOrDefaultAsync();

                if (classEntity == null)
                {
                    TempData["Error"] = "Class not found.";
                    return RedirectToAction("Index");
                }

                // Get all students enrolled in this class by matching schedule ID in Learn table
                var enrolledStudents = await _context.Learns
                    .Where(l => l.IsActive &&
                               l.SubjectCode == classEntity.SubjectCode &&
                               l.TeacherCode == classEntity.TeacherCode &&
                               (l.ScheduleCode == classEntity.ScheduleCode ||
                                (classEntity.YearCode.HasValue && l.YearCode == classEntity.YearCode)))
                    .Include(l => l.StudentCodeNavigation)
                        .ThenInclude(s => s.BranchCodeNavigation)
                    .Include(l => l.StudentCodeNavigation)
                        .ThenInclude(s => s.YearCodeNavigation)
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .Include(l => l.ScheduleCodeNavigation)
                    .ToListAsync();

                // Get attendance records for this class
                var attendanceRecords = await _context.Attends
                    .Where(a => a.ClassId == classCode)
                    .Select(a => new { a.StudentId, a.AttendDate })
                    .ToListAsync();

                var studentViewModels = new List<ClassStudentViewModel>();

                foreach (var learn in enrolledStudents)
                {
                    if (learn.StudentCodeNavigation != null)
                    {
                        var attendance = attendanceRecords.FirstOrDefault(a => a.StudentId == learn.StudentCode);

                        studentViewModels.Add(new ClassStudentViewModel
                        {
                            StudentCode = learn.StudentCode,
                            StudentName = learn.StudentCodeNavigation.StudentName,
                            StudentPhone = learn.StudentCodeNavigation.StudentPhone,
                            StudentParentPhone = learn.StudentCodeNavigation.StudentFatherPhone,
                            BranchName = learn.StudentCodeNavigation.BranchCodeNavigation?.BranchName ?? "N/A",
                            YearName = learn.StudentCodeNavigation.YearCodeNavigation?.YearName ?? "N/A",
                            IsAttended = attendance != null,
                            AttendanceTime = attendance?.AttendDate.ToString("HH:mm"),
                            ScheduleCode = learn.ScheduleCode??0,
                            EnrolledSubjectName = learn.SubjectCodeNavigation?.SubjectName ?? "N/A"
                        });
                    }
                }

                var viewModel = new ClassDetailViewModel
                {
                    ClassCode = classEntity.ClassCode,
                    ClassName = classEntity.ClassName,
                    SubjectName = classEntity.SubjectCodeNavigation?.SubjectName ?? "N/A",
                    TeacherName = classEntity.TeacherCodeNavigation?.TeacherName ?? "N/A",
                    HallName = classEntity.HallCodeNavigation?.HallName ?? "N/A",
                    BranchName = classEntity.BranchCodeNavigation?.BranchName ?? "N/A",
                    StartTime = classEntity.ClassStartTime?.ToString("HH:mm") ?? "N/A",
                    EndTime = classEntity.ClassEndTime?.ToString("HH:mm") ?? "N/A",
                    ClassDate = classEntity.ClassDate?.ToString("yyyy-MM-dd") ?? "N/A",
                    ScheduleCode = classEntity.ScheduleCode,
                    Students = studentViewModels.OrderBy(s => s.StudentName).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving class detail for class {ClassCode}", classCode);
                TempData["Error"] = "An error occurred while loading class details.";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// POST: LiveAttendance/ScanQRAttendance - Process QR code scan and mark attendance
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ScanQRAttendance([FromBody] QRAttendanceRequest request)
        {
            try
            {
                _logger.LogInformation("Processing QR attendance scan for item key: {ItemKey} in class: {ClassCode}",
                    request.ItemKey, request.ClassCode);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    return Json(new { success = false, error = string.Join(", ", errors) });
                }

                // Get class details
                var classEntity = await _context.Classes
                    .Include(c => c.ScheduleCodeNavigation)
                    .FirstOrDefaultAsync(c => c.ClassCode == request.ClassCode);

                if (classEntity == null)
                {
                    return Json(new { success = false, error = "Class not found." });
                }

                // Get student from item key
                var item = await _context.Items
                    .Include(i => i.StudentCodeNavigation)
                    .Where(i => i.ItemKey == request.ItemKey && i.IsActive && i.StudentCode.HasValue)
                    .FirstOrDefaultAsync();

                if (item?.StudentCodeNavigation == null)
                {
                    return Json(new { success = false, error = "Student not found or not linked to item key." });
                }

                var student = item.StudentCodeNavigation;

                // Verify student is enrolled in this class by checking Learn table with schedule ID matching
                var enrollmentCheck = await _context.Learns
                    .FirstOrDefaultAsync(l =>
                        l.StudentCode == student.StudentCode &&
                        l.SubjectCode == classEntity.SubjectCode &&
                        l.TeacherCode == classEntity.TeacherCode &&
                        l.IsActive &&
                        (l.ScheduleCode == classEntity.ScheduleCode ||
                         (classEntity.YearCode.HasValue && l.YearCode == classEntity.YearCode)));

                if (enrollmentCheck == null)
                {
                    _logger.LogWarning("Student {StudentCode} is not enrolled in class {ClassCode}. Schedule validation failed.",
                        student.StudentCode, request.ClassCode);
                    return Json(new
                    {
                        success = false,
                        error = $"Student {student.StudentName} is not enrolled in this class or schedule does not match."
                    });
                }

                // Check if already attended
                var existingAttendance = await _context.Attends
                    .FirstOrDefaultAsync(a => a.StudentId == student.StudentCode && a.ClassId == request.ClassCode);

                if (existingAttendance != null)
                {
                    return Json(new
                    {
                        success = false,
                        error = $"Student {student.StudentName} has already been marked as attended."
                    });
                }

                // Create attendance record
                var attendance = new Attend
                {
                    TeacherCode = classEntity.TeacherCode,
                    ScheduleCode = classEntity.ScheduleCode,
                    ClassId = request.ClassCode,
                    HallId = classEntity.HallCode,
                    StudentId = student.StudentCode,
                    AttendDate = DateTime.Now,
                    SessionPrice = classEntity.ClassPrice ?? 0,
                    RootCode = student.RootCode,
                    Type = request.AttendanceType ?? 1 // Default to regular attendance
                };

                _context.Attends.Add(attendance);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully marked attendance for student {StudentCode} in class {ClassCode}",
                    student.StudentCode, request.ClassCode);

                return Json(new
                {
                    success = true,
                    message = $"Attendance marked successfully for {student.StudentName}",
                    studentName = student.StudentName,
                    studentCode = student.StudentCode,
                    attendanceTime = attendance.AttendDate.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing QR attendance scan for request {@Request}", request);
                return Json(new { success = false, error = "An error occurred while marking attendance." });
            }
        }

        /// <summary>
        /// GET: LiveAttendance/GetClassStudents/{classCode} - AJAX endpoint to get updated student list
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetClassStudents(int classCode)
        {
            try
            {
                // Get class details
                var classEntity = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassCode == classCode);

                if (classEntity == null)
                {
                    return Json(new { success = false, error = "Class not found." });
                }

                // Get enrolled students with their attendance status
                var enrolledStudents = await _context.Learns
                    .Where(l => l.IsActive &&
                               l.SubjectCode == classEntity.SubjectCode &&
                               l.TeacherCode == classEntity.TeacherCode &&
                               (l.ScheduleCode == classEntity.ScheduleCode ||
                                (classEntity.YearCode.HasValue && l.YearCode == classEntity.YearCode)))
                    .Include(l => l.StudentCodeNavigation)
                        .ThenInclude(s => s.BranchCodeNavigation)
                    .Include(l => l.StudentCodeNavigation)
                        .ThenInclude(s => s.YearCodeNavigation)
                    .ToListAsync();

                // Get current attendance records
                var attendanceRecords = await _context.Attends
                    .Where(a => a.ClassId == classCode)
                    .Select(a => new { a.StudentId, a.AttendDate })
                    .ToListAsync();

                var students = enrolledStudents
                    .Where(l => l.StudentCodeNavigation != null)
                    .Select(l =>
                    {
                        var attendance = attendanceRecords.FirstOrDefault(a => a.StudentId == l.StudentCode);
                        return new
                        {
                            studentCode = l.StudentCode,
                            studentName = l.StudentCodeNavigation.StudentName,
                            studentPhone = l.StudentCodeNavigation.StudentPhone,
                            isAttended = attendance != null,
                            AttendanceTime = attendance?.AttendDate.ToString("HH:mm"),
                        };
                    })
                    .OrderBy(s => s.studentName)
                    .ToList();

                return Json(new { success = true, students = students });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting class students for class {ClassCode}", classCode);
                return Json(new { success = false, error = "Failed to load students." });
            }
        }

        /// <summary>
        /// GET: LiveAttendance/GetAttendanceTypes - Get available attendance types for dropdown
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAttendanceTypes()
        {
            try
            {
                var attendanceTypes = await _context.Lockups
                    .Where(l => l.PaymentCode > 0 && !string.IsNullOrEmpty(l.PaymentName))
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
    }

    // ==================== VIEW MODELS ====================

    public class RunningClassViewModel
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string HallName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string ClassDate { get; set; } = string.Empty;
        public int EnrolledStudentsCount { get; set; }
        public int AttendedStudentsCount { get; set; }
        public int? ScheduleCode { get; set; }
    }

    public class ClassDetailViewModel
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string HallName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string ClassDate { get; set; } = string.Empty;
        public int? ScheduleCode { get; set; }
        public List<ClassStudentViewModel> Students { get; set; } = new();
    }

    public class ClassStudentViewModel
    {
        public int StudentCode { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentPhone { get; set; } = string.Empty;
        public string StudentParentPhone { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string YearName { get; set; } = string.Empty;
        public bool IsAttended { get; set; }
        public string? AttendanceTime { get; set; }
        public int ScheduleCode { get; set; }
        public string EnrolledSubjectName { get; set; } = string.Empty;
    }

    // ==================== REQUEST MODELS ====================

    public class QRAttendanceRequest
    {
        [Required]
        public string ItemKey { get; set; } = string.Empty;

        [Required]
        public int ClassCode { get; set; }

        public int? AttendanceType { get; set; }
    }
}