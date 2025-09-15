using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
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

        // --- Authority Check ---
        private bool UserHasSubscriptionPermission()
        {
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return false;

            var userGroupCodes = _context.Users
                .Where(ug => ug.UserCode == user.UserCode)
                .Select(ug => ug.GroupCode)
                .ToList();

            // Use your page path as stored in Pages (adjust as needed)
            var page = _context.Pages.FirstOrDefault(p => p.PagePath == "Subscription/Index");
            if (page == null)
                return false;

            return _context.GroupPages.Any(gp => userGroupCodes.Contains(gp.GroupCode) && gp.PageCode == page.PageCode);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        public IActionResult Index()
        {
            if (!UserHasSubscriptionPermission())
            {
                return View("~/Views/Login/AccessDenied.cshtml");
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSubscriptions()
        {
            if (!UserHasSubscriptionPermission())
                return Json(new { success = false, message = "Access denied." });

            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);

            if (user == null)
                return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null)
                return Unauthorized();

            int rootCode = group.RootCode;

            var subs = await _context.SubscriptionPlans
                .Where(e => e.RootCode == rootCode && e.IsActive)
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
            if (!UserHasSubscriptionPermission())
                return Json(new { success = false, message = "Access denied." });

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.SubPlanCode == id);
            if (plan == null)
                return NotFound();

            // Also fetch plan subjects
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

        public async Task<IActionResult> GetYears()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null)
                return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null)
                return Unauthorized();

            int rootCode = group.RootCode;

            // Join Year and EduYear, filter by EduYear
            var years = await (
                from year in _context.Years
                join eduYear in _context.EduYears
                    on year.EduYearCode equals eduYear.EduCode
                where eduYear.RootCode == rootCode
                      && eduYear.IsActive
                select new
                {
                    year.YearCode,
                    year.YearName
                }
            )
            .OrderBy(y => y.YearName)
            .ToListAsync();

            return Json(years);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects(int yearCode)
        {
            // Returns subjects for selected year, active, for root code and in teach record for the year
            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null)
                return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null)
                return Unauthorized();

            int rootCode = group.RootCode;

            var subjects = await (
                from s in _context.Subjects
                join t in _context.Teaches on s.SubjectCode equals t.SubjectCode
                where s.RootCode == rootCode &&
                     
                      t.YearCode == yearCode &&
                      t.IsActive
                select new { s.SubjectCode, s.SubjectName }
            ).Distinct().OrderBy(s => s.SubjectName).ToListAsync();

            return Json(subjects);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionDto dto)
        {
            if (!UserHasSubscriptionPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null || string.IsNullOrWhiteSpace(dto.SubPlanName) || dto.Subjects == null || dto.Subjects.Count == 0)
                return BadRequest("Invalid data.");

            int userId = GetCurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserCode == userId);
            if (user == null) return Unauthorized();

            int groupCode = user.GroupCode;
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
            if (group == null) return Unauthorized();

            int rootCode = group.RootCode;

            // Calculate total count from subjects
            int totalCount = dto.Subjects.Sum(s => s.Count);

            var plan = new SubscriptionPlan
            {
                SubPlanName = dto.SubPlanName,
                Price = dto.Price,
                TotalCount = totalCount,
                ExpiryMonths = dto.ExpiryMonths,
                Description = dto.Description,
                RootCode = rootCode,
                InsertUser = userId,
                InsertTime = DateTime.Now,
                IsActive = true
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            // Now create PlanSubjects
            foreach (var subjectDto in dto.Subjects)
            {
                var planSubject = new PlanSubject
                {
                    YearCode = subjectDto.YearCode,
                    SubjectCode = subjectDto.SubjectCode,
                    Count = subjectDto.Count,
                    SubscribtionPlanCode = plan.SubPlanCode,
                    IsActive = true,
                    InsertUser = userId,
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
            if (!UserHasSubscriptionPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null || string.IsNullOrWhiteSpace(dto.SubPlanName) || dto.Subjects == null || dto.Subjects.Count == 0)
                return BadRequest("Invalid data.");

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.SubPlanCode == dto.SubPlanCode);
            if (plan == null)
                return NotFound("Subscription plan not found.");

            plan.SubPlanName = dto.SubPlanName;
            plan.Price = dto.Price;
            plan.TotalCount = dto.Subjects.Sum(s => s.Count);
            plan.ExpiryMonths = dto.ExpiryMonths;
            plan.Description = dto.Description;
            plan.LastUpdateUser = GetCurrentUserId();
            plan.LastUpdateTime = DateTime.Now;

            // Remove old PlanSubjects for this plan
            var oldSubjects = await _context.PlanSubjects.Where(ps => ps.SubscribtionPlanCode == plan.SubPlanCode && ps.IsActive).ToListAsync();
            foreach (var old in oldSubjects)
            {
                old.IsActive = false;
                old.LastInsertUser = plan.LastUpdateUser;
                old.LastInsertTime = plan.LastUpdateTime;
            }

            // Add new PlanSubjects
            int userId = GetCurrentUserId();
            foreach (var subjectDto in dto.Subjects)
            {
                var planSubject = new PlanSubject
                {
                    YearCode = subjectDto.YearCode,
                    SubjectCode = subjectDto.SubjectCode,
                    Count = subjectDto.Count,
                    SubscribtionPlanCode = plan.SubPlanCode,
                    IsActive = true,
                    InsertUser = userId,
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
            if (!UserHasSubscriptionPermission())
                return Json(new { success = false, message = "Access denied." });

            if (dto == null || dto.SubPlanCode == 0)
                return BadRequest("Invalid data.");

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.SubPlanCode == dto.SubPlanCode);
            if (plan == null)
                return NotFound("Subscription plan not found.");

            plan.IsActive = false;
            plan.LastUpdateUser = GetCurrentUserId();
            plan.LastUpdateTime = DateTime.Now;

            // Also mark PlanSubjects as inactive
            var planSubjects = await _context.PlanSubjects.Where(ps => ps.SubscribtionPlanCode == dto.SubPlanCode && ps.IsActive).ToListAsync();
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
    }
}