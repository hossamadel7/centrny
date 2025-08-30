using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using centrny.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace centrny.Controllers
{
    [Authorize]
    public class PinController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<PinController> _logger;

        public PinController(CenterContext context, ILogger<PinController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // -------- ViewModel / DTOs ----------

        public class PinsViewModel
        {
            public int RootCode { get; set; }
            public int UserCode { get; set; }
            public int WalletCodesCount { get; set; }
            public List<PinRow> Pins { get; set; } = new();
            public string? Message { get; set; }
            public string? Error { get; set; }

            // Form default values (optional)
            public int GenerateType { get; set; } = 0;
            public int GenerateTimes { get; set; } = 1;
            public int GenerateNumber { get; set; } = 1;
        }

        public class PinRow
        {
            public int PinCode { get; set; }
            public string Watermark { get; set; } = "";
            public bool Type { get; set; }
            public int Times { get; set; }
            public int Status { get; set; }
            public int IsActive { get; set; }
            public DateTime InsertTime { get; set; }
        }

        public class GenerateRequest
        {
            [Range(0, 1)]
            public int Type { get; set; }              // 0=session 1=exam
            [RegularExpression("^(1|4|16)$")]
            public int Times { get; set; }
            [Range(1, 10000)]
            public int Number { get; set; }
        }

        // -------- Actions ----------

        // GET: /Pin
        public async Task<IActionResult> Index(string? message = null, string? error = null)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            var userCode = HttpContext.Session.GetInt32("UserCode");
            if (rootCode is null || userCode is null)
            {
                return RedirectToAction("Index", "Login");
            }

            var vm = new PinsViewModel
            {
                RootCode = rootCode.Value,
                UserCode = userCode.Value,
                WalletCodesCount = await GetWalletCodesCountAsync(rootCode.Value),
                Pins = await GetPinsAsync(rootCode.Value),
                Message = message,
                Error = error
            };

            return View("Index", vm);
        }

        // POST: /Pin/Generate  (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate([FromForm] GenerateRequest req)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            var userCode = HttpContext.Session.GetInt32("UserCode");

            if (rootCode is null || userCode is null)
            {
                return Json(new { success = false, error = "Session expired. Please log in again." });
            }

            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Invalid input.";
                return Json(new { success = false, error = firstError });
            }

            try
            {
                await GeneratePinsAsync(rootCode.Value, req.Type == 1, req.Times, req.Number, userCode.Value);

                var pins = await GetPinsAsync(rootCode.Value);
                var walletCount = await GetWalletCodesCountAsync(rootCode.Value);

                return Json(new
                {
                    success = true,
                    message = "Pins generated successfully.",
                    pins,
                    walletCodesCount = walletCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating pins for RootCode={RootCode}", rootCode);
                return Json(new { success = false, error = "Error generating pins. Please try again." });
            }
        }

        // GET: /Pin/List  (AJAX refresh)
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            if (rootCode is null)
            {
                return Json(new { success = false, error = "Session expired." });
            }

            try
            {
                var pins = await GetPinsAsync(rootCode.Value);
                var walletCount = await GetWalletCodesCountAsync(rootCode.Value);
                return Json(new { success = true, pins, walletCodesCount = walletCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pins list for RootCode {RootCode}", rootCode);
                return Json(new { success = false, error = "Failed to load pins." });
            }
        }

        // -------- Data Helpers ----------

        private async Task<List<PinRow>> GetPinsAsync(int rootCode)
        {
            return await _context.Pins
                .AsNoTracking()
                .Where(p => p.RootCode == rootCode)
                .OrderByDescending(p => p.PinCode)
                .Select(p => new PinRow
                {
                    PinCode = p.PinCode,
                    Watermark = p.Watermark,
                    Type = p.Type,
                    Times = p.Times,
                    Status = p.Status,
                    IsActive = p.IsActive,
                    InsertTime = p.InsertTime
                })
                .ToListAsync();
        }

        private Task<int> GetWalletCodesCountAsync(int rootCode)
        {
            // Counting all wallet codes for this root. Adjust filter if you only need "active" etc.
            return _context.WalletCodes.CountAsync(w => w.RootCode == rootCode);
        }

        private async Task GeneratePinsAsync(int rootCode, bool type, int times, int number, int insertUser)
        {
            var cs = _context.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Connection string not configured.");

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("Generate_Pins", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@rootcode", rootCode);
            cmd.Parameters.AddWithValue("@type", type ? 1 : 0);
            cmd.Parameters.AddWithValue("@times", times);
            cmd.Parameters.AddWithValue("@number", number);
            cmd.Parameters.AddWithValue("@Insert_User", insertUser);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}