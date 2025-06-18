using centrny.Models;
using centrny.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace centrny.Controllers
{
    // Apply page access to the entire controller for view permission
    [RequirePageAccess("WalletExam")] // This uses Page_Code 20 from your database
    public class WalletExamController : Controller
    {
        private readonly CenterContext _context;

        public WalletExamController(CenterContext context)
        {
            _context = context;
        }

        // View permission - inherited from controller-level attribute
        public async Task<IActionResult> Index()
        {
            var walletExams = await _context.WalletExams.ToListAsync();
            return View(walletExams);
        }

        // INSERT permission required for showing create form
        [RequirePageAccess("WalletExam", "insert")]
        public IActionResult Create()
        {
            return View();
        }

        // INSERT permission required for creating new wallet exam
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

        // UPDATE permission required for editing
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

        // UPDATE permission required for AJAX updates
        [RequirePageAccess("WalletExam", "update")]
        [HttpPost]
        public async Task<IActionResult> UpdateWalletExam([FromBody] WalletExam walletExam)
        {
            // Manually remove the navigation property validation
            ModelState.Remove(nameof(WalletExam.RootCodeNavigation));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _context.WalletExams.FindAsync(walletExam.WalletExamCode);
            if (existing == null)
                return NotFound();

            // Update scalar fields only
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

        // INSERT permission required for AJAX creation
        [RequirePageAccess("WalletExam", "insert")]
        [HttpPost]
        public async Task<IActionResult> AddWalletExam([FromBody] WalletExam walletExam)
        {
            ModelState.Remove(nameof(WalletExam.RootCodeNavigation));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.WalletExams.Add(walletExam);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // DELETE permission required for showing delete confirmation
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

        // DELETE permission required for actual deletion
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
    }
}