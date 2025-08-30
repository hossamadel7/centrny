using centrny.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;

namespace centrny.Controllers
{
    [Authorize]
    public class ContentController : Controller
    {
        CenterContext DB = new CenterContext();

        // Helper: Check if user has permission for this page
        private bool UserHasPagePermission(string pagePath)
        {
            var groupCode = HttpContext.Session.GetInt32("GroupCode");
            if (groupCode == null) return false;

            var page = DB.Pages.FirstOrDefault(p => p.PagePath == pagePath);
            if (page == null) return false;

            var pageCode = page.PageCode;
            if (pageCode == 0) return false;

            return DB.GroupPages.Any(gp => gp.GroupCode == groupCode && gp.PageCode == pageCode);
        }

        public ActionResult Index()
        {
            string pagePath = $"{ControllerContext.ActionDescriptor.ControllerName}/Index";
            if (!UserHasPagePermission(pagePath))
                return View("~/Views/Login/AccessDenied.cshtml");

            return View();
        }

        // ALL ENDPOINTS BELOW HAVE NO RESTRICTION!
        public string get_gym()
        {
            var Ruselt = DB.Roots.Select(x => new { CODE = x.RootCode, NAME = x.RootName }).ToList();
            return Newtonsoft.Json.JsonConvert.SerializeObject(Ruselt);
        }

        public string get_column()
        {
            var Ruselt = new List<dynamic>
            {
                new { CODE = "Title", NAME = "Title" },
                new { CODE = "WebLayoutH", NAME = "WebLayoutH" },
                new { CODE = "WebLayoutF", NAME = "WebLayoutF" },
                new { CODE = "AppLayoutH", NAME = "AppLayoutH" },
                new { CODE = "AppLayoutF", NAME = "AppLayoutF" },
                new { CODE = "Home", NAME = "Home" },
                new { CODE = "About", NAME = "About" },
                new { CODE = "Coches", NAME = "Coches" },
                new { CODE = "Contact", NAME = "Contact" },
                new { CODE = "Apply", NAME = "Apply" },
                new { CODE = "Gallery", NAME = "Gallery" },
                new { CODE = "Login", NAME = "Login" },
                new { CODE = "First", NAME = "First" },
                new { CODE = "Renew", NAME = "Renew" },
                new { CODE = "Client", NAME = "Client" }
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(Ruselt);
        }

        public string getContent(int gym, string column)
        {
            var validColumns = new[] {
                "Title", "WebLayoutH", "WebLayoutF", "AppLayoutH", "AppLayoutF",
                "Home", "About", "Coches", "Contact", "Apply", "Gallery", "Login", "First", "Renew", "Client"
            };
            if (!validColumns.Contains(column))
                return Newtonsoft.Json.JsonConvert.SerializeObject(new List<string> { "" });

            var parameter = Expression.Parameter(typeof(Content), "x");
            var property = Expression.Property(parameter, column);
            var lambda = Expression.Lambda(property, parameter);

            var Ruselt = DB.Contents
                           .Where(x => x.RootCode == gym)
                           .Select(Expression.Lambda<Func<Content, object>>(
                               Expression.Convert(property, typeof(object)), parameter))
                           .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(Ruselt);
        }

        public class data
        {
            public int gym { get; set; }
            public string column { get; set; }
            public string content { get; set; }
        }

        public string save([FromBody] data d)
        {
            var validColumns = new[] {
                "Title", "WebLayoutH", "WebLayoutF", "AppLayoutH", "AppLayoutF",
                "Home", "About", "Coches", "Contact", "Apply", "Gallery", "Login", "First", "Renew", "Client"
            };
            if (!validColumns.Contains(d.column))
                return "0";

            Content c = DB.Contents.Where(x => x.RootCode == d.gym).FirstOrDefault();
            if (c == null) return "0";

            var propertyInfo = c.GetType().GetProperty(d.column);
            if (propertyInfo == null) return "0";

            propertyInfo.SetValue(c, d.content);

            DB.Entry(c).State = EntityState.Modified;
            DB.SaveChanges();
            return "1";
        }
    }
}