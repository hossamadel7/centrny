using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Http;
using centrny.Attributes;

namespace centrny.Controllers
{
    [RequirePageAccess("Income")]
    [Authorize]
    public class IncomeController : Controller
    {
        private readonly CenterContext _context;

        public IncomeController(CenterContext context)
        {
            _context = context;
        }

        private int GetSessionInt(string key) => (int)HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);

        private (int userCode, int groupCode, int rootCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                _context.Roots.Where(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", "")).FirstOrDefault().RootCode,
                GetSessionString("Username")
            );
        }

        public async Task<IActionResult> Index()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            ViewBag.UserRootCode = rootCode;
            ViewBag.UserGroupCode = groupCode;
            ViewBag.CurrentUserName = username;
            ViewBag.UserCode = userCode;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetIncomes()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var incomes = await _context.Incomes
                .Where(i => i.RootCode == rootCode)
                .OrderByDescending(i => i.PaymentDate)
                .Select(i => new
                {
                    i.Id,
                    i.RootCode,
                    i.Amount,
                    PaymentDate = i.PaymentDate.ToString("yyyy-MM-dd"),
                    i.Description,
                    InsertTime = i.InsertTime.HasValue ? i.InsertTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                    i.InsertUserCode
                })
                .ToListAsync();

            return Json(new { data = incomes });
        }

        [RequirePageAccess("Income", "insert")]
        [HttpPost]
        public async Task<IActionResult> AddIncome([FromBody] IncomeCreateDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (dto == null) return BadRequest("Invalid data");

            if (dto.Amount <= 0)
                return BadRequest("Amount must be greater than 0");

            var income = new Income
            {
                RootCode = rootCode,
                Amount = dto.Amount,
                PaymentDate = dto.PaymentDate,
                Description = dto.Description,
                InsertTime = DateTime.Now,
                InsertUserCode = userCode
            };

            _context.Incomes.Add(income);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Income added successfully" });
        }

        [RequirePageAccess("Income", "update")]
        [HttpPut]
        public async Task<IActionResult> EditIncome([FromBody] IncomeEditDto dto)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            if (dto == null) return BadRequest("Invalid data");

            var income = await _context.Incomes
                .FirstOrDefaultAsync(i => i.Id == dto.Id && i.RootCode == rootCode);

            if (income == null) return NotFound("Income not found");

            if (dto.Amount <= 0)
                return BadRequest("Amount must be greater than 0");

            income.Amount = dto.Amount;
            income.PaymentDate = dto.PaymentDate;
            income.Description = dto.Description;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Income updated successfully" });
        }

        [RequirePageAccess("Income", "delete")]
        [HttpDelete]
        public async Task<IActionResult> DeleteIncome(int id)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var income = await _context.Incomes
                .FirstOrDefaultAsync(i => i.Id == id && i.RootCode == rootCode);

            if (income == null) return NotFound("Income not found");

            _context.Incomes.Remove(income);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Income deleted successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> GetIncome(int id)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            var income = await _context.Incomes
                .Where(i => i.Id == id && i.RootCode == rootCode)
                .Select(i => new
                {
                    i.Id,
                    i.Amount,
                    PaymentDate = i.PaymentDate.ToString("yyyy-MM-dd"),
                    i.Description
                })
                .FirstOrDefaultAsync();

            if (income == null) return NotFound("Income not found");

            return Json(income);
        }
    }

    public class IncomeCreateDto
    {
        public decimal Amount { get; set; }
        public DateOnly PaymentDate { get; set; }
        public string? Description { get; set; }
    }

    public class IncomeEditDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateOnly PaymentDate { get; set; }
        public string? Description { get; set; }
    }
}