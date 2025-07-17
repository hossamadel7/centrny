using centrny.Attributes;
using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace centrny.Controllers
{
    [RequirePageAccess("Exam")]
    public class ExamController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<ExamController> _logger;

        public ExamController(CenterContext context, ILogger<ExamController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==================== HELPER METHODS ====================




        /// <summary>
        /// DEBUG: Temporary method to check page access logic
        /// Add this to ExamController temporarily
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DebugPageAccess()
        {
            try
            {
                _logger.LogInformation("=== DEBUG PAGE ACCESS ===");

                // Check what pages exist that might match "Exam"
                var matchingPages = await _context.Pages
                    .Where(p =>
                        p.PagePath.ToLower() == "exam" ||
                        p.PageName.ToLower() == "exam" ||
                        p.PagePath.ToLower().Contains("exam") ||
                        "exam".Contains(p.PagePath.ToLower()) ||
                        p.PageName.ToLower().Contains("exam") ||
                        "exam".Contains(p.PageName.ToLower()))
                    .Select(p => new {
                        p.PageCode,
                        p.PageName,
                        p.PagePath,
                        p.ModuleCode,
                        MatchType =
                            p.PagePath.ToLower() == "exam" ? "Exact PagePath" :
                            p.PageName.ToLower() == "exam" ? "Exact PageName" :
                            p.PagePath.ToLower().Contains("exam") ? "PagePath Contains Exam" :
                            "exam".Contains(p.PagePath.ToLower()) ? "Exam Contains PagePath" :
                            p.PageName.ToLower().Contains("exam") ? "PageName Contains Exam" :
                            "exam".Contains(p.PageName.ToLower()) ? "Exam Contains PageName" : "Unknown"
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} potentially matching pages", matchingPages.Count);

                foreach (var page in matchingPages)
                {
                    _logger.LogInformation("Page: Code={PageCode}, Name='{PageName}', Path='{PagePath}', Match='{MatchType}'",
                        page.PageCode, page.PageName, page.PagePath, page.MatchType);
                }

                // Check user's group permissions
                var groupCodeClaim = User.FindFirst("GroupCode");
                if (groupCodeClaim != null && int.TryParse(groupCodeClaim.Value, out int groupCode))
                {
                    _logger.LogInformation("User GroupCode: {GroupCode}", groupCode);

                    foreach (var page in matchingPages)
                    {
                        var groupPage = await _context.GroupPages
                            .FirstOrDefaultAsync(gp => gp.GroupCode == groupCode && gp.PageCode == page.PageCode);

                        if (groupPage != null)
                        {
                            _logger.LogInformation("Permissions for Page {PageCode}: Insert={Insert}, Update={Update}, Delete={Delete}",
                                page.PageCode, groupPage.InsertFlag, groupPage.UpdateFlag, groupPage.DeleteFlag);
                        }
                        else
                        {
                            _logger.LogInformation("No permissions found for Page {PageCode}", page.PageCode);
                        }
                    }
                }

                return Json(new
                {
                    matchingPages = matchingPages,
                    userGroupCode = groupCodeClaim?.Value,
                    userName = User.Identity?.Name,
                    isAuthenticated = User.Identity?.IsAuthenticated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DebugPageAccess");
                return Json(new { error = ex.Message });
            }
        }
        /// <summary>
        /// Gets the current user's RootCode from claims - REQUIRED for all users
        /// </summary>
        /// 

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
        /// Checks if current user is a center (not a teacher)
        /// </summary>
        private bool IsCurrentUserCenter()
        {
            var isCenterClaim = User.FindFirst("IsCenter");
            return isCenterClaim?.Value == "True";
        }

        /// <summary>
        /// Gets the current user's ID for audit fields
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("NameIdentifier");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return 1; // Fallback
        }

        /// <summary>
        /// Gets user context information
        /// </summary>
        private async Task<(int? rootCode, string rootName, bool isCenter)> GetUserContext()
        {
            var rootCode = GetCurrentUserRootCode();
            var rootName = User.FindFirst("RootName")?.Value ?? "Unknown";
            var isCenter = IsCurrentUserCenter();

            return (rootCode, rootName, isCenter);
        }

        private TimeOnly ParseTimeOnly(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString))
                throw new ArgumentException("Time string cannot be null or empty");

            timeString = timeString.Replace("ص", "").Replace("م", "").Trim();
            timeString = timeString.Replace("AM", "").Replace("PM", "").Replace("am", "").Replace("pm", "").Trim();

            if (TimeOnly.TryParse(timeString, out var timeOnly))
                return timeOnly;

            if (TimeSpan.TryParse(timeString, out var timeSpan))
                return new TimeOnly(timeSpan.Hours, timeSpan.Minutes);

            string[] formats = { "HH:mm", "H:mm", "hh:mm", "h:mm" };
            foreach (var format in formats)
                if (TimeOnly.TryParseExact(timeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnly))
                    return timeOnly;

            throw new FormatException($"Unable to parse time string: '{timeString}'");
        }

        // ==================== MAIN ACTIONS ====================

        /// <summary>
        /// GET: Exam - Main page
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var (rootCode, rootName, isCenter) = await GetUserContext();

            if (!rootCode.HasValue)
            {
                ViewBag.Error = "Unable to determine your root assignment. Please contact administrator.";
                return View();
            }

            ViewBag.CurrentUserRootCode = rootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            _logger.LogInformation("Loading Exam index for user {Username} (Root: {RootCode})",
                User.Identity?.Name, rootCode);

            return View();
        }

        // ==================== API ENDPOINTS ====================

        [HttpGet]
        public async Task<IActionResult> GetCentersByRootCode()
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var centers = await _context.Centers
                    .Where(c => c.RootCode == rootCode.Value && c.IsActive)
                    .Select(c => new { value = c.CenterCode, text = c.CenterName })
                    .ToListAsync();

                return Json(centers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting centers for user {Username}", User.Identity?.Name);
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByCenter(int centerCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var branches = await _context.Branches
                    .Where(b => b.CenterCode == centerCode && b.RootCode == rootCode.Value && b.IsActive)
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

        [HttpGet]
        public async Task<IActionResult> GetTeacherByRoot()
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(null);
                }

                var teacher = await _context.Teachers
                    .Where(t => t.RootCode == rootCode.Value && t.IsActive)
                    .FirstOrDefaultAsync();

                if (teacher == null)
                    return Json(null);

                return Json(new { value = teacher.TeacherCode, text = teacher.TeacherName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teacher for user {Username}", User.Identity?.Name);
                return Json(null);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllExams()
        {
            try
            {
                var (rootCode, _, isCenter) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var exams = await (from e in _context.Exams
                                   where e.IsActive == true
                                   join edu in _context.EduYears on e.EduYearCode equals edu.EduCode
                                   join teacher in _context.Teachers on e.TeacherCode equals teacher.TeacherCode
                                   // Only exams where both the teacher and the eduyear belong to the current root
                                   where edu.RootCode == rootCode.Value && teacher.RootCode == rootCode.Value
                                   join subject in _context.Subjects on e.SubjectCode equals subject.SubjectCode into subjectGroup
                                   from subject in subjectGroup.DefaultIfEmpty()
                                   join year in _context.Years on e.YearCode equals year.YearCode into yearGroup
                                   from year in yearGroup.DefaultIfEmpty()
                                   join branch in _context.Branches on e.BranchCode equals branch.BranchCode into branchGroup
                                   from branch in branchGroup.DefaultIfEmpty()
                                   select new
                                   {
                                       examCode = e.ExamCode,
                                       examName = e.ExamName,
                                       examDegree = e.ExamDegree,
                                       averageMarks = _context.StudentExams
                                           .Where(se => se.ExamCode == e.ExamCode)
                                           .Any()
                                           ? (double)_context.StudentExams.Where(se => se.ExamCode == e.ExamCode).Sum(se => se.StudentResult ?? 0)
                                               / _context.StudentExams.Where(se => se.ExamCode == e.ExamCode).Count()
                                           : 0,
                                       examPercentage = e.ExamSuccessPercent,
                                       examTimer = e.ExamTimer.ToString(@"HH\:mm"),
                                       examDurationMinutes = e.ExamTimer.Hour * 60 + e.ExamTimer.Minute,
                                       isDone = e.IsDone,
                                       isExam = e.IsExam,
                                       isOnline = e.IsOnline,
                                       teacherCode = e.TeacherCode,
                                       teacherName = teacher.TeacherName,
                                       subjectCode = e.SubjectCode,
                                       subjectName = subject != null ? subject.SubjectName : "Unknown Subject",
                                       branchCode = e.BranchCode,
                                       branchName = branch != null ? branch.BranchName : "Unknown Branch",
                                       yearCode = e.YearCode,
                                       yearName = year != null ? year.YearName : "Unknown Year",
                                       eduYearCode = e.EduYearCode,
                                       eduYearName = edu.EduName,
                                       insertUser = e.InsertUser,
                                       insertTime = e.InserTime
                                   }).ToListAsync();

                _logger.LogInformation("Loaded {Count} exams for user {Username} (Root: {RootCode})",
                    exams.Count, User.Identity?.Name, rootCode);

                return Json(exams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading exams for user {Username}", User.Identity?.Name);
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetExam(int id)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return NotFound();
                }

                var exam = await (from e in _context.Exams
                                  join teacher in _context.Teachers on e.TeacherCode equals teacher.TeacherCode
                                  join eduYear in _context.EduYears on e.EduYearCode equals eduYear.EduCode
                                  where e.ExamCode == id && e.IsActive == true
                                        && teacher.RootCode == rootCode.Value
                                        && eduYear.RootCode == rootCode.Value
                                  select e).FirstOrDefaultAsync();

                if (exam == null)
                    return NotFound();

                return Json(new
                {
                    examCode = exam.ExamCode,
                    examName = exam.ExamName,
                    examDegree = exam.ExamDegree,
                    examResult = exam.ExamAverageMark,
                    examPercentage = exam.ExamSuccessPercent,
                    examTimer = exam.ExamTimer.ToString(@"HH\:mm"),
                    examDurationMinutes = exam.ExamTimer.Hour * 60 + exam.ExamTimer.Minute,
                    isDone = exam.IsDone,
                    isExam = exam.IsExam,
                    isOnline = exam.IsOnline,
                    teacherCode = exam.TeacherCode,
                    subjectCode = exam.SubjectCode,
                    branchCode = exam.BranchCode,
                    yearCode = exam.YearCode,
                    eduYearCode = exam.EduYearCode,
                    insertUser = exam.InsertUser,
                    insertTime = exam.InserTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam {ExamId}", id);
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetExamStats(int examCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine root assignment" });
                }

                var exam = await (from e in _context.Exams
                                  join teacher in _context.Teachers on e.TeacherCode equals teacher.TeacherCode
                                  join eduYear in _context.EduYears on e.EduYearCode equals eduYear.EduCode
                                  where e.ExamCode == examCode && e.IsActive == true
                                        && teacher.RootCode == rootCode.Value
                                        && eduYear.RootCode == rootCode.Value
                                  select e).FirstOrDefaultAsync();

                if (exam == null)
                    return Json(new { success = false, error = "Exam not found" });

                // Get eligible students
                var eligibleStudentCodes = await _context.Learns
                    .Where(l => l.TeacherCode == exam.TeacherCode &&
                               l.SubjectCode == exam.SubjectCode &&
                               l.BranchCode == exam.BranchCode &&
                               l.EduYearCode == exam.EduYearCode &&
                               l.RootCode == rootCode.Value &&
                               l.IsActive)
                    .Select(l => l.StudentCode)
                    .ToListAsync();

                // Get students who took exam
                var studentExams = await _context.StudentExams
                    .Where(se => se.ExamCode == exam.ExamCode && eligibleStudentCodes.Contains(se.StudentCode))
                    .ToListAsync();

                int tookExam = studentExams.Select(se => se.StudentCode).Distinct().Count();
                int didntTakeExam = eligibleStudentCodes.Count - tookExam;

                // Calculate success percent (students with result >= 50% of degree)
                int successCount = studentExams.Count(se =>
                    se.StudentResult.HasValue &&
                    exam.ExamDegree != null &&
                    int.TryParse(exam.ExamDegree, out int deg) &&
                    deg > 0 &&
                    se.StudentResult.Value >= deg * 0.5);

                double successPercent = tookExam > 0 ? ((double)successCount * 100) / tookExam : 0.0;

                // Calculate average marks for students who took exam
                double avgMarks = studentExams.Count > 0
                    ? studentExams.Average(se => se.StudentResult ?? 0)
                    : 0.0;

                return Json(new
                {
                    success = true,
                    examCode = exam.ExamCode,
                    examName = exam.ExamName,
                    numberTookExam = tookExam,
                    numberDidNotTakeExam = didntTakeExam,
                    examPercentage = Math.Round(successPercent, 1), // Success percent
                    averageMarks = Math.Round(avgMarks, 1)          // Average marks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam stats for exam {ExamCode}", examCode);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Exam", "insert")]
        public async Task<IActionResult> AddExam([FromBody] Exam exam)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return BadRequest(new { success = false, error = "Unable to determine root assignment" });
                }

                if (string.IsNullOrWhiteSpace(exam.ExamName))
                    return BadRequest(new { success = false, error = "Exam Name is required." });
                if (exam.TeacherCode <= 0)
                    return BadRequest(new { success = false, error = "Teacher must be set." });
                if (exam.SubjectCode <= 0)
                    return BadRequest(new { success = false, error = "Subject must be selected." });
                if (exam.YearCode == null || exam.YearCode <= 0)
                    return BadRequest(new { success = false, error = "Year must be selected." });
                if (exam.BranchCode <= 0)
                    return BadRequest(new { success = false, error = "Branch must be selected." });

                // Validate that teacher and eduYear belong to user's root
                var teacherExists = await _context.Teachers.AnyAsync(t => t.TeacherCode == exam.TeacherCode && t.RootCode == rootCode.Value);
                var eduYearExists = await _context.EduYears.AnyAsync(e => e.EduCode == exam.EduYearCode && e.RootCode == rootCode.Value);
                var branchExists = await _context.Branches.AnyAsync(b => b.BranchCode == exam.BranchCode && b.RootCode == rootCode.Value);

                if (!teacherExists)
                    return BadRequest(new { success = false, error = "Selected teacher does not belong to your root." });
                if (!eduYearExists)
                    return BadRequest(new { success = false, error = "Selected education year does not belong to your root." });
                if (!branchExists)
                    return BadRequest(new { success = false, error = "Selected branch does not belong to your root." });

                string timerString = exam.ExamTimer.ToString();
                exam.ExamTimer = ParseTimeOnly(timerString);

                // Initial dummy values; will be set correctly in SetExamQuestions
                exam.ExamDegree = "1";
                exam.ExamAverageMark = "1";
                exam.ExamSuccessPercent = "1";
                exam.IsDone = false;
                exam.IsActive = true;
                exam.InsertUser = GetCurrentUserId();
                exam.InserTime = DateTime.Now;

                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created exam {ExamName} (ID: {ExamId}) by user {Username}",
                    exam.ExamName, exam.ExamCode, User.Identity?.Name);

                return Json(new { success = true, examCode = exam.ExamCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding exam for user {Username}", User.Identity?.Name);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Exam", "update")]
        public async Task<IActionResult> EditExam([FromBody] Exam exam)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return BadRequest(new { success = false, error = "Unable to determine root assignment" });
                }

                if (exam.TeacherCode == 0)
                    return BadRequest(new { success = false, error = "Teacher is required." });
                if (exam.SubjectCode == 0)
                    return BadRequest(new { success = false, error = "Subject is required." });
                if (exam.YearCode == null || exam.YearCode == 0)
                    return BadRequest(new { success = false, error = "Year is required." });
                if (exam.BranchCode == 0)
                    return BadRequest(new { success = false, error = "Branch is required." });

                var dbExam = await (from e in _context.Exams
                                    join teacher in _context.Teachers on e.TeacherCode equals teacher.TeacherCode
                                    join eduYear in _context.EduYears on e.EduYearCode equals eduYear.EduCode
                                    where e.ExamCode == exam.ExamCode && e.IsActive == true
                                          && teacher.RootCode == rootCode.Value
                                          && eduYear.RootCode == rootCode.Value
                                    select e).FirstOrDefaultAsync();

                if (dbExam == null)
                    return NotFound(new { success = false, error = "Exam not found or access denied." });

                dbExam.ExamName = exam.ExamName;
                string timerString = exam.ExamTimer.ToString();
                dbExam.ExamTimer = ParseTimeOnly(timerString);
                dbExam.TeacherCode = exam.TeacherCode;
                dbExam.SubjectCode = exam.SubjectCode;
                dbExam.YearCode = exam.YearCode;
                dbExam.BranchCode = exam.BranchCode;
                dbExam.EduYearCode = exam.EduYearCode;
                dbExam.IsExam = exam.IsExam;
                dbExam.IsOnline = exam.IsOnline;
                dbExam.IsDone = exam.IsDone;
                dbExam.LastUpdateUser = GetCurrentUserId();
                dbExam.LastUpdateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated exam {ExamId} by user {Username}", exam.ExamCode, User.Identity?.Name);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing exam {ExamId} for user {Username}", exam.ExamCode, User.Identity?.Name);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Exam", "delete")]
        public async Task<IActionResult> DeleteExam([FromBody] int examCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine root assignment" });
                }

                var exam = await (from e in _context.Exams
                                  join teacher in _context.Teachers on e.TeacherCode equals teacher.TeacherCode
                                  join eduYear in _context.EduYears on e.EduYearCode equals eduYear.EduCode
                                  where e.ExamCode == examCode
                                        && teacher.RootCode == rootCode.Value
                                        && eduYear.RootCode == rootCode.Value
                                  select e).FirstOrDefaultAsync();

                if (exam == null)
                    return Json(new { success = false, error = "Exam not found or access denied." });

                exam.IsActive = false;
                exam.LastUpdateUser = GetCurrentUserId();
                exam.LastUpdateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted exam {ExamId} by user {Username}", examCode, User.Identity?.Name);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting exam {ExamId} for user {Username}", examCode, User.Identity?.Name);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEduYears(string rootCode)
        {
            try
            {
                var (userRootCode, _, _) = await GetUserContext();
                if (!userRootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                // Always use the user's actual root, ignore the parameter
                var eduYears = await _context.EduYears
                    .Where(e => e.RootCode == userRootCode.Value && e.IsActive)
                    .Select(e => new { value = e.EduCode, text = e.EduName })
                    .ToListAsync();

                return Json(eduYears);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting edu years for user {Username}", User.Identity?.Name);
                return Json(new List<object>());
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetFilterYears()
        {
            var (rootCode, _, _) = await GetUserContext();
            if (!rootCode.HasValue) return Json(new List<object>());

            // Get all EduYear codes that are active and belong to this root
            var activeEduYearCodes = await _context.EduYears
                .Where(e => e.RootCode == rootCode.Value && e.IsActive)
                .Select(e => e.EduCode)
                .ToListAsync();

            // Return all Years for this root that are associated with an active EduYear
            var years = await (from y in _context.Years
                               join edu in _context.EduYears on y.EduYearCode equals edu.EduCode
                               where edu.RootCode == rootCode.Value
                                     && edu.IsActive
                               select new { value = y.YearCode, text = y.YearName })
                   .OrderBy(y => y.text)
                   .ToListAsync();

            return Json(years);
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersByEduYear(int eduYearCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var teacherCodes = await _context.Teaches
                    .Where(t => t.EduYearCode == eduYearCode && t.RootCode == rootCode.Value && t.IsActive)
                    .Select(t => t.TeacherCode)
                    .Distinct()
                    .ToListAsync();

                var teachers = await _context.Teachers
                    .Where(t => teacherCodes.Contains(t.TeacherCode) && t.RootCode == rootCode.Value && t.IsActive)
                    .Select(t => new { value = t.TeacherCode, text = t.TeacherName })
                    .ToListAsync();

                return Json(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teachers by edu year {EduYearCode}", eduYearCode);
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjectsByTeacherAndEduYear(int teacherCode, int eduYearCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var subjectCodes = await _context.Teaches
                    .Where(t => t.TeacherCode == teacherCode && t.EduYearCode == eduYearCode && t.RootCode == rootCode.Value && t.IsActive)
                    .Select(t => t.SubjectCode)
                    .Distinct()
                    .ToListAsync();

                var subjects = await _context.Subjects
                    .Where(s => subjectCodes.Contains(s.SubjectCode))
                    .Select(s => new { value = s.SubjectCode, text = s.SubjectName })
                    .ToListAsync();

                return Json(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subjects by teacher {TeacherCode} and edu year {EduYearCode}",
                    teacherCode, eduYearCode);
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetYearsByTeacherEduYearSubject(int teacherCode, int eduYearCode, int subjectCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var yearCodes = await _context.Teaches
                    .Where(t => t.TeacherCode == teacherCode && t.EduYearCode == eduYearCode &&
                               t.SubjectCode == subjectCode && t.RootCode == rootCode.Value && t.IsActive)
                    .Select(t => t.YearCode)
                    .Distinct()
                    .ToListAsync();

                var years = await _context.Years
                    .Where(y => yearCodes.Contains(y.YearCode))
                    .Select(y => new { value = y.YearCode, text = y.YearName })
                    .ToListAsync();

                return Json(years);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting years by teacher {TeacherCode}, edu year {EduYearCode}, subject {SubjectCode}",
                    teacherCode, eduYearCode, subjectCode);
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesByAll(int teacherCode, int eduYearCode, int subjectCode, int yearCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new List<object>());
                }

                var branchCodes = await _context.Teaches
                    .Where(t => t.TeacherCode == teacherCode && t.EduYearCode == eduYearCode &&
                               t.SubjectCode == subjectCode && t.YearCode == yearCode &&
                               t.RootCode == rootCode.Value && t.IsActive)
                    .Select(t => t.BranchCode)
                    .Distinct()
                    .ToListAsync();

                var branches = await _context.Branches
                    .Where(b => branchCodes.Contains(b.BranchCode) && b.RootCode == rootCode.Value && b.IsActive)
                    .Select(b => new { value = b.BranchCode, text = b.BranchName })
                    .ToListAsync();

                return Json(branches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branches by all parameters");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetExamQuestions(int examCode, int teacherCode, int subjectCode, int yearCode)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new { chosen = new object[0], available = new object[0], chosenFlat = new object[0], availableFlat = new object[0] });
                }

                var exam = await (from e in _context.Exams
                                  join teacher in _context.Teachers on e.TeacherCode equals teacher.TeacherCode
                                  join eduYear in _context.EduYears on e.EduYearCode equals eduYear.EduCode
                                  where e.ExamCode == examCode && e.IsActive == true
                                        && teacher.RootCode == rootCode.Value
                                        && eduYear.RootCode == rootCode.Value
                                  select e).FirstOrDefaultAsync();

                if (exam == null)
                {
                    return Json(new { chosen = new object[0], available = new object[0], chosenFlat = new object[0], availableFlat = new object[0] });
                }

                // Filter lessons by teacherCode, subjectCode, yearCode
                var teacherLessons = await _context.Lessons
                    .Where(l => l.TeacherCode == teacherCode
                                && l.SubjectCode == subjectCode
                                && l.YearCode == yearCode
                                && l.RootCode == rootCode.Value)
                    .Select(l => new {
                        l.LessonCode,
                        l.LessonName,
                        l.ChapterCode,
                        l.TeacherCode
                    })
                    .ToListAsync();

                var lessonCodes = teacherLessons.Select(l => l.LessonCode).ToList();
                var chapters = teacherLessons
                    .Where(l => l.ChapterCode == null)
                    .ToDictionary(l => l.LessonCode, l => l.LessonName ?? "Unnamed Chapter");

                // Get chosen questions for the exam
                var chosen = await (from eq in _context.ExamQuestions
                                    join q in _context.Questions on eq.QuestionCode equals q.QuestionCode
                                    join lesson in _context.Lessons on q.LessonCode equals lesson.LessonCode into lessonGroup
                                    from lesson in lessonGroup.DefaultIfEmpty()
                                    where eq.ExamCode == examCode && eq.IsActive == true
                                    select new
                                    {
                                        QuestionCode = q.QuestionCode,
                                        QuestionContent = q.QuestionContent,
                                        QuestionDegree = eq.QuestionDegree,
                                        LessonCode = lesson != null ? lesson.LessonCode : 0,
                                        LessonName = lesson != null ? lesson.LessonName : "Unknown Lesson",
                                        ChapterCode = lesson != null ? lesson.ChapterCode : null,
                                        ChapterName = lesson != null && lesson.ChapterCode != null && chapters.ContainsKey(lesson.ChapterCode.Value)
                                                      ? chapters[lesson.ChapterCode.Value]
                                                      : "Unknown Chapter"
                                    }).ToListAsync();

                var chosenCodes = chosen.Select(q => q.QuestionCode).ToList();

                // Only available questions for those lesson codes, and not already chosen
                var availableData = await (from q in _context.Questions
                                           join lesson in _context.Lessons on q.LessonCode equals lesson.LessonCode into lessonGroup
                                           from lesson in lessonGroup.DefaultIfEmpty()
                                           where lessonCodes.Contains(q.LessonCode ?? 0) && !chosenCodes.Contains(q.QuestionCode)
                                           select new
                                           {
                                               QuestionCode = q.QuestionCode,
                                               QuestionContent = q.QuestionContent,
                                               LessonCode = lesson != null ? lesson.LessonCode : 0,
                                               LessonName = lesson != null ? lesson.LessonName : "Unknown Lesson",
                                               ChapterCode = lesson != null ? lesson.ChapterCode : null,
                                               ChapterName = lesson != null && lesson.ChapterCode != null && chapters.ContainsKey(lesson.ChapterCode.Value)
                                                             ? chapters[lesson.ChapterCode.Value]
                                                             : "Unknown Chapter"
                                           }).ToListAsync();

                return Json(new
                {
                    chosen = chosen,
                    available = availableData,
                    chosenFlat = chosen,
                    availableFlat = availableData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam questions for exam {ExamCode}", examCode);
                return Json(new { chosen = new object[0], available = new object[0], chosenFlat = new object[0], availableFlat = new object[0] });
            }
        }

        [HttpPost]
        [RequirePageAccess("Exam", "update")]
        public async Task<IActionResult> SetExamQuestions([FromBody] SetExamQuestionsModel model)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue)
                {
                    return Json(new { success = false, error = "Unable to determine root assignment" });
                }

                if (model == null || model.Questions == null || model.Questions.Count == 0)
                    return Json(new { success = false, error = "Please select at least one question." });

                int examCode = model.ExamCode;
                int insertUserCode = GetCurrentUserId();
                var questions = model.Questions;

                // Verify exam belongs to user's root
                var exam = await (from e in _context.Exams
                                  join teacher in _context.Teachers on e.TeacherCode equals teacher.TeacherCode
                                  join eduYear in _context.EduYears on e.EduYearCode equals eduYear.EduCode
                                  where e.ExamCode == examCode && e.IsActive == true
                                        && teacher.RootCode == rootCode.Value
                                        && eduYear.RootCode == rootCode.Value
                                  select e).FirstOrDefaultAsync();

                if (exam == null)
                    return Json(new { success = false, error = "Exam not found or access denied." });

                var existingQuestions = await _context.ExamQuestions.Where(eq => eq.ExamCode == examCode).ToListAsync();
                if (existingQuestions.Any())
                    _context.ExamQuestions.RemoveRange(existingQuestions);

                int totalDegree = 0;
                foreach (var q in questions)
                {
                    _context.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamCode = examCode,
                        QuestionCode = q.QuestionCode,
                        QuestionDegree = q.QuestionDegree,
                        IsActive = true,
                        InsertUser = insertUserCode,
                        InsertTime = DateTime.Now
                    });
                    totalDegree += q.QuestionDegree;
                }

                // Update exam degree
                exam.ExamDegree = totalDegree.ToString();
                exam.LastUpdateUser = insertUserCode;
                exam.LastUpdateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Sync Student_Exam.Exam_Degree for all students of this exam
                await SyncAllStudentExamDegrees(examCode);

                _logger.LogInformation("Set {QuestionCount} questions for exam {ExamCode} with total degree {TotalDegree} by user {Username}",
                    questions.Count, examCode, totalDegree, User.Identity?.Name);

                return Json(new
                {
                    success = true,
                    message = $"Successfully saved {questions.Count} questions with total degree {totalDegree}",
                    totalQuestions = questions.Count,
                    totalDegree = totalDegree
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting exam questions for exam {ExamCode} by user {Username}",
                    model?.ExamCode, User.Identity?.Name);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // Helper: Update all Student_Exam.Exam_Degree for a given exam
        private async Task SyncAllStudentExamDegrees(int examCode)
        {
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.ExamCode == examCode);
            if (exam == null) return;

            var studentExams = await _context.StudentExams.Where(se => se.ExamCode == examCode).ToListAsync();
            foreach (var studentExam in studentExams)
            {
                if (int.TryParse(exam.ExamDegree, out int parsedDegree))
                {
                    studentExam.ExamDegree = parsedDegree;
                }
                else
                {
                    studentExam.ExamDegree = null;
                }
            }
            await _context.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IActionResult> SearchQuestions(string term)
        {
            try
            {
                var (rootCode, _, _) = await GetUserContext();
                if (!rootCode.HasValue || string.IsNullOrWhiteSpace(term))
                {
                    return Json(new List<object>());
                }

                var query = await (from q in _context.Questions
                                   join lesson in _context.Lessons on q.LessonCode equals lesson.LessonCode
                                   where lesson.RootCode == rootCode.Value
                                      && q.QuestionContent.Contains(term)
                                   select new
                                   {
                                       questionCode = q.QuestionCode,
                                       questionContent = q.QuestionContent,
                                       lessonName = lesson.LessonName,
                                       lessonCode = lesson.LessonCode
                                   }).Take(50).ToListAsync();

                return Json(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching questions for term {Term} by user {Username}", term, User.Identity?.Name);
                return Json(new List<object>());
            }
        }
    }

    // ==================== VIEW MODELS ====================

    public class ExamStatsViewModel
    {
        public int ExamCode { get; set; }
        public string ExamName { get; set; } = string.Empty;
        public int NumberTookExam { get; set; }
        public int NumberDidNotTakeExam { get; set; }
    }

    public class ExamQuestionItem
    {
        public int QuestionCode { get; set; }
        public int QuestionDegree { get; set; }
    }

    public class SetExamQuestionsModel
    {
        public int ExamCode { get; set; }
        public int InsertUserCode { get; set; }
        public List<ExamQuestionItem> Questions { get; set; } = new List<ExamQuestionItem>();
    }
}