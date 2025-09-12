using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace centrny.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly CenterContext _context;

        public SubscriptionController(CenterContext context)
        {
            _context = context;
        }

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);

        /// <summary>
        /// Returns (userCode, groupCode, rootCode).
        /// rootCode may be null if the host is not registered in Roots table.
        /// </summary>
        private (int? userCode, int? groupCode, int? rootCode) GetSessionContext()
        {
            // Host WITHOUT port (important for matching stored root domain)
            var host = HttpContext.Request.Host.Host?.Replace("www.", "") ?? string.Empty;

            var root = _context.Roots
                .AsNoTracking()
                .FirstOrDefault(r => r.RootDomain == host);

            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                root?.RootCode  // null-safe
            );
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSubscriptions()
        {
            var (userCode, groupCode, rootCode) = GetSessionContext();
            if (!rootCode.HasValue)
            {
                return Json(new
                {
                    success = false,
                    message = "Root domain not registered or not found."
                });
            }

            var subs = await _context.SubscriptionPlans
                .Where(e => e.RootCode == rootCode.Value && e.IsActive)
                .OrderByDescending(e => e.InsertTime)
                .Select(e => new
                {
                    subPlanCode = e.SubPlanCode,
                    subPlanName = e.SubPlanName,
                    price = e.Price,
                    totalCount = e.TotalCount,
                    expiryMonths = e.ExpiryMonths,
                    description = e.Description,
                    subjects = (
                        from ps in _context.PlanSubjects
                        join s in _context.Subjects on ps.SubjectCode equals s.SubjectCode
                        where ps.SubscribtionPlanCode == e.SubPlanCode && ps.IsActive
                        select new
                        {
                            subjectCode = s.SubjectCode,
                            subjectName = s.SubjectName,
                            count = ps.Count
                        }
                    ).ToList()
                })
                .ToListAsync();

            return Json(subs);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubscription(int id)
        {
            var (userCode, groupCode, rootCode) = GetSessionContext();
            if (!rootCode.HasValue)
                return Json(new { success = false, message = "Root not found." });

            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.SubPlanCode == id && sp.RootCode == rootCode.Value);

            if (plan == null)
                return NotFound();

            var planSubjects = await _context.PlanSubjects
                .Where(ps => ps.SubscribtionPlanCode == id && ps.IsActive)
                .Select(ps => new
                {
                    ps.SubscribtionPlanCode,
                    ps.YearCode,
                    ps.SubjectCode,
                    ps.Count
                }).ToListAsync();

            return Json(new
            {
                plan.SubPlanCode,
                plan.SubPlanName,
                plan.Price,
                plan.TotalCount,
                plan.ExpiryMonths,
                plan.Description,
                Subjects = planSubjects
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetYears()
        {
            var (_, _, rootCode) = GetSessionContext();
            if (!rootCode.HasValue)
                return Json(Array.Empty<object>());

            var years = await _context.Years
                .Where(y => y.RootCode == rootCode.Value)
                .OrderBy(y => y.YearName)
                .Select(y => new { y.YearCode, y.YearName })
                .ToListAsync();

            return Json(years);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects(int yearCode)
        {
            var (_, _, rootCode) = GetSessionContext();
            if (!rootCode.HasValue)
                return Json(Array.Empty<object>());

            var subjects = await (
                from s in _context.Subjects
                join t in _context.Teaches on s.SubjectCode equals t.SubjectCode
                where s.RootCode == rootCode.Value &&
                      t.YearCode == yearCode &&
                      t.IsActive
                select new { s.SubjectCode, s.SubjectName }
            )
            .Distinct()
            .OrderBy(s => s.SubjectName)
            .ToListAsync();

            return Json(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> SearchStudentsByPhone(string phone)
        {
            var (_, _, rootCode) = GetSessionContext();
            if (!rootCode.HasValue)
                return Json(new { success = true, students = new object[0] });

            var students = await (
                from s in _context.Students
                join y in _context.Years on s.YearCode equals y.YearCode
                where s.StudentPhone == phone && s.IsActive && y.RootCode == rootCode.Value
                select new
                {
                    studentCode = s.StudentCode,
                    name = s.StudentName,
                    yearName = y.YearName,
                    yearCode = y.YearCode
                }
            ).ToListAsync();

            return Json(new { success = true, students });
        }

        [HttpPost]
        public async Task<IActionResult> BuyStudentPlan([FromBody] BuyStudentPlanDto dto)
        {
            if (dto == null || dto.SubscriptionPlanCode == 0 || dto.StudentCode == 0)
                return BadRequest("Invalid data.");

            var (userCode, _, rootCode) = GetSessionContext();
            if (!rootCode.HasValue)
                return Json(new { success = false, message = "Root not found." });

            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.SubPlanCode == dto.SubscriptionPlanCode && p.IsActive && p.RootCode == rootCode.Value);

            if (plan == null)
                return Json(new { success = false, message = "Plan not found." });

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentCode == dto.StudentCode && s.IsActive && s.RootCode == rootCode.Value);

            if (student == null)
                return Json(new { success = false, message = "Student not found." });

            var year = await _context.Years
                .FirstOrDefaultAsync(y => y.YearCode == student.YearCode && y.RootCode == rootCode.Value);

            if (year == null)
                return Json(new { success = false, message = "Student year not found." });

            var eduYearCode = await _context.EduYears
     .Where(ey => ey.RootCode == rootCode.Value && ey.IsActive)
     .Select(ey => (int?)ey.EduCode)   // cast to nullable
     .FirstOrDefaultAsync();

            if (!eduYearCode.HasValue)
                return Json(new { success = false, message = "Educational year not configured." });
            var today = DateOnly.FromDateTime(DateTime.Today);
            var expiryDate = today.AddMonths((int)plan.ExpiryMonths);

            var studentPlan = new StudentPlan
            {
                SubscriptionPlanCode = plan.SubPlanCode,
                StudentCode = student.StudentCode,
                EduYearCode = (int)eduYearCode,
                SubDate = today,
                IsActive = true,
                InsertUser = userCode ?? 0,
                InsertTime = DateTime.Now,
                LastInsertUser = null,
                LastInsertTime = null,
                Price = plan.Price,
                ExpiryDate = expiryDate,
                IsExpired = false
            };

            _context.StudentPlans.Add(studentPlan);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SubPlanName) || dto.Subjects == null || dto.Subjects.Count == 0)
                return BadRequest("Invalid data.");

            var (userCode, _, rootCode) = GetSessionContext();
            if (!rootCode.HasValue || !userCode.HasValue)
                return Unauthorized();

            int totalCount = dto.Subjects.Sum(s => s.Count);

            var plan = new SubscriptionPlan
            {
                SubPlanName = dto.SubPlanName,
                Price = dto.Price,
                TotalCount = totalCount,
                ExpiryMonths = dto.ExpiryMonths,
                Description = dto.Description,
                RootCode = rootCode.Value,
                InsertUser = userCode.Value,
                InsertTime = DateTime.Now,
                IsActive = true
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            foreach (var subjectDto in dto.Subjects)
            {
                var planSubject = new PlanSubject
                {
                    YearCode = subjectDto.YearCode,
                    SubjectCode = subjectDto.SubjectCode,
                    Count = subjectDto.Count,
                    SubscribtionPlanCode = plan.SubPlanCode,
                    IsActive = true,
                    InsertUser = userCode.Value,
                    InsertTime = DateTime.Now
                };
                _context.PlanSubjects.Add(planSubject);
            }
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] EditSubscriptionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SubPlanName) || dto.Subjects == null || dto.Subjects.Count == 0)
                return BadRequest("Invalid data.");

            var (userCode, _, rootCode) = GetSessionContext();
            if (!userCode.HasValue)
                return Unauthorized();

            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.SubPlanCode == dto.SubPlanCode && sp.RootCode == rootCode);

            if (plan == null)
                return NotFound("Subscription plan not found.");

            plan.SubPlanName = dto.SubPlanName;
            plan.Price = dto.Price;
            plan.TotalCount = dto.Subjects.Sum(s => s.Count);
            plan.ExpiryMonths = dto.ExpiryMonths;
            plan.Description = dto.Description;
            plan.LastUpdateUser = userCode.Value;
            plan.LastUpdateTime = DateTime.Now;

            var oldSubjects = await _context.PlanSubjects
                .Where(ps => ps.SubscribtionPlanCode == plan.SubPlanCode && ps.IsActive)
                .ToListAsync();

            foreach (var old in oldSubjects)
            {
                old.IsActive = false;
                old.LastInsertUser = plan.LastUpdateUser;
                old.LastInsertTime = plan.LastUpdateTime;
            }

            foreach (var subjectDto in dto.Subjects)
            {
                var planSubject = new PlanSubject
                {
                    YearCode = subjectDto.YearCode,
                    SubjectCode = subjectDto.SubjectCode,
                    Count = subjectDto.Count,
                    SubscribtionPlanCode = plan.SubPlanCode,
                    IsActive = true,
                    InsertUser = userCode.Value,
                    InsertTime = DateTime.Now
                };
                _context.PlanSubjects.Add(planSubject);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] DeleteSubscriptionDto dto)
        {
            if (dto == null || dto.SubPlanCode == 0)
                return BadRequest("Invalid data.");

            var (userCode, _, rootCode) = GetSessionContext();
            if (!userCode.HasValue)
                return Unauthorized();

            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.SubPlanCode == dto.SubPlanCode && sp.RootCode == rootCode);

            if (plan == null)
                return NotFound("Subscription plan not found.");

            plan.IsActive = false;
            plan.LastUpdateUser = userCode.Value;
            plan.LastUpdateTime = DateTime.Now;

            var planSubjects = await _context.PlanSubjects
                .Where(ps => ps.SubscribtionPlanCode == dto.SubPlanCode && ps.IsActive)
                .ToListAsync();

            foreach (var subject in planSubjects)
            {
                subject.IsActive = false;
                subject.LastInsertUser = plan.LastUpdateUser;
                subject.LastInsertTime = plan.LastUpdateTime;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // DTOs
        public class CreateSubscriptionDto
        {
            public string SubPlanName { get; set; }
            public int Price { get; set; }
            public double ExpiryMonths { get; set; }
            public string Description { get; set; }
            public List<PlanSubjectDto> Subjects { get; set; }
        }
        public class EditSubscriptionDto
        {
            public int SubPlanCode { get; set; }
            public string SubPlanName { get; set; }
            public int Price { get; set; }
            public double ExpiryMonths { get; set; }
            public string Description { get; set; }
            public List<PlanSubjectDto> Subjects { get; set; }
        }
        public class DeleteSubscriptionDto
        {
            public int SubPlanCode { get; set; }
        }
        public class PlanSubjectDto
        {
            public int YearCode { get; set; }
            public int SubjectCode { get; set; }
            public int Count { get; set; }
        }
        public class BuyStudentPlanDto
        {
            public int SubscriptionPlanCode { get; set; }
            public int StudentCode { get; set; }
        }
    }
}