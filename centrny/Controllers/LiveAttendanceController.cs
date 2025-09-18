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

        // Helper: Get RootCode from current domain
        private int GetUserRootCode()
        {
            var host = HttpContext.Request.Host.Host.ToString().Replace("www.", "");
            var root = _context.Roots.FirstOrDefault(x => x.RootDomain == host);
            return root?.RootCode ?? 0;
        }

        /// <summary>
        /// GET: LiveAttendance - Show all currently running classes, only for user's root
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var rootCode = GetUserRootCode();
                if (rootCode == 0)
                {
                    TempData["Error"] = "Unable to determine your center (root). Please contact support.";
                    return View(new List<RunningClassViewModel>());
                }

                var currentTime = DateTime.Now;
                var today = DateOnly.FromDateTime(currentTime);

                // Only classes for correct root
                var runningClasses = await _context.Classes
                    .Where(c => c.ClassDate.HasValue && c.ClassDate == today &&
                               c.ClassStartTime.HasValue && c.ClassEndTime.HasValue &&
                               c.RootCode == rootCode)
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
                                   l.ScheduleCode == c.ScheduleCode)
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
        /// GET: LiveAttendance/ClassDetail/{classCode} - Show students enrolled in a specific class, only for user's root
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ClassDetail(int classCode)
        {
            try
            {
                var rootCode = GetUserRootCode();
                if (rootCode == 0)
                {
                    TempData["Error"] = "Unable to determine your center (root). Please contact support.";
                    return RedirectToAction("Index");
                }

                // Get class details, filtered by root
                var classEntity = await _context.Classes
                    .Where(c => c.ClassCode == classCode && c.RootCode == rootCode)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.ScheduleCodeNavigation)
                    .FirstOrDefaultAsync();

                if (classEntity == null)
                {
                    TempData["Error"] = "Class not found or not available in your center.";
                    return RedirectToAction("Index");
                }

                // Get all students enrolled in this class by matching schedule ID in Learn table
                var enrolledStudents = await _context.Learns
                    .Where(l => l.IsActive &&
                                l.ScheduleCode == classEntity.ScheduleCode)
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
                    .Select(a => new { a.StudentId, a.AttendDate, a.IsHisSchedule })
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
                            ScheduleCode = learn.ScheduleCode ?? 0,
                            EnrolledSubjectName = learn.SubjectCodeNavigation?.SubjectName ?? "N/A",
                            IsHisSchedule = attendance?.IsHisSchedule ?? true
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
        /// POST: LiveAttendance/ScanQRAttendance - Process QR code scan and mark attendance, only for user's root.
        /// Handles forced attendance (out-of-schedule) logic, setting IsHisSchedule column accordingly.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ScanQRAttendance([FromBody] QRAttendanceRequest request)
        {
            try
            {
                var rootCode = GetUserRootCode();
                if (rootCode == 0)
                {
                    return Json(new { success = false, error = "Unable to determine your center/root. Please contact support." });
                }

                _logger.LogInformation("Processing QR attendance scan for item key: {ItemKey} in class: {ClassCode}",
                    request.ItemKey, request.ClassCode);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    return Json(new { success = false, error = string.Join(", ", errors) });
                }

                // Get class details, filtered by root
                var classEntity = await _context.Classes
                    .Include(c => c.ScheduleCodeNavigation)
                    .FirstOrDefaultAsync(c => c.ClassCode == request.ClassCode && c.RootCode == rootCode);

                if (classEntity == null)
                {
                    return Json(new { success = false, error = "Class not found or not available in your center." });
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

                // Strict schedule check: student must have Learn record with matching schedule
                var enrollmentCheck = await _context.Learns
                    .FirstOrDefaultAsync(l =>
                        l.StudentCode == student.StudentCode &&
                        l.IsActive &&
                        l.ScheduleCode == classEntity.ScheduleCode);

                bool isHisSchedule = enrollmentCheck != null;

                // If not enrolled, handle forced attendance option
                if (!isHisSchedule)
                {
                    if (!request.IsForcedAttendance)
                    {
                        return Json(new
                        {
                            success = false,
                            error = $"Student {student.StudentName} is not enrolled in this class or schedule does not match.",
                            canForce = true // Tell frontend confirmation is possible
                        });
                    }
                    // else: proceed, mark as forced attendance
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

                // Create attendance record, set IsHisSchedule flag
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
                    Type = request.AttendanceType ?? 1,
                    IsHisSchedule = isHisSchedule
                };

                _context.Attends.Add(attendance);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully marked attendance for student {StudentCode} in class {ClassCode}",
                    student.StudentCode, request.ClassCode);

                return Json(new
                {
                    success = true,
                    message = $"Attendance marked successfully for {student.StudentName}" + (isHisSchedule ? "" : " (Out of Schedule)"),
                    studentName = student.StudentName,
                    studentCode = student.StudentCode,
                    attendanceTime = attendance.AttendDate.ToString("HH:mm"),
                    isHisSchedule = attendance.IsHisSchedule
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing QR attendance scan for request {@Request}", request);
                return Json(new { success = false, error = "An error occurred while marking attendance." });
            }
        }

        // Add/Update this method in your controller
        [HttpGet]
        public async Task<IActionResult> GetClassStudents(int classCode, int page = 1, int pageSize = 10)
        {
            try
            {
                var rootCode = GetUserRootCode();
                if (rootCode == 0)
                    return Json(new { success = false, error = "Unable to determine your center/root. Please contact support." });

                // Get class details
                var classEntity = await _context.Classes
                    .FirstOrDefaultAsync(c => c.ClassCode == classCode && c.RootCode == rootCode);

                if (classEntity == null)
                    return Json(new { success = false, error = "Class not found or not available in your center." });

                // Enrolled students
                var enrolledStudents = await _context.Learns
                    .Where(l => l.IsActive && l.ScheduleCode == classEntity.ScheduleCode)
                    .Include(l => l.StudentCodeNavigation)
                        .ThenInclude(s => s.BranchCodeNavigation)
                    .Include(l => l.StudentCodeNavigation)
                        .ThenInclude(s => s.YearCodeNavigation)
                    .ToListAsync();

                // Attendance records
                var attendanceRecords = await _context.Attends
                    .Where(a => a.ClassId == classCode)
                    .Include(a => a.Student)
                        .ThenInclude(s => s.BranchCodeNavigation)
                    .Include(a => a.Student)
                        .ThenInclude(s => s.YearCodeNavigation)
                    .ToListAsync();

                // Build dict: key = StudentId, value = student info
                var studentDict = new Dictionary<int, dynamic>();
                foreach (var l in enrolledStudents)
                {
                    var attendance = attendanceRecords.FirstOrDefault(a => a.StudentId == l.StudentCode);
                    studentDict[l.StudentCode] = new
                    {
                        studentCode = l.StudentCode,
                        studentName = l.StudentCodeNavigation.StudentName,
                        studentPhone = l.StudentCodeNavigation.StudentPhone,
                        studentParentPhone = l.StudentCodeNavigation.StudentFatherPhone,
                        branchName = l.StudentCodeNavigation.BranchCodeNavigation?.BranchName ?? "N/A",
                        yearName = l.StudentCodeNavigation.YearCodeNavigation?.YearName ?? "N/A",
                        isAttended = attendance != null,
                        AttendanceTime = attendance?.AttendDate.ToString("HH:mm"),
                        isHisSchedule = attendance?.IsHisSchedule ?? true
                    };
                }

                // Add attended students who are not enrolled (forced/out-of-schedule)
                foreach (var a in attendanceRecords)
                {
                    if (!studentDict.ContainsKey(a.StudentId) && a.Student != null)
                    {
                        studentDict[a.StudentId] = new
                        {
                            studentCode = a.StudentId,
                            studentName = a.Student.StudentName,
                            studentPhone = a.Student.StudentPhone,
                            studentParentPhone = a.Student.StudentFatherPhone,
                            branchName = a.Student.BranchCodeNavigation?.BranchName ?? "N/A",
                            yearName = a.Student.YearCodeNavigation?.YearName ?? "N/A",
                            isAttended = true,
                            AttendanceTime = a.AttendDate.ToString("HH:mm"),
                            isHisSchedule = a.IsHisSchedule
                        };
                    }
                }

                // Sort and paginate
                var allStudents = studentDict.Values.OrderBy(s => s.studentName).ToList();
                var totalStudents = allStudents.Count;
                var pagedStudents = allStudents.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                // Stats
                var totalEnrolled = enrolledStudents.Count;
                var attendedOnSchedule = allStudents.Count(s => s.isAttended && s.isHisSchedule);
                var attendedOutOfSchedule = allStudents.Count(s => s.isAttended && !s.isHisSchedule);
                var totalAttended = allStudents.Count(s => s.isAttended);
                var totalAbsent = totalEnrolled - attendedOnSchedule;
                var totalPages = (int)Math.Ceiling((double)totalStudents / pageSize);

                return Json(new
                {
                    success = true,
                    students = pagedStudents,
                    page,
                    pageSize,
                    totalPages,
                    totalStudents,
                    totalEnrolled,
                    attendedOnSchedule,
                    attendedOutOfSchedule,
                    totalAttended,
                    totalAbsent
                });
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
        public bool IsHisSchedule { get; set; } = true; // For forced/out-of-schedule attendance badge
    }

    // ==================== REQUEST MODELS ====================

    public class QRAttendanceRequest
    {
        [Required]
        public string ItemKey { get; set; } = string.Empty;

        [Required]
        public int ClassCode { get; set; }

        public int? AttendanceType { get; set; }
        public bool IsForcedAttendance { get; set; } = false; // Frontend confirmation
    }
}