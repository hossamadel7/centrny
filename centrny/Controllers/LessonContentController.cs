using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text;

namespace centrny.Controllers
{
    public enum LessonAccessResult
    {
        DirectAccess,    // Student can access directly
        RequirePIN,      // Student needs to enter PIN
        AccessDenied     // Student cannot access (PIN owned by another student)
    }

    public class LessonContentController : Controller
    {
        private readonly ILogger<LessonContentController> _logger;
        private readonly CenterContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly FileUploadSettings _fileUploadSettings;


        public LessonContentController(ILogger<LessonContentController> logger,
     CenterContext context,
     IWebHostEnvironment webHostEnvironment,
     IOptions<FileUploadSettings> fileUploadSettings)
        {
            _logger = logger; // ✅ add this line
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _fileUploadSettings = fileUploadSettings.Value;
        }


        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int? userCode, int? groupCode, int? rootCode) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                _context.Roots.Where(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", "")).FirstOrDefault().RootCode
            );
        }

        #region Teacher Interface (Management + Filters)

        // Main teacher page
        public async Task<IActionResult> Index()
        {
            var userCode = GetSessionInt("UserCode");
            var rootCode = GetSessionInt("RootCode");

            if (userCode == null || rootCode == null)
                return Unauthorized();

            ViewBag.UserCode = userCode.Value;
            ViewBag.RootCode = rootCode.Value;

            return View();
        }


        private async Task<(LessonAccessResult Result, string Message, string RedirectUrl)> CheckStudentLessonAccess(
            int studentCode, int lessonCode, int rootCode)
        {
            Console.WriteLine($"🔍 Checking seamless access for Student {studentCode}, Lesson {lessonCode}");

            // Case 1: Check if student is already linked with lesson via OnlineAttend
            var existingAttend = await _context.OnlineAttends
                .Include(oa => oa.PinCodeNavigation)
                .FirstOrDefaultAsync(oa => oa.StudentCode == studentCode &&
                                           oa.LessonCode == lessonCode &&
                                           oa.RootCode == rootCode);

            if (existingAttend != null)
            {
                Console.WriteLine($"📋 Found existing OnlineAttend record with PIN {existingAttend.PinCode}");

                // Check if PIN times > 0 (or pin still valid)
                var pin = existingAttend.PinCodeNavigation;
                if (pin == null)
                {
                    pin = await _context.Pins.FirstOrDefaultAsync(p => p.PinCode == existingAttend.PinCode);
                }

                if (pin != null && pin.Times >= 0) // Allow 0 or more (since student already has access)
                {
                    // Check expiry using OnlineAttend.ExpiryDate
                    if (existingAttend.ExpiryDate.HasValue)
                    {
                        var currentDate = DateOnly.FromDateTime(DateTime.Now);
                        if (currentDate <= existingAttend.ExpiryDate.Value)
                        {
                            Console.WriteLine($"✅ Direct access granted - valid until {existingAttend.ExpiryDate.Value}");
                            return (LessonAccessResult.DirectAccess,
                                   "Access granted",
                                   $"/LessonContent/ViewLesson?lessonCode={lessonCode}&pinCode={pin.Watermark}");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Access expired on {existingAttend.ExpiryDate.Value}");
                            return (LessonAccessResult.RequirePIN,
                                   "Previous access has expired. Please enter PIN again.",
                                   $"/LessonContent/StudentViewer?lessonCode={lessonCode}");
                        }
                    }
                }

                Console.WriteLine($"⚠️ Existing access invalid - PIN times: {pin?.Times}");
                return (LessonAccessResult.RequirePIN,
                       "Previous access no longer valid. Please enter PIN again.",
                       $"/LessonContent/StudentViewer?lessonCode={lessonCode}");
            }

            // Case 2 & 4: Check if student is linked with any PIN that belongs to another student
            var studentOwnedPins = await _context.Pins
                .Where(p => p.StudentCode.HasValue &&
                           p.StudentCode.Value != studentCode &&
                           p.RootCode == rootCode &&
                           p.IsActive == 1)
                .Join(_context.OnlineAttends,
                      p => p.PinCode,
                      oa => oa.PinCode,
                      (p, oa) => new { Pin = p, Attend = oa })
                .Where(joined => joined.Attend.StudentCode == studentCode)
                .Select(joined => joined.Pin)
                .ToListAsync();

            if (studentOwnedPins.Any())
            {
                Console.WriteLine($"❌ Student trying to use PIN owned by another student");
                return (LessonAccessResult.AccessDenied,
                       "Access denied. PIN belongs to another student.",
                       null);
            }

            // Case 3: Check if student is linked with PIN but not with this specific lesson
            var studentPinsWithoutThisLesson = await _context.OnlineAttends
                .Where(oa => oa.StudentCode == studentCode &&
                            oa.RootCode == rootCode &&
                            oa.LessonCode != lessonCode) // Different lesson
                .Include(oa => oa.PinCodeNavigation)
                .Select(oa => oa.PinCodeNavigation)
                .Where(p => p != null && p.Times > 0 && p.IsActive == 1)
                .ToListAsync();

            if (studentPinsWithoutThisLesson.Any())
            {
                Console.WriteLine($"📌 Student has valid PIN for other lessons, redirecting to PIN entry");
                return (LessonAccessResult.RequirePIN,
                       "Please enter your PIN to access this lesson.",
                       $"/LessonContent/StudentViewer?lessonCode={lessonCode}");
            }

            // Case 5: Student is not linked with any PIN
            Console.WriteLine($"🆕 Student not linked with any PIN, redirecting to PIN entry");
            return (LessonAccessResult.RequirePIN,
                   "Please enter PIN to access this lesson.",
                   $"/LessonContent/StudentViewer?lessonCode={lessonCode}");
        }
        private async Task<bool> CanStudentAttendLesson(int studentCode, int lessonCode, int rootCode)
        {
            // Query OnlineAttend for the student's attendance record for this lesson
            var attend = await _context.OnlineAttends
                .FirstOrDefaultAsync(oa => oa.StudentCode == studentCode
                                        && oa.LessonCode == lessonCode
                                        && oa.RootCode == rootCode
                                        && oa.Status == true);

            if (attend == null)
            {
                // No attendance record found (student has not attended before)
                return false;
            }

            // If expiry date is not set, treat as unlimited access (can attend)
            if (!attend.ExpiryDate.HasValue)
                return true;

            // If expiry date is today or in the future, access is allowed
            if (attend.ExpiryDate.Value >= DateOnly.FromDateTime(DateTime.Today))
                return true;

            // Expiry date has passed, cannot attend
            return false;
        }
        [HttpGet]
        public async Task<IActionResult> CanAccessLesson(int lessonCode)
        {
            var studentCode = GetSessionInt("StudentCode");
            var rootCode = GetSessionInt("RootCode");

            if (!studentCode.HasValue || !rootCode.HasValue)
                return Json(new { canAttend = false, message = "Not logged in." });

            bool canAttend = await CanStudentAttendLesson(studentCode.Value, lessonCode, rootCode.Value);

            string message = canAttend
      ? "Access allowed."
      : "Your access to this lesson has expired. Please enter a PIN to buy or renew access.";

            return Json(new
            {
                canAttend,
                message
            });
        }
        // <summary>
        /// Enhanced AccessLesson method with proper PIN validation
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AccessLesson(string pinCode, int lessonCode)
        {
            Console.WriteLine($"🔐 Enhanced AccessLesson attempt by {GetCurrentUser()} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   - LessonCode: {lessonCode}");
            Console.WriteLine($"   - PinCode: {pinCode}");

            var rootCode = GetSessionInt("RootCode");
            var studentCode = GetSessionInt("StudentCode") ?? 1;

            if (string.IsNullOrWhiteSpace(pinCode) || lessonCode <= 0)
            {
                TempData["ErrorMessage"] = "Please enter both PIN code and select a lesson";
                return RedirectToAction("StudentViewer", new { lessonCode });
            }

            try
            {
                // Find the PIN record
                var pin = await _context.Pins
                    .FirstOrDefaultAsync(p => p.Watermark == pinCode &&
                                              p.RootCode == rootCode &&
                                              p.IsActive == 1);

                if (pin == null)
                {
                    TempData["ErrorMessage"] = "Invalid PIN code";
                    return RedirectToAction("StudentViewer", new { lessonCode });
                }

                Console.WriteLine($"✅ PIN found: Times={pin.Times}, StudentCode={pin.StudentCode}, Status={pin.Status}");

                // Validate PIN constraints
                if (pin.Type != false)
                {
                    TempData["ErrorMessage"] = "PIN type not valid for lesson access";
                    return RedirectToAction("StudentViewer", new { lessonCode });
                }

                if (pin.Status != 1 && pin.Status != 2)
                {
                    TempData["ErrorMessage"] = "PIN is not active";
                    return RedirectToAction("StudentViewer", new { lessonCode });
                }

                // Case 3.1: Check if PIN times = 0 (exhausted)
                if (pin.Times <= 0)
                {
                    Console.WriteLine($"❌ PIN exhausted: Times = {pin.Times}");
                    TempData["ErrorMessage"] = "PIN has no remaining uses";
                    return RedirectToAction("StudentViewer", new { lessonCode });
                }

                // Case 4: Check if PIN belongs to another student
                if (pin.StudentCode.HasValue && pin.StudentCode.Value != studentCode)
                {
                    Console.WriteLine($"❌ PIN belongs to student {pin.StudentCode.Value}, current student is {studentCode}");
                    TempData["ErrorMessage"] = "This PIN belongs to another student";
                    return RedirectToAction("StudentViewer", new { lessonCode });
                }

                // Get lesson for validation
                var lesson = await _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .FirstOrDefaultAsync(l => l.LessonCode == lessonCode &&
                                              l.RootCode == rootCode &&
                                              l.IsActive);

                if (lesson == null)
                {
                    TempData["ErrorMessage"] = "Lesson not found or not available";
                    return RedirectToAction("StudentViewer", new { lessonCode });
                }

                // Check for existing OnlineAttend record
                var existingAttend = await _context.OnlineAttends
                    .FirstOrDefaultAsync(oa => oa.StudentCode == studentCode &&
                                               oa.LessonCode == lessonCode &&
                                               oa.PinCode == pin.PinCode &&
                                               oa.RootCode == rootCode.Value);

                if (existingAttend != null)
                {
                    // Student already has access, just increment views
                    existingAttend.Views++;
                    existingAttend.InsertTime = DateTime.Now;
                    Console.WriteLine($"📈 Incremented views to {existingAttend.Views}");
                }
                else
                {
                    // Create new OnlineAttend record (this will trigger the database trigger for ExpiryDate)
                    var newAttend = new OnlineAttend
                    {
                        StudentCode = studentCode,
                        LessonCode = lessonCode,
                        PinCode = pin.PinCode,
                        Views = 1,
                        Status = true,
                        RootCode = rootCode.Value,
                        InsertUser = studentCode,
                        InsertTime = DateTime.Now
                        // ExpiryDate will be set by database trigger
                    };

                    _context.OnlineAttends.Add(newAttend);
                    Console.WriteLine($"➕ Created new OnlineAttend record (PIN times handled by database)");

                    // Link PIN to student if not already linked
                    if (!pin.StudentCode.HasValue)
                    {
                        pin.StudentCode = studentCode;
                        Console.WriteLine($"🔗 Linked PIN to student {studentCode}");
                    }
                }

                // Update PIN status from 1 to 2 if needed
                if (pin.Status == 1)
                {
                    pin.Status = 2;
                    pin.LastUpdateTime = DateTime.Now;
                    Console.WriteLine($"🔄 Updated PIN status from 1 to 2");
                }
                else
                {
                    pin.LastUpdateTime = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Set session variables for secure access
                HttpContext.Session.SetString("ValidatedPin", pinCode);
                HttpContext.Session.SetInt32("AccessibleLesson", lessonCode);
                HttpContext.Session.SetString("AccessGrantedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                HttpContext.Session.SetString("AccessGrantedToUser", GetCurrentUser());

                Console.WriteLine($"✅ Access granted, redirecting to ViewLesson");
                return RedirectToAction("ViewLesson", new { lessonCode, pinCode });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in AccessLesson: {ex.Message}");
                TempData["ErrorMessage"] = "Error processing request. Please try again.";
                return RedirectToAction("StudentViewer", new { lessonCode });
            }
        }
        [HttpGet]
        public async Task<IActionResult> CheckLessonAccess(int lessonCode)
        {
            var studentCode = GetSessionInt("StudentCode");
            var rootCode = GetSessionInt("RootCode");

            Console.WriteLine($"🔍 CheckLessonAccess called for Student={studentCode}, Lesson={lessonCode}, Root={rootCode}");

            if (!studentCode.HasValue || !rootCode.HasValue)
            {
                return Json(new { result = "RequirePIN", message = "Please log in first" });
            }

            try
            {
                var accessResult = await CheckStudentLessonAccess(studentCode.Value, lessonCode, rootCode.Value);

                Console.WriteLine($"📋 Access result: {accessResult.Result}");

                // NEW: If DirectAccess, set session variables for seamless access
                if (accessResult.Result == LessonAccessResult.DirectAccess)
                {
                    // Extract pinCode from the redirect URL
                    var uri = new Uri($"http://localhost{accessResult.RedirectUrl}");
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var pinCode = query["pinCode"];

                    if (!string.IsNullOrEmpty(pinCode))
                    {
                        // Set session variables just like AccessLesson does
                        HttpContext.Session.SetString("ValidatedPin", pinCode);
                        HttpContext.Session.SetInt32("AccessibleLesson", lessonCode);
                        HttpContext.Session.SetString("AccessGrantedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                        HttpContext.Session.SetString("AccessGrantedToUser", GetCurrentUser());

                        Console.WriteLine($"✅ Session variables set for DirectAccess - Pin: {pinCode}, Lesson: {lessonCode}");
                    }
                }

                return Json(new
                {
                    result = accessResult.Result.ToString(),
                    message = accessResult.Message,
                    redirectUrl = accessResult.RedirectUrl
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in CheckLessonAccess: {ex.Message}");
                return Json(new { result = "RequirePIN", message = "Error checking access" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLessonFilters()
        {
            var (userCode, groupCode, rootCode) = GetSessionContext();


            try
            {
                var activeEduYear = await _context.EduYears
                    .Where(e => e.RootCode == rootCode.Value && (e.IsActive == true || e.IsActive == true))
                    .OrderBy(e => e.EduCode)
                    .FirstOrDefaultAsync();

                int? activeEduYearCode = activeEduYear?.EduCode;

                var subjects = await _context.Subjects
                    .Where(s => s.RootCode == rootCode.Value)
                    .Select(s => new
                    {
                        subjectCode = s.SubjectCode,
                        subjectName = s.SubjectName
                    })
                    .OrderBy(s => s.subjectName)
                    .ToListAsync();

                IQueryable<Year> yearsBase = _context.Years;

                if (activeEduYearCode.HasValue)
                {
                    yearsBase = yearsBase.Where(y => y.RootCode == rootCode);
                }
                else
                {
                    yearsBase =
                        from y in _context.Years
                       
                        where y.RootCode == rootCode.Value
                        select y;
                }

                var years = await (
                    from y in yearsBase
                  
                    where y.RootCode == rootCode.Value
                    select new
                    {
                        yearCode = y.YearCode,
                        yearName = y.YearName
                    })
                    .OrderBy(y => y.yearName)
                    .ToListAsync();

                return Json(new
                {
                    subjects,
                    years,
                    activeEduYear = activeEduYearCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load filters", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOnlineLessonStatus([FromBody] UpdateOnlineLessonDto dto)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                var file = await _context.Files
                    .FirstOrDefaultAsync(f => f.FileCode == dto.FileCode &&
                                              f.RootCode == rootCode.Value);

                if (file == null)
                    return NotFound("File not found");

                file.IsOnlineLesson = dto.IsOnlineLesson;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Online lesson status updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update status", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkUpdateOnlineLessonStatus([FromBody] BulkUpdateOnlineLessonDto dto)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                var files = await _context.Files
                    .Where(f => dto.FileCodes.Contains(f.FileCode) &&
                               f.RootCode == rootCode.Value)
                    .ToListAsync();

                foreach (var file in files)
                {
                    file.IsOnlineLesson = dto.IsOnlineLesson;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Updated {files.Count} file(s) as {(dto.IsOnlineLesson ? "online" : "offline")} lessons"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update statuses", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTeacherLessons(
            int? subjectCode = null,
            int? yearCode = null)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                var baseQuery = _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Where(l => l.RootCode == rootCode.Value && l.IsActive);

                var filteredQuery = baseQuery;

                if (subjectCode.HasValue)
                    filteredQuery = filteredQuery.Where(l => l.SubjectCode == subjectCode.Value);

                if (yearCode.HasValue)
                {
                    filteredQuery =
                        from l in filteredQuery
                        join y in _context.Years on l.RootCode equals y.RootCode
                        where y.YearCode == yearCode.Value
                        select l;
                }

                var allLessons = await baseQuery
                    .OrderBy(l => l.SubjectCode)
                    .ThenBy(l => l.LessonName)
                    .ToListAsync();

                var filteredLessons = await filteredQuery
                    .OrderBy(l => l.SubjectCode)
                    .ThenBy(l => l.LessonName)
                    .ToListAsync();

                var chaptersRaw = allLessons.Where(l => l.ChapterCode == null).ToList();

                var result = new List<object>();

                foreach (var chapter in chaptersRaw)
                {
                    var children = filteredLessons
                        .Where(l => l.ChapterCode == chapter.LessonCode)
                        .Select(l => new
                        {
                            lessonCode = l.LessonCode,
                            lessonName = l.LessonName,
                            subjectName = l.SubjectCodeNavigation?.SubjectName ?? "Unknown",
                            eduYearName = l.EduYearCodeNavigation?.EduName ?? "Unknown"
                        })
                        .ToList();

                    if (children.Any())
                    {
                        result.Add(new
                        {
                            chapterCode = chapter.LessonCode,
                            chapterName = chapter.LessonName,
                            subjectName = chapter.SubjectCodeNavigation?.SubjectName ?? "Unknown",
                            eduYearName = chapter.EduYearCodeNavigation?.EduName ?? "Unknown",
                            lessons = children
                        });
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error loading lessons", details = ex.Message });
            }
        }

        // Get content (videos, files, and exams) for a specific lesson
        [HttpGet]
        public async Task<IActionResult> GetLessonContent(int lessonCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                var filesData = await _context.Files
                    .Where(f => f.LessonCode == lessonCode
                        && f.RootCode == rootCode.Value
                        && f.IsActive)
                    .ToListAsync();

                // Files that link to an exam/assignment
                var examFileLinks = filesData
                    .Where(f => f.ExamCode.HasValue)
                    .ToList();

                var examCodes = examFileLinks
                    .Select(f => f.ExamCode.Value)
                    .Distinct()
                    .ToList();

                var examsData = await _context.Exams
                    .Where(e => examCodes.Contains(e.ExamCode) && e.IsActive == true)
                    .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                    .Where(et => et.Teacher.RootCode == rootCode.Value)
                    .Select(et => et.Exam)
                    .ToListAsync();

                // Find the required lesson exam (IsExam == true)
                var requiredExam = examsData.FirstOrDefault(e => e.IsExam == true);
                var studentCode = GetSessionInt("StudentCode");
                bool hasAttendedRequiredExam = false;
                if (studentCode.HasValue && requiredExam != null)
                {
                    hasAttendedRequiredExam = await _context.StudentExams
                        .AnyAsync(se => se.StudentCode == studentCode.Value
                                        && se.ExamCode == requiredExam.ExamCode
                                        && se.IsActive == true);
                }

                // Build exam/assignment items with explicit examCode and fileCode
                var examItems = examFileLinks.Select(f =>
                {
                    var exam = examsData.FirstOrDefault(e => e.ExamCode == f.ExamCode);
                    if (exam == null) return null;

                    return new
                    {
                        // keep existing itemCode for backward-compat (it was FileCode)
                        itemCode = f.FileCode,
                        // explicit identifiers
                        fileCode = f.FileCode,
                        examCode = exam.ExamCode,
                        isExam = exam.IsExam, // important for front-end label

                        displayName = exam.ExamName ?? "Unknown Exam",
                        itemType = exam.IsExam == true ? "Exam" : "Assignment",
                        fileType = 3,
                        // prefer exam.SortOrder, fallback to file.SortOrder
                        sortOrder = exam.SortOrder ?? f.SortOrder,
                        videoProvider = (int?)null,
                        videoProviderName = (string)null,
                        fileExtension = (string)null,
                        fileSizeBytes = (long?)null,
                        fileSizeFormatted = (string)null,
                        duration = (TimeSpan?)null,
                        durationFormatted = (string)null,
                        contentType = exam.IsExam == true ? "exam" : "assignment",
                        examDegree = exam.ExamDegree,
                        examTimer = exam.ExamTimer.ToString(@"HH\:mm"),
                        isOnline = exam.IsOnline,
                        isDone = exam.IsDone,
                        // helpful for client (optional)
                        isLessonExam = (exam.IsExam == true)
                    };
                }).Where(x => x != null).ToList();

                // Other files (videos, regular files) — add fileCode explicitly
                var normalFiles = filesData
                    .Where(f => !f.ExamCode.HasValue)
                    .Select(f => new
                    {
                        itemCode = f.FileCode, // keep for backward-compat
                        fileCode = f.FileCode, // explicit
                        displayName = f.DisplayName ?? "Unknown",
                        itemType = f.FileType == 1 ? "Video" : "File",
                        fileType = f.FileType,
                        sortOrder = f.SortOrder,
                        videoProvider = f.VideoProvider,
                        videoProviderName = f.VideoProvider == 0 ? "YouTube" :
                                            f.VideoProvider == 1 ? "Bunny CDN" :
                                            f.FileType == 1 ? "Unknown Provider" : null,
                        fileExtension = f.FileExtension ?? "",
                        fileSizeBytes = f.FileSizeBytes,
                        fileSizeFormatted = f.FileSizeBytes.HasValue ? FormatFileSize(f.FileSizeBytes.Value) : null,
                        duration = f.Duration,
                        durationFormatted = f.Duration?.ToString(@"hh\:mm\:ss"),
                        contentType = "content",
                        isOnlineLesson = f.IsOnlineLesson,
                        // FIX: coalesce nullable bool to avoid '&&' with bool?
                        locked = (f.FileType == 1 && (f.IsOnlineLesson ?? false) && !hasAttendedRequiredExam),
                        lockReason = (f.FileType == 1 && (f.IsOnlineLesson ?? false) && !hasAttendedRequiredExam)
                                        ? "ExamRequired" : null
                    }).ToList();

                var allContent = normalFiles.Cast<object>().Concat(examItems.Cast<object>())
                    .OrderBy(x =>
                    {
                        var prop = x.GetType().GetProperty("sortOrder");
                        return prop?.GetValue(x) ?? 999;
                    })
                    .ToList();

                return Json(allContent);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading lesson content: {ex.Message}");
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetAvailableExams(int lessonCode, bool? isExam = null)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                var linkedExamCodes = await _context.Exams
                    .Where(e => e.LessonCode == lessonCode && e.IsActive == true)
                    .Select(e => e.ExamCode)
                    .ToListAsync();

                var examsQuery = _context.Exams
                    .Where(e => e.IsActive == true && !linkedExamCodes.Contains(e.ExamCode));

                // Only filter by IsExam if specified
                if (isExam.HasValue)
                    examsQuery = examsQuery.Where(e => e.IsExam == isExam);

                var availableExams = await examsQuery
                    .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                    .Where(et => et.Teacher.RootCode == rootCode.Value)
                    .Select(et => new
                    {
                        examCode = et.Exam.ExamCode,
                        examName = et.Exam.ExamName,
                        examDegree = et.Exam.ExamDegree,
                        examTimer = et.Exam.ExamTimer.ToString(@"HH\:mm"),
                        teacherName = et.Teacher.TeacherName,
                        isOnline = et.Exam.IsOnline
                    })
                    .OrderBy(e => e.examName)
                    .ToListAsync();

                return Json(availableExams);
            }
            catch
            {
                return Json(new List<object>());
            }
        }
        [HttpPost]
        public async Task<IActionResult> LinkExamToLesson([FromBody] LinkExamDto dto)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                var exam = await _context.Exams
                    .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                    .Where(et => et.Exam.ExamCode == dto.ExamCode && et.Teacher.RootCode == rootCode.Value && et.Exam.IsActive == true)
                    .Select(et => et.Exam)
                    .FirstOrDefaultAsync();

                if (exam == null)
                    return NotFound("Exam not found or access denied");

                exam.LessonCode = dto.LessonCode;
                exam.SortOrder = dto.SortOrder;
                exam.LastUpdateUser = GetSessionInt("UserCode") ?? 1;
                exam.LastUpdateTime = DateTime.Now;

                bool isAssignment = exam.IsExam == false;

                int assignmentMinutes = 0;
                if (exam.ExamTimer != null)
                    assignmentMinutes = exam.ExamTimer.Hour * 60 + exam.ExamTimer.Minute;

                int fileType = isAssignment ? (assignmentMinutes > 0 ? 4 : 5) : 3;

                bool fileExists = await _context.Files.AnyAsync(f =>
                    f.LessonCode == dto.LessonCode &&
                    f.FileType == fileType &&
                    f.DisplayName == exam.ExamName);

                if (!fileExists)
                {
                    var file = new centrny.Models.File
                    {
                        LessonCode = dto.LessonCode,
                        RootCode = rootCode.Value,
                        InsertUser = GetSessionInt("UserCode") ?? 1,
                        InsertTime = DateTime.Now,
                        FileType = fileType,
                        DisplayName = exam.ExamName,
                        SortOrder = dto.SortOrder,
                        IsActive = true,
                         ExamCode = exam.ExamCode // <-- Link to Exams table
                    };
                    _context.Files.Add(file);
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null)
                    msg += " | INNER: " + ex.InnerException.Message;

                return StatusCode(500, $"Error linking exam to lesson: {msg}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UnlinkExamFromLesson([FromBody] int examCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                var exam = await _context.Exams
                    .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                    .Where(et => et.Exam.ExamCode == examCode && et.Teacher.RootCode == rootCode.Value && et.Exam.IsActive == true)
                    .Select(et => et.Exam)
                    .FirstOrDefaultAsync();

                if (exam == null)
                    return NotFound("Exam not found or access denied");

                // ✅ NEW: Find and remove the corresponding File record
                var examFile = await _context.Files
                    .FirstOrDefaultAsync(f => f.ExamCode == examCode &&
                                              f.RootCode == rootCode.Value &&
                                              f.IsActive);

                if (examFile != null)
                {
                    _context.Files.Remove(examFile);
                    Console.WriteLine($"🗑️ Removed File record {examFile.FileCode} for exam {examCode}");
                }

                // ✅ Update the exam record
                exam.LessonCode = null;
                exam.SortOrder = null;
                exam.LastUpdateUser = GetSessionInt("UserCode") ?? 1;
                exam.LastUpdateTime = DateTime.Now;

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error unlinking exam: {ex.Message}");
                return StatusCode(500, "Error unlinking exam from lesson");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddVideo([FromBody] AddVideoDto dto)
        {
            var userCode = GetSessionInt("UserCode");
            var rootCode = GetSessionInt("RootCode");

            if (userCode == null || rootCode == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.DisplayName) || string.IsNullOrWhiteSpace(dto.VideoUrl))
                return BadRequest("Display name and video URL are required");

            try
            {
                var encryptedUrl = EncryptString(dto.VideoUrl);

                var videoFile = new centrny.Models.File
                {
                    FileLocation = encryptedUrl,
                    RootCode = rootCode.Value,
                    LessonCode = dto.LessonCode,
                    InsertUser = userCode.Value,
                    InsertTime = DateTime.Now,
                    FileType = 1, // video
                    DisplayName = dto.DisplayName,
                    SortOrder = dto.SortOrder,
                    VideoProvider = dto.VideoProvider,
                    Duration = !string.IsNullOrEmpty(dto.Duration) ? TimeOnly.Parse(dto.Duration) : null,
                    IsActive = true
                };

                _context.Files.Add(videoFile);
                await _context.SaveChangesAsync();

                return Ok(new { fileCode = videoFile.FileCode });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error adding video: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddFile(int lessonCode, string displayName, int sortOrder, IFormFile file)
        {
            var userCode = GetSessionInt("UserCode");
            var rootCode = GetSessionInt("RootCode");

            if (userCode == null || rootCode == null)
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("File is required");

            if (file.Length > _fileUploadSettings.MaxFileSizeBytes)
                return BadRequest($"File size cannot exceed {_fileUploadSettings.MaxFileSizeBytes / (1024 * 1024)}MB");

            if (string.IsNullOrWhiteSpace(displayName))
                return BadRequest("Display name is required");

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_fileUploadSettings.AllowedExtensions.Contains(fileExtension))
                return BadRequest($"File type {fileExtension} is not allowed");

            try
            {
                var fileRecord = new centrny.Models.File
                {
                    FileLocation = "temp",
                    RootCode = rootCode.Value,
                    LessonCode = lessonCode,
                    InsertUser = userCode.Value,
                    InsertTime = DateTime.Now,
                    FileType = 0,
                    DisplayName = displayName,
                    SortOrder = sortOrder,
                    FileExtension = fileExtension,
                    FileSizeBytes = file.Length,
                    IsActive = true
                };

                _context.Files.Add(fileRecord);
                await _context.SaveChangesAsync();

                var fileName = $"{fileRecord.FileCode}{fileExtension}";
                Directory.CreateDirectory(_fileUploadSettings.UploadPath);
                var fullPath = Path.Combine(_fileUploadSettings.UploadPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = Path.Combine("uploads", "files", fileName);
                fileRecord.FileLocation = relativePath;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    fileCode = fileRecord.FileCode,
                    fileName = fileName,
                    fileSize = FormatFileSize(file.Length),
                    message = "File uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error uploading file: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSortOrder([FromBody] List<UpdateSortOrderDto> items)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                foreach (var item in items)
                {
                    if (item.ItemType == "exam")
                    {
                        var exam = await _context.Exams
                            .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                            .Where(et => et.Exam.ExamCode == item.ItemCode && et.Teacher.RootCode == rootCode.Value)
                            .Select(et => et.Exam)
                            .FirstOrDefaultAsync();

                        if (exam != null)
                        {
                            exam.SortOrder = item.SortOrder;
                            exam.LastUpdateUser = GetSessionInt("UserCode") ?? 1;
                            exam.LastUpdateTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        var file = await _context.Files
                            .FirstOrDefaultAsync(f => f.FileCode == item.ItemCode && f.RootCode == rootCode.Value);

                        if (file != null)
                        {
                            file.SortOrder = item.SortOrder;
                        }
                    }
                }

                var changesCount = await _context.SaveChangesAsync();
                return Ok(new { success = true, changesCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteContent(int fileCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            var file = await _context.Files
                .FirstOrDefaultAsync(f => f.FileCode == fileCode && f.RootCode == rootCode.Value);

            if (file == null)
                return NotFound();

            if (file.FileType == 0 && !string.IsNullOrEmpty(file.FileLocation))
            {
                var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, file.FileLocation);
                if (System.IO.File.Exists(fullPath))
                {
                    try { System.IO.File.Delete(fullPath); } catch { /* swallow */ }
                }
            }

            _context.Files.Remove(file);
            await _context.SaveChangesAsync();
            return Ok();
        }

        #endregion

        #region Student Interface - Enhanced 3-Step Secure Flow with PIN Validation

        [HttpGet]
        public async Task<IActionResult> StudentViewer(int? lessonCode = null)
        {
            var rootCode = GetSessionInt("RootCode");
            var studentCode = GetSessionInt("StudentCode");

            ViewBag.PreFilledLessonCode = lessonCode;

            // Only attempt auto-redirect if all context values and lessonCode exist
            if (lessonCode.HasValue && rootCode.HasValue && studentCode.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);

                var attend = await _context.OnlineAttends
                    .Include(a => a.PinCodeNavigation)
                    .FirstOrDefaultAsync(a =>
                        a.StudentCode == studentCode.Value &&
                        a.LessonCode == lessonCode.Value &&
                        a.RootCode == rootCode.Value);

                if (attend != null && attend.PinCodeNavigation != null)
                {
                    bool stillValid = !attend.ExpiryDate.HasValue || attend.ExpiryDate.Value >= today;

                    if (stillValid)
                    {
                        var pinCode = attend.PinCodeNavigation.Watermark;
                        if (!string.IsNullOrEmpty(pinCode))
                        {
                            // Set session (same shape as AccessLesson)
                            HttpContext.Session.SetString("ValidatedPin", pinCode);
                            HttpContext.Session.SetInt32("AccessibleLesson", lessonCode.Value);
                            HttpContext.Session.SetString("AccessGrantedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                            HttpContext.Session.SetString("AccessGrantedToUser", GetCurrentUser());

                            return RedirectToAction("ViewLesson", new { lessonCode = lessonCode.Value, pinCode });
                        }
                    }
                    // If expired → fall through to show PIN form (renew required)
                }
                // If no attendance → fall through (needs to enter PIN first)
            }

            // Only build lesson dropdown (or list) when no specific lesson requested
            if (rootCode.HasValue && !lessonCode.HasValue)
            {
                ViewBag.AvailableLessons = await _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Where(l => l.RootCode == rootCode.Value && l.IsActive)
                    .OrderBy(l => l.LessonName)
                    .ToListAsync();
            }
            else
            {
                ViewBag.AvailableLessons = new List<Lesson>();
            }

            return View(); // Renders PIN entry
        }
        private async Task CreateOrUpdateAttendanceRecord(int studentCode, int lessonCode, int pinCode, int rootCode)
        {
            try
            {
                // Check if attendance record already exists
                var existingAttend = await _context.OnlineAttends
                    .FirstOrDefaultAsync(oa => oa.StudentCode == studentCode &&
                                               oa.LessonCode == lessonCode &&
                                               oa.PinCode == pinCode &&
                                               oa.RootCode == rootCode);

                if (existingAttend != null)
                {

                    existingAttend.InsertTime = DateTime.Now;
                    Console.WriteLine($"📈 Incremented views to {existingAttend.Views} for existing attendance record");
                }
                else
                {
                    // Create new attendance record
                    var newAttend = new OnlineAttend
                    {
                        StudentCode = studentCode,
                        LessonCode = lessonCode,
                        PinCode = pinCode,
                        Views = 1,
                        Status = true,
                        RootCode = rootCode,
                        InsertUser = studentCode,
                        InsertTime = DateTime.Now
                    };

                    _context.OnlineAttends.Add(newAttend);
                    Console.WriteLine($"➕ Created new attendance record with 1 view");
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating attendance record: {ex.Message}");
                // Don't throw - attendance tracking failure shouldn't block lesson access
            }
        }

        [HttpGet]
        public async Task<IActionResult> LessonAccessState(int lessonCode)
        {
            var rootCode = GetSessionInt("RootCode");
            var studentCode = GetSessionInt("StudentCode");

            if (!rootCode.HasValue || !studentCode.HasValue)
                return Json(new { state = "NotLoggedIn" });

            var attend = await _context.OnlineAttends
                .Include(a => a.PinCodeNavigation)
                .FirstOrDefaultAsync(a => a.StudentCode == studentCode.Value &&
                                          a.LessonCode == lessonCode &&
                                          a.RootCode == rootCode.Value);

            if (attend == null)
                return Json(new { state = "NeedPIN", reason = "NoAttendance" });

            bool valid = !attend.ExpiryDate.HasValue || attend.ExpiryDate.Value >= DateOnly.FromDateTime(DateTime.Today);
            if (valid)
                return Json(new { state = "DirectAccess", pin = attend.PinCodeNavigation?.Watermark });

            return Json(new { state = "NeedPIN", reason = "Expired" });
        }

        [HttpGet]
        public IActionResult GetSessionStudentCode()
        {
            try
            {
                Console.WriteLine("GetSessionStudentCode called");

                var studentCode = GetSessionInt("StudentCode");
                Console.WriteLine($"StudentCode from session: {studentCode}");

                if (!studentCode.HasValue)
                {
                    Console.WriteLine("StudentCode is null, returning error");
                    return Json(new { error = "Student not found in session" });
                }

                Console.WriteLine($"Returning studentCode: {studentCode.Value}");
                return Json(new { studentCode = studentCode.Value });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in GetSessionStudentCode: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return Json(new { error = "Failed to get student info from session" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewLesson(int lessonCode, string pinCode)
        {

            var sessionPin = HttpContext.Session.GetString("ValidatedPin");
            var sessionLesson = HttpContext.Session.GetInt32("AccessibleLesson");
            var sessionAccessTime = HttpContext.Session.GetString("AccessGrantedAt");
            var studentCode = GetSessionInt("StudentCode");
            var rootCode = GetSessionInt("RootCode");
            var username = GetSessionString("StudentUsername");

            if (sessionPin != pinCode || sessionLesson != lessonCode)
            {
                TempData["ErrorMessage"] = "Session expired or invalid access. Please enter your PIN again.";
                return RedirectToAction("StudentViewer", new { lessonCode });
            }

            try
            {

                var lesson = await _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .FirstOrDefaultAsync(l => l.LessonCode == lessonCode &&
                                              l.RootCode == rootCode &&
                                              l.IsActive);

                if (lesson == null)
                {
                    TempData["ErrorMessage"] = "Lesson not available";
                    return RedirectToAction("StudentViewer", new { lessonCode });
                }

                var content = await GetLessonContentForStudent(lessonCode, rootCode.Value);

                // Set ViewBag properties
                ViewBag.Lesson = lesson;
                ViewBag.LessonCode = lessonCode;
                ViewBag.LessonName = lesson.LessonName;
                ViewBag.SubjectName = lesson.SubjectCodeNavigation?.SubjectName ?? "Unknown Subject";
                ViewBag.PinCode = pinCode;
                ViewBag.LessonContent = content;
                ViewBag.AccessGrantedAt = sessionAccessTime;
                ViewBag.LessonExpireDays = lesson.LessonExpireDays ?? 30;

                Console.WriteLine($"✅ ViewLesson loaded successfully with {content.Count} content items");
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in ViewLesson: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading lesson content. Please try again.";
                return RedirectToAction("StudentViewer", new { lessonCode });
            }
        }

        private async Task<List<object>> GetLessonContentForStudent(int lessonCode, int rootCode)
        {
            var filesData = await _context.Files
                .Where(f => f.LessonCode == lessonCode && f.RootCode == rootCode && f.IsActive)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            var examsData = await _context.Exams
                .Where(e => e.LessonCode == lessonCode && e.IsActive == true)
                .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                .Where(et => et.Teacher.RootCode == rootCode)
                .Select(et => et.Exam)
                .OrderBy(e => e.SortOrder ?? 999)
                .ToListAsync();

            var files = filesData.Select(f => new
            {
                itemCode = f.FileCode,
                displayName = f.DisplayName ?? "Unknown",
                itemType = f.FileType == 1 ? "Video" : "File",
                fileType = f.FileType,
                sortOrder = f.SortOrder,
                videoProvider = f.VideoProvider,
                videoProviderName = f.VideoProvider == 0 ? "YouTube" :
                                    f.VideoProvider == 1 ? "Bunny CDN" :
                                    f.FileType == 1 ? "Unknown Provider" : null,
                fileExtension = f.FileExtension ?? "",
                fileSizeBytes = f.FileSizeBytes,
                fileSizeFormatted = f.FileSizeBytes.HasValue ? FormatFileSize(f.FileSizeBytes.Value) : null,
                duration = f.Duration,
                durationFormatted = f.Duration?.ToString(@"hh\:mm\:ss"),
                contentType = "content"
            }).Cast<object>().ToList();

            var exams = examsData.Select(e => new
            {
                itemCode = e.ExamCode,
                displayName = e.ExamName ?? "Unknown Exam",
                itemType = "Exam",
                fileType = 3,
                sortOrder = e.SortOrder ?? 999,
                videoProvider = (int?)null,
                videoProviderName = (string)null,
                fileExtension = (string)null,
                fileSizeBytes = (long?)null,
                fileSizeFormatted = (string)null,
                duration = (TimeSpan?)null,
                durationFormatted = (string)null,
                contentType = "exam",
                examDegree = e.ExamDegree,
                examTimer = e.ExamTimer.ToString(@"HH\:mm"),
                isOnline = e.IsOnline,
                isDone = e.IsDone,
                isExam = e.IsExam // <--- ADD THIS LINE
            }).Cast<object>().ToList();

            return files.Concat(exams).OrderBy(o =>
            {
                var prop = o.GetType().GetProperty("sortOrder") ?? o.GetType().GetProperty("SortOrder");
                return prop?.GetValue(o) ?? 999;
            }).ToList();
        }

        #endregion

        #region Existing Student Content Access Methods

        [HttpGet]
        public async Task<IActionResult> GetSecureVideoUrl(int fileCode, string pinCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            var sessionPin = HttpContext.Session.GetString("ValidatedPin");
            if (sessionPin != pinCode)
                return BadRequest("Session expired. Please refresh and enter your PIN again.");

            var pin = await _context.Pins
                .FirstOrDefaultAsync(p => p.Watermark == pinCode &&
                                          p.RootCode == rootCode.Value &&
                                          p.IsActive == 1 &&
                                          (p.Status == 1 || p.Status == 2));

            if (pin == null)
                return BadRequest("Invalid or expired pin code");

            var videoFile = await _context.Files
                .Include(f => f.LessonCodeNavigation)
                .FirstOrDefaultAsync(f => f.FileCode == fileCode && f.FileType == 1 && f.IsActive);

            if (videoFile == null)
                return NotFound("Video not found");

            // FIX: handle nullable bool (IsOnlineLesson is bool?)
            if (videoFile.IsOnlineLesson == true)
            {
                var studentCode = GetSessionInt("StudentCode");
                // Find the lesson's main exam (IsExam == true)
                var requiredExam = await _context.Exams
                    .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { e, t })
                    .Where(et => et.e.LessonCode == videoFile.LessonCode
                                 && et.e.IsActive == true
                                 && et.e.IsExam == true
                                 && et.t.RootCode == rootCode.Value)
                    .Select(et => et.e)
                    .FirstOrDefaultAsync();

                if (requiredExam != null)
                {
                    var attended = studentCode.HasValue && await _context.StudentExams
                        .AnyAsync(se => se.StudentCode == studentCode.Value
                                        && se.ExamCode == requiredExam.ExamCode
                                        && se.IsActive == true);

                    if (!attended)
                    {
                        // Deny until exam is attended
                        return StatusCode(403, "You must attend the exam before accessing this video.");
                    }
                }
                // If no required exam exists, allow by default
            }

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

        [HttpPost]
        public async Task<IActionResult> TrackAccess([FromBody] TrackAccessDto dto)
        {
            var rootCode = GetSessionInt("RootCode");
            var studentCode = GetSessionInt("StudentCode");

            if (rootCode == null)
                return Unauthorized("Root code not found in session");

            var trackingStudentCode = studentCode ?? 1;
            var sessionPin = HttpContext.Session.GetString("ValidatedPin");
            if (sessionPin != dto.PinCode)
                return BadRequest("Session expired. Please refresh and enter your PIN again.");

            var pin = await _context.Pins
                .FirstOrDefaultAsync(p => p.Watermark == dto.PinCode &&
                                          p.RootCode == rootCode.Value &&
                                          p.IsActive == 1 &&
                                          (p.Status == 1 || p.Status == 2));

            if (pin == null)
                return BadRequest("Invalid or expired pin code");

            await CreateOrUpdateAttendanceRecord(trackingStudentCode, dto.LessonCode, pin.PinCode, rootCode.Value);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressDto dto)
        {
            // Placeholder for more detailed tracking
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFile(int fileCode, string pinCode = null)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            var file = await _context.Files
                .FirstOrDefaultAsync(f => f.FileCode == fileCode &&
                                          f.RootCode == rootCode &&
                                          (f.FileType == 0 || f.FileType == 2) &&
                                          f.IsActive);

            if (file == null)
                return NotFound("File not found in database");

            var fileName = $"{file.FileCode}{file.FileExtension}";
            var pathsToTry = new[]
            {
                Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "files", fileName),
                Path.Combine(_fileUploadSettings.UploadPath, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "files", fileName),
                Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "files", fileName)
            };

            string workingPath = pathsToTry.FirstOrDefault(System.IO.File.Exists);

            if (workingPath == null)
                return NotFound($"Physical file not found: {fileName}");

            try
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(workingPath);
                var downloadName = $"{file.DisplayName}{file.FileExtension}";
                var mimeType = GetMimeType(file.FileExtension);
                return File(fileBytes, mimeType, downloadName);
            }
            catch
            {
                return StatusCode(500, "Error reading file");
            }
        }

        [HttpGet]
        public async Task<IActionResult> StreamVideo(string token, string url)
        {
            try
            {
                var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decodedToken.Split(':');
                if (parts.Length != 2) return BadRequest("Invalid token");

                var expiry = long.Parse(parts[1]);
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiry);
                if (DateTimeOffset.Now > expiryTime) return BadRequest("Video link has expired");

                var decodedUrl = Uri.UnescapeDataString(url);
                return Redirect(decodedUrl);
            }
            catch
            {
                return BadRequest("Invalid token format");
            }
        }

        #endregion

        #region Helper Methods

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string EncryptString(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(data);
        }

        private string DecryptString(string encryptedText)
        {
            var data = Convert.FromBase64String(encryptedText);
            return Encoding.UTF8.GetString(data);
        }

        private string GetMimeType(string extension) =>
            extension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

        private string GetCurrentUser()
        {
            return HttpContext.Session.GetString("UserLogin") ??
                   GetSessionInt("UserCode")?.ToString() ??
                   GetSessionInt("StudentCode")?.ToString() ??
                   "Anonymous";
        }

        #endregion
    }

    #region DTOs

    public class AddVideoDto
    {
        public int LessonCode { get; set; }
        public string DisplayName { get; set; } = null!;
        public string VideoUrl { get; set; } = null!;
        public int VideoProvider { get; set; } // 0=YouTube, 1=Bunny CDN
        public int SortOrder { get; set; }
        public string? Duration { get; set; } // "HH:MM:SS"
    }

    public class UpdateSortOrderDto
    {
        public int ItemCode { get; set; }
        public int SortOrder { get; set; }
        public string ItemType { get; set; } = "file"; // "file" or "exam"
    }

    public class LinkExamDto
    {
        public int ExamCode { get; set; }
        public int LessonCode { get; set; }
        public int SortOrder { get; set; }
    }

    public class TrackAccessDto
    {
        public int LessonCode { get; set; }
        public string PinCode { get; set; } = null!;
    }

    public class UpdateOnlineLessonDto
    {
        public int FileCode { get; set; }
        public bool IsOnlineLesson { get; set; }
    }

    public class BulkUpdateOnlineLessonDto
    {
        public List<int> FileCodes { get; set; } = new();
        public bool IsOnlineLesson { get; set; }
    }
    public class UpdateProgressDto
    {
        public int StudentCode { get; set; }
        public int LessonCode { get; set; }
        public int? VideoCode { get; set; }
        public string WatchTime { get; set; } = null!;
        public string? LastPosition { get; set; }
        public bool IsCompleted { get; set; }
        public decimal? CompletionPercentage { get; set; }
    }

    public class AccessLessonDto
    {
        public string PinCode { get; set; } = null!;
        public int LessonCode { get; set; }
    }

    public class FileUploadSettings
    {
        public string UploadPath { get; set; } = "wwwroot/uploads/files";
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
        public List<string> AllowedExtensions { get; set; } = new()
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".avi"
        };
    }

    #endregion
}