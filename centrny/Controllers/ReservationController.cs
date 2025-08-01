﻿using centrny.Models;
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

        // --- Authority Check ---
        private bool UserHasReservationPermission()
        {
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return false;

            var userGroupCodes = _context.Users
                .Where(ug => ug.UserCode == user.UserCode)
                .Select(ug => ug.GroupCode)
                .ToList();

            var page = _context.Pages.FirstOrDefault(p => p.PagePath == "Reservation/Index");
            if (page == null)
                return false;

            return _context.GroupPages.Any(gp => userGroupCodes.Contains(gp.GroupCode) && gp.PageCode == page.PageCode);
        }

        // Helper to get logged-in user's info from claims & DB (like EduYear)
        private async Task<(int userCode, int groupCode, int rootCode, bool isCenter)> GetUserInfoAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int userCode = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userCode);
            if (user == null)
                return (0, 0, 0, true);

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null)
                return (userCode, groupCode, 0, true);

            int rootCode = group.RootCode;
            var root = await _context.Roots.FirstOrDefaultAsync(r => r.RootCode == rootCode);
            bool isCenter = root?.IsCenter ?? true;
            Console.WriteLine($"DEBUG ReservationController User: UserCode={userCode}, GroupCode={groupCode}, RootCode={rootCode}, IsCenter={isCenter}");
            return (userCode, groupCode, rootCode, isCenter);
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            if (!UserHasReservationPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }
            var (_, _, rootCode, isCenter) = await GetUserInfoAsync();
            ViewBag.IsCenter = isCenter;
            ViewBag.RootCode = rootCode;
            return View();
        }

        [HttpGet("GetSingleTeacherForRoot")]
        public async Task<IActionResult> GetSingleTeacherForRoot()
        {
            var (_, _, rootCode, isCenter) = await GetUserInfoAsync();
            if (isCenter) return Json(new { success = false, message = "Not applicable" });
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.RootCode == rootCode);
            if (teacher == null) return Json(new { success = false, message = "No teacher found" });
            return Json(new { success = true, teacherCode = teacher.TeacherCode, teacherName = teacher.TeacherName });
        }

        [HttpGet("GetRootReservations")]
        public async Task<IActionResult> GetRootReservations(DateTime? reservationDate)
        {
            var (_, _, rootCode, isCenter) = await GetUserInfoAsync();
            if (!UserHasReservationPermission() || isCenter)
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var date = reservationDate?.Date ?? DateTime.Today;
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.RootCode == rootCode);

            if (teacher == null)
            {
                return Json(new { success = true, reservations = new List<object>() });
            }

            var reservations = await _context.Reservations
                .Where(r =>
                    r.RTime == DateOnly.FromDateTime(date)
                    && r.TeacherCode == teacher.TeacherCode
                )
                .ToListAsync();

            return Json(new
            {
                success = true,
                reservations = reservations.Select(r => new
                {
                    reservationCode = r.ReservationCode,
                    teacherName = teacher.TeacherName,
                    description = r.Description,
                    capacity = r.Capacity,
                    cost = r.Cost,
                    period = r.Period,
                    deposit = r.Deposit,
                    finalCost = r.FinalCost,
                    rTime = r.RTime.ToString("yyyy-MM-dd"),
                    reservationStartTime = r.ReservationStartTime?.ToString("HH:mm"),
                    reservationEndTime = r.ReservationEndTime?.ToString("HH:mm")
                }).ToList()
            });
        }

        [HttpPost("AddRootReservation")]
        public async Task<IActionResult> AddRootReservation([FromForm] Reservation reservation)
        {
            var (_, _, rootCode, isCenter) = await GetUserInfoAsync();
            if (!UserHasReservationPermission() || isCenter)
            {
                return Json(new { success = false, message = "Access denied." });
            }

            reservation.BranchCode = null;
            reservation.HallCode = null;

            // Defensive validation
            if (reservation.TeacherCode == 0 || reservation.RTime == default ||
                reservation.ReservationStartTime == null || reservation.ReservationEndTime == null)
            {
                return Json(new { success = false, message = "Missing required reservation data." });
            }

            var newStart = reservation.ReservationStartTime;
            var newEnd = reservation.ReservationEndTime;
            var date = reservation.RTime;
            var teacherCode = reservation.TeacherCode;

            // Only compare with reservations that have non-null times
            var overlappingReservation = await _context.Reservations
                .Where(r => r.RTime == date && r.TeacherCode == teacherCode
                    && r.ReservationStartTime != null && r.ReservationEndTime != null
                    && r.ReservationStartTime < newEnd && r.ReservationEndTime > newStart)
                .FirstOrDefaultAsync();

            if (overlappingReservation != null)
            {
                return Json(new { success = false, message = "This teacher already has a reservation at the selected time. Please choose a different time." });
            }

            await _context.Reservations.AddAsync(reservation);
            var result = await _context.SaveChangesAsync();
            if (result > 0)
                return Json(new { success = true });
            else
                return Json(new { success = false, message = "Reservation not saved." });
        }

        [HttpPost("EditRootReservation")]
        public async Task<IActionResult> EditRootReservation([FromForm] int ReservationCode, [FromForm] string Description, [FromForm] int Capacity, [FromForm] decimal Cost, [FromForm] int Deposit, [FromForm] int? FinalCost,
            [FromForm] decimal? Period, [FromForm] TimeOnly? ReservationStartTime, [FromForm] TimeOnly? ReservationEndTime)
        {
            var (_, _, _, isCenter) = await GetUserInfoAsync();
            if (!UserHasReservationPermission() || isCenter)
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var reservation = await _context.Reservations.FindAsync(ReservationCode);
            if (reservation == null) return NotFound();

            if (ReservationStartTime == null || ReservationEndTime == null)
            {
                return Json(new { success = false, message = "Missing start or end time." });
            }

            var newStart = ReservationStartTime;
            var newEnd = ReservationEndTime;
            var date = reservation.RTime;
            var teacherCode = reservation.TeacherCode;

            var overlappingReservation = await _context.Reservations
                .Where(r => r.RTime == date && r.TeacherCode == teacherCode && r.ReservationCode != ReservationCode
                    && r.ReservationStartTime != null && r.ReservationEndTime != null
                    && r.ReservationStartTime < newEnd && r.ReservationEndTime > newStart)
                .FirstOrDefaultAsync();

            if (overlappingReservation != null)
            {
                return Json(new { success = false, message = "This teacher already has a reservation at the selected time. Please choose a different time." });
            }

            reservation.Description = Description;
            reservation.Capacity = Capacity;
            reservation.Cost = Cost;
            reservation.Deposit = Deposit;
            reservation.FinalCost = FinalCost;
            reservation.Period = Period;
            reservation.ReservationStartTime = ReservationStartTime;
            reservation.ReservationEndTime = ReservationEndTime;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("DeleteRootReservation")]
        public async Task<IActionResult> DeleteRootReservation([FromForm] int reservationCode)
        {
            var (_, _, _, isCenter) = await GetUserInfoAsync();
            if (!UserHasReservationPermission() || isCenter)
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var reservation = await _context.Reservations.FindAsync(reservationCode);
            if (reservation == null) return NotFound();
            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet("GetBranchCodes")]
        public async Task<IActionResult> GetBranchCodes()
        {
            if (!UserHasReservationPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }
            var (_, _, rootCode, _) = await GetUserInfoAsync();
            var branches = await _context.Branches
                .Where(b => b.RootCode == rootCode)
                .Select(b => new { branchCode = b.BranchCode, branchName = b.BranchName })
                .ToListAsync();
            return Json(branches);
        }

        [HttpGet("GetReservationGrid")]
        public async Task<IActionResult> GetReservationGrid(DateTime? reservationDate, int? branchCode)
        {
            if (!UserHasReservationPermission())
            {
                return Json(new { periods = new List<string>(), grid = new List<List<object>>() });
            }
            if (!branchCode.HasValue)
                return Json(new { periods = new List<string>(), grid = new List<List<object>>(), halls = new List<object>() });

            int sessionCount = 10; // Change as needed

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
                        row.Add(null);
                    }
                }
                return row;
            }).ToList();

            var result = new
            {
                periods = sessionHeaders,
                grid,
                halls = halls.Select(h => new { hallCode = h.HallCode, hallName = h.HallName }).ToList()
            };

            // Debug logging
            Console.WriteLine($"DEBUG: Returning {halls.Count} halls");
            foreach (var hall in halls)
            {
                Console.WriteLine($"DEBUG: Hall {hall.HallCode} - {hall.HallName}");
            }

            return Json(result);
        }

        [HttpGet("GetTeachers")]
        public async Task<IActionResult> GetTeachers()
        {
            if (!UserHasReservationPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var teachers = await _context.Teachers
                .Where(t => !t.IsStaff)
                .Select(t => new { teacherCode = t.TeacherCode, teacherName = t.TeacherName })
                .ToListAsync();
            return Json(teachers);
        }

        [HttpGet("GetHalls")]
        public async Task<IActionResult> GetHalls(int branchCode)
        {
            if (!UserHasReservationPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

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
            if (!UserHasReservationPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            // Check for schedule conflicts
            var reservationDate = DateOnly.FromDateTime(DateTime.Parse(Request.Form["RTime"]));
            var dayOfWeek = reservationDate.ToDateTime(TimeOnly.MinValue).ToString("dddd"); // Gets "Sunday", "Monday", etc.

            var conflictingSchedules = await _context.Schedules
                .Where(s => s.BranchCode == reservation.BranchCode &&
                           s.HallCode == reservation.HallCode &&
                           s.DayOfWeek == dayOfWeek &&
                           s.StartTime.HasValue &&
                           s.EndTime.HasValue)
                .Include(s => s.TeacherCodeNavigation)
                .Include(s => s.SubjectCodeNavigation)
                .ToListAsync();

            foreach (var schedule in conflictingSchedules)
            {
                var scheduleStart = TimeOnly.FromDateTime(schedule.StartTime.Value);
                var scheduleEnd = TimeOnly.FromDateTime(schedule.EndTime.Value);

                // Check for time overlap
                if (reservation.ReservationStartTime.HasValue && reservation.ReservationEndTime.HasValue)
                {
                    var reservationStart = reservation.ReservationStartTime.Value;
                    var reservationEnd = reservation.ReservationEndTime.Value;

                    // Check if times overlap: (start1 < end2) && (start2 < end1)
                    if (reservationStart < scheduleEnd && scheduleStart < reservationEnd)
                    {
                        var teacherName = schedule.TeacherCodeNavigation?.TeacherName ?? "Unknown Teacher";
                        var subjectName = schedule.SubjectCodeNavigation?.SubjectName ?? "Unknown Subject";

                        return Json(new
                        {
                            success = false,
                            alert = true,
                            message = $"Time conflict detected! There is a scheduled class on {dayOfWeek} from {scheduleStart:HH:mm} to {scheduleEnd:HH:mm} with Teacher: {teacherName}, Subject: {subjectName}"
                        });
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(reservation.Description)) reservation.Description = null;
            await _context.Reservations.AddAsync(reservation);
            var result = await _context.SaveChangesAsync();
            if (result > 0)
                return Json(new { success = true });
            else
                return Json(new { success = false, message = "Reservation not saved." });
        }

        [HttpPost("EditReservation")]
        public async Task<IActionResult> EditReservation([FromForm] int ReservationCode, [FromForm] string Description, [FromForm] int TeacherCode, [FromForm] TimeOnly ReservationStartTime, [FromForm] TimeOnly ReservationEndTime)
        {
            if (!UserHasReservationPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var reservation = await _context.Reservations.FindAsync(ReservationCode);
            if (reservation == null) return NotFound();
            reservation.Description = Description;
            reservation.TeacherCode = TeacherCode;
            reservation.ReservationStartTime = ReservationStartTime;
            reservation.ReservationEndTime = ReservationEndTime;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        [HttpPost("DeleteReservation")]
        public async Task<IActionResult> DeleteReservation([FromForm] int reservationCode)
        {
            if (!UserHasReservationPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var reservation = await _context.Reservations.FindAsync(reservationCode);
            if (reservation == null) return NotFound();
            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("AddTeacher")]
        public async Task<IActionResult> AddTeacher([FromForm] string TeacherName, [FromForm] string TeacherPhone, [FromForm] string? TeacherAddress)
        {
            if (!UserHasReservationPermission())
            {
                return Json(new { success = false, message = "Access denied." });
            }

            var (userCode, _, rootCode, _) = await GetUserInfoAsync();
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
            return Json(new { teacherCode = newTeacher.TeacherCode, teacherName = newTeacher.TeacherName });
        }
    }
}