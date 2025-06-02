using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace centrny.Controllers
{
    public class WalletExamController : Controller
    {
        private readonly CenterContext _context;

        public WalletExamController(CenterContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var walletExams = await _context.WalletExams.ToListAsync();
            return View(walletExams);
        }

        public IActionResult Create()
        {
            return View();
        }


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

        public async Task<IActionResult> Delete(int id)
        {
            var walletExam = await _context.WalletExams.FindAsync(id);
            if (walletExam == null)
            {
                return NotFound();
            }
            return View(walletExam);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var walletExam = await _context.WalletExams.FindAsync(id);
            _context.WalletExams.Remove(walletExam);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
