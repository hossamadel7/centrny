using centrny.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using Microsoft.AspNetCore.Authorization;

namespace centrny.Controllers
{

    public class HomeController : Controller
    {
        private readonly CenterContext DB = new CenterContext();

        
        private static string ApplyTokens(string s, string rootCode, string currentYear)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var registerUrl = string.IsNullOrWhiteSpace(rootCode) ? "/Register" : $"/Register/{rootCode}";
            return s
                .Replace("{{register_url}}", registerUrl)
                .Replace("{{root_code}}", rootCode ?? "")
                .Replace("{{date}}", currentYear);
        }

        public IActionResult Index()
        {
            // Host and culture
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var culture = Request.Cookies["SelectedCulture"]; // "ar" or "en" (null => en)
            var currentYear = DateTime.Now.Year.ToString();

            // Load content for this root/domain
            var row = (
                from c in DB.Contents
                join r in DB.Roots on c.RootCode equals r.RootCode
                where r.RootDomain == domain || r.RootDomain == domainNoPort
                select new
                {
                    root_code = c.RootCode,
                    title = c.Title,
                    title_ar = c.TitleAr,
                    web_header = c.WebLayoutH,
                    web_header_ar = c.WebLayoutHAr,
                    web_footer = c.WebLayoutF,
                    web_footer_ar = c.WebLayoutFAr,
                    home = c.Home,
                    home_ar = c.HomaAr
                }
            ).FirstOrDefault();

            // Defaults
            string title = "Home";
            string web_header = "";
            string web_footer = "";
            string home = "";
            string rootCodeStr = "";

            if (row == null)
            {
                ViewBag.root_code = "NOT_FOUND";
            }
            else
            {
                var useArabic = string.Equals(culture, "ar", StringComparison.OrdinalIgnoreCase);

                rootCodeStr = row.root_code.ToString() ;
                ViewBag.root_code = string.IsNullOrWhiteSpace(rootCodeStr) ? "NOT_FOUND" : rootCodeStr;

                title = useArabic && !string.IsNullOrWhiteSpace(row.title_ar) ? row.title_ar : row.title;

                var headerRaw = useArabic && !string.IsNullOrWhiteSpace(row.web_header_ar) ? row.web_header_ar : row.web_header;
                var footerRaw = useArabic && !string.IsNullOrWhiteSpace(row.web_footer_ar) ? row.web_footer_ar : row.web_footer;
                var chosenHome = useArabic && !string.IsNullOrWhiteSpace(row.home_ar) ? row.home_ar : row.home;

                // Inject tokens
                web_header = ApplyTokens(headerRaw, rootCodeStr, currentYear);
                web_footer = ApplyTokens(footerRaw, rootCodeStr, currentYear);
                home = ApplyTokens(chosenHome, rootCodeStr, currentYear);
            }

            ViewBag.title = title;
            ViewBag.web_header = web_header;
            ViewBag.web_footer = web_footer;
            ViewBag.home = home;

            ViewBag.Culture = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
            ViewBag.CurrentLanguage = string.Equals(ViewBag.Culture, "ar", StringComparison.OrdinalIgnoreCase) ? "العربية" : "English";

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult SetCulture(string culture)
        {
            if (string.IsNullOrWhiteSpace(culture)) culture = "en";
            Response.Cookies.Append("SelectedCulture", culture, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(7), Path = "/" });
            return RedirectToAction(nameof(Index));
        }

        public IActionResult About()
        {
            var culture = Request.Cookies["SelectedCulture"];
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var fullHost = HttpContext.Request.Host.ToString();
            var currentYear = DateTime.Now.Year.ToString();

            var res = (from c in DB.Contents
                       join r in DB.Roots on c.RootCode equals r.RootCode
                       where (r.RootDomain == domain || r.RootDomain == domainNoPort || r.RootName + ".gymsofto.com" == fullHost)
                       select new
                       {
                           root_code = c.RootCode,
                           title = c.Title,
                           title_ar = c.TitleAr,
                           web_header = c.WebLayoutH,
                           web_header_ar = c.WebLayoutHAr,
                           web_footer = c.WebLayoutF,
                           web_footer_ar = c.WebLayoutFAr,
                           about = c.About,
                           about_ar = c.AboutAr
                       }).FirstOrDefault();

            if (res == null)
            {
                ViewBag.title = "About";
                ViewBag.web_header = "";
                ViewBag.web_footer = "";
                ViewBag.about = "<h1>About Page - No Content Found</h1>";
            }
            else
            {
                var useArabic = string.Equals(culture, "ar", StringComparison.OrdinalIgnoreCase);
                var rootCodeStr = res.root_code.ToString();
                ViewBag.root_code = string.IsNullOrWhiteSpace(rootCodeStr) ? "NOT_FOUND" : rootCodeStr;

                ViewBag.title = useArabic && !string.IsNullOrWhiteSpace(res.title_ar) ? res.title_ar : res.title;

                var headerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_header_ar) ? res.web_header_ar : res.web_header;
                var footerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_footer_ar) ? res.web_footer_ar : res.web_footer;
                var content = useArabic && !string.IsNullOrWhiteSpace(res.about_ar) ? res.about_ar : res.about;

