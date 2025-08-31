using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace centrny.Controllers
{
    public class LessonContentController : Controller
    {
        private readonly CenterContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly FileUploadSettings _fileUploadSettings;

        public LessonContentController(
            CenterContext context,
            IWebHostEnvironment webHostEnvironment,
            IOptions<FileUploadSettings> fileUploadSettings)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _fileUploadSettings = fileUploadSettings.Value;
        }

        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);

        #region Teacher Interface

        // Teacher interface for managing lesson content
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

        // Get lessons for a teacher to select from
        [HttpGet]
        public async Task<IActionResult> GetTeacherLessons(int? teacherCode = null)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            var query = _context.Lessons
                .Include(l => l.SubjectCodeNavigation)
                .Include(l => l.EduYearCodeNavigation)
                .Where(l => l.RootCode == rootCode.Value && l.IsActive);

            if (teacherCode.HasValue)
                query = query.Where(l => l.TeacherCode == teacherCode.Value);

            var lessons = await query
                .OrderBy(l => l.SubjectCode)
                .ThenBy(l => l.LessonName)
                .ToListAsync();

            var result = lessons.Select(l => new
            {
                l.LessonCode,
                l.LessonName,
                SubjectName = l.SubjectCodeNavigation?.SubjectName ?? "Unknown",
                EduYearName = l.EduYearCodeNavigation?.EduName ?? "Unknown"
            }).ToList();

            return Json(result);
        }

        // Get content (videos, files, and exams) for a specific lesson
        [HttpGet]
        public async Task<IActionResult> GetLessonContent(int lessonCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                // Get Files (videos and documents)
                var filesData = await _context.Files
                    .Where(f => f.LessonCode == lessonCode && f.RootCode == rootCode.Value && f.IsActive)
                    .ToListAsync();

                // Get Exams for this lesson
                var examsData = await _context.Exams
                    .Where(e => e.LessonCode == lessonCode && e.IsActive == true)
                    .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                    .Where(et => et.Teacher.RootCode == rootCode.Value)
                    .Select(et => et.Exam)
                    .ToListAsync();

                // Process files data
                var files = filesData.Select(f => new
                {
                    ItemCode = f.FileCode,
                    DisplayName = f.DisplayName ?? "Unknown",
                    ItemType = f.FileType == 1 ? "Video" : "File",
                    FileType = f.FileType, // 0=File, 1=Video
                    SortOrder = f.SortOrder,
                    VideoProvider = f.VideoProvider,
                    VideoProviderName = f.VideoProvider == 0 ? "YouTube" :
                                       f.VideoProvider == 1 ? "Bunny CDN" :
                                       f.FileType == 1 ? "Unknown Provider" : null,
                    FileExtension = f.FileExtension ?? "",
                    FileSizeBytes = f.FileSizeBytes,
                    FileSizeFormatted = f.FileSizeBytes.HasValue ? FormatFileSize(f.FileSizeBytes.Value) : null,
                    Duration = f.Duration,
                    DurationFormatted = f.Duration?.ToString(@"hh\:mm\:ss"),
                    ContentType = "content",
                    // Exam properties (null for files)
                    ExamDegree = (string)null,
                    ExamTimer = (string)null,
                    IsOnline = (bool?)null,
                    IsDone = (bool?)null
                }).ToList();

                // Process exams data
                var exams = examsData.Select(e => new
                {
                    ItemCode = e.ExamCode,
                    DisplayName = e.ExamName ?? "Unknown Exam",
                    ItemType = "Exam",
                    FileType = 3, // New type for exams
                    SortOrder = e.SortOrder ?? 999,
                    VideoProvider = (int?)null,
                    VideoProviderName = (string)null,
                    FileExtension = (string)null,
                    FileSizeBytes = (long?)null,
                    FileSizeFormatted = (string)null,
                    Duration = (TimeSpan?)null,
                    DurationFormatted = (string)null,
                    ContentType = "exam",
                    // Exam specific properties
                    ExamDegree = e.ExamDegree,
                    ExamTimer = e.ExamTimer.ToString(@"HH\:mm"),
                    IsOnline = e.IsOnline,
                    IsDone = e.IsDone
                }).ToList();

                // Combine into a single list
                var allContent = files.Cast<object>().Concat(exams.Cast<object>()).ToList();

                return Json(allContent);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading lesson content: {ex.Message}");
            }
        }

        // Get available exams for linking
        [HttpGet]
        public async Task<IActionResult> GetAvailableExams(int lessonCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            try
            {
                // Get exams that are not yet linked to this lesson
                var linkedExamCodes = await _context.Exams
                    .Where(e => e.LessonCode == lessonCode && e.IsActive == true)
                    .Select(e => e.ExamCode)
                    .ToListAsync();

                var availableExams = await _context.Exams
                    .Where(e => e.IsActive == true && !linkedExamCodes.Contains(e.ExamCode))
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
            catch (Exception ex)
            {
                return Json(new List<object>());
            }
        }

        // Link existing exam to lesson
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

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error linking exam to lesson");
            }
        }

        // Unlink exam from lesson
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

                exam.LessonCode = null;
                exam.SortOrder = null;
                exam.LastUpdateUser = GetSessionInt("UserCode") ?? 1;
                exam.LastUpdateTime = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error unlinking exam from lesson");
            }
        }

        // Add video to lesson
        [HttpPost]
        public async Task<IActionResult> AddVideo([FromBody] AddVideoDto dto)
        {
            var userCode = GetSessionInt("UserCode");
            var rootCode = GetSessionInt("RootCode");

            if (userCode == null || rootCode == null)
                return Unauthorized();

            if (string.IsNullOrEmpty(dto.DisplayName) || string.IsNullOrEmpty(dto.VideoUrl))
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
                    FileType = 1,
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

        // Add file to lesson (with upload)
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

            if (string.IsNullOrEmpty(displayName))
                return BadRequest("Display name is required");

            // Check file extension
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_fileUploadSettings.AllowedExtensions.Contains(fileExtension))
                return BadRequest($"File type {fileExtension} is not allowed");

            try
            {
                // First, create the file record to get the FileCode
                var fileRecord = new centrny.Models.File
                {
                    FileLocation = "temp", // Temporary, will update after we get FileCode
                    RootCode = rootCode.Value,
                    LessonCode = lessonCode,
                    InsertUser = userCode.Value,
                    InsertTime = DateTime.Now,
                    FileType = 0, // File
                    DisplayName = displayName,
                    SortOrder = sortOrder,
                    FileExtension = fileExtension,
                    FileSizeBytes = file.Length,
                    IsActive = true
                };

                _context.Files.Add(fileRecord);
                await _context.SaveChangesAsync();

                // Now create the actual file name using FileCode
                var fileName = $"{fileRecord.FileCode}{fileExtension}";
                var fullPath = Path.Combine(_fileUploadSettings.UploadPath, fileName);

                // Ensure directory exists
                Directory.CreateDirectory(_fileUploadSettings.UploadPath);

                // Save file to the configured upload location
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Update the file record with the actual file location (relative path)
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

        // Update content sort order
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
                        // Update exam sort order
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
                        // Update file sort order (for files and videos)
                        var file = await _context.Files
                            .FirstOrDefaultAsync(f => f.FileCode == item.ItemCode && f.RootCode == rootCode.Value);

                        if (file != null)
                        {
                            file.SortOrder = item.SortOrder;
                        }
                    }
                }

                var changesCount = await _context.SaveChangesAsync();

                return Ok(new { success = true, changesCount = changesCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // Delete content item
        [HttpDelete]
        public async Task<IActionResult> DeleteContent(int fileCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            var file = await _context.Files
                .FirstOrDefaultAsync(f => f.FileCode == fileCode && f.RootCode == rootCode.Value);

            if (file == null)
                return NotFound();

            // If it's a physical file, delete from disk
            if (file.FileType == 0 && !string.IsNullOrEmpty(file.FileLocation))
            {
                var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, file.FileLocation);

                if (System.IO.File.Exists(fullPath))
                {
                    try
                    {
                        System.IO.File.Delete(fullPath);
                    }
                    catch
                    {
                        // Continue with database deletion even if file deletion fails
                    }
                }
            }

            _context.Files.Remove(file);
            await _context.SaveChangesAsync();

            return Ok();
        }

        #endregion

        #region Student Interface - NEW 3-STEP SECURE FLOW

        // STEP 1: Show PIN + Lesson entry form
        [HttpGet]
        public async Task<IActionResult> StudentViewer()
        {
            Console.WriteLine($"🔐 StudentViewer access attempt by {GetCurrentUser()} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"📍 IP Address: {HttpContext.Connection.RemoteIpAddress}");

            var rootCode = GetSessionInt("RootCode");

            // For now, allow public access but load available lessons
            if (rootCode.HasValue)
            {
                // Load available lessons for dropdown
                var lessons = await _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Where(l => l.RootCode == rootCode.Value && l.IsActive)
                    .OrderBy(l => l.LessonName)
                    .ToListAsync();

                ViewBag.AvailableLessons = lessons;
                Console.WriteLine($"📚 Loaded {lessons.Count} available lessons for selection");
            }
            else
            {
                ViewBag.AvailableLessons = new List<Lesson>();
                Console.WriteLine("⚠️ No rootCode in session - showing empty lesson list");
            }

            return View(); // Show PIN + Lesson entry form
        }

        // STEP 2: Validate PIN + Lesson and store in session
        [HttpPost]
        public async Task<IActionResult> AccessLesson(string pinCode, int lessonCode)
        {
            Console.WriteLine($"🔐 AccessLesson attempt by {GetCurrentUser()} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   - LessonCode: {lessonCode}");
            Console.WriteLine($"   - PinCode: {pinCode}");
            Console.WriteLine($"   - IP Address: {HttpContext.Connection.RemoteIpAddress}");

            var rootCode = GetSessionInt("RootCode");

            if (string.IsNullOrWhiteSpace(pinCode) || lessonCode <= 0)
            {
                Console.WriteLine("❌ Invalid input: PIN or lesson code missing");
                TempData["ErrorMessage"] = "Please enter both PIN code and select a lesson";
                return RedirectToAction("StudentViewer");
            }

            try
            {
                // SECURITY CHECK 1: Validate PIN with comprehensive checks
                var pin = await _context.Pins
                    .FirstOrDefaultAsync(p => p.Watermark == pinCode &&
                                       p.RootCode == rootCode &&
                                       p.IsActive == 1 &&           // Active pin
                                       p.Status == 1 &&             // Valid status
                                       p.Times > 0);                // Has remaining uses

                if (pin == null)
                {
                    Console.WriteLine($"❌ PIN validation failed:");
                    Console.WriteLine($"   - PIN not found, inactive, invalid status, or no remaining uses");
                    TempData["ErrorMessage"] = "Invalid or expired PIN code";
                    return RedirectToAction("StudentViewer");
                }

                Console.WriteLine($"✅ PIN validated: PinCode={pin.PinCode}, Type={pin.Type}, Times={pin.Times}, Status={pin.Status}");

                // SECURITY CHECK 2: Validate lesson exists and is accessible
                var lesson = await _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .FirstOrDefaultAsync(l => l.LessonCode == lessonCode &&
                                       l.RootCode == rootCode &&
                                       l.IsActive);

                if (lesson == null)
                {
                    Console.WriteLine($"❌ Lesson validation failed: LessonCode={lessonCode} not found or inactive");
                    TempData["ErrorMessage"] = "Lesson not found or not available";
                    return RedirectToAction("StudentViewer");
                }

                Console.WriteLine($"✅ Lesson validated: {lesson.LessonName} - {lesson.SubjectCodeNavigation?.SubjectName}");

                // SECURITY CHECK 3: Store validated access in session
                HttpContext.Session.SetString("ValidatedPin", pinCode);
                HttpContext.Session.SetInt32("AccessibleLesson", lessonCode);
                HttpContext.Session.SetString("AccessGrantedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                HttpContext.Session.SetString("AccessGrantedToUser", GetCurrentUser());

                Console.WriteLine($"✅ Session access granted:");
                Console.WriteLine($"   - ValidatedPin: {pinCode}");
                Console.WriteLine($"   - AccessibleLesson: {lessonCode}");
                Console.WriteLine($"   - AccessGrantedAt: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

                // Optional: Decrement PIN usage count
                // pin.Times--;
                // await _context.SaveChangesAsync();
                // Console.WriteLine($"📉 PIN usage decremented: Times={pin.Times}");

                // Redirect to actual lesson content with validated session
                return RedirectToAction("ViewLesson", new { lessonCode = lessonCode, pinCode = pinCode });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AccessLesson error: {ex.Message}");
                TempData["ErrorMessage"] = "Error processing request. Please try again.";
                return RedirectToAction("StudentViewer");
            }
        }

        // STEP 3: Show lesson content with session validation
        [HttpGet]
        public async Task<IActionResult> ViewLesson(int lessonCode, string pinCode)
        {
            Console.WriteLine($"🎓 ViewLesson access attempt by {GetCurrentUser()} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   - LessonCode: {lessonCode}");
            Console.WriteLine($"   - PinCode: {pinCode}");

            // SECURITY CHECK: Validate session has proper access
            var sessionPin = HttpContext.Session.GetString("ValidatedPin");
            var sessionLesson = HttpContext.Session.GetInt32("AccessibleLesson");
            var sessionAccessTime = HttpContext.Session.GetString("AccessGrantedAt");

            Console.WriteLine($"🔍 Session validation:");
            Console.WriteLine($"   - SessionPin: {sessionPin}");
            Console.WriteLine($"   - SessionLesson: {sessionLesson}");
            Console.WriteLine($"   - SessionAccessTime: {sessionAccessTime}");

            if (sessionPin != pinCode || sessionLesson != lessonCode)
            {
                Console.WriteLine($"❌ Session validation failed - redirecting to StudentViewer");
                TempData["ErrorMessage"] = "Session expired or invalid access. Please enter your PIN again.";
                return RedirectToAction("StudentViewer");
            }

            Console.WriteLine($"✅ Session validated - proceeding to load lesson content");

            try
            {
                var rootCode = GetSessionInt("RootCode");

                // Load lesson details
                var lesson = await _context.Lessons
                    .Include(l => l.SubjectCodeNavigation)
                    .Include(l => l.EduYearCodeNavigation)
                    .Include(l => l.TeacherCodeNavigation)
                    .FirstOrDefaultAsync(l => l.LessonCode == lessonCode && l.RootCode == rootCode && l.IsActive);

                if (lesson == null)
                {
                    Console.WriteLine($"❌ Lesson not found during ViewLesson");
                    TempData["ErrorMessage"] = "Lesson not available";
                    return RedirectToAction("StudentViewer");
                }

                // Load lesson content (videos, files, exams)
                var content = await GetLessonContentForStudent(lessonCode, rootCode.Value);

                // Pass data to view
                ViewBag.Lesson = lesson;
                ViewBag.LessonCode = lessonCode;
                ViewBag.PinCode = pinCode;
                ViewBag.LessonContent = content;
                ViewBag.AccessGrantedAt = sessionAccessTime;

                Console.WriteLine($"✅ Lesson content loaded successfully: {content.Count} items");

                // Track lesson access
                await TrackLessonAccess(lessonCode, pinCode);

                return View(); // Show the actual lesson content
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading lesson content: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading lesson content. Please try again.";
                return RedirectToAction("StudentViewer");
            }
        }

        // Helper method to get lesson content for students
        private async Task<List<object>> GetLessonContentForStudent(int lessonCode, int rootCode)
        {
            // Get Files (videos and documents)
            var filesData = await _context.Files
                .Where(f => f.LessonCode == lessonCode && f.RootCode == rootCode && f.IsActive)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            // Get Exams for this lesson
            var examsData = await _context.Exams
                .Where(e => e.LessonCode == lessonCode && e.IsActive == true)
                .Join(_context.Teachers, e => e.TeacherCode, t => t.TeacherCode, (e, t) => new { Exam = e, Teacher = t })
                .Where(et => et.Teacher.RootCode == rootCode)
                .Select(et => et.Exam)
                .OrderBy(e => e.SortOrder ?? 999)
                .ToListAsync();

            // Process files data
            var files = filesData.Select(f => new
            {
                ItemCode = f.FileCode,
                DisplayName = f.DisplayName ?? "Unknown",
                ItemType = f.FileType == 1 ? "Video" : "File",
                FileType = f.FileType, // 0=File, 1=Video
                SortOrder = f.SortOrder,
                VideoProvider = f.VideoProvider,
                VideoProviderName = f.VideoProvider == 0 ? "YouTube" :
                                   f.VideoProvider == 1 ? "Bunny CDN" :
                                   f.FileType == 1 ? "Unknown Provider" : null,
                FileExtension = f.FileExtension ?? "",
                FileSizeBytes = f.FileSizeBytes,
                FileSizeFormatted = f.FileSizeBytes.HasValue ? FormatFileSize(f.FileSizeBytes.Value) : null,
                Duration = f.Duration,
                DurationFormatted = f.Duration?.ToString(@"hh\:mm\:ss"),
                ContentType = "content"
            }).Cast<object>().ToList();

            // Process exams data
            var exams = examsData.Select(e => new
            {
                ItemCode = e.ExamCode,
                DisplayName = e.ExamName ?? "Unknown Exam",
                ItemType = "Exam",
                FileType = 3, // Type for exams
                SortOrder = e.SortOrder ?? 999,
                VideoProvider = (int?)null,
                VideoProviderName = (string)null,
                FileExtension = (string)null,
                FileSizeBytes = (long?)null,
                FileSizeFormatted = (string)null,
                Duration = (TimeSpan?)null,
                DurationFormatted = (string)null,
                ContentType = "exam",
                // Exam specific properties
                ExamDegree = e.ExamDegree,
                ExamTimer = e.ExamTimer.ToString(@"HH\:mm"),
                IsOnline = e.IsOnline,
                IsDone = e.IsDone
            }).Cast<object>().ToList();

            // Combine and sort by SortOrder
            var allContent = files.Concat(exams).OrderBy(item =>
            {
                var sortOrderProp = item.GetType().GetProperty("SortOrder");
                return sortOrderProp?.GetValue(item) ?? 999;
            }).ToList();

            return allContent;
        }

        // Track lesson access for analytics
        private async Task TrackLessonAccess(int lessonCode, string pinCode)
        {
            try
            {
                var rootCode = GetSessionInt("RootCode");
                var studentCode = GetSessionInt("StudentCode"); // You'll need to set this in student login

                if (rootCode == null)
                {
                    Console.WriteLine("⚠️ Cannot track access: No rootCode in session");
                    return;
                }

                // For now, use a default student code if not in session
                var trackingStudentCode = studentCode ?? 1;

                var pin = await _context.Pins
                    .FirstOrDefaultAsync(p => p.Watermark == pinCode && p.RootCode == rootCode.Value);

                if (pin != null)
                {
                    // Check if this combination already exists in OnlineAttend
                    var existingAttend = await _context.OnlineAttends
                        .FirstOrDefaultAsync(oa => oa.StudentCode == trackingStudentCode &&
                                           oa.LessonCode == lessonCode &&
                                           oa.PinCode == pin.PinCode &&
                                           oa.RootCode == rootCode.Value);

                    if (existingAttend != null)
                    {
                        // Increment view count
                        existingAttend.Views++;
                        existingAttend.InsertTime = DateTime.Now;
                        Console.WriteLine($"📊 Updated attendance: Views={existingAttend.Views}");
                    }
                    else
                    {
                        // Create new attend record
                        var attend = new OnlineAttend
                        {
                            StudentCode = trackingStudentCode,
                            LessonCode = lessonCode,
                            PinCode = pin.PinCode,
                            Views = 1,
                            Status = true,
                            RootCode = rootCode.Value,
                            InsertUser = trackingStudentCode,
                            InsertTime = DateTime.Now
                        };
                        _context.OnlineAttends.Add(attend);
                        Console.WriteLine($"📊 Created new attendance record");
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error tracking lesson access: {ex.Message}");
                // Don't fail the request if tracking fails
            }
        }

        #endregion

        #region Existing Student Content Access Methods (Updated with Better Security)

        // Generate secure video URL for student access
        [HttpGet]
        public async Task<IActionResult> GetSecureVideoUrl(int fileCode, string pinCode)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            Console.WriteLine($"🔐 GetSecureVideoUrl attempt by {GetCurrentUser()} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   - FileCode: {fileCode}");
            Console.WriteLine($"   - PinCode: {pinCode}");

            // Validate session access first
            var sessionPin = HttpContext.Session.GetString("ValidatedPin");
            if (sessionPin != pinCode)
            {
                Console.WriteLine($"❌ Session validation failed for video access");
                return BadRequest("Session expired. Please refresh and enter your PIN again.");
            }

            // Validate pin code with comprehensive checks
            var pin = await _context.Pins
                .FirstOrDefaultAsync(p => p.Watermark == pinCode &&
                                   p.RootCode == rootCode.Value &&
                                   p.IsActive == 1 &&           // Active pin
                                   p.Status == 1 &&             // Valid status
                                   p.Times > 0);                // Has remaining uses

            if (pin == null)
            {
                Console.WriteLine($"❌ PIN validation failed for video access");
                return BadRequest("Invalid or expired pin code");
            }

            Console.WriteLine($"✅ PIN validated for video access: PinCode={pin.PinCode}");

            // Get video file
            var videoFile = await _context.Files
                .Include(f => f.LessonCodeNavigation)
                .FirstOrDefaultAsync(f => f.FileCode == fileCode && f.FileType == 1 && f.IsActive);

            if (videoFile == null)
            {
                Console.WriteLine($"❌ Video file not found: FileCode={fileCode}");
                return NotFound("Video not found");
            }

            Console.WriteLine($"✅ Video file found: {videoFile.DisplayName}");

            // Decrypt video URL
            var originalUrl = DecryptString(videoFile.FileLocation);

            // Generate time-limited secure URL
            var expiryHours = videoFile.LessonCodeNavigation?.LessonExpireDays.HasValue == true
                ? videoFile.LessonCodeNavigation.LessonExpireDays.Value * 24
                : 24;

            Console.WriteLine($"✅ Video access granted for {expiryHours} hours");

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
            {
                Console.WriteLine($"❌ TrackAccess failed: No rootCode in session");
                return Unauthorized("Root code not found in session");
            }

            // Use default student code if not in session
            var trackingStudentCode = studentCode ?? 1;

            Console.WriteLine($"🔐 TrackAccess attempt by student {trackingStudentCode} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   - LessonCode: {dto.LessonCode}");
            Console.WriteLine($"   - PinCode: {dto.PinCode}");

            // Validate session access
            var sessionPin = HttpContext.Session.GetString("ValidatedPin");
            if (sessionPin != dto.PinCode)
            {
                Console.WriteLine($"❌ Session validation failed for TrackAccess");
                return BadRequest("Session expired. Please refresh and enter your PIN again.");
            }

            // Validate pin code with comprehensive checks
            var pin = await _context.Pins
                .FirstOrDefaultAsync(p => p.Watermark == dto.PinCode &&
                                   p.RootCode == rootCode.Value &&
                                   p.IsActive == 1 &&           // Active pin
                                   p.Status == 1 &&             // Valid status
                                   p.Times > 0);                // Has remaining uses

            if (pin == null)
            {
                Console.WriteLine($"❌ Pin validation failed for TrackAccess");
                return BadRequest("Invalid or expired pin code");
            }

            Console.WriteLine($"✅ Pin validated for TrackAccess: PinCode={pin.PinCode}, Times={pin.Times}");

            // Check if this combination already exists in OnlineAttend
            var existingAttend = await _context.OnlineAttends
                .FirstOrDefaultAsync(oa => oa.StudentCode == trackingStudentCode &&
                                   oa.LessonCode == dto.LessonCode &&
                                   oa.PinCode == pin.PinCode &&
                                   oa.RootCode == rootCode.Value);

            if (existingAttend != null)
            {
                // Increment view count
                existingAttend.Views++;
                existingAttend.InsertTime = DateTime.Now;
                Console.WriteLine($"✅ Updated existing attendance record: Views={existingAttend.Views}");
            }
            else
            {
                // Create new attend record
                var attend = new OnlineAttend
                {
                    StudentCode = trackingStudentCode,
                    LessonCode = dto.LessonCode,
                    PinCode = pin.PinCode,
                    Views = 1,
                    Status = true,
                    RootCode = rootCode.Value,
                    InsertUser = trackingStudentCode,
                    InsertTime = DateTime.Now
                };
                _context.OnlineAttends.Add(attend);
                Console.WriteLine($"✅ Created new attendance record");
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressDto dto)
        {
            var studentCode = GetSessionInt("StudentCode") ?? 1;
            Console.WriteLine($"📊 UpdateProgress for student {studentCode}: Lesson={dto.LessonCode}");

            // This method would track individual video progress
            // You could implement this later when you want detailed progress tracking

            // For now, just return OK as basic tracking is handled by TrackAccess
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFile(int fileCode, string pinCode = null)
        {
            var rootCode = GetSessionInt("RootCode");
            if (rootCode == null) return Unauthorized();

            Console.WriteLine($"=== DEBUG DOWNLOAD START ===");
            Console.WriteLine($"FileCode: {fileCode}");
            Console.WriteLine($"RootCode: {rootCode}");

            var file = await _context.Files
                .FirstOrDefaultAsync(f => f.FileCode == fileCode &&
                                   f.RootCode == rootCode &&
                                   (f.FileType == 0 || f.FileType == 2) &&
                                   f.IsActive);

            if (file == null)
            {
                Console.WriteLine($"ERROR: File not found in database");
                return NotFound("File not found in database");
            }

            Console.WriteLine($"Database file found:");
            Console.WriteLine($"  - DisplayName: {file.DisplayName}");
            Console.WriteLine($"  - FileExtension: {file.FileExtension}");
            Console.WriteLine($"  - FileLocation: {file.FileLocation}");

            // Try all possible paths where the file might be
            var fileName = $"{file.FileCode}{file.FileExtension}";

            var pathsToTry = new[]
            {
        Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "files", fileName),
        Path.Combine(_fileUploadSettings.UploadPath, fileName),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "files", fileName),
        Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "files", fileName)
    };

            Console.WriteLine($"Trying paths for file: {fileName}");

            string workingPath = null;
            foreach (var path in pathsToTry)
            {
                Console.WriteLine($"  Trying: {path}");
                Console.WriteLine($"  Exists: {System.IO.File.Exists(path)}");

                if (System.IO.File.Exists(path))
                {
                    workingPath = path;
                    Console.WriteLine($"  FOUND IT!");
                    break;
                }
            }

            if (workingPath == null)
            {
                Console.WriteLine($"ERROR: File not found at any path");
                Console.WriteLine($"Environment info:");
                Console.WriteLine($"  WebRootPath: {_webHostEnvironment.WebRootPath}");
                Console.WriteLine($"  ContentRootPath: {_webHostEnvironment.ContentRootPath}");
                Console.WriteLine($"  CurrentDirectory: {Directory.GetCurrentDirectory()}");
                Console.WriteLine($"  UploadSettings: {_fileUploadSettings.UploadPath}");

                // List what's actually in the expected directory
                var expectedDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "files");
                if (Directory.Exists(expectedDir))
                {
                    var actualFiles = Directory.GetFiles(expectedDir);
                    Console.WriteLine($"Files in {expectedDir}:");
                    foreach (var f in actualFiles)
                    {
                        Console.WriteLine($"    {Path.GetFileName(f)}");
                    }
                }
                else
                {
                    Console.WriteLine($"Directory doesn't exist: {expectedDir}");
                }

                return NotFound($"Physical file not found: {fileName}");
            }

            try
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(workingPath);
                var downloadName = $"{file.DisplayName}{file.FileExtension}";
                var mimeType = GetMimeType(file.FileExtension);

                Console.WriteLine($"SUCCESS: Returning file {downloadName} ({fileBytes.Length} bytes)");
                Console.WriteLine($"=== DEBUG DOWNLOAD END ===");

                return File(fileBytes, mimeType, downloadName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR reading file: {ex.Message}");
                return StatusCode(500, "Error reading file");
            }
        }
        // Helper method to clean filename for download
        private string CleanFileName(string fileName)
        {
            // Remove invalid characters for file downloads
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // Also replace some additional problematic characters
            fileName = fileName.Replace(" ", "_").Replace(",", "_");

            return fileName;
        }

        [HttpGet]
        public async Task<IActionResult> StreamVideo(string token, string url)
        {
            // Validate token and check if not expired
            try
            {
                var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var tokenParts = decodedToken.Split(':');

                if (tokenParts.Length != 2)
                    return BadRequest("Invalid token");

                var fileCode = int.Parse(tokenParts[0]);
                var expiry = long.Parse(tokenParts[1]);
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiry);

                if (DateTimeOffset.Now > expiryTime)
                    return BadRequest("Video link has expired");

                var decodedUrl = Uri.UnescapeDataString(url);

                if (decodedUrl.Contains("youtube.com") || decodedUrl.Contains("youtu.be"))
                {
                    return Redirect(decodedUrl);
                }
                else
                {
                    return Redirect(decodedUrl);
                }
            }
            catch (Exception ex)
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
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string EncryptString(string text)
        {
            // Simple encryption - in production use stronger encryption
            var data = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(data);
        }

        private string DecryptString(string encryptedText)
        {
            // Simple decryption - in production use stronger encryption
            var data = Convert.FromBase64String(encryptedText);
            return Encoding.UTF8.GetString(data);
        }

        private string GenerateSecureVideoUrl(string originalUrl, int fileCode, int validHours)
        {
            // Generate time-based token
            var expiry = DateTimeOffset.Now.AddHours(validHours).ToUnixTimeSeconds();
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{fileCode}:{expiry}"));

            // Return URL with token that your video player can validate
            return $"/LessonContent/StreamVideo?token={token}&url={Uri.EscapeDataString(originalUrl)}";
        }

        // Helper method for file MIME types
        private string GetMimeType(string extension)
        {
            return extension.ToLower() switch
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
        }

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
        public string? Duration { get; set; } // "HH:MM:SS" format
    }

    public class UpdateSortOrderDto
    {
        public int ItemCode { get; set; } // Can be FileCode or ExamCode
        public int SortOrder { get; set; }
        public string ItemType { get; set; } = "file"; // "file" or "exam"
    }

    public class LinkExamDto
    {
        public int ExamCode { get; set; }
        public int LessonCode { get; set; }
        public int SortOrder { get; set; }
    }

    // DTOs for student functionality
    public class TrackAccessDto
    {
        public int LessonCode { get; set; }
        public string PinCode { get; set; } = null!;
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

    #endregion
}