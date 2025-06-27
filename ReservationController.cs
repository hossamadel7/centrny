using centrny1.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace centrny1.Controllers
{
    [Route("Reservation")]
    public class ReservationController : Controller
    {
        private readonly CenterContext _context;

        public ReservationController(CenterContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("GetReservationGrid")]
        public async Task<IActionResult> GetReservationGrid(DateTime? reservationDate, int? branchCode)
        {
            if (!branchCode.HasValue)
                return Json(new { periods = new List<string>(), grid = new List<List<object>>() });

            var periodStart = new TimeSpan(8, 0, 0);
            var periods = Enumerable.Range(0, 10)
                .Select(i => periodStart.Add(TimeSpan.FromHours(i)))
                .ToList();

            var halls = await _context.Halls
                .Where(h => h.BranchCode == branchCode.Value)
                .OrderBy(h => h.HallCode)
                .Select(h => new { h.HallName, h.HallCode })
                .ToListAsync();

            var date = reservationDate?.Date ?? DateTime.Today;

            var reservations = await _context.Reservations
                .Where(r => r.RTime == DateOnly.FromDateTime(date) && r.BranchCode == branchCode)
                .ToListAsync();

            var teachers = await _context.Teachers.ToListAsync();

            var grid = halls.Select(hall =>
            {
                var row = new List<object> { hall.HallName };
                foreach (var periodTime in periods)
                {
                    var periodEnd = periodTime.Add(TimeSpan.FromHours(1));
                    var periodReservations = reservations
                        .Where(r =>
                            r.HallCode == hall.HallCode &&
                            r.ReservationStartTime.HasValue &&
                            r.ReservationStartTime.Value.ToTimeSpan() >= periodTime &&
                            r.ReservationStartTime.Value.ToTimeSpan() < periodEnd
                        )
                        .Select(r => new
                        {
                            reservationCode = r.ReservationCode,
                            teacherName = teachers.FirstOrDefault(t => t.TeacherCode == r.TeacherCode)?.TeacherName ?? "",
                            teacherCode = r.TeacherCode,
                            description = r.Description ?? "",
                            start = r.ReservationStartTime.HasValue ? r.ReservationStartTime.Value.ToString("HH:mm") : "",
                            end = r.ReservationEndTime.HasValue ? r.ReservationEndTime.Value.ToString("HH:mm") : ""
                        })
                        .ToList();

                    row.Add(periodReservations.Any() ? periodReservations : null);
                }
                return row;
            }).ToList();

            var result = new
            {
                periods = periods.Select(t => t.ToString(@"hh\:mm")).ToList(),
                grid
            };

            return Json(result);
        }

        [HttpGet("GetBranchCodes")]
        public async Task<IActionResult> GetBranchCodes()
        {
            var branches = await _context.Branches
                .Select(b => new { branchCode = b.BranchCode, branchName = b.BranchName })
                .ToListAsync();
            return Json(branches);
        }

        [HttpGet("GetTeachers")]
        public async Task<IActionResult> GetTeachers()
        {
            var teachers = await _context.Teachers
                .Where(t => !t.IsStaff)
                .Select(t => new { teacherCode = t.TeacherCode, teacherName = t.TeacherName })
                .ToListAsync();
            return Json(teachers);
        }

        [HttpGet("GetHalls")]
        public async Task<IActionResult> GetHalls(int branchCode)
        {
            var halls = await _context.Halls
                .Where(h => h.BranchCode == branchCode)
                .OrderBy(h => h.HallCode)
                .Select(h => new { hallCode = h.HallCode, hallName = h.HallName })
                .ToListAsync();
            return Json(halls);
        }

        [HttpPost("AddReservation")]
        public async Task<IActionResult> AddReservation([FromForm] Reservation reservation)
        {
            if (string.IsNullOrWhiteSpace(reservation.Description)) reservation.Description = null;
            await _context.Reservations.AddAsync(reservation);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("EditReservation")]
        public async Task<IActionResult> EditReservation([FromForm] int ReservationCode, [FromForm] string Description, [FromForm] int TeacherCode, [FromForm] TimeOnly ReservationStartTime, [FromForm] TimeOnly ReservationEndTime)
        {
            var reservation = await _context.Reservations.FindAsync(ReservationCode);
            if (reservation == null) return NotFound();
            reservation.Description = Description;
            reservation.TeacherCode = TeacherCode;
            reservation.ReservationStartTime = ReservationStartTime;
            reservation.ReservationEndTime = ReservationEndTime;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("DeleteReservation")]
        public async Task<IActionResult> DeleteReservation([FromForm] int reservationCode)
        {
            var reservation = await _context.Reservations.FindAsync(reservationCode);
            if (reservation == null) return NotFound();
            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}