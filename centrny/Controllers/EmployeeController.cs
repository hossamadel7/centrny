using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;

namespace centrny.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {
        private readonly CenterContext _context;

        public EmployeeController(CenterContext context)
        {
            _context = context;
        }

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);

        /// <summary>
        /// For convenience, get all main session context at once.
        /// Assumes you set these at login: RootCode, UserCode, GroupCode, Username
        /// </summary>
        private (int? rootCode, int? userCode, int? groupCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("RootCode"),
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                GetSessionString("Username")
            );
        }

        public IActionResult Index()
        {
            // The view will fetch data via AJAX
            return View();
        }

        // GET: /Employee/GetEmployees
        [HttpGet]
        public async Task<IActionResult> GetEmployees()
        {
            var (rootCode, userCode, groupCode, username) = GetSessionContext();
            if (rootCode == null)
                return Unauthorized();

            var employees = await _context.Employees
                .Where(e => e.RootCode == rootCode.Value)
                .Select(e => new {
                    e.EmployeeName,
                    e.EmployeePhone,
                    e.EmployeeEmail,
                    EmployeeStartDate = e.EmployeeStartDate.ToString("yyyy-MM-dd"),
                    e.EmployeeSalary,
                    e.IsActive,
                    e.EmployeeCode,
                    e.UserCode,
                    e.BranchCode
                }).ToListAsync();

            return Json(employees);
        }

        // GET: /Employee/GetUsersForDropdown
        [HttpGet]
        public async Task<IActionResult> GetUsersForDropdown()
        {
            var (rootCode, userCode, groupCode, username) = GetSessionContext();
            if (rootCode == null)
                return Unauthorized();

            // Get group codes for this root
            var groupCodes = await _context.Groups
                .Where(g => g.RootCode == rootCode.Value)
                .Select(g => g.GroupCode)
                .ToListAsync();

            // Get users in those groups
            var users = await _context.Users
                .Where(u => groupCodes.Contains(u.GroupCode))
                .Select(u => new { u.UserCode, u.Username })
                .ToListAsync();

            return Json(users);
        }

        // GET: /Employee/GetBranchesForDropdown
        [HttpGet]
        public async Task<IActionResult> GetBranchesForDropdown()
        {
            var (rootCode, userCode, groupCode, username) = GetSessionContext();
            if (rootCode == null)
                return Unauthorized();

            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode.Value)
                .Select(b => new { b.BranchCode, b.BranchName })
                .ToListAsync();

            return Json(branches);
        }

        // POST: /Employee/AddEmployee
        [HttpPost]
        public async Task<IActionResult> AddEmployee([FromBody] EmployeeDto dto)
        {
            if (dto == null)
                return BadRequest();

            var (rootCode, userCode, groupCode, username) = GetSessionContext();
            if (rootCode == null || userCode == null)
                return Unauthorized();

            var employee = new Employee
            {
                EmployeeName = dto.EmployeeName,
                EmployeePhone = dto.EmployeePhone,
                EmployeeEmail = dto.EmployeeEmail,
                EmployeeStartDate = dto.EmployeeStartDate,
                EmployeeSalary = dto.EmployeeSalary,
                IsActive = true, // always set to true
                UserCode = dto.UserCode,
                BranchCode = dto.BranchCode,
                RootCode = rootCode.Value,
                InsertUser = userCode.Value,
                InsertTime = DateTime.Now
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // PUT: /Employee/EditEmployee
        [HttpPut]
        public async Task<IActionResult> EditEmployee([FromBody] EmployeeEditDto dto)
        {
            if (dto == null)
                return BadRequest();

            var employee = await _context.Employees.FindAsync(dto.EmployeeCode);
            if (employee == null) return NotFound();

            employee.EmployeeName = dto.EmployeeName;
            employee.EmployeePhone = dto.EmployeePhone;
            employee.EmployeeEmail = dto.EmployeeEmail;
            employee.EmployeeStartDate = dto.EmployeeStartDate;
            employee.EmployeeSalary = dto.EmployeeSalary;
            employee.IsActive = dto.IsActive;
            employee.UserCode = dto.UserCode;
            employee.BranchCode = dto.BranchCode;
            // Do not update InsertUser or InsertTime

            await _context.SaveChangesAsync();
            return Ok();
        }

        // DELETE: /Employee/DeleteEmployee/{id}
        [HttpDelete]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }

    public class EmployeeDto
    {
        public string EmployeeName { get; set; }
        public string EmployeePhone { get; set; }
        public string EmployeeEmail { get; set; }
        public DateOnly EmployeeStartDate { get; set; }
        public decimal EmployeeSalary { get; set; }
        public int? UserCode { get; set; }
        public int? BranchCode { get; set; }
    }

    public class EmployeeEditDto : EmployeeDto
    {
        public int EmployeeCode { get; set; }
        public bool IsActive { get; set; }
    }
}