using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using centrny.Models;

namespace centrny.Controllers
{
    [Route("[controller]")]
    public class StudentLoginController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<StudentLoginController> _logger;

        public StudentLoginController(CenterContext context, ILogger<StudentLoginController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Helper methods for session management
        private int GetSessionInt(string key) => (int)HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);

        private (int userCode, int groupCode, int rootCode, string username, bool isValidRoot) GetSessionContext()
        {
            var sessionRootCode = HttpContext.Session.GetInt32("RootCode");
            var domainRoot = _context.Roots
                .AsNoTracking()
                .Where(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", ""))
                .FirstOrDefault();

            var isValidRoot = sessionRootCode.HasValue &&
                             domainRoot != null &&
                             sessionRootCode.Value == domainRoot.RootCode;

            return (
                HttpContext.Session.GetInt32("UserCode") ?? 0,
                HttpContext.Session.GetInt32("GroupCode") ?? 0,
                domainRoot?.RootCode ?? 0,
                HttpContext.Session.GetString("Username") ?? "",
                isValidRoot
            );
        }

        private bool IsStudentRootValid()
        {
            var sessionRootCode = HttpContext.Session.GetInt32("RootCode");
            if (!sessionRootCode.HasValue) return false;

            var domainRoot = _context.Roots
                .AsNoTracking()
                .Where(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", ""))
                .FirstOrDefault();

            return domainRoot != null && sessionRootCode.Value == domainRoot.RootCode;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            var code = HttpContext.Session.GetInt32("StudentCode");

            if (code.HasValue)
            {
                // STEP 1: Verify domain/root matches session
                if (!IsStudentRootValid())
                {
                    _logger.LogWarning("StudentLogin Index: root domain mismatch for StudentCode={Code}. Clearing session.", code);
                    HttpContext.Session.Clear();
                    return View("Index");
                }

                // STEP 2: Verify student still exists and is active
                bool valid = _context.Students.Any(s => s.StudentCode == code && s.IsActive);
                if (valid)
                {
                    _logger.LogInformation("StudentLogin Index: existing valid session StudentCode={Code}", code);
                    return Redirect("/OnlineStudent");
                }

                _logger.LogInformation("StudentLogin Index: clearing stale/inactive session StudentCode={Code}", code);
                HttpContext.Session.Clear();
            }

            return View("Index");
        }

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Json(new { success = false, error = "Username and password are required." });

                string normalized = username.Trim().ToLower();
                _logger.LogInformation("Student login attempt: {User} on domain {Domain}", normalized, HttpContext.Request.Host.Host);

                // STEP 1: Get the root for current domain
                var domainRoot = await _context.Roots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", ""));

                if (domainRoot == null)
                {
                    _logger.LogWarning("Login attempt on unrecognized domain: {Domain}", HttpContext.Request.Host.Host);
                    return Json(new { success = false, error = "Invalid domain." });
                }

                // STEP 2: Find student with matching credentials AND root
                var student = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentUsername.ToLower() == normalized &&
                        s.StudentPassword == password &&
                        s.IsActive &&
                        s.RootCode == domainRoot.RootCode);  // Ensure student belongs to this domain

                if (student == null)
                {
                    _logger.LogWarning("Failed login attempt: {User} on domain {Domain}", normalized, HttpContext.Request.Host.Host);
                    return Json(new { success = false, error = "Invalid credentials or inactive account." });
                }

              
                // STEP 4: Set up session with validated data
                HttpContext.Session.SetInt32("StudentCode", student.StudentCode);
                HttpContext.Session.SetString("StudentUsername", student.StudentUsername);
                HttpContext.Session.SetInt32("RootCode", domainRoot.RootCode);
                _logger.LogInformation("Successful login: StudentCode={StudentCode}, RootCode={RootCode}",
                                     student.StudentCode, domainRoot.RootCode);

                return Json(new { success = true, redirectUrl = "/OnlineStudent" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Student login error for user {User}", username);
#if DEBUG
                return Json(new { success = false, error = ex.Message });
#else
                return Json(new { success = false, error = "An error occurred. Try again later." });
#endif
            }
        }

        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            var studentCode = HttpContext.Session.GetInt32("StudentCode");
            HttpContext.Session.Clear();

            _logger.LogInformation("Student logout: StudentCode={StudentCode}", studentCode);
            return Json(new { success = true, redirectUrl = Url.Content("/StudentLogin") });
        }

        // New method to validate root in other controllers
        public bool ValidateStudentRoot()
        {
            return IsStudentRootValid();
        }
    }
}