using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using centrny.Attributes;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using OfficeOpenXml;
using iTextSharp.text.pdf;
using iTextSharp.text;
using System.Diagnostics;

namespace centrny.Controllers
{
    [Authorize]
    [RequirePageAccess("Reports")]
    public class ReportsController : Controller
    {
        private readonly CenterContext _context;

        public ReportsController(CenterContext context)
        {
            _context = context;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var context = await GetUserContextAsync();
                ViewBag.IsTeacher = context.isTeacher;
                ViewBag.BranchCode = context.branchCode;
                ViewBag.RootCode = context.rootCode;
                return View();
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Helper methods for user context
        private async Task<(User user, Group group, int rootCode, int? branchCode, bool isTeacher)> GetUserContextAsync()
        {
            var username = User.Identity?.Name;
            var user = await _context.Users
                .Include(u => u.GroupCodeNavigation)
                .ThenInclude(g => g.RootCodeNavigation)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            var group = user.GroupCodeNavigation;
            var rootCode = group.RootCode;
            var branchCode = group.BranchCode;

            // Use the correct logic - check if it's a center
            var isCenter = group.RootCodeNavigation?.IsCenter ?? false;
            var isTeacher = !isCenter; // Teachers are non-center users, or use your actual logic

            return (user, group, rootCode, branchCode, isTeacher);
        }
        #region Center Root User Reports

        [HttpGet]
        public async Task<IActionResult> FinancialSummary(DateTime? startDate, DateTime? endDate, int? branchCode, int page = 1, int pageSize = 20)
        {
            try
            {
                var context = await GetUserContextAsync();

                // Build the query for classes with financial data
                var query = _context.Classes
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Where(c => c.RootCode == context.rootCode);

                // For teacher users, filter by their teacher code
                if (context.isTeacher)
                {
                    var teacherId = await GetTeacherIdForUser(context.user.UserCode);
                    if (!teacherId.HasValue)
                        return Forbid("User is not associated with a teacher record");

                    query = query.Where(c => c.TeacherCode == teacherId.Value);
                }
                else
                {
                    // Apply branch filter for non-teacher users
                    if (context.branchCode.HasValue)
                        query = query.Where(c => c.BranchCode == context.branchCode.Value);
                    else if (branchCode.HasValue)
                        query = query.Where(c => c.BranchCode == branchCode.Value);
                }

                // Apply date filters
                if (startDate.HasValue)
                    query = query.Where(c => c.ClassDate >= DateOnly.FromDateTime(startDate.Value));
                if (endDate.HasValue)
                    query = query.Where(c => c.ClassDate <= DateOnly.FromDateTime(endDate.Value));

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Get paginated results
                var classes = await query
                    .OrderByDescending(c => c.ClassDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new FinancialSummaryDto
                    {
                        ClassCode = c.ClassCode,
                        ClassName = c.ClassName ?? "Unknown Class",
                        ClassDate = c.ClassDate,
                        BranchName = c.BranchCodeNavigation.BranchName ?? "Unknown Branch",
                        SubjectName = c.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        TeacherName = c.TeacherCodeNavigation.TeacherName ?? "Unknown Teacher",
                        NoOfStudents = c.NoOfStudents,
                        TotalAmount = c.TotalAmount ?? 0,
                        // For teachers, hide sensitive financial data
                        TeacherAmount = context.isTeacher ? 0 : (c.TeacherAmount ?? 0),
                        CenterAmount = context.isTeacher ? 0 : ((c.TotalAmount ?? 0) - (c.TeacherAmount ?? 0))
                    })
                    .ToListAsync();

                // Calculate totals from all matching records (not just current page)
                var totalsQuery = _context.Classes.Where(c => c.RootCode == context.rootCode);

                if (context.isTeacher)
                {
                    var teacherId = await GetTeacherIdForUser(context.user.UserCode);
                    if (teacherId.HasValue)
                        totalsQuery = totalsQuery.Where(c => c.TeacherCode == teacherId.Value);
                }
                else
                {
                    if (context.branchCode.HasValue)
                        totalsQuery = totalsQuery.Where(c => c.BranchCode == context.branchCode.Value);
                    else if (branchCode.HasValue)
                        totalsQuery = totalsQuery.Where(c => c.BranchCode == branchCode.Value);
                }

                if (startDate.HasValue)
                    totalsQuery = totalsQuery.Where(c => c.ClassDate >= DateOnly.FromDateTime(startDate.Value));
                if (endDate.HasValue)
                    totalsQuery = totalsQuery.Where(c => c.ClassDate <= DateOnly.FromDateTime(endDate.Value));

                var totalRevenue = await totalsQuery.SumAsync(c => c.TotalAmount ?? 0);
                var totalTeacherPayments = context.isTeacher ? 0 : await totalsQuery.SumAsync(c => c.TeacherAmount ?? 0);
                var totalCenterRevenue = context.isTeacher ? 0 : (totalRevenue - totalTeacherPayments);
                var totalStudents = await totalsQuery.SumAsync(c => c.NoOfStudents);

                var summary = new FinancialSummaryReport
                {
                    Classes = classes,
                    TotalRevenue = totalRevenue,
                    TotalTeacherPayments = totalTeacherPayments,
                    TotalCenterRevenue = totalCenterRevenue,
                    TotalStudents = totalStudents,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    StartDate = startDate,
                    EndDate = endDate,
                    BranchCode = branchCode
                };

                // Get filter options
                ViewBag.Branches = await GetBranchesForUser(context);
                ViewBag.Subjects = await _context.Subjects
                    .Where(s => s.RootCode == context.rootCode)
                    .OrderBy(s => s.SubjectName)
                    .ToListAsync();
                ViewBag.Teachers = await _context.Teachers
                    .Where(t => t.RootCode == context.rootCode && t.IsActive)
                    .OrderBy(t => t.TeacherName)
                    .ToListAsync();

                return View(summary);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace, innerException = ex.InnerException?.Message });
            }
        }
        [HttpGet]
        public async Task<IActionResult> StudentEnrollmentReport(int? branchCode, int? subjectCode, int? yearCode, int page = 1, int pageSize = 20)
        {
            try
            {
                var context = await GetUserContextAsync();

                var query = _context.Students
                    .Include(s => s.BranchCodeNavigation)
                    .Include(s => s.YearCodeNavigation)
                    .Include(s => s.Learns)
                        .ThenInclude(l => l.SubjectCodeNavigation)
                    .Where(s => s.RootCode == context.rootCode && s.IsActive);

                // Apply branch filter
                if (context.branchCode.HasValue)
                    query = query.Where(s => s.BranchCode == context.branchCode.Value);
                else if (branchCode.HasValue)
                    query = query.Where(s => s.BranchCode == branchCode.Value);

                // Apply filters
                if (subjectCode.HasValue)
                    query = query.Where(s => s.Learns.Any(l => l.SubjectCode == subjectCode.Value));
                if (yearCode.HasValue)
                    query = query.Where(s => s.YearCode == yearCode.Value);

                var totalCount = await query.CountAsync();
                var students = await query
                    .OrderBy(s => s.StudentName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new StudentEnrollmentDto
                    {
                        StudentCode = s.StudentCode,
                        StudentName = s.StudentName ?? "Unknown",
                        StudentPhone = s.StudentPhone ?? "",
                        StudentParentPhone = s.StudentFatherPhone ?? "",
                        BranchName = s.BranchCodeNavigation.BranchName ?? "Unknown Branch",
                        YearName = s.YearCodeNavigation != null ? s.YearCodeNavigation.YearName : "Not Assigned",
                        // Fixed: Convert DateOnly to DateTime using ToDateTime()
                        SubscriptionTime = s.SubscribtionTime != default(DateOnly) ? s.SubscribtionTime.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                        EnrolledSubjects = s.Learns.Count(),
                        SubjectNames = string.Join(", ", s.Learns.Select(l => l.SubjectCodeNavigation.SubjectName))
                    })
                    .ToListAsync();

                var report = new StudentEnrollmentReport
                {
                    Students = students,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    BranchCode = branchCode,
                    SubjectCode = subjectCode,
                    YearCode = yearCode
                };

                // Get filter options
                ViewBag.Branches = await GetBranchesForUser(context);
                ViewBag.Subjects = await _context.Subjects.Where(s => s.RootCode == context.rootCode).ToListAsync();
                ViewBag.Years = await _context.Years.ToListAsync();

                return View(report);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var context = await GetUserContextAsync();

                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                // Base queries filtered by user context
                var studentsQuery = _context.Students.Where(s => s.RootCode == context.rootCode && s.IsActive);
                var classesQuery = _context.Classes.Where(c => c.RootCode == context.rootCode);
                var attendsQuery = _context.Attends.Where(a => a.RootCode == context.rootCode);

                // Apply branch filter if user belongs to a specific branch
                if (context.branchCode.HasValue)
                {
                    studentsQuery = studentsQuery.Where(s => s.BranchCode == context.branchCode.Value);
                    classesQuery = classesQuery.Where(c => c.BranchCode == context.branchCode.Value);
                    attendsQuery = attendsQuery.Where(a => _context.Classes.Any(cl => cl.ClassCode == a.ClassId && cl.BranchCode == context.branchCode.Value));
                }

                // Apply teacher filter if user is a teacher
                if (context.isTeacher)
                {
                    var teacherId = await GetTeacherIdForUser(context.user.UserCode);
                    if (teacherId.HasValue)
                    {
                        classesQuery = classesQuery.Where(c => c.TeacherCode == teacherId.Value);
                        attendsQuery = attendsQuery.Where(a => a.TeacherCode == teacherId.Value);
                    }
                }

                // Calculate statistics
                var totalStudents = await studentsQuery.CountAsync();

                var monthlyClasses = await classesQuery
                    .Where(c => c.ClassDate.HasValue &&
                               c.ClassDate.Value.Month == currentMonth &&
                               c.ClassDate.Value.Year == currentYear)
                    .CountAsync();

                var monthlyRevenue = await classesQuery
                    .Where(c => c.ClassDate.HasValue &&
                               c.ClassDate.Value.Month == currentMonth &&
                               c.ClassDate.Value.Year == currentYear)
                    .SumAsync(c => c.TotalAmount ?? 0);

                // Calculate attendance rate
                var totalScheduledAttendance = await classesQuery
                    .Where(c => c.ClassDate.HasValue &&
                               c.ClassDate.Value.Month == currentMonth &&
                               c.ClassDate.Value.Year == currentYear)
                    .SumAsync(c => c.NoOfStudents);

                var totalActualAttendance = await attendsQuery
                    .Where(a => a.AttendDate.Month == currentMonth &&
                               a.AttendDate.Year == currentYear)
                    .CountAsync();

                var attendanceRate = totalScheduledAttendance > 0 ?
                    (double)totalActualAttendance / totalScheduledAttendance * 100 : 0;

                var stats = new
                {
                    totalStudents = totalStudents,
                    monthlyClasses = monthlyClasses,
                    monthlyRevenue = monthlyRevenue,
                    attendanceRate = Math.Round(attendanceRate, 1),
                    isTeacher = context.isTeacher
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> TeacherPerformanceReport(int? teacherCode, int? subjectCode, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 20)
        {
            try
            {
                var context = await GetUserContextAsync();

                if (context.isTeacher)
                    return Forbid("Teachers cannot access other teachers' performance reports");

                var query = _context.Classes
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.Attends)
                    .Where(c => c.RootCode == context.rootCode);

                // Apply branch filter
                if (context.branchCode.HasValue)
                    query = query.Where(c => c.BranchCode == context.branchCode.Value);

                // Apply filters
                if (teacherCode.HasValue)
                    query = query.Where(c => c.TeacherCode == teacherCode.Value);
                if (subjectCode.HasValue)
                    query = query.Where(c => c.SubjectCode == subjectCode.Value);
                if (startDate.HasValue)
                    query = query.Where(c => c.ClassDate >= DateOnly.FromDateTime(startDate.Value));
                if (endDate.HasValue)
                    query = query.Where(c => c.ClassDate <= DateOnly.FromDateTime(endDate.Value));

                var totalCount = await query.CountAsync();
                var classes = await query
                    .OrderByDescending(c => c.ClassDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new TeacherPerformanceDto
                    {
                        ClassCode = c.ClassCode,
                        ClassName = c.ClassName ?? "Unknown Class",
                        ClassDate = c.ClassDate,
                        TeacherName = c.TeacherCodeNavigation.TeacherName ?? "Unknown Teacher",
                        SubjectName = c.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        BranchName = c.BranchCodeNavigation.BranchName ?? "Unknown Branch",
                        ScheduledStudents = c.NoOfStudents,
                        AttendedStudents = c.Attends.Count(),
                        AttendanceRate = c.NoOfStudents > 0 ? (decimal)c.Attends.Count() / c.NoOfStudents * 100 : 0,
                        TeacherAmount = c.TeacherAmount ?? 0
                    })
                    .ToListAsync();

                var report = new TeacherPerformanceReport
                {
                    Classes = classes,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TeacherCode = teacherCode,
                    SubjectCode = subjectCode,
                    StartDate = startDate,
                    EndDate = endDate
                };

                // Get filter options
                ViewBag.Teachers = await GetTeachersForUser(context);
                ViewBag.Subjects = await _context.Subjects.Where(s => s.RootCode == context.rootCode).ToListAsync();

                return View(report);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExamPerformanceReport(int? branchCode, int? subjectCode, int? teacherCode, int page = 1, int pageSize = 20)
        {
            try
            {
                var context = await GetUserContextAsync();

                var query = _context.Exams
                    .Include(e => e.BranchCodeNavigation)
                    .Include(e => e.SubjectCodeNavigation)
                    .Include(e => e.TeacherCodeNavigation)
                    .Include(e => e.StudentExams)
                    .Where(e => e.EduYearCodeNavigation.RootCode == context.rootCode);

                // Apply branch filter
                if (context.branchCode.HasValue)
                    query = query.Where(e => e.BranchCode == context.branchCode.Value);
                else if (branchCode.HasValue)
                    query = query.Where(e => e.BranchCode == branchCode.Value);

                // Apply teacher filter for teacher users
                if (context.isTeacher)
                {
                    var teacherId = await GetTeacherIdForUser(context.user.UserCode);
                    if (teacherId.HasValue)
                        query = query.Where(e => e.TeacherCode == teacherId.Value);
                }
                else if (teacherCode.HasValue)
                    query = query.Where(e => e.TeacherCode == teacherCode.Value);

                // Apply other filters
                if (subjectCode.HasValue)
                    query = query.Where(e => e.SubjectCode == subjectCode.Value);

                var totalCount = await query.CountAsync();
                var exams = await query
                    .OrderByDescending(e => e.InserTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new ExamPerformanceDto
                    {
                        ExamCode = e.ExamCode,
                        ExamName = e.ExamName ?? "Unknown Exam",
                        ExamDate = e.InserTime,
                        BranchName = e.BranchCodeNavigation != null ? e.BranchCodeNavigation.BranchName : "All Branches",
                        SubjectName = e.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        TeacherName = e.TeacherCodeNavigation.TeacherName ?? "Unknown Teacher",
                        TotalStudents = e.StudentExams.Count(),
                        AverageScore = e.StudentExams.Any() ? e.StudentExams.Average(se => se.StudentPercentage ?? 0) : 0,
                        PassingStudents = e.StudentExams.Count(se => (se.StudentPercentage ?? 0) >= 60),
                        PassingRate = e.StudentExams.Any() ?
                            (decimal)e.StudentExams.Count(se => (se.StudentPercentage ?? 0) >= 60) / e.StudentExams.Count() * 100 : 0
                    })
                    .ToListAsync();

                var report = new ExamPerformanceReport
                {
                    Exams = exams,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    BranchCode = branchCode,
                    SubjectCode = subjectCode,
                    TeacherCode = teacherCode
                };

                // Get filter options
                ViewBag.Branches = await GetBranchesForUser(context);
                ViewBag.Subjects = await _context.Subjects.Where(s => s.RootCode == context.rootCode).ToListAsync();
                ViewBag.Teachers = await GetTeachersForUser(context);

                return View(report);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        #endregion

        #region Teacher Root User Reports

        [HttpGet]
        public async Task<IActionResult> MyClassesReport(DateTime? startDate, DateTime? endDate, int? subjectCode, int page = 1, int pageSize = 20)
        {
            try
            {
                var context = await GetUserContextAsync();
                var teacherId = await GetTeacherIdForUser(context.user.UserCode);

                if (!teacherId.HasValue)
                    return Forbid("User is not associated with a teacher record");

                var query = _context.Classes
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Include(c => c.Attends)
                    .Where(c => c.TeacherCode == teacherId.Value);

                // Apply filters
                if (startDate.HasValue)
                    query = query.Where(c => c.ClassDate >= DateOnly.FromDateTime(startDate.Value));
                if (endDate.HasValue)
                    query = query.Where(c => c.ClassDate <= DateOnly.FromDateTime(endDate.Value));
                if (subjectCode.HasValue)
                    query = query.Where(c => c.SubjectCode == subjectCode.Value);

                var totalCount = await query.CountAsync();
                var classes = await query
                    .OrderByDescending(c => c.ClassDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new MyClassDto
                    {
                        ClassCode = c.ClassCode,
                        ClassName = c.ClassName ?? "Unknown Class",
                        ClassDate = c.ClassDate,
                        ClassStartTime = c.ClassStartTime,
                        ClassEndTime = c.ClassEndTime,
                        SubjectName = c.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        BranchName = c.BranchCodeNavigation.BranchName ?? "Unknown Branch",
                        HallName = c.HallCodeNavigation.HallName ?? "Unknown Hall",
                        ScheduledStudents = c.NoOfStudents,
                        AttendedStudents = c.Attends.Count(),
                        TeacherAmount = c.TeacherAmount ?? 0
                    })
                    .ToListAsync();

                var report = new MyClassesReport
                {
                    Classes = classes,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    StartDate = startDate,
                    EndDate = endDate,
                    SubjectCode = subjectCode
                };

                // Get subjects for this teacher
                ViewBag.Subjects = await _context.Subjects
                    .Where(s => _context.Classes.Any(c => c.TeacherCode == teacherId.Value && c.SubjectCode == s.SubjectCode))
                    .ToListAsync();

                return View(report);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyStudentPerformance(int? subjectCode, int? yearCode, int page = 1, int pageSize = 20)
        {
            try
            {
                var context = await GetUserContextAsync();
                var teacherId = await GetTeacherIdForUser(context.user.UserCode);

                if (!teacherId.HasValue)
                    return Forbid("User is not associated with a teacher record");

                var query = _context.Learns
                    .Include(l => l.StudentCodeNavigation)
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.StudentCodeNavigation.YearCodeNavigation)
                    .Where(l => l.TeacherCode == teacherId.Value && l.IsActive);

                // Apply filters
                if (subjectCode.HasValue)
                    query = query.Where(l => l.SubjectCode == subjectCode.Value);
                if (yearCode.HasValue)
                    query = query.Where(l => l.YearCode == yearCode.Value);

                var totalCount = await query.CountAsync();
                var students = await query
                    .OrderBy(l => l.StudentCodeNavigation.StudentName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new StudentPerformanceDto
                    {
                        StudentCode = l.StudentCode,
                        StudentName = l.StudentCodeNavigation.StudentName ?? "Unknown Student",
                        StudentPhone = l.StudentCodeNavigation.StudentPhone ?? "",
                        StudentParentPhone = l.StudentCodeNavigation.StudentFatherPhone ?? "",
                        SubjectName = l.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        YearName = l.StudentCodeNavigation.YearCodeNavigation != null ?
                            l.StudentCodeNavigation.YearCodeNavigation.YearName : "Not Assigned",
                        TotalClasses = _context.Classes.Count(c => c.TeacherCode == teacherId.Value &&
                            c.SubjectCode == l.SubjectCode),
                        AttendedClasses = _context.Attends.Count(a => a.StudentId == l.StudentCode &&
                            a.TeacherCode == teacherId.Value),
                        AttendancePercentage = 0 // Will be calculated
                    })
                    .ToListAsync();

                // Calculate attendance percentage
                foreach (var student in students)
                {
                    if (student.TotalClasses > 0)
                        student.AttendancePercentage = (decimal)student.AttendedClasses / student.TotalClasses * 100;
                }

                var report = new StudentPerformanceReport
                {
                    Students = students,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    SubjectCode = subjectCode,
                    YearCode = yearCode
                };

                // Get filter options for this teacher
                ViewBag.Subjects = await _context.Subjects
                    .Where(s => _context.Learns.Any(l => l.TeacherCode == teacherId.Value && l.SubjectCode == s.SubjectCode))
                    .ToListAsync();
                ViewBag.Years = await _context.Years.ToListAsync();

                return View(report);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyEarningsReport(DateTime? startDate, DateTime? endDate, int? subjectCode, int page = 1, int pageSize = 20)
        {
            try
            {
                var context = await GetUserContextAsync();
                var teacherId = await GetTeacherIdForUser(context.user.UserCode);

                if (!teacherId.HasValue)
                    return Forbid();
                // OR if you want to return a message in JSON:
                return Json(new { error = "User is not associated with a teacher record" });

                var query = _context.Classes
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Where(c => c.TeacherCode == teacherId.Value);

                // Apply filters
                if (startDate.HasValue)
                    query = query.Where(c => c.ClassDate >= DateOnly.FromDateTime(startDate.Value));
                if (endDate.HasValue)
                    query = query.Where(c => c.ClassDate <= DateOnly.FromDateTime(endDate.Value));
                if (subjectCode.HasValue)
                    query = query.Where(c => c.SubjectCode == subjectCode.Value);

                var totalCount = await query.CountAsync();
                var earnings = await query
                    .OrderByDescending(c => c.ClassDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new TeacherEarningsDto
                    {
                        ClassCode = c.ClassCode,
                        ClassName = c.ClassName ?? "Unknown Class",
                        ClassDate = c.ClassDate,
                        SubjectName = c.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        BranchName = c.BranchCodeNavigation.BranchName ?? "Unknown Branch",
                        StudentCount = c.NoOfStudents,
                        TeacherAmount = c.TeacherAmount ?? 0,
                        TeacherSubAmount = c.TeacherSubAmount ?? 0,
                        TotalEarning = (c.TeacherAmount ?? 0) + (c.TeacherSubAmount ?? 0)
                    })
                    .ToListAsync();

                var report = new TeacherEarningsReport
                {
                    Earnings = earnings,
                    TotalEarnings = earnings.Sum(e => e.TotalEarning),
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    StartDate = startDate,
                    EndDate = endDate,
                    SubjectCode = subjectCode
                };

                // Get subjects for this teacher
                ViewBag.Subjects = await _context.Subjects
                    .Where(s => _context.Classes.Any(c => c.TeacherCode == teacherId.Value && c.SubjectCode == s.SubjectCode))
                    .ToListAsync();

                return View(report);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        #endregion

        #region Class Attendance Report

        [HttpGet]
        public async Task<IActionResult> ClassAttendanceReport()
        {
            try
            {
                var context = await GetUserContextAsync();

                // Get filter options for the initial view
                ViewBag.Teachers = await GetTeachersForUser(context);
                ViewBag.Subjects = await _context.Subjects.Where(s => s.RootCode == context.rootCode).ToListAsync();
                ViewBag.IsTeacher = context.isTeacher;
                ViewBag.BranchCode = context.branchCode;
                ViewBag.RootCode = context.rootCode;

                return View();
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetClassAttendanceData(int? teacherCode, int? subjectCode, DateTime? classDate, int page = 1, int pageSize = 10)
        {
            try
            {
                var context = await GetUserContextAsync();

                

                var query = _context.Classes
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Where(c => c.RootCode == context.rootCode);

         

               
                // Apply branch filter
                if (context.branchCode.HasValue)
                    query = query.Where(c => c.BranchCode == context.branchCode.Value);

                // Apply teacher filter for teacher users
                if (context.isTeacher)
                {
                    var teacherId = await GetTeacherIdForUser(context.user.UserCode);
                    if (teacherId.HasValue)
                        query = query.Where(c => c.TeacherCode == teacherId.Value);
                }
                else if (teacherCode.HasValue)
                    query = query.Where(c => c.TeacherCode == teacherCode.Value);

                // Apply other filters
                if (subjectCode.HasValue)
                    query = query.Where(c => c.SubjectCode == subjectCode.Value);
                if (classDate.HasValue)
                    query = query.Where(c => c.ClassDate == DateOnly.FromDateTime(classDate.Value));

                var totalCount = await query.CountAsync();
                var classes = await query
                    .OrderByDescending(c => c.ClassDate)
                    .ThenByDescending(c => c.ClassStartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var classAttendanceReports = new List<object>();

                foreach (var cls in classes)
                {
                    // Get all enrolled students for this specific class
                    var enrolledStudents = await _context.Learns
                        .Include(l => l.StudentCodeNavigation)
                        .Where(l => l.TeacherCode == cls.TeacherCode &&
                                  l.SubjectCode == cls.SubjectCode &&
                                  l.ScheduleCode == cls.ScheduleCode &&
                                  l.IsActive)
                        .Select(l => new
                        {
                            StudentCode = l.StudentCode,
                            StudentName = l.StudentCodeNavigation.StudentName ?? "Unknown",
                            StudentPhone = l.StudentCodeNavigation.StudentPhone ?? "",
                            StudentParentPhone = l.StudentCodeNavigation.StudentFatherPhone ?? "",
                            IsEnrolled = true,
                            IsPresent = false // Will be updated below
                        })
                        .OrderBy(s => s.StudentName)
                        .ToListAsync();

                    // Get actual attendance for this class
                    var attendedStudentIds = await _context.Attends
                        .Where(a =>a.ClassId == cls.ClassCode)
                        .Select(a => a.StudentId)
                        .ToListAsync();

                    // Update presence status for each student
                    var studentsWithAttendance = enrolledStudents.Select(s => new
                    {
                        s.StudentCode,
                        s.StudentName,
                        s.StudentPhone,
                        s.StudentParentPhone,
                        s.IsEnrolled,
                        IsPresent = attendedStudentIds.Contains(s.StudentCode)
                    }).ToList();

                    // Create attendance detail for this class
                    var attendanceDetail = new
                    {
                        ClassCode = cls.ClassCode,
                        ClassName = cls.ClassName ?? "Unknown Class",
                        ClassDate = cls.ClassDate?.ToString("yyyy-MM-dd"),
                        ClassStartTime = cls.ClassStartTime?.ToString("HH:mm"),
                        ClassEndTime = cls.ClassEndTime?.ToString("HH:mm"),
                        TeacherName = cls.TeacherCodeNavigation?.TeacherName ?? "Unknown Teacher",
                        SubjectName = cls.SubjectCodeNavigation?.SubjectName ?? "Unknown Subject",
                        BranchName = cls.BranchCodeNavigation?.BranchName ?? "Unknown Branch",
                        HallName = cls.HallCodeNavigation?.HallName ?? "Unknown Hall",
                        EnrolledCount = studentsWithAttendance.Count,
                        PresentCount = studentsWithAttendance.Count(s => s.IsPresent),
                        AbsentCount = studentsWithAttendance.Count(s => !s.IsPresent),
                        AttendanceRate = studentsWithAttendance.Count > 0 ?
                            Math.Round((decimal)studentsWithAttendance.Count(s => s.IsPresent) / studentsWithAttendance.Count * 100, 1) : 0,
                        Students = studentsWithAttendance
                    };

                    classAttendanceReports.Add(attendanceDetail);
                }

                var response = new
                {
                    AttendanceDetails = classAttendanceReports,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    TeacherCode = teacherCode,
                    SubjectCode = subjectCode,
                    ClassDate = classDate?.ToString("yyyy-MM-dd")
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAttendance(int classCode, int studentCode, bool isPresent)
        {
            try
            {
                var context = await GetUserContextAsync();

                // Check if attendance record exists
                var attendanceRecord = await _context.Attends
                    .FirstOrDefaultAsync(a => a.ClassId == classCode && a.StudentId == studentCode);


                if (isPresent)
                {
                    if (attendanceRecord == null)
                    {
                        // Get class details to fill required fields
                        var classDetails = await _context.Classes
                            .Where(c => c.ClassCode == classCode)
                            .Select(c => new
                            {
                                c.TeacherCode,
                                c.HallCode,
                                c.ScheduleCode
                            })
                            .FirstOrDefaultAsync();

                        if (classDetails == null)
                        {
                            return Json(new { success = false, error = "Class not found" });
                        }

                      

                        // Verify all foreign keys exist
                        var teacherExists = await _context.Teachers.AnyAsync(t => t.TeacherCode == classDetails.TeacherCode);
                        var hallExists = await _context.Halls.AnyAsync(h => h.HallCode == classDetails.HallCode);
                        var studentExists = await _context.Students.AnyAsync(s => s.StudentCode == studentCode);
                        var scheduleExists = classDetails.ScheduleCode.HasValue ?
                            await _context.Schedules.AnyAsync(s => s.ScheduleCode == classDetails.ScheduleCode.Value) : true;

                    

                        if (!teacherExists)
                        {
                            return Json(new { success = false, error = "Teacher not found" });
                        }
                        if (!hallExists)
                        {
                            return Json(new { success = false, error = "Hall not found" });
                        }
                        if (!studentExists)
                        {
                            return Json(new { success = false, error = "Student not found" });
                        }
                        if (!scheduleExists)
                        {
                            return Json(new { success = false, error = "Schedule not found" });
                        }

                        // Add attendance record with all required fields
                        var newAttendance = new Attend
                        {
                            ClassId = classCode,
                            StudentId = studentCode,
                            AttendDate = DateTime.Now,
                            TeacherCode = classDetails.TeacherCode,
                            HallId = (int)classDetails.HallCode,
                            ScheduleCode = classDetails.ScheduleCode,
                            SessionPrice = 0,
                            Type = 1,
                            RootCode = context.rootCode
                        };

                    
                        _context.Attends.Add(newAttendance);
                    }
                    else
                    {
                        Debug.WriteLine("Attendance record already exists - no action needed");
                    }
                }
                else
                {
                    if (attendanceRecord != null)
                    {
                        Debug.WriteLine("Removing existing attendance record");
                        _context.Attends.Remove(attendanceRecord);
                    }
                    else
                    {
                        Debug.WriteLine("No attendance record to remove");
                    }
                }
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Attendance updated successfully" });
            }
            catch (Exception ex)
            {
                

                // Get the most detailed error message
                var detailedException = ex;
                while (detailedException.InnerException != null)
                {
                    detailedException = detailedException.InnerException;
                }


                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    detailedError = detailedException.Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkMarkAttendance(int classCode, List<int> studentCodes, bool isPresent)
        {
            try
            {
                var context = await GetUserContextAsync();

               
                // Get class details once for all students
                var classDetails = await _context.Classes
                    .Where(c => c.ClassCode == classCode)
                    .Select(c => new {
                        c.TeacherCode,
                        c.HallCode,
                        c.ScheduleCode
                    })
                    .FirstOrDefaultAsync();

                if (classDetails == null)
                {
                    return Json(new { success = false, error = "Class not found" });
                }

               

                foreach (var studentCode in studentCodes)
                {

                    var attendanceRecord = await _context.Attends
                        .FirstOrDefaultAsync(a => a.ClassId == classCode && a.StudentId == studentCode);


                    if (isPresent)
                    {
                        if (attendanceRecord == null)
                        {

                            var newAttendance = new Attend
                            {
                                ClassId = classCode,
                                StudentId = studentCode,
                                AttendDate = DateTime.Now,
                                TeacherCode = classDetails.TeacherCode,
                                HallId = (int)classDetails.HallCode,  // ← Added
                                ScheduleCode = classDetails.ScheduleCode,  // ← Added
                                SessionPrice = 0,  // ← Added
                                Type = 1,  // ← Added
                                RootCode = context.rootCode
                            };

                            _context.Attends.Add(newAttendance);
                            Debug.WriteLine($"  Attendance record added for student {studentCode}");
                        }
                        else
                        {
                            Debug.WriteLine($"  Attendance record already exists for student {studentCode}");
                        }
                    }
                    else
                    {
                        if (attendanceRecord != null)
                        {
                            Debug.WriteLine($"  Removing attendance record for student {studentCode}");
                            _context.Attends.Remove(attendanceRecord);
                        }
                        else
                        {
                            Debug.WriteLine($"  No attendance record to remove for student {studentCode}");
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Bulk attendance updated for {studentCodes.Count} students" });
            }
            catch (Exception ex)
            {
               

                // Get the most detailed error message
                var detailedException = ex;
                while (detailedException.InnerException != null)
                {
                    detailedException = detailedException.InnerException;
                }



                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    detailedError = detailedException.Message
                });
            }
        }
        [HttpPost]
        public async Task<IActionResult> SendSmsToParents(List<int> studentCodes, string message)
        {
            try
            {
                var context = await GetUserContextAsync();

                var students = await _context.Students
                    .Where(s => studentCodes.Contains(s.StudentCode))
                    .Select(s => new { s.StudentName, s.StudentFatherPhone })
                    .ToListAsync();

                var sentCount = 0;
                foreach (var student in students)
                {
                    if (!string.IsNullOrEmpty(student.StudentFatherPhone))
                    {
                        // Here you would integrate with your SMS service
                        // await _smsService.SendSms(student.StudentParentPhone, message);
                        sentCount++;
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"SMS sent to {sentCount} parents",
                    sentCount = sentCount,
                    totalStudents = students.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFilterOptions()
        {
            try
            {
                var context = await GetUserContextAsync();

                var teachers = await GetTeachersForUser(context);
                var subjects = await _context.Subjects
                    .Where(s => s.RootCode == context.rootCode)
                    .Select(s => new { s.SubjectCode, s.SubjectName })
                    .ToListAsync();

                return Json(new
                {
                    teachers = teachers.Select(t => new { t.TeacherCode, t.TeacherName }),
                    subjects = subjects,
                    isTeacher = context.isTeacher
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private async Task<int> GetTeacherCodeForClass(int classCode)
        {
            var classInfo = await _context.Classes
                .Where(c => c.ClassCode == classCode)
                .Select(c => c.TeacherCode)
                .FirstOrDefaultAsync();

            return classInfo;
        }

        #endregion

        #region Helper Methods

        [HttpGet]
        public async Task<IActionResult> ExportClassAttendance(string format = "excel", int? teacherCode = null, int? subjectCode = null, DateTime? classDate = null)
        {
            try
            {
                var context = await GetUserContextAsync();

                // Get attendance data (existing logic)
                var query = _context.Classes
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Where(c => c.RootCode == context.rootCode);

                // Apply filters
                if (context.branchCode.HasValue)
                    query = query.Where(c => c.BranchCode == context.branchCode.Value);

                if (context.isTeacher)
                {
                    var teacherId = await GetTeacherIdForUser(context.user.UserCode);
                    if (teacherId.HasValue)
                        query = query.Where(c => c.TeacherCode == teacherId.Value);
                }
                else if (teacherCode.HasValue)
                    query = query.Where(c => c.TeacherCode == teacherCode.Value);

                if (subjectCode.HasValue)
                    query = query.Where(c => c.SubjectCode == subjectCode.Value);
                if (classDate.HasValue)
                    query = query.Where(c => c.ClassDate == DateOnly.FromDateTime(classDate.Value));

                var classes = await query
                    .OrderByDescending(c => c.ClassDate)
                    .ToListAsync();

                // Build attendance data
                var attendanceData = new List<AttendanceExportDto>();

                foreach (var cls in classes)
                {
                    var enrolledStudents = await _context.Learns
                        .Include(l => l.StudentCodeNavigation)
                        .Where(l => l.TeacherCode == cls.TeacherCode &&
                                  l.SubjectCode == cls.SubjectCode &&
                                  l.IsActive)
                        .ToListAsync();

                    var attendedStudents = await _context.Attends
                        .Where(a => a.TeacherCode == cls.TeacherCode &&
                                  a.ClassId == cls.ClassCode)
                        .Select(a => a.StudentId)
                        .ToListAsync();

                    foreach (var enrollment in enrolledStudents)
                    {
                        var isPresent = attendedStudents.Contains(enrollment.StudentCode);
                        attendanceData.Add(new AttendanceExportDto
                        {
                            ClassName = cls.ClassName ?? "Unknown Class",
                            ClassDate = cls.ClassDate?.ToString("yyyy-MM-dd") ?? "No Date",
                            ClassTime = $"{cls.ClassStartTime?.ToString("HH:mm")} - {cls.ClassEndTime?.ToString("HH:mm")}",
                            TeacherName = cls.TeacherCodeNavigation?.TeacherName ?? "Unknown Teacher",
                            SubjectName = cls.SubjectCodeNavigation?.SubjectName ?? "Unknown Subject",
                            BranchName = cls.BranchCodeNavigation?.BranchName ?? "Unknown Branch",
                            HallName = cls.HallCodeNavigation?.HallName ?? "Unknown Hall",
                            StudentName = enrollment.StudentCodeNavigation?.StudentName ?? "Unknown Student",
                            StudentPhone = enrollment.StudentCodeNavigation?.StudentPhone ?? "",
                            StudentParentPhone = enrollment.StudentCodeNavigation?.StudentFatherPhone ?? "",
                            AttendanceStatus = isPresent ? "Present" : "Absent",
                            IsPresent = isPresent
                        });
                    }
                }

                var fileName = $"ClassAttendance_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (format.ToLower() == "pdf")
                {
                    return ExportAttendanceToPdf(attendanceData, fileName);
                }
                else
                {
                    return ExportAttendanceToExcel(attendanceData, fileName);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private IActionResult ExportToExcel(List<AttendanceExportDto> data)
        {
            try
            {
                var csv = new StringBuilder();

                // Add header
                csv.AppendLine("Class Name,Date,Time,Teacher,Subject,Branch,Hall,Student Name,Student Phone,Parent Phone,Status");

                // Add data rows
                foreach (var item in data)
                {
                    csv.AppendLine($"\"{item.ClassName}\",\"{item.ClassDate}\",\"{item.ClassTime}\",\"{item.TeacherName}\",\"{item.SubjectName}\",\"{item.BranchName}\",\"{item.HallName}\",\"{item.StudentName}\",\"{item.StudentPhone}\",\"{item.StudentParentPhone}\",\"{item.AttendanceStatus}\"");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"ClassAttendance_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private IActionResult ExportToPdf(List<AttendanceExportDto> data)
        {
            try
            {
                // For now, we'll create an HTML version that can be printed as PDF
                var html = GenerateAttendanceHtml(data);
                var bytes = Encoding.UTF8.GetBytes(html);
                var fileName = $"ClassAttendance_{DateTime.Now:yyyyMMdd_HHmmss}.html";

                return File(bytes, "text/html", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private string GenerateAttendanceHtml(List<AttendanceExportDto> data)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<title>Class Attendance Report</title>");
            html.AppendLine("<style>");
            html.AppendLine(@"
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1 { color: #333; text-align: center; }
        table { width: 100%; border-collapse: collapse; margin-top: 20px; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; font-weight: bold; }
        .present { background-color: #d4edda; }
        .absent { background-color: #f8d7da; }
        .summary { margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 5px; }
    ");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            html.AppendLine("<h1>Class Attendance Report</h1>");
            html.AppendLine($"<p style='text-align: center;'>Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}</p>");

            // Summary
            var totalStudents = data.Count;
            var presentCount = data.Count(d => d.IsPresent);
            var absentCount = totalStudents - presentCount;
            var attendanceRate = totalStudents > 0 ? (decimal)presentCount / totalStudents * 100 : 0;

            html.AppendLine("<div class='summary'>");
            html.AppendLine("<h3>Summary</h3>");
            html.AppendLine($"<p><strong>Total Students:</strong> {totalStudents}</p>");
            html.AppendLine($"<p><strong>Present:</strong> {presentCount}</p>");
            html.AppendLine($"<p><strong>Absent:</strong> {absentCount}</p>");
            html.AppendLine($"<p><strong>Attendance Rate:</strong> {attendanceRate:F1}%</p>");
            html.AppendLine("</div>");

            // Table
            html.AppendLine("<table>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Class</th>");
            html.AppendLine("<th>Date</th>");
            html.AppendLine("<th>Time</th>");
            html.AppendLine("<th>Teacher</th>");
            html.AppendLine("<th>Subject</th>");
            html.AppendLine("<th>Student</th>");
            html.AppendLine("<th>Student Phone</th>");
            html.AppendLine("<th>Parent Phone</th>");
            html.AppendLine("<th>Status</th>");
            html.AppendLine("</tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");

            foreach (var item in data)
            {
                var rowClass = item.IsPresent ? "present" : "absent";
                html.AppendLine($"<tr class='{rowClass}'>");
                html.AppendLine($"<td>{item.ClassName}</td>");
                html.AppendLine($"<td>{item.ClassDate}</td>");
                html.AppendLine($"<td>{item.ClassTime}</td>");
                html.AppendLine($"<td>{item.TeacherName}</td>");
                html.AppendLine($"<td>{item.SubjectName}</td>");
                html.AppendLine($"<td>{item.StudentName}</td>");
                html.AppendLine($"<td>{item.StudentPhone}</td>");
                html.AppendLine($"<td>{item.StudentParentPhone}</td>");
                html.AppendLine($"<td><strong>{item.AttendanceStatus}</strong></td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private async Task<List<Branch>> GetBranchesForUser((User user, Group group, int rootCode, int? branchCode, bool isTeacher) context)
        {
            if (context.branchCode.HasValue)
                return await _context.Branches.Where(b => b.BranchCode == context.branchCode.Value).ToListAsync();
            else
                return await _context.Branches.Where(b => b.RootCode == context.rootCode).ToListAsync();
        }

        private async Task<List<Teacher>> GetTeachersForUser((User user, Group group, int rootCode, int? branchCode, bool isTeacher) context)
        {
            return await _context.Teachers.Where(t => t.RootCode == context.rootCode && t.IsActive).ToListAsync();
        }

        private async Task<int?> GetTeacherIdForUser(int userId)
        {
            // Get the user's root code through their group
            var userContext = await _context.Users
                .Include(u => u.GroupCodeNavigation)
                .Where(u => u.UserCode == userId)
                .Select(u => u.GroupCodeNavigation.RootCode)
                .FirstOrDefaultAsync();

            if (userContext == 0)
                return null;

            // Find the teacher for this root code (since there's only one teacher per root)
            var teacher = await _context.Teachers
                .Where(t => t.RootCode == userContext && t.IsActive)
                .FirstOrDefaultAsync();

            return teacher?.TeacherCode;
        }

        #endregion

        #region Export Actions

        // Add this method to your ReportsController.cs
        [HttpGet]
        public async Task<IActionResult> ClassStudents(int id)
        {
            try
            {
                var context = await GetUserContextAsync();

                // Get the class details with financial info
                var classDetails = await _context.Classes
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Include(c => c.HallCodeNavigation)
                    .Where(c => c.ClassCode == id && c.RootCode == context.rootCode)
                    .FirstOrDefaultAsync();

                if (classDetails == null)
                {
                    return NotFound("Class not found");
                }

                // Get enrolled students for this class
                var enrolledStudents = await _context.Learns
                    .Include(l => l.StudentCodeNavigation)
                    .Where(l => l.TeacherCode == classDetails.TeacherCode &&
                              l.SubjectCode == classDetails.SubjectCode &&
                              l.ScheduleCode == classDetails.ScheduleCode &&
                              l.IsActive)
                    .Select(l => new
                    {
                        StudentCode = l.StudentCode,
                        StudentName = l.StudentCodeNavigation.StudentName ?? "Unknown",
                        StudentPhone = l.StudentCodeNavigation.StudentPhone ?? "",
                        StudentParentPhone = l.StudentCodeNavigation.StudentFatherPhone ?? "",
                        IsEnrolled = true,
                        IsPresent = false // Will be updated below
                    })
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();

                // Get actual attendance for this class
                var attendedStudentIds = await _context.Attends
                    .Where(a => a.ClassId == classDetails.ClassCode)
                    .Select(a => a.StudentId)
                    .ToListAsync();

                // Update presence status for each student
                var studentsWithAttendance = enrolledStudents.Select(s => new
                {
                    s.StudentCode,
                    s.StudentName,
                    s.StudentPhone,
                    s.StudentParentPhone,
                    s.IsEnrolled,
                    IsPresent = attendedStudentIds.Contains(s.StudentCode)
                }).ToList();

                // Create the view model with financial data
                var viewModel = new ClassStudentsViewModel
                {
                    ClassCode = classDetails.ClassCode,
                    ClassName = classDetails.ClassName ?? "Unknown Class",
                    ClassDate = classDetails.ClassDate?.ToString("yyyy-MM-dd"),
                    ClassStartTime = classDetails.ClassStartTime?.ToString("HH:mm"),
                    ClassEndTime = classDetails.ClassEndTime?.ToString("HH:mm"),
                    TeacherName = classDetails.TeacherCodeNavigation?.TeacherName ?? "Unknown Teacher",
                    SubjectName = classDetails.SubjectCodeNavigation?.SubjectName ?? "Unknown Subject",
                    BranchName = classDetails.BranchCodeNavigation?.BranchName ?? "Unknown Branch",
                    HallName = classDetails.HallCodeNavigation?.HallName ?? "Unknown Hall",
                    EnrolledCount = studentsWithAttendance.Count,
                    PresentCount = studentsWithAttendance.Count(s => s.IsPresent),
                    AbsentCount = studentsWithAttendance.Count(s => !s.IsPresent),
                    AttendanceRate = studentsWithAttendance.Count > 0 ?
                        Math.Round((decimal)studentsWithAttendance.Count(s => s.IsPresent) / studentsWithAttendance.Count * 100, 1) : 0,

                    // ADD FINANCIAL DATA FROM DATABASE
                    TotalAmount = classDetails.TotalAmount ?? 0,
                    TeacherAmount = classDetails.TeacherAmount ?? 0,
                    CenterAmount = classDetails.CenterAmount ?? 0,  // Use actual CenterAmount from DB

                    Students = studentsWithAttendance.Select(s => new ClassStudentDto
                    {
                        StudentCode = s.StudentCode,
                        StudentName = s.StudentName,
                        StudentPhone = s.StudentPhone,
                        StudentParentPhone = s.StudentParentPhone,
                        IsPresent = s.IsPresent
                    }).ToList()
                };

                // Pass user type to view for conditional display
                ViewBag.IsTeacher = context.isTeacher;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // Get attendance types from Lockup table
        [HttpGet]
        public async Task<ActionResult<List<object>>> GetAttendanceTypes()
        {
            try
            {
                var attendanceTypes = await _context.Lockups
                    .Where(l => l.PaymentCode != null) // Exclude NULL entries
                    .OrderBy(l => l.PaymentCode)
                    .Select(l => new {
                        paymentCode = l.PaymentCode,
                        paymentName = l.PaymentName
                    })
                    .ToListAsync();

                return Ok(attendanceTypes);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Mark attendance with type selection and optional session price
        [HttpPost]
        public async Task<ActionResult> MarkAttendanceWithType(int classCode, int studentCode, bool isPresent, int attendanceType = 0, decimal? sessionPrice = null)
        {
            try
            {
                var context = await GetUserContextAsync();
                var attendanceRecord = await _context.Attends
                    .FirstOrDefaultAsync(a => a.ClassId == classCode && a.StudentId == studentCode);

                if (isPresent)
                {
                    if (attendanceType <= 0 || attendanceType > 4)
                        return Json(new { success = false, error = $"Invalid attendance type: {attendanceType}" });

                    if (attendanceType == 3 && (!sessionPrice.HasValue || sessionPrice.Value <= 0))
                        return Json(new { success = false, error = "Session price is required for discount attendance" });

                    if (attendanceRecord == null)
                    {
                        var classDetails = await _context.Classes
                            .Where(c => c.ClassCode == classCode)
                            .Select(c => new { c.TeacherCode, c.HallCode, c.ScheduleCode })
                            .FirstOrDefaultAsync();

                        if (classDetails == null)
                            return Json(new { success = false, error = "Class not found" });

                        // Determine SessionPrice based on attendance type
                        decimal sessionPriceValue;
                        switch (attendanceType)
                        {
                            case 1: // Regular - use magic number so trigger will handle it
                                sessionPriceValue = -1m; // Magic number for "trigger should set this"
                                break;
                            case 2: // Free - explicitly 0
                                sessionPriceValue = 0m;
                                break;
                            case 3: // Discount - use provided price
                                sessionPriceValue = sessionPrice ?? 0m;
                                break;
                            case 4: // Subscribed - use magic number so trigger will handle it
                                sessionPriceValue = -1m; // Magic number for "trigger should set this"
                                break;
                            default:
                                sessionPriceValue = 0m;
                                break;
                        }

                        var newAttendance = new Attend
                        {
                            ClassId = classCode,
                            StudentId = studentCode,
                            AttendDate = DateTime.Now,
                            TeacherCode = classDetails.TeacherCode,
                            HallId = (int)classDetails.HallCode,
                            ScheduleCode = classDetails.ScheduleCode,
                            SessionPrice = sessionPriceValue, // Now using decimal, not decimal?
                            Type = attendanceType,
                            RootCode = context.rootCode
                        };

                        _context.Attends.Add(newAttendance);
                    }
                    else
                    {
                        // Update existing record logic stays the same...
                        attendanceRecord.Type = attendanceType;
                        attendanceRecord.AttendDate = DateTime.Now;

                        switch (attendanceType)
                        {
                            case 1: // Regular - get schedule amount
                                var scheduleAmount = await _context.Schedules
                                    .Where(s => s.ScheduleCode == attendanceRecord.ScheduleCode)
                                    .Select(s => s.ScheduleAmount)
                                    .FirstOrDefaultAsync();
                                attendanceRecord.SessionPrice = scheduleAmount ?? 0m;
                                break;
                            case 2: // Free
                                attendanceRecord.SessionPrice = 0m;
                                break;
                            case 3: // Discount
                                attendanceRecord.SessionPrice = sessionPrice ?? 0m;
                                break;
                            case 4: // Subscribed
                                attendanceRecord.SessionPrice = 0m;
                                break;
                        }
                    }
                }
                else
                {
                    if (attendanceRecord != null)
                        _context.Attends.Remove(attendanceRecord);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Attendance updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
        // Add these DTOs/ViewModels to your project


        [HttpGet]
        public async Task<IActionResult> ExportFinancialSummary(string format = "excel", DateTime? startDate = null, DateTime? endDate = null, int? branchCode = null)
        {
            try
            {
                var context = await GetUserContextAsync();

                if (context.isTeacher)
                    return Forbid("Teachers cannot access financial summary reports");

                // Get the financial data (same logic as FinancialSummary method)
                var query = _context.Classes
                    .Include(c => c.TeacherCodeNavigation)
                    .Include(c => c.SubjectCodeNavigation)
                    .Include(c => c.BranchCodeNavigation)
                    .Where(c => c.RootCode == context.rootCode);

                // Apply filters
                if (context.branchCode.HasValue)
                    query = query.Where(c => c.BranchCode == context.branchCode.Value);
                else if (branchCode.HasValue)
                    query = query.Where(c => c.BranchCode == branchCode.Value);

                if (startDate.HasValue)
                    query = query.Where(c => c.ClassDate >= DateOnly.FromDateTime(startDate.Value));
                if (endDate.HasValue)
                    query = query.Where(c => c.ClassDate <= DateOnly.FromDateTime(endDate.Value));

                var classes = await query
                    .OrderByDescending(c => c.ClassDate)
                    .Select(c => new FinancialSummaryDto
                    {
                        ClassCode = c.ClassCode,
                        ClassName = c.ClassName ?? "Unknown Class",
                        ClassDate = c.ClassDate,
                        BranchName = c.BranchCodeNavigation.BranchName ?? "Unknown Branch",
                        SubjectName = c.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        TeacherName = c.TeacherCodeNavigation.TeacherName ?? "Unknown Teacher",
                        NoOfStudents = c.NoOfStudents,
                        TotalAmount = c.TotalAmount ?? 0,
                        TeacherAmount = c.TeacherAmount ?? 0,
                        CenterAmount = (c.TotalAmount ?? 0) - (c.TeacherAmount ?? 0)
                    })
                    .ToListAsync();

                // Calculate totals
                var totalRevenue = classes.Sum(c => c.TotalAmount);
                var totalTeacherPayments = classes.Sum(c => c.TeacherAmount);
                var totalCenterRevenue = totalRevenue - totalTeacherPayments;

                var fileName = $"FinancialSummary_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (format.ToLower() == "pdf")
                {
                    return ExportFinancialSummaryToPdf(classes, totalRevenue, totalTeacherPayments, totalCenterRevenue, fileName);
                }
                else
                {
                    return ExportFinancialSummaryToExcel(classes, totalRevenue, totalTeacherPayments, totalCenterRevenue, fileName);
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private IActionResult ExportStudentEnrollmentToExcel(List<StudentEnrollmentDto> data, string fileName)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Student Enrollment");

                // Headers
                string[] headers = {
                    "Student Code", "Student Name", "Phone", "Parent Phone", "Branch",
                    "Year", "Subscription Date", "Enrolled Subjects", "Subject Names"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                }

                // Style headers
                using (var range = worksheet.Cells[1, 1, 1, headers.Length])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                }

                // Data rows
                for (int i = 0; i < data.Count; i++)
                {
                    int row = i + 2;
                    var item = data[i];

                    worksheet.Cells[row, 1].Value = item.StudentCode;
                    worksheet.Cells[row, 2].Value = item.StudentName;
                    worksheet.Cells[row, 3].Value = item.StudentPhone;
                    worksheet.Cells[row, 4].Value = item.StudentParentPhone;
                    worksheet.Cells[row, 5].Value = item.BranchName;
                    worksheet.Cells[row, 6].Value = item.YearName;
                    worksheet.Cells[row, 7].Value = item.SubscriptionTime?.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 8].Value = item.EnrolledSubjects;
                    worksheet.Cells[row, 9].Value = item.SubjectNames;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
            }
        }
        private IActionResult ExportFinancialSummaryToPdf(List<FinancialSummaryDto> data, decimal totalRevenue, decimal totalTeacherPayments, decimal totalCenterRevenue, string fileName, bool isTeacher = false)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document(PageSize.A4.Rotate()); // Landscape for wide table
                var writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph(isTeacher ? "My Revenue Report" : "Financial Summary Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);
                document.Add(new Paragraph("\n"));

                // Table
                var columnCount = isTeacher ? 8 : 10;
                var table = new PdfPTable(columnCount);
                table.WidthPercentage = 100;

                // Headers
                var headers = new List<string>
        {
            "Class Code", "Class Name", "Date", "Branch", "Subject", "Teacher", "Students", "Total Amount"
        };

                if (!isTeacher)
                {
                    headers.AddRange(new[] { "Teacher Amount", "Center Amount" });
                }

                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
                    cell.BackgroundColor = BaseColor.LIGHT_GRAY;
                    table.AddCell(cell);
                }

                // Data rows
                foreach (var item in data)
                {
                    table.AddCell(item.ClassCode.ToString());
                    table.AddCell(item.ClassName);
                    table.AddCell(item.ClassDate?.ToString("yyyy-MM-dd") ?? "");
                    table.AddCell(item.BranchName);
                    table.AddCell(item.SubjectName);
                    table.AddCell(item.TeacherName);
                    table.AddCell(item.NoOfStudents.ToString());
                    table.AddCell(item.TotalAmount.ToString("C"));

                    if (!isTeacher)
                    {
                        table.AddCell(item.TeacherAmount.ToString("C"));
                        table.AddCell(item.CenterAmount.ToString("C"));
                    }
                }

                document.Add(table);

                // Summary
                document.Add(new Paragraph("\n"));
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                document.Add(new Paragraph("Summary", summaryFont));
                document.Add(new Paragraph($"Total Revenue: {totalRevenue:C}"));

                if (!isTeacher)
                {
                    document.Add(new Paragraph($"Total Teacher Payments: {totalTeacherPayments:C}"));
                    document.Add(new Paragraph($"Total Center Revenue: {totalCenterRevenue:C}"));
                }

                document.Close();

                return File(stream.ToArray(), "application/pdf", $"{fileName}.pdf");
            }
        }



        private IActionResult ExportAttendanceToPdf(List<AttendanceExportDto> data, string fileName)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document(PageSize.A4.Rotate());
                var writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph("Class Attendance Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);
                document.Add(new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}"));
                document.Add(new Paragraph("\n"));

                // Summary
                var totalStudents = data.Count;
                var presentCount = data.Count(d => d.IsPresent);
                var absentCount = totalStudents - presentCount;
                var attendanceRate = totalStudents > 0 ? (decimal)presentCount / totalStudents * 100 : 0;

                document.Add(new Paragraph($"Total Students: {totalStudents}"));
                document.Add(new Paragraph($"Present: {presentCount}"));
                document.Add(new Paragraph($"Absent: {absentCount}"));
                document.Add(new Paragraph($"Attendance Rate: {attendanceRate:F1}%"));
                document.Add(new Paragraph("\n"));

                // Table
                var table = new PdfPTable(9);
                table.WidthPercentage = 100;

                // Headers
                string[] headers = {
                    "Class", "Date", "Teacher", "Subject", "Student",
                    "Student Phone", "Parent Phone", "Status"
                };

                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8)));
                    cell.BackgroundColor = BaseColor.LIGHT_GRAY;
                    table.AddCell(cell);
                }

                // Data rows
                foreach (var item in data)
                {
                    table.AddCell(new Phrase(item.ClassName, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                    table.AddCell(new Phrase(item.ClassDate, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                    table.AddCell(new Phrase(item.TeacherName, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                    table.AddCell(new Phrase(item.SubjectName, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                    table.AddCell(new Phrase(item.StudentName, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                    table.AddCell(new Phrase(item.StudentPhone, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                    table.AddCell(new Phrase(item.StudentParentPhone, FontFactory.GetFont(FontFactory.HELVETICA, 7)));

                    var statusCell = new PdfPCell(new Phrase(item.AttendanceStatus, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                    statusCell.BackgroundColor = item.IsPresent ? BaseColor.LIGHT_GRAY : BaseColor.PINK;
                    table.AddCell(statusCell);
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", $"{fileName}.pdf");
            }
        }

        private IActionResult ExportStudentEnrollmentToPdf(List<StudentEnrollmentDto> data, string fileName)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document(PageSize.A4.Rotate());
                var writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph("Student Enrollment Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);
                document.Add(new Paragraph("\n"));

                // Table
                var table = new PdfPTable(7);
                table.WidthPercentage = 100;

                // Headers
                string[] headers = {
                    "Student Name", "Phone", "Parent Phone", "Branch", "Year", "Enrolled Subjects", "Subject Names"
                };

                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
                    cell.BackgroundColor = BaseColor.LIGHT_GRAY;
                    table.AddCell(cell);
                }

                // Data rows
                foreach (var item in data)
                {
                    table.AddCell(new Phrase(item.StudentName, FontFactory.GetFont(FontFactory.HELVETICA, 8)));
                    table.AddCell(new Phrase(item.StudentPhone, FontFactory.GetFont(FontFactory.HELVETICA, 8)));
                    table.AddCell(new Phrase(item.StudentParentPhone, FontFactory.GetFont(FontFactory.HELVETICA, 8)));
                    table.AddCell(new Phrase(item.BranchName, FontFactory.GetFont(FontFactory.HELVETICA, 8)));
                    table.AddCell(new Phrase(item.YearName, FontFactory.GetFont(FontFactory.HELVETICA, 8)));
                    table.AddCell(new Phrase(item.EnrolledSubjects.ToString(), FontFactory.GetFont(FontFactory.HELVETICA, 8)));
                    table.AddCell(new Phrase(item.SubjectNames, FontFactory.GetFont(FontFactory.HELVETICA, 7)));
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", $"{fileName}.pdf");
            }
        }

        private IActionResult ExportAttendanceToExcel(List<AttendanceExportDto> data, string fileName)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Class Attendance");

                // Headers
                string[] headers = {
                    "Class Name", "Date", "Time", "Teacher", "Subject", "Branch", "Hall",
                    "Student Name", "Student Phone", "Parent Phone", "Status"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                }

                // Style headers
                using (var range = worksheet.Cells[1, 1, 1, headers.Length])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                }

                // Data rows
                for (int i = 0; i < data.Count; i++)
                {
                    int row = i + 2;
                    var item = data[i];

                    worksheet.Cells[row, 1].Value = item.ClassName;
                    worksheet.Cells[row, 2].Value = item.ClassDate;
                    worksheet.Cells[row, 3].Value = item.ClassTime;
                    worksheet.Cells[row, 4].Value = item.TeacherName;
                    worksheet.Cells[row, 5].Value = item.SubjectName;
                    worksheet.Cells[row, 6].Value = item.BranchName;
                    worksheet.Cells[row, 7].Value = item.HallName;
                    worksheet.Cells[row, 8].Value = item.StudentName;
                    worksheet.Cells[row, 9].Value = item.StudentPhone;
                    worksheet.Cells[row, 10].Value = item.StudentParentPhone;
                    worksheet.Cells[row, 11].Value = item.AttendanceStatus;

                    // Color code attendance status
                    if (item.IsPresent)
                    {
                        worksheet.Cells[row, 11].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[row, 11].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    }
                    else
                    {
                        worksheet.Cells[row, 11].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[row, 11].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                    }
                }

                // Summary
                var totalStudents = data.Count;
                var presentCount = data.Count(d => d.IsPresent);
                var absentCount = totalStudents - presentCount;
                var attendanceRate = totalStudents > 0 ? (decimal)presentCount / totalStudents * 100 : 0;

                int summaryRow = data.Count + 3;
                worksheet.Cells[summaryRow, 1].Value = "SUMMARY";
                worksheet.Cells[summaryRow, 1].Style.Font.Bold = true;

                worksheet.Cells[summaryRow + 1, 1].Value = "Total Students:";
                worksheet.Cells[summaryRow + 1, 2].Value = totalStudents;
                worksheet.Cells[summaryRow + 2, 1].Value = "Present:";
                worksheet.Cells[summaryRow + 2, 2].Value = presentCount;
                worksheet.Cells[summaryRow + 3, 1].Value = "Absent:";
                worksheet.Cells[summaryRow + 3, 2].Value = absentCount;
                worksheet.Cells[summaryRow + 4, 1].Value = "Attendance Rate:";
                worksheet.Cells[summaryRow + 4, 2].Value = $"{attendanceRate:F1}%";

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
            }
        }


        private IActionResult ExportFinancialSummaryToExcel(List<FinancialSummaryDto> data, decimal totalRevenue, decimal totalTeacherPayments, decimal totalCenterRevenue, string fileName, bool isTeacher = false)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add(isTeacher ? "My Revenue Report" : "Financial Summary");

                // Headers based on user type
                var headers = new List<string>
        {
            "Class Code", "Class Name", "Date", "Branch", "Subject", "Teacher", "Students", "Total Amount"
        };

                if (!isTeacher)
                {
                    headers.AddRange(new[] { "Teacher Amount", "Center Amount" });
                }

                for (int i = 0; i < headers.Count; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                }

                // Style headers
                using (var range = worksheet.Cells[1, 1, 1, headers.Count])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Data rows
                for (int i = 0; i < data.Count; i++)
                {
                    int row = i + 2;
                    var item = data[i];

                    worksheet.Cells[row, 1].Value = item.ClassCode;
                    worksheet.Cells[row, 2].Value = item.ClassName;
                    worksheet.Cells[row, 3].Value = item.ClassDate?.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 4].Value = item.BranchName;
                    worksheet.Cells[row, 5].Value = item.SubjectName;
                    worksheet.Cells[row, 6].Value = item.TeacherName;
                    worksheet.Cells[row, 7].Value = item.NoOfStudents;
                    worksheet.Cells[row, 8].Value = item.TotalAmount;

                    if (!isTeacher)
                    {
                        worksheet.Cells[row, 9].Value = item.TeacherAmount;
                        worksheet.Cells[row, 10].Value = item.CenterAmount;
                    }
                }

                // Summary section
                int summaryRow = data.Count + 4;
                worksheet.Cells[summaryRow, 1].Value = "SUMMARY";
                worksheet.Cells[summaryRow, 1].Style.Font.Bold = true;

                worksheet.Cells[summaryRow + 1, 1].Value = "Total Revenue:";
                worksheet.Cells[summaryRow + 1, 2].Value = totalRevenue;

                if (!isTeacher)
                {
                    worksheet.Cells[summaryRow + 2, 1].Value = "Total Teacher Payments:";
                    worksheet.Cells[summaryRow + 2, 2].Value = totalTeacherPayments;
                    worksheet.Cells[summaryRow + 3, 1].Value = "Total Center Revenue:";
                    worksheet.Cells[summaryRow + 3, 2].Value = totalCenterRevenue;
                }

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
            }
        }     
        #endregion

    }
}