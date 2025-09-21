using centrny.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Authorization;

namespace centrny.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        CenterContext DB = new CenterContext();

        public IActionResult Index()
        {
            // Get the domain with port (e.g., "localhost:7187")
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            // Get the domain without port (e.g., "localhost")
            var domainNoPort = HttpContext.Request.Host.Host;

            // Query for either match (with or without port), include both columns
            var Ruselt = (from c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where r.RootDomain == domain || r.RootDomain == domainNoPort
                          select new
                          {
                              root_code = c.RootCode,
                              title = c.Title,
                              title_ar = c.TitleAr, // Add Arabic title if exists
                              web_header = c.WebLayoutH,
                              web_header_ar = c.WebLayoutHAr, // Add Arabic header if exists
                              web_footer = c.WebLayoutF,
                              web_footer_ar = c.WebLayoutFAr, // Add Arabic footer if exists
                              home = c.Home,
                              home_ar = c.HomaAr // This property must exist in your Content model and DB
                          }).ToList();

            var culture = Request.Cookies["SelectedCulture"];
            ViewBag.domain = domain;
            ViewBag.domain_no_port = domainNoPort;
            ViewBag.root_code = Ruselt.Count > 0 ? Ruselt[0].root_code.ToString() : "NOT FOUND";

            // Set title based on culture
            ViewBag.title = Ruselt.Count > 0
                ? ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].title_ar)) ? Ruselt[0].title_ar : Ruselt[0].title)
                : "";

            // Set header based on culture
            ViewBag.web_header = Ruselt.Count > 0
                ? ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_header_ar)) ? Ruselt[0].web_header_ar : Ruselt[0].web_header)
                : "";

            // Set footer based on culture
            ViewBag.web_footer = Ruselt.Count > 0
                ? ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_footer_ar)) ? Ruselt[0].web_footer_ar : Ruselt[0].web_footer)?.Replace("{{date}}", DateTime.Now.Year.ToString())
                : "";

            // Show Arabic if cookie is "ar", otherwise show English
            ViewBag.home = Ruselt.Count > 0
                ? ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].home_ar)) ? Ruselt[0].home_ar : Ruselt[0].home)?.Replace("{{date}}", DateTime.Now.Year.ToString())
                : "";

            // Set current language for language switcher button
            ViewBag.CurrentLanguage = culture == "ar" ? "العربية" : "English";
            ViewBag.Culture = culture ?? "en";

            // Debug output for diagnostics
            System.Diagnostics.Debug.WriteLine($"Domain with port: {domain}");
            System.Diagnostics.Debug.WriteLine($"Domain without port: {domainNoPort}");
            if (Ruselt.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("RootCode: " + Ruselt[0].root_code);
                System.Diagnostics.Debug.WriteLine("Home value: " + Ruselt[0].home);
                System.Diagnostics.Debug.WriteLine("Home-ar value: " + Ruselt[0].home_ar);
                System.Diagnostics.Debug.WriteLine("Culture: " + culture);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Ruselt.Count == 0");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult SetCulture(string culture)
        {
            Response.Cookies.Append("SelectedCulture", culture, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(7) });
            return RedirectToAction("Index");
        }

        public IActionResult Test()
        {
            return View();
        }

        public IActionResult About()
        {
            var culture = Request.Cookies["SelectedCulture"];

            // Get domain information
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var fullHost = HttpContext.Request.Host.ToString();

            var Ruselt = (from c in DB.Contents
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
                          }).ToList();

            // Set ViewBag values
            if (Ruselt.Count == 0)
            {
                ViewBag.title = "About";
                ViewBag.web_header = "";
                ViewBag.web_footer = "";
                ViewBag.about = "<h1>About Page - No Content Found</h1>";
            }
            else
            {
                var aboutContent = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].about_ar)) ? Ruselt[0].about_ar : Ruselt[0].about;

                ViewBag.title = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].title_ar)) ? Ruselt[0].title_ar : Ruselt[0].title;
                ViewBag.web_header = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_header_ar)) ? Ruselt[0].web_header_ar : Ruselt[0].web_header;
                ViewBag.web_footer = ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_footer_ar)) ? Ruselt[0].web_footer_ar : Ruselt[0].web_footer)?.Replace("{{date}}", DateTime.Now.Year.ToString());
                ViewBag.about = string.IsNullOrWhiteSpace(aboutContent) ? "<h1>About Page - Content Empty</h1>" : aboutContent;
            }

            ViewBag.CurrentLanguage = culture == "ar" ? "العربية" : "English";
            ViewBag.Culture = culture ?? "en";

            return View();
        }

        public IActionResult Coaches()
        {
            var culture = Request.Cookies["SelectedCulture"];

            var Ruselt = (from c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") || r.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString())
                          select new
                          {
                              title = c.Title,
                              title_ar = c.TitleAr,
                              web_header = c.WebLayoutH,
                              web_header_ar = c.WebLayoutHAr,
                              web_footer = c.WebLayoutF,
                              web_footer_ar = c.WebLayoutFAr,
                              coaches = c.Teacher,
                              coaches_ar = c.TeacherAr // Add Arabic teacher if exists
                          }).ToList();

            if (Ruselt.Count == 0) return RedirectToAction("Index", "Home");

            // Check if we have content for the selected culture
            var coachesContent = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].coaches_ar)) ? Ruselt[0].coaches_ar : Ruselt[0].coaches;
            if (string.IsNullOrWhiteSpace(coachesContent)) return RedirectToAction("Index", "Home");

            ViewBag.title = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].title_ar)) ? Ruselt[0].title_ar : Ruselt[0].title;
            ViewBag.web_header = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_header_ar)) ? Ruselt[0].web_header_ar : Ruselt[0].web_header;
            ViewBag.web_footer = ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_footer_ar)) ? Ruselt[0].web_footer_ar : Ruselt[0].web_footer)?.Replace("{{date}}", DateTime.Now.Year.ToString());
            ViewBag.coaches = coachesContent;
            ViewBag.CurrentLanguage = culture == "ar" ? "العربية" : "English";
            ViewBag.Culture = culture ?? "en";

            return View();
        }

        public IActionResult Contact()
        {
            var culture = Request.Cookies["SelectedCulture"];

            var Ruselt = (from c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") || r.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString())
                          select new
                          {
                              title = c.Title,
                              title_ar = c.TitleAr,
                              web_header = c.WebLayoutH,
                              web_header_ar = c.WebLayoutHAr,
                              web_footer = c.WebLayoutF,
                              web_footer_ar = c.WebLayoutFAr,
                              contact = c.Contact,
                              contact_ar = c.ContactAr // Add Arabic contact if exists
                          }).ToList();

            if (Ruselt.Count == 0) return RedirectToAction("Index", "Home");

            // Check if we have content for the selected culture
            var contactContent = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].contact_ar)) ? Ruselt[0].contact_ar : Ruselt[0].contact;
            if (string.IsNullOrWhiteSpace(contactContent)) return RedirectToAction("Index", "Home");

            ViewBag.title = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].title_ar)) ? Ruselt[0].title_ar : Ruselt[0].title;
            ViewBag.web_header = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_header_ar)) ? Ruselt[0].web_header_ar : Ruselt[0].web_header;
            ViewBag.web_footer = ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_footer_ar)) ? Ruselt[0].web_footer_ar : Ruselt[0].web_footer)?.Replace("{{date}}", DateTime.Now.Year.ToString());
            ViewBag.contact = contactContent;
            ViewBag.CurrentLanguage = culture == "ar" ? "العربية" : "English";
            ViewBag.Culture = culture ?? "en";

            return View();
        }
        public IActionResult Students()
        {
            var culture = Request.Cookies["SelectedCulture"];

            // Get domain information
            var domain = HttpContext.Request.Host.ToString().Replace("www.", "");
            var domainNoPort = HttpContext.Request.Host.Host;
            var fullHost = HttpContext.Request.Host.ToString();

            var Ruselt = (from c in DB.Contents
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
                              outstanding_students_ar = c.OutstandingStudentsAr // Make sure this exists
                          }).ToList();

            // Set ViewBag values
            if (Ruselt.Count == 0)
            {
                ViewBag.title = "Outstanding Students";
                ViewBag.web_header = "";
                ViewBag.web_footer = "";
                ViewBag.outstanding_students = "<h1>No Outstanding Students found.</h1>";
            }
            else
            {
                var studentsContent = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].outstanding_students_ar)) ? Ruselt[0].outstanding_students_ar : Ruselt[0].outstanding_students;

                ViewBag.title = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].title_ar)) ? Ruselt[0].title_ar : Ruselt[0].title;
                ViewBag.web_header = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_header_ar)) ? Ruselt[0].web_header_ar : Ruselt[0].web_header;
                ViewBag.web_footer = ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_footer_ar)) ? Ruselt[0].web_footer_ar : Ruselt[0].web_footer)?.Replace("{{date}}", DateTime.Now.Year.ToString());
                ViewBag.outstanding_students = string.IsNullOrWhiteSpace(studentsContent) ? "<h1>No Outstanding Students found.</h1>" : studentsContent;
            }

            ViewBag.CurrentLanguage = culture == "ar" ? "العربية" : "English";
            ViewBag.Culture = culture ?? "en";

            return View();
        }

        public IActionResult Gallery()
        {
            var culture = Request.Cookies["SelectedCulture"];

            var Ruselt = (from c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") || r.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString())
                          select new
                          {
                              title = c.Title,
                              title_ar = c.TitleAr,
                              web_header = c.WebLayoutH,
                              web_header_ar = c.WebLayoutHAr,
                              web_footer = c.WebLayoutF,
                              web_footer_ar = c.WebLayoutFAr,
                              gallery = c.Gallery,
                              gallery_ar = c.GallerAr // Add Arabic gallery if exists
                          }).ToList();

            if (Ruselt.Count == 0) return RedirectToAction("Index", "Home");

            // Check if we have content for the selected culture
            var galleryContent = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].gallery_ar)) ? Ruselt[0].gallery_ar : Ruselt[0].gallery;
            if (string.IsNullOrWhiteSpace(galleryContent)) return RedirectToAction("Index", "Home");

            ViewBag.title = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].title_ar)) ? Ruselt[0].title_ar : Ruselt[0].title;
            ViewBag.web_header = (culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_header_ar)) ? Ruselt[0].web_header_ar : Ruselt[0].web_header;
            ViewBag.web_footer = ((culture == "ar" && !string.IsNullOrWhiteSpace(Ruselt[0].web_footer_ar)) ? Ruselt[0].web_footer_ar : Ruselt[0].web_footer)?.Replace("{{date}}", DateTime.Now.Year.ToString());
            ViewBag.gallery = galleryContent;
            ViewBag.CurrentLanguage = culture == "ar" ? "العربية" : "English";
            ViewBag.Culture = culture ?? "en";

            return View();
        }

        public string get_branches()
        {
            var Ruselt = DB.Branches
                .Where(x => x.IsActive == true &&
                    (x.RootCodeNavigation.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") ||
                     x.RootCodeNavigation.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString()))
                .Select(x => new { branch_code = x.BranchCode, branch_name = x.BranchName })
                .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(Ruselt);
        }

        public class table_data
        {
            public string branch_code { get; set; }
        }

        public string get_table([FromBody] table_data table_data)
        {
            var Ruselt = DB.Schedules
                .Where(x => x.BranchCode == Convert.ToInt32(table_data.branch_code)
                    && (x.BranchCodeNavigation.RootCodeNavigation.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") ||
                        x.BranchCodeNavigation.RootCodeNavigation.RootName + ".clasrio.com" == HttpContext.Request.Host.ToString()))
                .OrderBy(x => x.BranchCode)
                .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(Ruselt);
        }
    }
}