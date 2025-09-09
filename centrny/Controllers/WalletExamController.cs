using centrny.Models;
using centrny.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;

namespace centrny.Controllers
{
    [RequirePageAccess("WalletExam")]
    public class WalletExamController : Controller
    {
        private readonly CenterContext _context;

        public WalletExamController(CenterContext context)
        {
            _context = context;
        }

        // --- Session Helpers (like BranchController) ---
        private int GetSessionInt(string key) => (int)HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);

        // --- Pagination: Index ---
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 10;
            var query = _context.WalletCodes
                .Include(w => w.RootCodeNavigation)
                .Where(w => w.IsActive);

            int totalItems = await query.CountAsync();
            var walletCodes = await query
                .OrderByDescending(w => w.WalletCode1)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = (int)System.Math.Ceiling((double)totalItems / pageSize);

            return View(walletCodes);
        }

        [RequirePageAccess("WalletExam", "insert")]
        public IActionResult Create()
        {
            return View();
        }

        [RequirePageAccess("WalletExam", "insert")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WalletCode walletCode)
        {
            if (ModelState.IsValid)
            {
                walletCode.IsActive = true; // Always true on add
                _context.WalletCodes.Add(walletCode);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(walletCode);
        }

        [RequirePageAccess("WalletExam", "update")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var walletCode = await _context.WalletCodes.FindAsync(id);
            if (walletCode == null)
            {
                return NotFound();
            }
            return View(walletCode);
        }

        [RequirePageAccess("WalletExam", "update")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WalletCode walletCode)
        {
            if (id != walletCode.WalletCode1)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.WalletCodes.FindAsync(id);
                    if (existing == null)
                        return NotFound();

                    // Update fields, force IsActive true
                    existing.Amount = walletCode.Amount;
                    existing.Count = walletCode.Count;
                    existing.OriginalCount = walletCode.OriginalCount;
                    existing.ExpireDate = walletCode.ExpireDate;
                    existing.DateStart = walletCode.DateStart;
                    existing.RootCode = walletCode.RootCode;
                    existing.Times = walletCode.Times;
                    existing.Type = walletCode.Type;
                    existing.Status = walletCode.Status;
                    existing.IsActive = true;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.WalletCodes.Any(e => e.WalletCode1 == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(walletCode);
        }

        [RequirePageAccess("WalletExam", "update")]
        [HttpPost]
        [Route("WalletExam/UpdateWalletExam")]
        public async Task<IActionResult> UpdateWalletExam([FromBody] WalletCode walletCode)
        {
            ModelState.Remove(nameof(WalletCode.RootCodeNavigation));
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _context.WalletCodes.FindAsync(walletCode.WalletCode1);
            if (existing == null)
                return NotFound();

            existing.Amount = walletCode.Amount;
            existing.Count = walletCode.Count;
            existing.OriginalCount = walletCode.OriginalCount;
            existing.ExpireDate = walletCode.ExpireDate;
            existing.DateStart = walletCode.DateStart;
            existing.IsActive = true; // Always true on edit
            existing.RootCode = walletCode.RootCode;
            existing.Times = walletCode.Times;
            existing.Type = walletCode.Type;
            existing.Status = walletCode.Status;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [RequirePageAccess("WalletExam", "insert")]
        [HttpPost]
        [Route("WalletExam/AddWalletExam")]
        public async Task<IActionResult> AddWalletExam([FromBody] WalletCode walletCode)
        {
            ModelState.Remove(nameof(WalletCode.RootCodeNavigation));
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            walletCode.IsActive = true; // Always true on add
            _context.WalletCodes.Add(walletCode);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [RequirePageAccess("WalletExam", "delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var walletCode = await _context.WalletCodes.FindAsync(id);
            if (walletCode == null)
            {
                return NotFound();
            }
            return View(walletCode);
        }

        [RequirePageAccess("WalletExam", "delete")]
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var walletCode = await _context.WalletCodes.FindAsync(id);
            if (walletCode != null)
            {
                walletCode.IsActive = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // Endpoint for JS soft delete
        [RequirePageAccess("WalletExam", "delete")]
        [HttpPost]
        [Route("WalletExam/SoftDelete/{id}")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var walletCode = await _context.WalletCodes.FindAsync(id);
            if (walletCode != null)
            {
                walletCode.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        // --- API Endpoint for roots ---
        [HttpGet]
        [Route("api/roots")]
        public async Task<IActionResult> GetRoots([FromQuery] bool isCenter, [FromQuery] bool isActive = true)
        {
            var roots = await _context.Roots
                .Where(r => r.IsCenter == isCenter && r.IsActive == isActive)
                .Select(r => new
                {
                    rootCode = r.RootCode,
                    rootName = r.RootName,
                    isCenter = r.IsCenter
                })
                .ToListAsync();
            return Json(roots);
        }
    }
}