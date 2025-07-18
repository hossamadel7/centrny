using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using centrny.Models;
using Microsoft.AspNetCore.Authorization;

namespace centrny.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly CenterContext db = new CenterContext();

        private User GetCurrentUser()
        {
            var username = User.Identity.Name;
            return db.Users.FirstOrDefault(u => u.Username == username);
        }

        private int GetCurrentRootCode()
        {
            var user = GetCurrentUser();
            if (user == null) return 0;
            var group = db.Groups.FirstOrDefault(g => g.GroupCode == user.GroupCode);
            return group?.RootCode ?? 0;
        }

        public IActionResult Index()
        {
            var user = GetCurrentUser();
            if (user == null) return Unauthorized();
            int rootCode = GetCurrentRootCode();

            ViewBag.Employees = db.Employees
                .Where(e => e.RootCode == rootCode)
                .Select(e => new { e.EmployeeCode, e.EmployeeName })
                .ToList();

            return View();
        }

        [HttpGet]
        public IActionResult GetExpenses()
        {
            var user = GetCurrentUser();
            if (user == null) return Unauthorized();
            int rootCode = GetCurrentRootCode();

            var expenses = db.Expenses
                .Where(e => e.RootCode == rootCode && e.IsActive)
                .OrderByDescending(e => e.ExpenseTime)
                .Select(e => new
                {
                    expensesCode = e.ExpensesCode,
                    expensesReason = e.ExpensesReason,
                    expensesAmount = e.ExpensesAmount,
                    employeeName = db.Employees.Where(emp => emp.EmployeeCode == e.EmployeeCode).Select(emp => emp.EmployeeName).FirstOrDefault(),
                    expenseTime = e.ExpenseTime.ToString("yyyy-MM-dd")
                })
                .ToList();

            return Json(new { data = expenses });
        }

        [HttpPost]
        public IActionResult AddExpense([FromForm] Expense expense)
        {
            var user = GetCurrentUser();
            if (user == null) return Unauthorized();

            expense.RootCode = GetCurrentRootCode();
            expense.InsertUser = user.UserCode;
            expense.InsertTime = DateTime.Now;
            expense.ExpenseTime = DateOnly.FromDateTime(DateTime.Now);
            expense.IsActive = true;

            db.Expenses.Add(expense);
            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult GetExpense(int id)
        {
            var exp = db.Expenses
                .Where(e => e.ExpensesCode == id && e.IsActive)
                .Select(e => new {
                    expensesCode = e.ExpensesCode,
                    expensesReason = e.ExpensesReason,
                    expensesAmount = e.ExpensesAmount,
                    employeeCode = e.EmployeeCode,
                    expenseTime = e.ExpenseTime.ToString("yyyy-MM-dd")
                })
                .FirstOrDefault();

            if (exp == null) return NotFound();
            return Json(exp);
        }

        [HttpPost]
        public IActionResult EditExpense([FromForm] Expense expense)
        {
            var exp = db.Expenses.FirstOrDefault(e => e.ExpensesCode == expense.ExpensesCode && e.IsActive);
            if (exp == null) return NotFound();

            exp.ExpensesReason = expense.ExpensesReason;
            exp.ExpensesAmount = expense.ExpensesAmount;
            exp.EmployeeCode = expense.EmployeeCode;
            exp.ExpenseTime = expense.ExpenseTime;

            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult DeleteExpense(int id)
        {
            var exp = db.Expenses.FirstOrDefault(e => e.ExpensesCode == id && e.IsActive);
            if (exp == null) return NotFound();

            exp.IsActive = false;
            db.SaveChanges();
            return Json(new { success = true });
        }
    }
}