                ViewBag.web_header = ApplyTokens(headerRaw, rootCodeStr, currentYear);
                ViewBag.web_footer = ApplyTokens(footerRaw, rootCodeStr, currentYear);
                ViewBag.about = string.IsNullOrWhiteSpace(content) ? "<h1>About Page - Content Empty</h1>" : ApplyTokens(content, rootCodeStr, currentYear);
            }

            ViewBag.Culture = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
            ViewBag.CurrentLanguage = string.Equals(ViewBag.Culture, "ar", StringComparison.OrdinalIgnoreCase) ? "العربية" : "English";
            return View();
        }

        public IActionResult Courses()
        {
            var culture = Request.Cookies["SelectedCulture"];
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var fullHost = HttpContext.Request.Host.ToString();
            var currentYear = DateTime.Now.Year.ToString();

            var res = (from c in DB.Contents
                       join r in DB.Roots on c.RootCode equals r.RootCode
                       where (r.RootDomain == domain || r.RootDomain == domainNoPort || r.RootName + ".Clasrio.com" == fullHost)
                       select new
                       {
                           root_code = c.RootCode,
                           title = c.Title,
                           title_ar = c.TitleAr,
                           web_header = c.WebLayoutH,
                           web_header_ar = c.WebLayoutHAr,
                           web_footer = c.WebLayoutF,
                           web_footer_ar = c.WebLayoutFAr,
                           courses = c.Courses,
                           courses_ar = c.CoursesAr
                       }).FirstOrDefault();

            if (res == null)
            {
                ViewBag.title = "Courses";
                ViewBag.web_header = "";
                ViewBag.web_footer = "";
                ViewBag.courses = "<h1>No Courses found.</h1>";
            }
            else
            {
                var useArabic = string.Equals(culture, "ar", StringComparison.OrdinalIgnoreCase);
                var rootCodeStr = res.root_code.ToString();
                ViewBag.root_code = string.IsNullOrWhiteSpace(rootCodeStr) ? "NOT_FOUND" : rootCodeStr;

                ViewBag.title = useArabic && !string.IsNullOrWhiteSpace(res.title_ar) ? res.title_ar : res.title;

                var headerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_header_ar) ? res.web_header_ar : res.web_header;
                var footerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_footer_ar) ? res.web_footer_ar : res.web_footer;
                var content = useArabic && !string.IsNullOrWhiteSpace(res.courses_ar) ? res.courses_ar : res.courses;

                ViewBag.web_header = ApplyTokens(headerRaw, rootCodeStr, currentYear);
                ViewBag.web_footer = ApplyTokens(footerRaw, rootCodeStr, currentYear);
                ViewBag.courses = string.IsNullOrWhiteSpace(content) ? "<h1>No Courses found.</h1>" : ApplyTokens(content, rootCodeStr, currentYear);
            }

            ViewBag.Culture = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
            ViewBag.CurrentLanguage = string.Equals(ViewBag.Culture, "ar", StringComparison.OrdinalIgnoreCase) ? "العربية" : "English";
            return View();
        }

        public IActionResult Center()
        {
            var culture = Request.Cookies["SelectedCulture"];
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var fullHost = HttpContext.Request.Host.ToString();
            var currentYear = DateTime.Now.Year.ToString();

            var res = (from c in DB.Contents
                       join r in DB.Roots on c.RootCode equals r.RootCode
                       where (r.RootDomain == domain || r.RootDomain == domainNoPort || r.RootName + ".gymsofto.com" == fullHost)
                       select new
                       {
                           root_code = c.RootCode,
                           title = c.Title,
                           title_ar = c.TitleAr,
                           web_header = c.WebLayoutH,
                           web_header_ar = c.WebLayoutHAr,
                           web_footer = c.WebLayoutF,
                           web_footer_ar = c.WebLayoutFAr,
                           center = c.Center,
                           center_ar = c.CenterAr
                       }).FirstOrDefault();

            if (res == null)
            {
                ViewBag.title = "Center";
                ViewBag.web_header = "";
                ViewBag.web_footer = "";
                ViewBag.center = "<h1>No Center info found.</h1>";
            }
            else
            {
                var useArabic = string.Equals(culture, "ar", StringComparison.OrdinalIgnoreCase);
                var rootCodeStr = res.root_code.ToString();
                ViewBag.root_code = string.IsNullOrWhiteSpace(rootCodeStr) ? "NOT_FOUND" : rootCodeStr;

                ViewBag.title = useArabic && !string.IsNullOrWhiteSpace(res.title_ar) ? res.title_ar : res.title;

                var headerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_header_ar) ? res.web_header_ar : res.web_header;
                var footerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_footer_ar) ? res.web_footer_ar : res.web_footer;
                var content = useArabic && !string.IsNullOrWhiteSpace(res.center_ar) ? res.center_ar : res.center;

                ViewBag.web_header = ApplyTokens(headerRaw, rootCodeStr, currentYear);
                ViewBag.web_footer = ApplyTokens(footerRaw, rootCodeStr, currentYear);
                ViewBag.center = string.IsNullOrWhiteSpace(content) ? "<h1>No Center info found.</h1>" : ApplyTokens(content, rootCodeStr, currentYear);
            }

            ViewBag.Culture = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
            ViewBag.CurrentLanguage = string.Equals(ViewBag.Culture, "ar", StringComparison.OrdinalIgnoreCase) ? "العربية" : "English";
            return View();
        }

        public IActionResult Students()
        {
            var culture = Request.Cookies["SelectedCulture"];
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var fullHost = HttpContext.Request.Host.ToString();
            var currentYear = DateTime.Now.Year.ToString();

            var res = (from c in DB.Contents
                       join r in DB.Roots on c.RootCode equals r.RootCode
                       where (r.RootDomain == domain || r.RootDomain == domainNoPort || r.RootName + ".gymsofto.com" == fullHost)
                       select new
                       {
                           root_code = c.RootCode,
                           title = c.Title,
                           title_ar = c.TitleAr,
                           web_header = c.WebLayoutH,
                           web_header_ar = c.WebLayoutHAr,
                           web_footer = c.WebLayoutF,
                           web_footer_ar = c.WebLayoutFAr,
                           outstanding_students = c.OutstandingStudents,
                           outstanding_students_ar = c.OutstandingStudentsAr
                       }).FirstOrDefault();

            if (res == null)
            {
                ViewBag.title = "Outstanding Students";
                ViewBag.web_header = "";
                ViewBag.web_footer = "";
                ViewBag.outstanding_students = "<h1>No Outstanding Students found.</h1>";
            }
            else
            {
                var useArabic = string.Equals(culture, "ar", StringComparison.OrdinalIgnoreCase);
                var rootCodeStr = res.root_code.ToString();
                ViewBag.root_code = string.IsNullOrWhiteSpace(rootCodeStr) ? "NOT_FOUND" : rootCodeStr;

                ViewBag.title = useArabic && !string.IsNullOrWhiteSpace(res.title_ar) ? res.title_ar : res.title;

                var headerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_header_ar) ? res.web_header_ar : res.web_header;
                var footerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_footer_ar) ? res.web_footer_ar : res.web_footer;
                var content = useArabic && !string.IsNullOrWhiteSpace(res.outstanding_students_ar) ? res.outstanding_students_ar : res.outstanding_students;

                ViewBag.web_header = ApplyTokens(headerRaw, rootCodeStr, currentYear);
                ViewBag.web_footer = ApplyTokens(footerRaw, rootCodeStr, currentYear);
                ViewBag.outstanding_students = string.IsNullOrWhiteSpace(content) ? "<h1>No Outstanding Students found.</h1>" : ApplyTokens(content, rootCodeStr, currentYear);
            }

            ViewBag.Culture = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
            ViewBag.CurrentLanguage = string.Equals(ViewBag.Culture, "ar", StringComparison.OrdinalIgnoreCase) ? "العربية" : "English";
            return View();
        }

        public IActionResult Test()
        {
            var culture = Request.Cookies["SelectedCulture"];
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var fullHost = HttpContext.Request.Host.ToString();
            var currentYear = DateTime.Now.Year.ToString();

            var res = (from c in DB.Contents
                       join r in DB.Roots on c.RootCode equals r.RootCode
                       where (r.RootDomain == domain || r.RootDomain == domainNoPort || r.RootName + ".gymsofto.com" == fullHost)
                       select new
                       {
                           root_code = c.RootCode,
                           title = c.Title,
                           title_ar = c.TitleAr,
                           web_header = c.WebLayoutH,
                           web_header_ar = c.WebLayoutHAr,
                           web_footer = c.WebLayoutF,
                           web_footer_ar = c.WebLayoutFAr,
                           about = c.About,
                           about_ar = c.AboutAr
                       }).FirstOrDefault();

            if (res == null)
            {
                ViewBag.title = "About";
                ViewBag.web_header = "";
                ViewBag.web_footer = "";
                ViewBag.about = "<h1>About Page - No Content Found</h1>";
            }
            else
            {
                var useArabic = string.Equals(culture, "ar", StringComparison.OrdinalIgnoreCase);
                var rootCodeStr = res.root_code.ToString();
                ViewBag.root_code = string.IsNullOrWhiteSpace(rootCodeStr) ? "NOT_FOUND" : rootCodeStr;

                ViewBag.title = useArabic && !string.IsNullOrWhiteSpace(res.title_ar) ? res.title_ar : res.title;

                var headerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_header_ar) ? res.web_header_ar : res.web_header;
                var footerRaw = useArabic && !string.IsNullOrWhiteSpace(res.web_footer_ar) ? res.web_footer_ar : res.web_footer;
                var content = useArabic && !string.IsNullOrWhiteSpace(res.about_ar) ? res.about_ar : res.about;

                ViewBag.web_header = ApplyTokens(headerRaw, rootCodeStr, currentYear);
                ViewBag.web_footer = ApplyTokens(footerRaw, rootCodeStr, currentYear);
                ViewBag.about = string.IsNullOrWhiteSpace(content) ? "<h1>About Page - Content Empty</h1>" : ApplyTokens(content, rootCodeStr, currentYear);
            }

            ViewBag.Culture = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
            ViewBag.CurrentLanguage = string.Equals(ViewBag.Culture, "ar", StringComparison.OrdinalIgnoreCase) ? "العربية" : "English";
            return View();
        }

        public IActionResult Gallery()
        {
            var culture = Request.Cookies["SelectedCulture"];
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var fullHost = HttpContext.Request.Host.ToString();
            var currentYear = DateTime.Now.Year.ToString();

            var res = (from c in DB.Contents
                       join r in DB.Roots on c.RootCode equals r.RootCode
                       where (r.RootDomain == domain || r.RootName + ".clasrio.com" == fullHost)
                       select new
                       {
                           root_code = c.RootCode,
                           title = c.Title,
                           title_ar = c.TitleAr,
                           web_header = c.WebLayoutH,
                           web_header_ar = c.WebLayoutHAr,
                           web_footer = c.WebLayoutF,
                           web_footer_ar = c.WebLayoutFAr,
                           gallery = c.Gallery
                       }).FirstOrDefault();

            if (res == null || string.IsNullOrWhiteSpace(res.gallery))
                return RedirectToAction(nameof(Index));

            var rootCodeStr = res.root_code.ToString();
            ViewBag.root_code = string.IsNullOrWhiteSpace(rootCodeStr) ? "NOT_FOUND" : rootCodeStr;
            ViewBag.title = res.title;
            var headerRaw = res.web_header;
            var footerRaw = res.web_footer;
            ViewBag.web_header = ApplyTokens(headerRaw, rootCodeStr, currentYear);
            ViewBag.web_footer = ApplyTokens(footerRaw, rootCodeStr, currentYear);
            ViewBag.Gallery = ApplyTokens(res.gallery, rootCodeStr, currentYear);

            ViewBag.Culture = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
            ViewBag.CurrentLanguage = string.Equals(ViewBag.Culture, "ar", StringComparison.OrdinalIgnoreCase) ? "العربية" : "English";
            return View();
        }

        // API helpers
        public string get_branches()
        {
            var data = DB.Branches
                .Where(x => x.IsActive == true &&
                    (x.RootCodeNavigation.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") ||
                     x.RootCodeNavigation.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString()))
                .Select(x => new { branch_code = x.BranchCode, branch_name = x.BranchName })
                .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(data);
        }

        public class table_data { public string branch_code { get; set; } }

        public string get_table([FromBody] table_data tableData)
        {
            var data = DB.Schedules
                .Where(x => x.BranchCode == Convert.ToInt32(tableData.branch_code)
                    && (x.BranchCodeNavigation.RootCodeNavigation.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") ||
                        x.BranchCodeNavigation.RootCodeNavigation.RootName + ".clasrio.com" == HttpContext.Request.Host.ToString()))
                .OrderBy(x => x.BranchCode)
                .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(data);
        }
    }
}