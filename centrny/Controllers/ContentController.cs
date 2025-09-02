using centrny.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;

namespace centrny.Controllers
{
    [Authorize]
    public class ContentController : Controller
    {
        private readonly CenterContext DB = new CenterContext();

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

        // Legacy endpoint (still works if you need it elsewhere)
        [HttpPost]
        public string get_gym()
        {
            var result = DB.Roots
                .Select(x => new { CODE = x.RootCode, NAME = x.RootName })
                .ToList();
            return Newtonsoft.Json.JsonConvert.SerializeObject(result);
        }

        public class GymFilterRequest
        {
            public string Mode { get; set; } = "all"; // all | center | noncenter
        }

        [HttpPost]
        public string get_gym_filtered([FromBody] GymFilterRequest req)
        {
            string mode = (req?.Mode ?? "all").ToLowerInvariant();
            var q = DB.Roots.AsQueryable();

            switch (mode)
            {
                case "center":
                    q = q.Where(r => r.IsCenter == true);
                    break;
                case "noncenter":
                    q = q.Where(r => r.IsCenter == false || r.IsCenter == null);
                    break;
                case "all":
                default:
                    break;
            }

            var result = q
                .OrderBy(r => r.RootName)
                .Select(x => new
                {
                    CODE = x.RootCode,
                    NAME = x.RootName,
                    IS_CENTER = x.IsCenter
                })
                .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(result);
        }

        [HttpPost]
        public string get_column()
        {
            // Exclude technical / key / navigation properties
            var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RootCode",
                "ContentCode",
                "RootCodeNavigation"
            };

            var columns = typeof(Content).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    p.CanRead &&
                    p.CanWrite &&
                    !exclude.Contains(p.Name) &&
                    (p.PropertyType == typeof(string) ||
                     p.PropertyType.IsValueType))
                .Select(p => new { CODE = p.Name, NAME = p.Name })
                .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(columns);
        }

        [HttpPost]
        public string getContent(int gym, string column)
        {
            var content = DB.Contents.FirstOrDefault(x => x.RootCode == gym);
            if (content == null)
                return Newtonsoft.Json.JsonConvert.SerializeObject(new List<string> { "" });

            var property = typeof(Content).GetProperty(column);
            if (property == null)
                return Newtonsoft.Json.JsonConvert.SerializeObject(new List<string> { "" });

            var value = property.GetValue(content);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new List<object> { value });
        }

        public class data
        {
            public int gym { get; set; }
            public string column { get; set; }
            public string content { get; set; }
        }

        [HttpPost]
        public string save([FromBody] data d)
        {
            if (d == null) return "0";
            var c = DB.Contents.FirstOrDefault(x => x.RootCode == d.gym);
            if (c == null) return "0";

            var propertyInfo = c.GetType().GetProperty(d.column ?? "", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propertyInfo == null || !propertyInfo.CanWrite) return "0";

            try
            {
                if (propertyInfo.PropertyType == typeof(string))
                {
                    propertyInfo.SetValue(c, d.content);
                }
                else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                {
                    if (int.TryParse(d.content, out int iv))
                        propertyInfo.SetValue(c, iv);
                }
                else if (propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(d.content, out DateTime dtv))
                        propertyInfo.SetValue(c, dtv);
                }
                DB.Entry(c).State = EntityState.Modified;
                DB.SaveChanges();
                return "1";
            }
            catch
            {
                return "0";
            }
        }

        [HttpPost]
        public string getRootColorsAll([FromBody] data d)
        {
            if (d == null) return Newtonsoft.Json.JsonConvert.SerializeObject(new { });
            var root = DB.Roots.FirstOrDefault(x => x.RootCode == d.gym);
            if (root == null)
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { });

            var colorColumns = new[]
            {
                "RootBodyColor", "RootButtonColor", "RootBodyFont",
                "RootButtonFontColor", "RootButtonFontColor2", "RootBackgroundColor"
            };

            var dict = new Dictionary<string, string>();
            foreach (var cc in colorColumns)
            {
                var prop = typeof(Root).GetProperty(cc);
                if (prop != null)
                {
                    var val = prop.GetValue(root)?.ToString() ?? "";
                    dict[cc] = val;
                }
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(dict);
        }

        [HttpPost]
        public string saveRootColumn([FromBody] data d)
        {
            if (d == null) return "0";
            var root = DB.Roots.FirstOrDefault(x => x.RootCode == d.gym);
            if (root == null) return "0";

            var propertyInfo = root.GetType().GetProperty(d.column ?? "", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propertyInfo == null || !propertyInfo.CanWrite) return "0";

            try
            {
                if (propertyInfo.PropertyType == typeof(string))
                {
                    string incoming = d.content?.Trim() ?? "";
                    if (string.IsNullOrEmpty(incoming))
                    {
                        // skip clearing (as per earlier requirement)
                        return "1";
                    }
                    if (!incoming.StartsWith("#"))
                        incoming = "#" + incoming;
                    incoming = incoming.ToUpperInvariant();
                    if (Regex.IsMatch(incoming, "^#[0-9A-F]{3}$"))
                    {
                        incoming = "#" + incoming[1] + incoming[1] + incoming[2] + incoming[2] + incoming[3] + incoming[3];
                    }
                    if (!Regex.IsMatch(incoming, "^#[0-9A-F]{6}$"))
                        return "0";
                    propertyInfo.SetValue(root, incoming);
                }
                else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                {
                    if (int.TryParse(d.content, out int iv))
                        propertyInfo.SetValue(root, iv);
                    else
                        return "0";
                }
                else if (propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(d.content, out DateTime dtv))
                        propertyInfo.SetValue(root, dtv);
                    else
                        return "0";
                }
                else
                {
                    return "0";
                }

                DB.Entry(root).State = EntityState.Modified;
                DB.SaveChanges();
                return "1";
            }
            catch
            {
                return "0";
            }
        }
    }
}