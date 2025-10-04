using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using centrny.Models;

namespace centrny.Controllers
{
    [Route("[controller]")]
    public class StudentLoginController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<StudentLoginController> _logger;
        private readonly IMemoryCache _cache;

        public StudentLoginController(CenterContext context, ILogger<StudentLoginController> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        private async Task<bool> IsStudentRootValidAsync()
        {
            var sessionRootCode = HttpContext.Session.GetInt32("RootCode");
            if (!sessionRootCode.HasValue) return false;

            var domain = HttpContext.Request.Host.Host.ToString().Replace("www.", "");
            var rootCode = await GetRootCodeForDomain(domain, HttpContext.RequestAborted);
            return rootCode != 0 && sessionRootCode.Value == rootCode;
        }

        private async Task<int> GetRootCodeForDomain(string domain, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(domain)) return 0;

            var cacheKey = $"root:{domain}";
            if (_cache.TryGetValue(cacheKey, out int cachedRoot))
                return cachedRoot;

            var rootCode = await _context.Roots
                .AsNoTracking()
                .Where(x => x.RootDomain == domain)
                .Select(x => x.RootCode)
                .FirstOrDefaultAsync(ct);

            if (rootCode != 0)
                _cache.Set(cacheKey, rootCode, TimeSpan.FromMinutes(10));
            else
                _cache.Set(cacheKey, 0, TimeSpan.FromMinutes(1)); // short negative cache

            return rootCode;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            // 1) Resolve RootCode from domain for Register links
            var domain = HttpContext.Request.Host.Host.ToString().Replace("www.", "");
            var rootCodeForLinks = await GetRootCodeForDomain(domain, HttpContext.RequestAborted);
            ViewBag.RootCode = rootCodeForLinks;

            // 2) Existing session handling
            var code = HttpContext.Session.GetInt32("StudentCode");

            if (code.HasValue)
            {
                if (!await IsStudentRootValidAsync())
                {
                    _logger.LogWarning("StudentLogin Index: root domain mismatch for StudentCode={Code}. Clearing session.", code);
                    HttpContext.Session.Clear();
                    return View("Index");
                }

                bool valid = await _context.Students.AnyAsync(s =>
                    s.StudentCode == code &&
                    s.IsActive &&
                    (s.IsConfirmed == true)
                );

                if (valid)
                {
                    _logger.LogInformation("StudentLogin Index: existing valid session StudentCode={Code}", code);
                    return Redirect("/OnlineStudent");
                }

                _logger.LogInformation("StudentLogin Index: clearing stale/inactive/unconfirmed session StudentCode={Code}", code);
                HttpContext.Session.Clear();
            }

            return View("Index");
        }

        private bool IsAjaxRequest()
        {
            var xrw = Request.Headers["X-Requested-With"].ToString();
            if (!string.IsNullOrEmpty(xrw) && xrw.Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                return true;

            var accept = Request.Headers.Accept.ToString();
            return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        }

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            bool isAjax = IsAjaxRequest();

            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    if (isAjax) return Json(new { success = false, error = "Username and password are required." });
                    ViewData["Error"] = "Username and password are required.";
                    return View("Index");
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                string trimmedUser = username.Trim();
                string domain = HttpContext.Request.Host.Host.ToString().Replace("www.", "");
                _logger.LogInformation("Student login attempt: {User} on domain {Domain}", trimmedUser, domain);

                var ct = HttpContext.RequestAborted;

                var rootCode = await GetRootCodeForDomain(domain, ct);
                if (rootCode == 0)
                {
                    _logger.LogWarning("Login attempt on unrecognized domain: {Domain}", domain);
                    if (isAjax) return Json(new { success = false, error = "Invalid domain." });
                    ViewData["Error"] = "Invalid domain.";
                    return View("Index");
                }

                var student = await _context.Students
                    .AsNoTracking()
                    .Where(s => s.RootCode == rootCode && s.IsActive && s.StudentUsername != null && s.StudentUsername == trimmedUser)
                    .Select(s => new { s.StudentCode, s.StudentUsername, s.StudentPassword, s.IsConfirmed })
                    .FirstOrDefaultAsync(ct);

                sw.Stop();
                _logger.LogInformation("Login DB lookup took {Ms} ms", sw.ElapsedMilliseconds);

                if (student == null || student.StudentPassword != password)
                {
                    _logger.LogInformation("Failed login (invalid creds): {User} on domain {Domain}", trimmedUser, domain);
                    if (isAjax) return Json(new { success = false, error = "Invalid username or password." });
                    ViewData["Error"] = "Invalid username or password.";
                    return View("Index");
                }

                if (student.IsConfirmed != true)
                {
                    _logger.LogInformation("Failed login (inactive): {User} on domain {Domain}", trimmedUser, domain);
                    if (isAjax) return Json(new { success = false, error = "inactive account." });
                    ViewData["Error"] = "inactive account.";
                    return View("Index");
                }

                HttpContext.Session.SetInt32("StudentCode", student.StudentCode);
                HttpContext.Session.SetString("StudentUsername", student.StudentUsername);
                HttpContext.Session.SetInt32("RootCode", rootCode);

                _logger.LogInformation("Successful login: StudentCode={StudentCode}, RootCode={RootCode}",
                    student.StudentCode, rootCode);

                if (isAjax) return Json(new { success = true, redirectUrl = "/OnlineStudent" });
                return Redirect("/OnlineStudent");
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Login request was canceled by the client");
                if (isAjax) return Json(new { success = false, error = "Request canceled." });
                ViewData["Error"] = "Request canceled.";
                return View("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Student login error for user {User}", username);
#if DEBUG
                var err = ex.Message;
#else
                var err = "An error occurred. Try again later.";
#endif
                if (isAjax) return Json(new { success = false, error = err });
                ViewData["Error"] = err;
                return View("Index");
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
    }
}