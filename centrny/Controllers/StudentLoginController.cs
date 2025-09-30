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

        private bool IsStudentRootValid()
        {
            var sessionRootCode = HttpContext.Session.GetInt32("RootCode");
            if (!sessionRootCode.HasValue) return false;

            var domain = HttpContext.Request.Host.Host.ToString().Replace("www.", "");
            var rootCode = GetRootCodeForDomain(domain).GetAwaiter().GetResult();
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

            return rootCode;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            var code = HttpContext.Session.GetInt32("StudentCode");

            if (code.HasValue)
            {
                if (!IsStudentRootValid())
                {
                    _logger.LogWarning("StudentLogin Index: root domain mismatch for StudentCode={Code}. Clearing session.", code);
                    HttpContext.Session.Clear();
                    return View("Index");
                }

                bool valid = _context.Students.Any(s =>
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

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Json(new { success = false, error = "Username and password are required." });

                var sw = System.Diagnostics.Stopwatch.StartNew();
                string normalized = username.Trim().ToLowerInvariant();
                string domain = HttpContext.Request.Host.Host.ToString().Replace("www.", "");
                _logger.LogInformation("Student login attempt: {User} on domain {Domain}", normalized, domain);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                // 1) Resolve root for this domain (cached)
                var rootCode = await GetRootCodeForDomain(domain, cts.Token);
                if (rootCode == 0)
                {
                    _logger.LogWarning("Login attempt on unrecognized domain: {Domain}", domain);
                    return Json(new { success = false, error = "Invalid domain." });
                }

                // 2) Fast path: exact match (no ToLower on column). Works if DB collation is case-insensitive.
                var student = await _context.Students
                    .AsNoTracking()
                    .Where(s => s.RootCode == rootCode && s.IsActive && s.StudentUsername != null && s.StudentUsername == normalized)
                    .Select(s => new { s.StudentCode, s.StudentUsername, s.StudentPassword, s.IsConfirmed })
                    .FirstOrDefaultAsync(cts.Token);

                // 3) Fallback path: case-insensitive scan using ToLower only if fast path failed.
                if (student == null)
                {
                    student = await _context.Students
                        .AsNoTracking()
                        .Where(s => s.RootCode == rootCode && s.IsActive && s.StudentUsername != null)
                        .Where(s => s.StudentUsername.ToLower() == normalized) // fallback (may be slower)
                        .Select(s => new { s.StudentCode, s.StudentUsername, s.StudentPassword, s.IsConfirmed })
                        .FirstOrDefaultAsync(cts.Token);
                }

                sw.Stop();
                _logger.LogInformation("Login DB lookup took {Ms} ms", sw.ElapsedMilliseconds);

                // 4) Differentiate errors as requested
                if (student == null || student.StudentPassword != password)
                {
                    _logger.LogWarning("Failed login (invalid creds): {User} on domain {Domain}", normalized, domain);
                    return Json(new { success = false, error = "Invalid username or password." });
                }

                if (student.IsConfirmed != true)
                {
                    _logger.LogWarning("Failed login (unconfirmed): {User} on domain {Domain}", normalized, domain);
                    return Json(new { success = false, error = "inactive account." });
                }

                // 5) Set session
                HttpContext.Session.SetInt32("StudentCode", student.StudentCode);
                HttpContext.Session.SetString("StudentUsername", student.StudentUsername);
                HttpContext.Session.SetInt32("RootCode", rootCode);

                _logger.LogInformation("Successful login: StudentCode={StudentCode}, RootCode={RootCode}",
                    student.StudentCode, rootCode);

                return Json(new { success = true, redirectUrl = "/OnlineStudent" });
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Login request timed out");
                return Json(new { success = false, error = "Login timed out. Please try again." });
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
    }
}