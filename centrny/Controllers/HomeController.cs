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
            var Ruselt = (from
                     c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", ""))
                          select new
                          {
                              title = c.Title,
                              web_header = c.WebLayoutH,
                              web_footer = c.WebLayoutF,
                              home = c.Home
                          }).ToList();
            ViewBag.title = Ruselt.Count > 0 ? Ruselt[0].title : "";
            ViewBag.web_header = Ruselt.Count > 0 ? Ruselt[0].web_header : "";
            ViewBag.web_footer = Ruselt.Count > 0 ? Ruselt[0].web_footer.Replace("{{date}}", DateTime.Now.Year.ToString()) : "";
            ViewBag.home = Ruselt.Count > 0 ? Ruselt[0].home.Replace("{{date}}", DateTime.Now.Year.ToString()) : "";
            return View();
        }

        public IActionResult Test()
        {
            return View();
        }

        public IActionResult About()
        {
            var Ruselt = (from
                    c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") || r.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString())
                          select new
                          {
                              title = c.Title,
                              web_header = c.WebLayoutH,
                              web_footer = c.WebLayoutF,
                              about = c.About
                          }).ToList();

            if (Ruselt.Count == 0 || Ruselt[0].about is null || Ruselt[0].about == "") return RedirectToAction("Index", "Home");

            ViewBag.title = Ruselt[0].title;
            ViewBag.web_header = Ruselt[0].web_header;
            ViewBag.web_footer = Ruselt[0].web_footer.Replace("{{date}}", DateTime.Now.Year.ToString());
            ViewBag.about = Ruselt[0].about;
            return View();
        }

        public IActionResult Coaches()
        {
            var Ruselt = (from
                   c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") || r.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString())
                          select new
                          {
                              title = c.Title,
                              web_header = c.WebLayoutH,
                              web_footer = c.WebLayoutF,
                              coaches = c.Teacher
                          }).ToList();

            if (Ruselt.Count == 0 || Ruselt[0].coaches is null || Ruselt[0].coaches == "") return RedirectToAction("Index", "Home");

            ViewBag.title = Ruselt[0].title;
            ViewBag.web_header = Ruselt[0].web_header;
            ViewBag.web_footer = Ruselt[0].web_footer.Replace("{{date}}", DateTime.Now.Year.ToString());
            ViewBag.coaches = Ruselt[0].coaches;
            return View();
        }

        public IActionResult Contact()
        {
            var Ruselt = (from
                   c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") || r.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString())
                          select new
                          {
                              title = c.Title,
                              web_header = c.WebLayoutH,
                              web_footer = c.WebLayoutF,
                              contact = c.Contact
                          }).ToList();

            if (Ruselt.Count == 0 || Ruselt[0].contact is null || Ruselt[0].contact == "") return RedirectToAction("Index", "Home");

            ViewBag.title = Ruselt[0].title;
            ViewBag.web_header = Ruselt[0].web_header;
            ViewBag.web_footer = Ruselt[0].web_footer.Replace("{{date}}", DateTime.Now.Year.ToString());
            ViewBag.contact = Ruselt[0].contact;
            return View();
        }

        public IActionResult Gallery()
        {
            var Ruselt = (from
                  c in DB.Contents
                          join r in DB.Roots on c.RootCode equals r.RootCode
                          where (r.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "") || r.RootName + ".gymsofto.com" == HttpContext.Request.Host.ToString())
                          select new
                          {
                              title = c.Title,
                              web_header = c.WebLayoutH,
                              web_footer = c.WebLayoutF,
                              gallery = c.Gallery
                          }).ToList();

            if (Ruselt.Count == 0 || Ruselt[0].gallery is null || Ruselt[0].gallery == "") return RedirectToAction("Index", "Home");

            ViewBag.title = Ruselt[0].title;
            ViewBag.web_header = Ruselt[0].web_header;
            ViewBag.web_footer = Ruselt[0].web_footer.Replace("{{date}}", DateTime.Now.Year.ToString());
            ViewBag.gallery = Ruselt[0].gallery;
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