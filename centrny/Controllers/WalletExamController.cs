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
            // Load WalletExams with Root navigation for table rendering
            var walletExams = await _context.WalletExams
                .Include(w => w.RootCodeNavigation)
                .ToListAsync();
            return View(walletExams);
        }

        [RequirePageAccess("WalletExam", "insert")]
        public IActionResult Create()
        {
            return View();
        }

        [RequirePageAccess("WalletExam", "insert")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WalletExam walletExam)
        {
            if (ModelState.IsValid)
            {
                _context.WalletExams.Add(walletExam);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(walletExam);
        }

        [RequirePageAccess("WalletExam", "update")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var walletExam = await _context.WalletExams.FindAsync(id);
            if (walletExam == null)
            {
                return NotFound();
            }
            return View(walletExam);
        }

        [RequirePageAccess("WalletExam", "update")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WalletExam walletExam)
        {
            if (id != walletExam.WalletExamCode)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(walletExam);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.WalletExams.Any(e => e.WalletExamCode == id))
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
            return View(walletExam);
        }

        [RequirePageAccess("WalletExam", "update")]
        [HttpPost]
        [Route("WalletExam/UpdateWalletExam")]
        public async Task<IActionResult> UpdateWalletExam([FromBody] WalletExam walletExam)
        {
            ModelState.Remove(nameof(WalletExam.RootCodeNavigation));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _context.WalletExams.FindAsync(walletExam.WalletExamCode);
            if (existing == null)
                return NotFound();

            existing.Amount = walletExam.Amount;
            existing.Count = walletExam.Count;
            existing.OriginalCount = walletExam.OriginalCount;
            existing.ExpireDate = walletExam.ExpireDate;
            existing.DateStart = walletExam.DateStart;
            existing.IsActive = walletExam.IsActive;
            existing.RootCode = walletExam.RootCode;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [RequirePageAccess("WalletExam", "insert")]
        [HttpPost]
        [Route("WalletExam/AddWalletExam")]
        public async Task<IActionResult> AddWalletExam([FromBody] WalletExam walletExam)
        {
            ModelState.Remove(nameof(WalletExam.RootCodeNavigation));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.WalletExams.Add(walletExam);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [RequirePageAccess("WalletExam", "delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var walletExam = await _context.WalletExams.FindAsync(id);
            if (walletExam == null)
            {
                return NotFound();
            }
            return View(walletExam);
        }

        [RequirePageAccess("WalletExam", "delete")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var walletExam = await _context.WalletExams.FindAsync(id);
            if (walletExam != null)
            {
                _context.WalletExams.Remove(walletExam);
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
                .Select(r => new {
                    rootCode = r.RootCode,
                    rootName = r.RootName,
                    isCenter = r.IsCenter
                })
                .ToListAsync();
            return Json(roots);
        }
    }
}