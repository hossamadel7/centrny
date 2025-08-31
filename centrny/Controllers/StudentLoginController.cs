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

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            var code = HttpContext.Session.GetInt32("StudentCode");
            if (code.HasValue)
            {
                // Verify student really exists and is active; otherwise clear stale session
                bool valid = _context.Students.Any(s => s.StudentCode == code && s.IsActive);
                if (valid)
                {
                    _logger.LogInformation("StudentLogin Index: existing active session StudentCode={Code}", code);
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
                _logger.LogInformation("Student login attempt: {User}", normalized);

                var student = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentUsername.ToLower() == normalized &&
                        s.StudentPassword == password &&
                        s.IsActive);

                if (student == null)
                    return Json(new { success = false, error = "Invalid credentials or inactive account." });

                var item = await _context.Items
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.StudentCode == student.StudentCode);

                if (item == null)
                    return Json(new { success = false, error = "Student is not registered." });

                HttpContext.Session.SetInt32("StudentCode", student.StudentCode);
                HttpContext.Session.SetString("StudentUsername", student.StudentUsername);
                if (student.RootCode is int rc)
                    HttpContext.Session.SetInt32("RootCode", rc);
                else
                    HttpContext.Session.Remove("RootCode");
                HttpContext.Session.SetString("ItemKey", item.ItemKey);

                return Json(new { success = true, redirectUrl = "/OnlineStudent" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Student login error");
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
            HttpContext.Session.Clear();
            return Json(new { success = true, redirectUrl = Url.Content("/StudentLogin") });
        }
    }
}