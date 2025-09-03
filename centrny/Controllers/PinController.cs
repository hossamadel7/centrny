using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using centrny.Attributes;
using centrny.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace centrny.Controllers
{
    
    [RequirePageAccess("Codes generator")]
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
            public int TotalCount { get; set; }
            public int UsedCount { get; set; }
            public int ActiveCount { get; set; }
            public int SoldCount { get; set; }
            public int CurrentPage { get; set; } = 1;
            public int PageSize { get; set; } = 10;
            public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        }

        public class PinRow
        {
            public int PinCode { get; set; }
            public string Watermark { get; set; } = "";
            public bool Type { get; set; }
            public int Times { get; set; }
            public int Status { get; set; }  // 0=Used, 1=Active, 2=Sold
            public int IsActive { get; set; }
            public DateTime InsertTime { get; set; }
            public int InsertUser { get; set; }
        }

        public class GenerateRequest
        {
            [Range(0, 1, ErrorMessage = "Type must be 0 (Session) or 1 (Exam)")]
            public int Type { get; set; }

            [RegularExpression("^(1|4|16)$", ErrorMessage = "Times must be 1, 4, or 16")]
            public int Times { get; set; }

            [Range(1, 10000, ErrorMessage = "Number must be between 1 and 10,000")]
            public int Number { get; set; }
        }

        public class UpdateRequest
        {
            [Required]
            public int PinCode { get; set; }

            [Required]
            [StringLength(20, ErrorMessage = "Watermark cannot exceed 20 characters")]
            public string Watermark { get; set; } = "";

            [Required]
            public bool Type { get; set; }

            [RegularExpression("^(1|4|16)$", ErrorMessage = "Times must be 1, 4, or 16")]
            public int Times { get; set; }

            [Range(0, 2, ErrorMessage = "Status must be 0 (Used), 1 (Active), or 2 (Sold)")]
            public int Status { get; set; }

            [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
            public int IsActive { get; set; }
        }

        public class PaginatedResponse<T>
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? Message { get; set; }
            public List<T> Data { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
            public bool HasNextPage => Page < TotalPages;
            public bool HasPreviousPage => Page > 1;
            public int WalletCodesCount { get; set; }
        }

        // -------- Actions ----------

        // GET: /Pin
        public async Task<IActionResult> Index(string? message = null, string? error = null, int page = 1, int pageSize = 10)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            var userCode = HttpContext.Session.GetInt32("UserCode");

            if (rootCode is null || userCode is null)
            {
                return RedirectToAction("Index", "Login");
            }

            try
            {
                var totalCount = await GetPinsTotalCountAsync(rootCode.Value);
                var pinsPage = await GetPinsAsync(rootCode.Value, page, pageSize);
                var walletCount = await GetWalletCodesCountAsync(rootCode.Value);

                // Status counts for ALL pins of this root (not just page)
                var statusCounts = await _context.Pins
                    .Where(p => p.RootCode == rootCode.Value)
                    .GroupBy(p => p.Status)
                    .Select(g => new { g.Key, C = g.Count() })
                    .ToListAsync();

                int used = statusCounts.FirstOrDefault(s => s.Key == 0)?.C ?? 0;
                int active = statusCounts.FirstOrDefault(s => s.Key == 1)?.C ?? 0;
                int sold = statusCounts.FirstOrDefault(s => s.Key == 2)?.C ?? 0;

                var vm = new PinsViewModel
                {
                    RootCode = rootCode.Value,
                    UserCode = userCode.Value,
                    WalletCodesCount = walletCount,
                    Pins = pinsPage, // only current page for initial render
                    TotalCount = totalCount,
                    UsedCount = used,
                    ActiveCount = active,
                    SoldCount = sold,
                    CurrentPage = page,
                    PageSize = pageSize,
                    Message = message,
                    Error = error
                };

                return View("Index", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pins page for RootCode={RootCode}", rootCode);
                return View("Index", new PinsViewModel
                {
                    RootCode = rootCode ?? 0,
                    UserCode = userCode ?? 0,
                    Error = "Failed to load pins data."
                });
            }
        }

        // POST: /Pin/Generate (AJAX)
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
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                _logger.LogInformation("Generating {Number} pins for RootCode={RootCode}, Type={Type}, Times={Times}",
                    req.Number, rootCode, req.Type, req.Times);

                await GeneratePinsAsync(rootCode.Value, req.Type == 1, req.Times, req.Number, userCode.Value);

                return Json(new { success = true, message = $"Successfully generated {req.Number} pins." });
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL error generating pins for RootCode={RootCode}", rootCode);
                return Json(new { success = false, error = "Database error occurred while generating pins." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating pins for RootCode={RootCode}", rootCode);
                return Json(new { success = false, error = "An unexpected error occurred while generating pins." });
            }
        }

        // POST: /Pin/Update (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] UpdateRequest req)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            var userCode = HttpContext.Session.GetInt32("UserCode");

            if (rootCode is null || userCode is null)
            {
                return Json(new { success = false, error = "Session expired. Please log in again." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, error = string.Join("; ", errors) });
            }

            try
            {
                var pin = await _context.Pins
                    .FirstOrDefaultAsync(p => p.PinCode == req.PinCode && p.RootCode == rootCode.Value);

                if (pin == null)
                {
                    return Json(new { success = false, error = "Pin not found or access denied." });
                }

                pin.Watermark = req.Watermark;
                pin.Type = req.Type;
                pin.Times = req.Times;
                pin.Status = req.Status;
                pin.IsActive = req.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Pin {PinCode} updated by user {UserCode}", req.PinCode, userCode);

                return Json(new { success = true, message = "Pin updated successfully." });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating pin {PinCode}", req.PinCode);
                return Json(new { success = false, error = "Database error occurred while updating pin." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pin {PinCode}", req.PinCode);
                return Json(new { success = false, error = "An unexpected error occurred while updating pin." });
            }
        }

        // POST: /Pin/Delete (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] int pinCode)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            var userCode = HttpContext.Session.GetInt32("UserCode");

            if (rootCode is null || userCode is null)
            {
                return Json(new { success = false, error = "Session expired. Please log in again." });
            }

            try
            {
                var pin = await _context.Pins
                    .FirstOrDefaultAsync(p => p.PinCode == pinCode && p.RootCode == rootCode.Value);

                if (pin == null)
                {
                    return Json(new { success = false, error = "Pin not found or access denied." });
                }

                var isUsed = await _context.OnlineAttends
                    .AnyAsync(oa => oa.PinCode == pinCode);

                if (isUsed)
                {
                    return Json(new { success = false, error = "Cannot delete pin that is currently in use." });
                }

                _context.Pins.Remove(pin);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Pin {PinCode} deleted by user {UserCode}", pinCode, userCode);

                return Json(new { success = true, message = "Pin deleted successfully." });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting pin {PinCode}", pinCode);
                return Json(new { success = false, error = "Database error occurred while deleting pin." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pin {PinCode}", pinCode);
                return Json(new { success = false, error = "An unexpected error occurred while deleting pin." });
            }
        }

        // GET: /Pin/List (paged AJAX - still available if needed elsewhere)
        [HttpGet]
        public async Task<IActionResult> List(int page = 1, int pageSize = 10, string? search = null)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            if (rootCode is null)
            {
                return Json(new { success = false, error = "Session expired." });
            }

            try
            {
                var totalCount = await GetPinsTotalCountAsync(rootCode.Value, search);
                var pins = await GetPinsAsync(rootCode.Value, page, pageSize, search);
                var walletCount = await GetWalletCodesCountAsync(rootCode.Value);

                return Json(new PaginatedResponse<PinRow>
                {
                    Success = true,
                    Data = pins,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    WalletCodesCount = walletCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pins list for RootCode={RootCode}", rootCode);
                return Json(new { success = false, error = "Failed to load pins." });
            }
        }

        // NEW: /Pin/All (returns all pins for root for client-side pagination & stats)
        [HttpGet]
        public async Task<IActionResult> All(string? search = null)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            if (rootCode is null)
            {
                return Json(new { success = false, error = "Session expired." });
            }

            try
            {
                var query = _context.Pins
                    .AsNoTracking()
                    .Where(p => p.RootCode == rootCode.Value);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(p =>
                        p.Watermark.Contains(search) ||
                        p.PinCode.ToString().Contains(search));
                }

                var pins = await query
                    .OrderByDescending(p => p.PinCode)
                    .Select(p => new PinRow
                    {
                        PinCode = p.PinCode,
                        Watermark = p.Watermark,
                        Type = p.Type,
                        Times = p.Times,
                        Status = p.Status,
                        IsActive = p.IsActive,
                        InsertTime = p.InsertTime,
                        InsertUser = p.InsertUser
                    })
                    .ToListAsync();

                var walletCount = await GetWalletCodesCountAsync(rootCode.Value);

                return Json(new
                {
                    success = true,
                    data = pins,
                    totalCount = pins.Count,
                    walletCodesCount = walletCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all pins for RootCode={RootCode}", rootCode);
                return Json(new { success = false, error = "Failed to load all pins." });
            }
        }

        // GET: /Pin/Details/{id} (AJAX)
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            if (rootCode is null)
            {
                return Json(new { success = false, error = "Session expired." });
            }

            try
            {
                var pin = await _context.Pins
                    .AsNoTracking()
                    .Where(p => p.PinCode == id && p.RootCode == rootCode.Value)
                    .Select(p => new PinRow
                    {
                        PinCode = p.PinCode,
                        Watermark = p.Watermark,
                        Type = p.Type,
                        Times = p.Times,
                        Status = p.Status,
                        IsActive = p.IsActive,
                        InsertTime = p.InsertTime,
                        InsertUser = p.InsertUser
                    })
                    .FirstOrDefaultAsync();

                if (pin == null)
                {
                    return Json(new { success = false, error = "Pin not found." });
                }

                return Json(new { success = true, pin });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pin details for PinCode={PinCode}", id);
                return Json(new { success = false, error = "Failed to load pin details." });
            }
        }

        // GET: /Pin/Stats (AJAX) - still available (not strictly needed with /All)
        [HttpGet]
        public async Task<IActionResult> Stats()
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            if (rootCode is null)
            {
                return Json(new { success = false, error = "Session expired." });
            }

            try
            {
                var stats = await _context.Pins
                    .Where(p => p.RootCode == rootCode.Value)
                    .GroupBy(p => p.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                var totalPins = await _context.Pins.CountAsync(p => p.RootCode == rootCode.Value);
                var walletCount = await GetWalletCodesCountAsync(rootCode.Value);
                var statsDict = stats.ToDictionary(s => s.Status, s => s.Count);

                return Json(new
                {
                    success = true,
                    stats = new
                    {
                        total = totalPins,
                        used = statsDict.GetValueOrDefault(0, 0),
                        active = statsDict.GetValueOrDefault(1, 0),
                        sold = statsDict.GetValueOrDefault(2, 0),
                        walletCodes = walletCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading stats for RootCode={RootCode}", rootCode);
                return Json(new { success = false, error = "Failed to load statistics." });
            }
        }

        // -------- Private Helper Methods ----------

        private async Task<List<PinRow>> GetPinsAsync(int rootCode, int page = 1, int pageSize = 10, string? search = null)
        {
            var query = _context.Pins
                .AsNoTracking()
                .Where(p => p.RootCode == rootCode);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.Watermark.Contains(search) ||
                    p.PinCode.ToString().Contains(search));
            }

            return await query
                .OrderByDescending(p => p.PinCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PinRow
                {
                    PinCode = p.PinCode,
                    Watermark = p.Watermark,
                    Type = p.Type,
                    Times = p.Times,
                    Status = p.Status,
                    IsActive = p.IsActive,
                    InsertTime = p.InsertTime,
                    InsertUser = p.InsertUser
                })
                .ToListAsync();
        }

        private async Task<int> GetPinsTotalCountAsync(int rootCode, string? search = null)
        {
            var query = _context.Pins.Where(p => p.RootCode == rootCode);
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.Watermark.Contains(search) ||
                    p.PinCode.ToString().Contains(search));
            }
            return await query.CountAsync();
        }

        private async Task<int> GetWalletCodesCountAsync(int rootCode)
        {
            try
            {
                var walletCode = await _context.WalletCodes
                    .Where(w => w.RootCode == rootCode && w.IsActive)
                    .OrderByDescending(w => w.WalletCode1)
                    .FirstOrDefaultAsync();

                return walletCode?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting wallet code count for RootCode={RootCode}", rootCode);
                return 0;
            }
        }

        private async Task GeneratePinsAsync(int rootCode, bool type, int times, int number, int insertUser)
        {
            var connectionString = _context.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string not configured.");
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("Generate_Pins", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 120
            };

            command.Parameters.Add(new SqlParameter("@rootcode", SqlDbType.Int) { Value = rootCode });
            command.Parameters.Add(new SqlParameter("@type", SqlDbType.Bit) { Value = type });
            command.Parameters.Add(new SqlParameter("@times", SqlDbType.Int) { Value = times });
            command.Parameters.Add(new SqlParameter("@number", SqlDbType.Int) { Value = number });
            command.Parameters.Add(new SqlParameter("@Insert_User", SqlDbType.Int) { Value = insertUser });

            await command.ExecuteNonQueryAsync();

            _logger.LogInformation(
                "Generated {Number} pins: RootCode={RootCode}, Type={Type}, Times={Times}, User={User}",
                number, rootCode, type ? "Exam" : "Session", times, insertUser);
        }

        // -------- Bulk Operations ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromForm] int[] pinCodes)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            var userCode = HttpContext.Session.GetInt32("UserCode");

            if (rootCode is null || userCode is null)
                return Json(new { success = false, error = "Session expired." });

            if (pinCodes == null || pinCodes.Length == 0)
                return Json(new { success = false, error = "No pins selected for deletion." });

            try
            {
                var pins = await _context.Pins
                    .Where(p => pinCodes.Contains(p.PinCode) && p.RootCode == rootCode.Value)
                    .ToListAsync();

                if (pins.Count == 0)
                    return Json(new { success = false, error = "No valid pins found for deletion." });

                var usedPins = await _context.OnlineAttends
                    .Where(oa => pinCodes.Contains(oa.PinCode))
                    .Select(oa => oa.PinCode)
                    .Distinct()
                    .ToListAsync();

                if (usedPins.Any())
                {
                    return Json(new
                    {
                        success = false,
                        error = $"Cannot delete pins that are in use: {string.Join(", ", usedPins)}"
                    });
                }

                _context.Pins.RemoveRange(pins);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Bulk deleted {Count} pins by user {UserCode}", pins.Count, userCode);

                return Json(new { success = true, message = $"Successfully deleted {pins.Count} pins." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting pins");
                return Json(new { success = false, error = "An error occurred while deleting pins." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateStatus([FromForm] int[] pinCodes, [FromForm] int status)
        {
            var rootCode = HttpContext.Session.GetInt32("RootCode");
            var userCode = HttpContext.Session.GetInt32("UserCode");

            if (rootCode is null || userCode is null)
                return Json(new { success = false, error = "Session expired." });

            if (pinCodes == null || pinCodes.Length == 0)
                return Json(new { success = false, error = "No pins selected." });

            if (status < 0 || status > 2)
                return Json(new { success = false, error = "Invalid status value. Must be 0 (Used), 1 (Active), or 2 (Sold)." });

            try
            {
                var updatedCount = await _context.Pins
                    .Where(p => pinCodes.Contains(p.PinCode) && p.RootCode == rootCode.Value)
                    .ExecuteUpdateAsync(p => p.SetProperty(pin => pin.Status, status));

                var statusText = status switch
                {
                    0 => "Used",
                    1 => "Active",
                    2 => "Sold",
                    _ => "Unknown"
                };

                _logger.LogInformation("Bulk updated status of {Count} pins to {Status} by user {UserCode}",
                    updatedCount, statusText, userCode);

                return Json(new { success = true, message = $"Successfully updated {updatedCount} pins to {statusText}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating pin status");
                return Json(new { success = false, error = "An error occurred while updating pins." });
            }
        }
    }
}