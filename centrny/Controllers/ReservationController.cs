using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace centrny1.Controllers
{
    [Authorize]
    [Route("Reservation")]
    public class ReservationController : Controller
    {
        private readonly CenterContext _context;

        public ReservationController(CenterContext context)
        {
            _context = context;
        }

        // Helper to get logged-in user's info from claims & DB (like EduYear)
        private async Task<(int userCode, int groupCode, int rootCode)> GetUserInfoAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int userCode = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userCode);
            if (user == null)
                return (0, 0, 0);

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null)
                return (userCode, groupCode, 0);

            int rootCode = group.RootCode;
            // Debug output
            Console.WriteLine($"DEBUG ReservationController User: UserCode={userCode}, GroupCode={groupCode}, RootCode={rootCode}");
            return (userCode, groupCode, rootCode);
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        // Branch dropdown filtered by root code
        [HttpGet("GetBranchCodes")]
        public async Task<IActionResult> GetBranchCodes()
        {
            var (_, _, rootCode) = await GetUserInfoAsync();
            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode)
                .Select(b => new { branchCode = b.BranchCode, branchName = b.BranchName })
                .ToListAsync();
            return Json(branches);
        }

        // Reservation grid (sessions arranged by time order per hall)
        [HttpGet("GetReservationGrid")]
        public async Task<IActionResult> GetReservationGrid(DateTime? reservationDate, int? branchCode)
        {
            if (!branchCode.HasValue)
                return Json(new { periods = new List<string>(), grid = new List<List<object>>() });

            int sessionCount = 10; // Change as needed

            // Session headers
            var sessionHeaders = new List<string>();
            for (int i = 1; i <= sessionCount; i++)
            {
                if (i == 1) sessionHeaders.Add("1st Session");
                else if (i == 2) sessionHeaders.Add("2nd Session");
                else if (i == 3) sessionHeaders.Add("3rd Session");
                else if (i == 4) sessionHeaders.Add("4th Session");
                else sessionHeaders.Add($"{i}th Session");
            }

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
                var hallReservations = reservations
                    .Where(r => r.HallCode == hall.HallCode)
                    .OrderBy(r => r.ReservationStartTime) // Sort by start time
                    .ToList();

                var row = new List<object> { hall.HallName };

                // Place reservations in session columns by time order
                for (int i = 0; i < sessionCount; i++)
                {
                    if (i < hallReservations.Count)
                    {
                        var r = hallReservations[i];
                        row.Add(new
                        {
                            reservationCode = r.ReservationCode,
                            teacherName = teachers.FirstOrDefault(t => t.TeacherCode == r.TeacherCode)?.TeacherName ?? "",
                            teacherCode = r.TeacherCode,
                            description = r.Description ?? "",
                            start = r.ReservationStartTime.HasValue ? r.ReservationStartTime.Value.ToString("HH:mm") : "",
                            end = r.ReservationEndTime.HasValue ? r.ReservationEndTime.Value.ToString("HH:mm") : ""
                        });
                    }
                    else
                    {
                        row.Add(null); // Empty session cell
                    }
                }
                return row;
            }).ToList();

            var result = new
            {
                periods = sessionHeaders,
                grid
            };

            return Json(result);
        }

        // Only teachers where IsStaff == false
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

        // Add new teacher (AJAX from modal)
        [HttpPost("AddTeacher")]
        public async Task<IActionResult> AddTeacher([FromForm] string TeacherName, [FromForm] string TeacherPhone, [FromForm] string? TeacherAddress)
        {
            var (userCode, _, rootCode) = await GetUserInfoAsync();
            var newTeacher = new Teacher
            {
                TeacherName = TeacherName,
                TeacherPhone = TeacherPhone,
                TeacherAddress = TeacherAddress,
                IsActive = true,
                IsStaff = false,
                RootCode = rootCode,
                InsertUser = userCode,
                InsertTime = DateTime.Now
            };
            _context.Teachers.Add(newTeacher);
            await _context.SaveChangesAsync();
            return Ok(new { teacherCode = newTeacher.TeacherCode, teacherName = newTeacher.TeacherName });
        }
    }
}