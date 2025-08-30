using centrny.Models;
using centrny.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public async Task<IActionResult> Index()
        {
            // Load WalletCodes with Root navigation for table rendering
            var walletCodes = await _context.WalletCodes
                .Include(w => w.RootCodeNavigation)
                .ToListAsync();
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
            // Use the correct PK property here, e.g. WalletCodeId
            if (id != walletCode.WalletCode1)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(walletCode);
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
            existing.IsActive = walletCode.IsActive;
            existing.RootCode = walletCode.RootCode;

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
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var walletCode = await _context.WalletCodes.FindAsync(id);
            if (walletCode != null)
            {
                _context.WalletCodes.Remove(walletCode);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
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