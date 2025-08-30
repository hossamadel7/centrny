using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using centrny.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace centrny.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly CenterContext db = new CenterContext();

        // Get values from session
        private int? GetCurrentUserCode()
        {
            return HttpContext.Session.GetInt32("UserCode");
        }

        private int? GetCurrentRootCode()
        {
            return HttpContext.Session.GetInt32("RootCode");
        }

        public IActionResult Index()
        {
            var userCode = GetCurrentUserCode();
            var rootCode = GetCurrentRootCode();

            if (userCode == null || rootCode == null)
                return Unauthorized();

            ViewBag.Employees = db.Employees
                .Where(e => e.RootCode == rootCode.Value)
                .Select(e => new { e.EmployeeCode, e.EmployeeName })
                .ToList();

            return View();
        }

        [HttpGet]
        public IActionResult GetExpenses()
        {
            var userCode = GetCurrentUserCode();
            var rootCode = GetCurrentRootCode();

            if (userCode == null || rootCode == null)
                return Unauthorized();

            var expenses = db.Expenses
                .Where(e => e.RootCode == rootCode.Value && e.IsActive)
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
            var userCode = GetCurrentUserCode();
            var rootCode = GetCurrentRootCode();

            if (userCode == null || rootCode == null)
                return Unauthorized();

            expense.RootCode = rootCode.Value;
            expense.InsertUser = userCode.Value;
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
            var userCode = GetCurrentUserCode();
            var rootCode = GetCurrentRootCode();

            if (userCode == null || rootCode == null)
                return Unauthorized();

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
            var userCode = GetCurrentUserCode();
            var rootCode = GetCurrentRootCode();

            if (userCode == null || rootCode == null)
                return Unauthorized();

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
            var userCode = GetCurrentUserCode();
            var rootCode = GetCurrentRootCode();

            if (userCode == null || rootCode == null)
                return Unauthorized();

            var exp = db.Expenses.FirstOrDefault(e => e.ExpensesCode == id && e.IsActive);
            if (exp == null) return NotFound();

            exp.IsActive = false;
            db.SaveChanges();
            return Json(new { success = true });
        }
    }
}