using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace centrny.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RootController : ControllerBase
    {
        private readonly CenterContext _context;

        public RootController(CenterContext context)
        {
            _context = context;
        }

        [HttpGet("centers")]
        public IActionResult GetCenters()
        {
            var centers = _context.Centers
                .Select(c => new { id = c.CenterCode, name = c.CenterName })
                .ToList();

            return Ok(centers);
        }

        [HttpGet("teachers")]
        public IActionResult GetTeachers()
        {
            var teachers = _context.Teachers
                .Select(t => new { id = t.TeacherCode, name = t.TeacherName })
                .ToList();

            return Ok(teachers);
        }
        // GET: /Root/GetRootsByType?isCenter=true
        [HttpGet("GetRootsByType")]
        public async Task<IActionResult> GetRootsByType(bool isCenter)
        {
            var roots = await _context.Roots
                .Where(r => r.IsCenter == isCenter)
                .Select(r => new
                {
                    r.RootCode,
                    r.RootName
                })
                .ToListAsync();

            return Ok(roots);
        }
    }
}
