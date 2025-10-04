using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using centrny.Models;

namespace centrny.Controllers
{
    [Route("[controller]")]
    public class OnlineStudentController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<OnlineStudentController> _logger;

        public OnlineStudentController(CenterContext context, ILogger<OnlineStudentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                _logger.LogWarning("Student not logged in, redirecting to login page at {DateTime}", DateTime.UtcNow);
                return Redirect("/StudentLogin");
            }

            try
            {
                _logger.LogInformation("Loading dashboard for StudentCode={StudentCode} at {DateTime} by user {User}",
                    studentCode.Value, DateTime.UtcNow, "Hamodyyy123");

                var dashboardData = await GetStudentDashboardData(studentCode.Value);
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student dashboard for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return View("Error");
            }
        }

        [HttpGet("Learning")]
        public async Task<IActionResult> Learning()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                _logger.LogWarning("Student not logged in, redirecting to login page at {DateTime}", DateTime.UtcNow);
                return Redirect("/StudentLogin");
            }

            try
            {
                _logger.LogInformation("Loading learning view for StudentCode={StudentCode} at {DateTime}",
                    studentCode.Value, DateTime.UtcNow);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading learning view for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return View("Error");
            }
        }

        private async Task<StudentDashboardViewModel> GetStudentDashboardData(int studentCode)
        {
            var student = await _context.Students
                .Include(s => s.BranchCodeNavigation)
                .Include(s => s.YearCodeNavigation)
                .Include(s => s.RootCodeNavigation)
                .FirstOrDefaultAsync(s => s.StudentCode == studentCode);

            if (student == null)
            {
                _logger.LogError("Student with code {StudentCode} not found at {DateTime}", studentCode, DateTime.UtcNow);
                throw new Exception($"Student with code {studentCode} not found");
            }

            var enrolledSubjects = await _context.Learns
                .Include(l => l.SubjectCodeNavigation)
                .Include(l => l.TeacherCodeNavigation)
                .Include(l => l.ScheduleCodeNavigation)
                    .ThenInclude(s => s.HallCodeNavigation)
                .Include(l => l.BranchCodeNavigation)
                .Include(l => l.EduYearCodeNavigation)
                .Where(l => l.StudentCode == studentCode && l.IsActive)
                .ToListAsync();

            var attendedExams = await _context.StudentExams
                .Include(se => se.ExamCodeNavigation)
                    .ThenInclude(e => e.SubjectCodeNavigation)
                .Include(se => se.ExamCodeNavigation)
                    .ThenInclude(e => e.TeacherCodeNavigation)
                .Where(se => se.StudentCode == studentCode && se.IsActive == true)
                .OrderByDescending(se => se.InsertTime)
                .ToListAsync();

            var activePlans = await _context.StudentPlans
                .Include(sp => sp.SubscriptionPlanCodeNavigation)
                .Include(sp => sp.EduYearCodeNavigation)
                .Where(sp => sp.StudentCode == studentCode && sp.IsActive && !sp.IsExpired)
                .ToListAsync();

            var totalAttendance = await _context.Attends
                .Where(a => a.StudentId == studentCode)
                .CountAsync();

            var recentAttendance = await _context.Attends
                .Where(a => a.StudentId == studentCode && a.AttendDate >= DateTime.Today.AddDays(-30))
                .CountAsync();

            _logger.LogInformation("Dashboard data loaded successfully for StudentCode={StudentCode}: {SubjectsCount} subjects, {ExamsCount} exams, {AttendanceCount} attendance records at {DateTime}",
                studentCode, enrolledSubjects.Count, attendedExams.Count, totalAttendance, DateTime.UtcNow);

            return new StudentDashboardViewModel
            {
                Student = student,
                EnrolledSubjects = enrolledSubjects,
                AttendedExams = attendedExams,
                ActivePlans = activePlans,
                TotalAttendanceCount = totalAttendance,
                RecentAttendanceCount = recentAttendance,
                IsSubscribed = activePlans.Any(p => p.IsActive && !p.IsExpired)
            };
        }

        [HttpGet("GetStudentSubjects")]
        public async Task<IActionResult> GetStudentSubjects()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                _logger.LogWarning("Unauthorized access to GetStudentSubjects at {DateTime}", DateTime.UtcNow);
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var subjects = await _context.Learns
                    .Where(l => l.StudentCode == studentCode.Value && l.IsActive)
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .Include(l => l.ScheduleCodeNavigation)
                        .ThenInclude(s => s.HallCodeNavigation)
                    .Include(l => l.BranchCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Select(l => new
                    {
                        subjectCode = l.SubjectCode,
                        subjectName = l.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        teacherCode = l.TeacherCode,
                        teacherName = l.TeacherCodeNavigation.TeacherName ?? "Unknown Teacher",
                        teacherPhone = l.TeacherCodeNavigation.TeacherPhone ?? "N/A",
                        branchCode = l.BranchCode,
                        branchName = l.BranchCodeNavigation.BranchName ?? "Unknown Branch",
                        eduYearName = l.EduYearCodeNavigation.EduName ?? "Unknown Year",
                        studentFee = l.StudentFee,
                        isOnline = l.IsOnline,
                        yearCode = l.YearCode,
                        insertTime = l.InsertTime.ToString("yyyy-MM-dd"),
                        scheduleDay = l.ScheduleCodeNavigation != null ? l.ScheduleCodeNavigation.DayOfWeek : null,
                        scheduleStartTime = l.ScheduleCodeNavigation != null && l.ScheduleCodeNavigation.StartTime.HasValue
                            ? l.ScheduleCodeNavigation.StartTime.Value.ToString("HH:mm") : null,
                        scheduleEndTime = l.ScheduleCodeNavigation != null && l.ScheduleCodeNavigation.EndTime.HasValue
                            ? l.ScheduleCodeNavigation.EndTime.Value.ToString("HH:mm") : null,
                        hallName = l.ScheduleCodeNavigation != null && l.ScheduleCodeNavigation.HallCodeNavigation != null
                            ? l.ScheduleCodeNavigation.HallCodeNavigation.HallName : null
                    })
                    .ToListAsync();

                return Json(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student subjects for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet("GetLearningSubjects")]
        public async Task<IActionResult> GetLearningSubjects()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                _logger.LogWarning("Unauthorized access to GetLearningSubjects at {DateTime}", DateTime.UtcNow);
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var subjects = await _context.Learns
                    .Where(l => l.StudentCode == studentCode.Value && l.IsActive)
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Include(l => l.YearCodeNavigation)
                    .Select(l => new
                    {
                        subjectCode = l.SubjectCode,
                        subjectName = l.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        eduYearName = l.EduYearCodeNavigation.EduName ?? "Unknown Year",
                        yearCode = l.YearCode,
                        eduYearCode = l.EduYearCode
                    })
                    .Distinct()
                    .OrderBy(s => s.subjectName)
                    .ToListAsync();

                return Json(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting learning subjects for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet("GetSubjectChapters")]
        public async Task<IActionResult> GetSubjectChapters(int subjectCode)
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var isEnrolled = await _context.Learns
                    .AnyAsync(l => l.StudentCode == studentCode.Value &&
                                  l.SubjectCode == subjectCode &&
                                  l.IsActive);

                if (!isEnrolled)
                {
                    return Json(new { error = "Student not enrolled in this subject" });
                }

                var chapters = await _context.Lessons
                    .Where(l => l.SubjectCode == subjectCode &&
                               l.ChapterCode == null &&
                               l.IsActive == true)
                    .Include(l => l.SubjectCodeNavigation)
                    .Select(l => new
                    {
                        lessonCode = l.LessonCode,
                        chapterName = l.LessonName ?? "Unnamed Chapter",
                        insertTime = l.InsertTime,
                        subjectName = l.SubjectCodeNavigation.SubjectName ?? "Unknown Subject",
                        lessonsCount = _context.Lessons.Count(lesson => lesson.ChapterCode == l.LessonCode && lesson.IsActive == true)
                    })
                    .OrderBy(c => c.insertTime)
                    .ToListAsync();

                return Json(chapters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chapters for SubjectCode={SubjectCode}, StudentCode={StudentCode} at {DateTime}",
                    subjectCode, studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet("GetChapterLessons")]
        public async Task<IActionResult> GetChapterLessons(int chapterCode)
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var chapter = await _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .FirstOrDefaultAsync(l => l.LessonCode == chapterCode &&
                                            l.ChapterCode == null &&
                                            l.IsActive == true);

                if (chapter == null)
                {
                    return Json(new { error = "Chapter not found" });
                }

                var isEnrolled = await _context.Learns
                    .AnyAsync(l => l.StudentCode == studentCode.Value &&
                                  l.SubjectCode == chapter.SubjectCode &&
                                  l.IsActive);

                if (!isEnrolled)
                {
                    return Json(new { error = "Student not enrolled in this subject" });
                }

                var lessons = await _context.Lessons
                    .Where(l => l.ChapterCode == chapterCode && l.IsActive == true)
                    .Select(l => new
                    {
                        lessonCode = l.LessonCode,
                        lessonName = l.LessonName ?? "Unnamed Lesson",
                        insertTime = l.InsertTime,
                        isActive = l.IsActive
                    })
                    .OrderBy(l => l.insertTime)
                    .ToListAsync();

                var result = new
                {
                    chapter = new
                    {
                        chapterCode = chapter.LessonCode,
                        chapterName = chapter.LessonName,
                        subjectName = chapter.SubjectCodeNavigation?.SubjectName ?? "Unknown Subject",
                        subjectCode = chapter.SubjectCode
                    },
                    lessons = lessons,
                    totalLessons = lessons.Count
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lessons for ChapterCode={ChapterCode}, StudentCode={StudentCode} at {DateTime}",
                    chapterCode, studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet("GetAttendedExams")]
        public async Task<IActionResult> GetAttendedExams()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                _logger.LogWarning("Unauthorized access to GetAttendedExams at {DateTime}", DateTime.UtcNow);
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var exams = await _context.StudentExams
            .Where(se => se.StudentCode == studentCode.Value && se.IsActive == true)
            .Where(se => se.ExamCodeNavigation.IsExam == true) 
            .Include(se => se.ExamCodeNavigation)
                .ThenInclude(e => e.SubjectCodeNavigation)
            .Include(se => se.ExamCodeNavigation)
                .ThenInclude(e => e.TeacherCodeNavigation)
            .OrderByDescending(se => se.InsertTime)
                    .Select(se => new
                    {
                        examCode = se.ExamCode,
                        examName = se.ExamCodeNavigation.ExamName ?? "Unknown Exam",
                        subjectName = se.ExamCodeNavigation.SubjectCodeNavigation != null
                            ? se.ExamCodeNavigation.SubjectCodeNavigation.SubjectName : "N/A",
                        teacherName = se.ExamCodeNavigation.TeacherCodeNavigation != null
                            ? se.ExamCodeNavigation.TeacherCodeNavigation.TeacherName : "N/A",
                        studentResult = se.StudentResult ?? 0,
                        examDegree = se.ExamDegree ?? 0,
                        studentPercentage = se.StudentPercentage ?? 0,
                        examDate = se.InsertTime.HasValue ? se.InsertTime.Value.ToString("yyyy-MM-dd") : "N/A",
                        isExam = se.ExamCodeNavigation.IsExam,
                        passed = (se.StudentPercentage ?? 0) >= 50
                    })
                    .ToListAsync();

                return Json(exams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attended exams for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet("GetSubscriptionStatus")]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var activePlans = await _context.StudentPlans
                    .Include(sp => sp.SubscriptionPlanCodeNavigation)
                    .Include(sp => sp.EduYearCodeNavigation)
                    .Where(sp => sp.StudentCode == studentCode.Value && sp.IsActive && !sp.IsExpired)
                    .Select(sp => new
                    {
                        planName = sp.SubscriptionPlanCodeNavigation.SubPlanName ?? "Unknown Plan",
                        description = sp.SubscriptionPlanCodeNavigation.Description ?? "No description",
                        price = sp.Price,
                        subDate = sp.SubDate.ToString("yyyy-MM-dd"),
                        expiryDate = sp.ExpiryDate.ToString("yyyy-MM-dd"),
                        eduYearName = sp.EduYearCodeNavigation.EduName ?? "Unknown Year",
                        isActive = sp.IsActive,
                        isExpired = sp.IsExpired,
                        daysRemaining = (sp.ExpiryDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days
                    })
                    .ToListAsync();

                var isSubscribed = activePlans.Any();

                return Json(new
                {
                    isSubscribed = isSubscribed,
                    status = isSubscribed ? "Subscribed" : "Regular",
                    plans = activePlans,
                    fetchedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription status for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet("GetStudentStats")]
        public async Task<IActionResult> GetStudentStats()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var subjectsCount = await _context.Learns
                    .Where(l => l.StudentCode == studentCode.Value && l.IsActive)
                    .CountAsync();

                var totalAttendance = await _context.Attends
                    .Where(a => a.StudentId == studentCode.Value)
                    .CountAsync();

                var examsCount = await _context.StudentExams
     .Where(se => se.StudentCode == studentCode.Value
               && se.IsActive == true
               && se.ExamCodeNavigation.IsExam == true)
     .CountAsync();

                var averageGrade = examsCount > 0 ? await _context.StudentExams
                    .Where(se => se.StudentCode == studentCode.Value && se.IsActive == true && se.StudentPercentage.HasValue)
                    .AverageAsync(se => se.StudentPercentage ?? 0) : 0;

                var recentAttendance = await _context.Attends
                    .Where(a => a.StudentId == studentCode.Value && a.AttendDate >= DateTime.Today.AddDays(-30))
                    .CountAsync();

                return Json(new
                {
                    subjectsCount,
                    totalAttendance,
                    examsCount,
                    averageGrade = Math.Round(averageGrade, 1),
                    recentAttendance
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student stats for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet("GetStudentProfile")]
        public async Task<IActionResult> GetStudentProfile()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var student = await _context.Students
                    .Include(s => s.BranchCodeNavigation)
                    .Include(s => s.YearCodeNavigation)
                    .Include(s => s.RootCodeNavigation)
                    .Where(s => s.StudentCode == studentCode.Value)
                    .Select(s => new
                    {
                        studentCode = s.StudentCode,
                        studentName = s.StudentName ?? "Unknown Student",
                        studentPhone = s.StudentPhone ?? "N/A",
                        studentParentPhone = s.StudentFatherPhone ?? s.StudentFatherPhone ?? "N/A",
                        studentBirthdate = s.StudentBirthdate.ToString("yyyy-MM-dd"),
                        studentGender = s.StudentGender.HasValue ? (s.StudentGender.Value ? "Male" : "Female") : "Not specified",
                        subscriptionTime = s.SubscribtionTime.ToString("yyyy-MM-dd"),
                        isActive = s.IsActive,
                        branchName = s.BranchCodeNavigation != null ? s.BranchCodeNavigation.BranchName : "N/A",
                        yearName = s.YearCodeNavigation != null ? s.YearCodeNavigation.YearName : "N/A",
                        rootName = s.RootCodeNavigation != null ? s.RootCodeNavigation.RootName : "N/A",
                        age = CalculateAge(s.StudentBirthdate)
                    })
                    .FirstOrDefaultAsync();

                if (student == null)
                {
                    return Json(new { error = "Student profile not found" });
                }

                return Json(student);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting student profile for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost("UpdateLastActivity")]
        public IActionResult UpdateLastActivity()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                return Json(new { error = "Student not logged in" });
            }

            HttpContext.Session.SetString("LastActivity", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            return Json(new
            {
                success = true,
                lastActivity = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                sessionId = HttpContext.Session.Id
            });
        }

        [HttpGet("GetStudentInfo")]
        public async Task<IActionResult> GetStudentInfo()
        {
            try
            {
                var studentCodeString = HttpContext.Session.GetString("StudentCode");
                if (string.IsNullOrEmpty(studentCodeString))
                {
                    return Json(new { error = "Student not found in session" });
                }

                if (!int.TryParse(studentCodeString, out int studentCode))
                {
                    return Json(new { error = "Invalid student code format" });
                }

                var student = await _context.Students
                    .Include(s => s.RootCodeNavigation)
                    .FirstOrDefaultAsync(s => s.StudentCode == studentCode);

                if (student == null)
                {
                    return Json(new { error = "Student not found" });
                }

                return Json(new
                {
                    rootName = student.RootCodeNavigation?.RootName ?? "EduCenter",
                    studentName = student.StudentName
                });
            }
            catch (Exception)
            {
                return Json(new { error = "Failed to load student info" });
            }
        }

        [HttpGet("GetSystemInfo")]
        public IActionResult GetSystemInfo()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");

            return Json(new
            {
                currentDateTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                loggedInStudent = studentCode,
                sessionId = HttpContext.Session.Id,
                applicationVersion = "1.0.0"
            });
        }

        [HttpPost("Logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Json(new
            {
                success = true,
                message = "Logged out successfully",
                logoutTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        private int CalculateAge(DateOnly birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;
            return age;
        }

        // ======================
        // Attended Sessions Feed
        // ======================
        [HttpGet("GetAttendedSessions")]
        public async Task<IActionResult> GetAttendedSessions()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                return Json(new { error = "Student not logged in" });
            }

            try
            {
                var onlineSessions = await (
                    from oa in _context.OnlineAttends
                    where oa.StudentCode == studentCode.Value
                    join ls in _context.Lessons on oa.LessonCode equals ls.LessonCode into lj
                    from ls in lj.DefaultIfEmpty()
                    select new AttendedSessionDto
                    {
                        Source = "Online",
                        LessonCode = oa.LessonCode,
                        LessonName = (ls != null && ls.LessonName != null) ? ls.LessonName : "Unknown Lesson",
                        ClassName = null,
                        AttendedAt = oa.InsertTime
                    }
                ).ToListAsync();

                var offlineSessions = await (
                    from a in _context.Attends
                    where a.StudentId == studentCode.Value
                    join c in _context.Classes on a.ClassId equals c.ClassCode into cj
                    from c in cj.DefaultIfEmpty()
                    join ls in _context.Lessons on c.ClassLessonCode equals ls.LessonCode into lj
                    from ls in lj.DefaultIfEmpty()
                    select new AttendedSessionDto
                    {
                        Source = "Center",
                        LessonCode = c != null ? c.ClassLessonCode : (int?)null,
                        LessonName = (ls != null && ls.LessonName != null)
                                        ? ls.LessonName
                                        : (c != null ? (c.ClassName ?? "Unknown Class") : "Unknown Class"),
                        ClassName = c != null ? c.ClassName : null,
                        AttendedAt = a.AttendDate
                    }
                ).ToListAsync();

                var sessions = onlineSessions
                    .Concat(offlineSessions)
                    .OrderByDescending(s => s.AttendedAt)
                    .Take(200)
                    .Select(s => new
                    {
                        source = s.Source,
                        lessonCode = s.LessonCode,
                        lessonName = s.LessonName,
                        className = s.ClassName,
                        attendedAt = s.AttendedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    })
                    .ToList();

                return Json(new { count = sessions.Count, sessions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attended sessions for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return Json(new { error = ex.Message });
            }
        }

        // ==============================
        // HOMEWORK: per-subject sections
        // ==============================

        [HttpGet("Homework")]
        public async Task<IActionResult> Homework()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
            {
                _logger.LogWarning("Student not logged in, redirecting to login page at {DateTime}", DateTime.UtcNow);
                return Redirect("/StudentLogin");
            }

            try
            {
                _logger.LogInformation("Loading homework view for StudentCode={StudentCode} at {DateTime}",
                    studentCode.Value, DateTime.UtcNow);

                var dashboardData = await GetStudentDashboardData(studentCode.Value);
                return View("Homework", dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading homework view for StudentCode={StudentCode} at {DateTime}",
                    studentCode, DateTime.UtcNow);
                return View("Error");
            }
        }
        // List of subjects for which we will display homework sections.
        [HttpGet("GetHomeworkSubjects")]
        public async Task<IActionResult> GetHomeworkSubjects()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
                return Json(new { error = "Student not logged in" });

            try
            {
                var subjects = await _context.Learns
                    .Where(l => l.StudentCode == studentCode.Value && l.IsActive)
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .Include(l => l.YearCodeNavigation)
                    .Select(l => new
                    {
                        subjectCode = l.SubjectCode,
                        subjectName = l.SubjectCodeNavigation.SubjectName,
                        teacherCode = l.TeacherCode,
                        teacherName = l.TeacherCodeNavigation.TeacherName,
                        yearCode = l.YearCode,
                        yearName = l.YearCodeNavigation.YearName
                    })
                    .Distinct()
                    .OrderBy(x => x.subjectName)
                    .ToListAsync();

                return Json(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHomeworkSubjects failed");
                return Json(new { error = ex.Message });
            }
        }
        // ADD these members to your existing OnlineStudentController class

        // Replace your existing WatchVideo action with this implementation
        [HttpGet("WatchVideo/{fileCode:int}")]
        public async Task<IActionResult> WatchVideo(int fileCode)
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
                return Redirect("/StudentLogin");

            // Authorize: the student must have attended a class that maps to this video's lesson
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == studentCode.Value)
                .Select(a => a.ClassId)
                .ToListAsync();

            var hasAccess = await _context.Files
                .Where(f => f.FileCode == fileCode &&
                            f.IsActive &&
                            f.FileType == 1 &&
                            (f.IsOnlineLesson == false)) // offline-accessible videos only
                .Join(_context.Classes.Where(c => attendedClassCodes.Contains(c.ClassCode)),
                      f => f.LessonCode,
                      c => c.ClassLessonCode,
                      (f, c) => f)
                .AnyAsync();

            if (!hasAccess)
                return Forbid("Access denied to this video.");

            var videoFile = await _context.Files
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.SubjectCodeNavigation)
                .Include(f => f.LessonCodeNavigation)
                    .ThenInclude(l => l.TeacherCodeNavigation)
                .FirstOrDefaultAsync(f => f.FileCode == fileCode);

            if (videoFile == null)
                return NotFound("Video not found.");

            ViewBag.VideoFile = videoFile;
            ViewBag.FileCode = fileCode;

            // IMPORTANT: this must match your actual view file name Views/OnlineStudent/video_viewer.cshtml
            return View("WatchVideo");
        }

        [HttpGet("GetSecureVideoUrl")]
        public async Task<IActionResult> GetSecureVideoUrl(int fileCode)
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
                return Unauthorized("Student not found");

            // Verify access same as above
            var attendedClassCodes = await _context.Attends
                .Where(a => a.StudentId == studentCode.Value)
                .Select(a => a.ClassId)
                .ToListAsync();

            var videoFile = await _context.Files
                .Where(f => f.FileCode == fileCode &&
                            f.IsActive &&
                            f.FileType == 1 &&
                            (f.IsOnlineLesson == false || f.IsOnlineLesson == null))
                .Join(_context.Classes.Where(c => attendedClassCodes.Contains(c.ClassCode)),
                      f => f.LessonCode,
                      c => c.ClassLessonCode,
                      (f, c) => f)
                .Include(f => f.LessonCodeNavigation)
                .FirstOrDefaultAsync();

            if (videoFile == null)
                return NotFound("Video not found or access denied");

            // Decrypt to original URL (same logic used in StudentController)
            var originalUrl = DecryptString(videoFile.FileLocation);
            var expiryHours = videoFile.LessonCodeNavigation?.LessonExpireDays.HasValue == true
                ? videoFile.LessonCodeNavigation.LessonExpireDays.Value * 24
                : 24;

            return Json(new
            {
                secureUrl = originalUrl,
                displayName = videoFile.DisplayName,
                duration = videoFile.Duration?.ToString(@"hh\:mm\:ss"),
                provider = videoFile.VideoProvider == 0 ? "YouTube" : "Bunny CDN",
                expiryTime = DateTime.Now.AddHours(expiryHours)
            });
        }

        // Reuse the same decrypt helper from StudentController
        private string DecryptString(string encryptedText)
        {
            var data = Convert.FromBase64String(encryptedText ?? "");
            return System.Text.Encoding.UTF8.GetString(data);
        }

        // Helper to resolve latest attended lesson code for a subject (same subject + student's year)
        private async Task<int?> GetLatestLessonCodeForSubject(int studentId, int subjectCode)
        {
            // Get student's year to constrain within the same academic year
            var stu = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentCode == studentId);
            int? studentYear = stu?.YearCode;

            var latest = await (
                from a in _context.Attends
                join c in _context.Classes on a.ClassId equals c.ClassCode
                where a.StudentId == studentId
                      && c.SubjectCode == subjectCode
                      && (!studentYear.HasValue || c.YearCode == studentYear.Value)
                orderby a.AttendDate descending
                select c.ClassLessonCode
            ).FirstOrDefaultAsync();

            return latest;
        }

        // Videos for latest attended lesson in a subject (FileType==1, IsOnlineLesson==false)
        [HttpGet("GetLatestSubjectVideos")]
        public async Task<IActionResult> GetLatestSubjectVideos(int subjectCode)
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
                return Json(new List<object>());

            try
            {
                var latestLesson = await GetLatestLessonCodeForSubject(studentCode.Value, subjectCode);
                if (!latestLesson.HasValue)
                    return Json(new List<object>());

                var videos = await _context.Files
                    .Where(f => f.LessonCode == latestLesson.Value
                                && f.IsActive
                                && f.FileType == 1
                                && (f.IsOnlineLesson == false)) // only offline-accessible lesson content
                    .Include(f => f.LessonCodeNavigation)
                        .ThenInclude(l => l.SubjectCodeNavigation)
                    .Include(f => f.LessonCodeNavigation)
                        .ThenInclude(l => l.TeacherCodeNavigation)
                    .OrderByDescending(f => f.InsertTime)
                    .Select(f => new
                    {
                        fileCode = f.FileCode,
                        displayName = f.DisplayName ?? "Video",
                        fileLocation = f.FileLocation,
                        insertTime = f.InsertTime,
                        lessonName = f.LessonCodeNavigation.LessonName,
                        subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                        teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName,
                        duration = f.Duration
                    })
                    .ToListAsync();

                return Json(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestSubjectVideos failed for Student={Student}, Subject={Subject}", studentCode, subjectCode);
                return Json(new { error = ex.Message });
            }
        }

        // Downloads for latest attended lesson in a subject (FileType==2, IsOnlineLesson false/null)
        [HttpGet("GetLatestSubjectDownloads")]
        public async Task<IActionResult> GetLatestSubjectDownloads(int subjectCode)
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
                return Json(new List<object>());

            try
            {
                var latestLesson = await GetLatestLessonCodeForSubject(studentCode.Value, subjectCode);
                if (!latestLesson.HasValue)
                    return Json(new List<object>());

                var files = await _context.Files
                    .Where(f => f.LessonCode == latestLesson.Value
                                && f.IsActive
                                && f.FileType == 2
                                && (f.IsOnlineLesson == false || f.IsOnlineLesson == null))
                    .OrderByDescending(f => f.InsertTime)
                    .Select(f => new
                    {
                        fileCode = f.FileCode,
                        displayName = f.DisplayName ?? "Downloadable File",
                        fileLocation = f.FileLocation,
                        insertTime = f.InsertTime,
                        lessonName = f.LessonCodeNavigation.LessonName,
                        subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                        teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName
                    })
                    .ToListAsync();

                return Json(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestSubjectDownloads failed for Student={Student}, Subject={Subject}", studentCode, subjectCode);
                return Json(new { error = ex.Message });
            }
        }

        // Assignments for latest attended lesson in a subject (FileType 4 or 5, IsOnlineLesson false/null, must have ExamCode)
        [HttpGet("GetLatestSubjectAssignments")]
        public async Task<IActionResult> GetLatestSubjectAssignments(int subjectCode)
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            if (!studentCode.HasValue)
                return Json(new List<object>());

            try
            {
                var latestLesson = await GetLatestLessonCodeForSubject(studentCode.Value, subjectCode);
                if (!latestLesson.HasValue)
                    return Json(new List<object>());

                var assignmentFiles = await _context.Files
                    .Where(f => f.LessonCode == latestLesson.Value
                                && f.IsActive
                                && (f.IsOnlineLesson == false || f.IsOnlineLesson == null)
                                && (f.FileType == 4 || f.FileType == 5)
                                && f.ExamCode.HasValue)
                    .Include(f => f.LessonCodeNavigation)
                        .ThenInclude(l => l.SubjectCodeNavigation)
                    .Include(f => f.LessonCodeNavigation)
                        .ThenInclude(l => l.TeacherCodeNavigation)
                    .OrderByDescending(f => f.InsertTime)
                    .ToListAsync();

                var assignmentExamCodes = assignmentFiles.Select(f => f.ExamCode.Value).Distinct().ToList();

                var assignmentsData = await _context.Exams
                    .Where(e => assignmentExamCodes.Contains(e.ExamCode) && e.IsActive == true)
                    .ToListAsync();

                var attendedAssignmentCodes = await _context.StudentExams
                    .Where(se => se.StudentCode == studentCode.Value && assignmentExamCodes.Contains(se.ExamCode) && se.IsActive == true)
                    .Select(se => se.ExamCode)
                    .ToListAsync();

                var result = assignmentFiles.Select(f =>
                {
                    var assignment = assignmentsData.FirstOrDefault(a => a.ExamCode == f.ExamCode);
                    bool attended = attendedAssignmentCodes.Contains(f.ExamCode.Value);

                    return new
                    {
                        examCode = f.ExamCode.Value,
                        examName = assignment?.ExamName ?? f.DisplayName ?? "Unknown Assignment",
                        fileCode = f.FileCode,
                        displayName = assignment?.ExamName ?? f.DisplayName,
                        fileType = f.FileType,
                        fileTypeName = f.FileType == 4 ? "Assignment" : "Assignment Video",
                        duration = f.Duration,
                        durationFormatted = f.Duration.HasValue ? f.Duration.Value.ToString(@"hh\:mm\:ss") : null,
                        sortOrder = f.SortOrder,
                        insertTime = f.InsertTime,
                        isActive = f.IsActive,
                        lessonCode = f.LessonCode,
                        lessonName = f.LessonCodeNavigation.LessonName,
                        subjectName = f.LessonCodeNavigation.SubjectCodeNavigation.SubjectName,
                        teacherName = f.LessonCodeNavigation.TeacherCodeNavigation.TeacherName,
                        attended = attended
                    };
                }).ToList();

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestSubjectAssignments failed for Student={Student}, Subject={Subject}", studentCode, subjectCode);
                return Json(new { error = ex.Message });
            }
        }

        public class StudentDashboardViewModel
        {
            public Student Student { get; set; }
            public List<Learn> EnrolledSubjects { get; set; } = new();
            public List<StudentExam> AttendedExams { get; set; } = new();
            public List<StudentPlan> ActivePlans { get; set; } = new();
            public int TotalAttendanceCount { get; set; }
            public int RecentAttendanceCount { get; set; }
            public bool IsSubscribed { get; set; }
        }

        private class AttendedSessionDto
        {
            public string Source { get; set; } = "";
            public int? LessonCode { get; set; }
            public string LessonName { get; set; } = "";
            public string? ClassName { get; set; }
            public DateTime AttendedAt { get; set; }
        }
    }
}