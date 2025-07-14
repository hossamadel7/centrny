using System.Linq;
using Microsoft.AspNetCore.Mvc;
using centrny.Models;
using Microsoft.AspNetCore.Authorization;
using System.Text;

namespace centrny.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly CenterContext db = new CenterContext();

        // --- Authority Check ---
        private bool UserHasExpensesPermission()
        {
            var username = User.Identity?.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return false;

            var userGroupCodes = db.Users
                .Where(ug => ug.UserCode == user.UserCode)
                .Select(ug => ug.GroupCode)
                .ToList();

            var page = db.Pages.FirstOrDefault(p => p.PagePath == "Expenses/Index");
            if (page == null)
                return false;

            return db.GroupPages.Any(gp => userGroupCodes.Contains(gp.GroupCode) && gp.PageCode == page.PageCode);
        }

        private User GetCurrentUser()
        {
            var username = User.Identity.Name;
            return db.Users.FirstOrDefault(u => u.Username == username);
        }

        public IActionResult Index()
        {
            if (!UserHasExpensesPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }

            var user = GetCurrentUser();
            if (user == null)
                return Unauthorized();

            var group = db.Groups.FirstOrDefault(g => g.GroupCode == user.GroupCode);
            if (group == null)
                return Unauthorized();

            bool isRootAdmin = group.RootCode == 1;
            ViewBag.IsRootAdmin = isRootAdmin;

            if (isRootAdmin)
            {
                ViewBag.Roots = db.Roots.Select(r => new { r.RootCode, r.RootName }).ToList();
                ViewBag.Expenses = null;
            }
            else
            {
                ViewBag.Roots = null;
                ViewBag.UserRootCode = group.RootCode;
                ViewBag.Expenses = db.Expenses.Where(e => e.RootCode == group.RootCode).ToList();
            }
            return View();
        }

        // Returns only the table HTML (no partial view!)
        [HttpGet]
        public IActionResult GetExpensesByRoot(int rootCode)
        {
            if (!UserHasExpensesPermission())
            {
                return Content("<div class='alert alert-danger'>Access denied.</div>", "text/html");
            }

            try
            {
                var expenses = db.Expenses.Where(e => e.RootCode == rootCode).ToList();

                var props = typeof(centrny.Models.Expense).GetProperties();
                var sb = new StringBuilder();

                if (expenses.Any())
                {
                    sb.Append("<table class=\"table table-bordered table-striped\"><thead><tr>");
                    foreach (var prop in props)
                        sb.Append($"<th>{prop.Name}</th>");
                    sb.Append("</tr></thead><tbody>");
                    foreach (var exp in expenses)
                    {
                        sb.Append("<tr>");
                        foreach (var prop in props)
                            sb.Append($"<td>{prop.GetValue(exp) ?? ""}</td>");
                        sb.Append("</tr>");
                    }
                    sb.Append("</tbody></table>");
                }
                else
                {
                    sb.Append("<div class=\"alert alert-info\">No expenses found for this root.</div>");
                }

                return Content(sb.ToString(), "text/html");
            }
            catch (System.Exception ex)
            {
                return Content("<div class='alert alert-danger'>Failed to load expenses: " +
                    ex.Message + "</div>", "text/html");
            }
        }
    }
}