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
        private (int? userCode, int? groupCode, int? rootCode) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                _context.Roots.Where(x => x.RootDomain == HttpContext.Request.Host.ToString().Replace("www.", "")).FirstOrDefault().RootCode
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

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.SubPlanCode == id && sp.RootCode == rootCode);
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

        public async Task<IActionResult> GetYears()
        {
            var (userCode, groupCode, rootCode) = GetSessionContext();
          
            var years = await (
                from year in _context.Years               
                where year.RootCode == rootCode.Value
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
            var (userCode, groupCode, rootCode) = GetSessionContext();
            

            var subjects = await (
                from s in _context.Subjects
                join t in _context.Teaches on s.SubjectCode equals t.SubjectCode
                where s.RootCode == rootCode.Value &&
                      t.YearCode == yearCode &&
                      t.IsActive
                select new { s.SubjectCode, s.SubjectName }
            ).Distinct().OrderBy(s => s.SubjectName).ToListAsync();

            return Json(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> SearchStudentsByPhone(string phone)
        {
            var (userCode, groupCode, rootCode) = GetSessionContext();
          
            var students = await (
                from s in _context.Students
                join y in _context.Years on s.YearCode equals y.YearCode
                where s.StudentPhone == phone && s.IsActive
                where y.RootCode == rootCode.Value
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

            var (userCode, groupCode, rootCode) = GetSessionContext();

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.SubPlanCode == dto.SubscriptionPlanCode && p.IsActive && p.RootCode==rootCode);
            if (plan == null)
                return Json(new { success = false, message = "Plan not found." });

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == dto.StudentCode && s.IsActive && s.IsActive && s.RootCode == rootCode);
            if (student == null)
                return Json(new { success = false, message = "Student not found." });

            var year = await _context.Years.FirstOrDefaultAsync(y => y.YearCode == student.YearCode && y.RootCode == rootCode);
            if (year == null)
                return Json(new { success = false, message = "Student year not found." });

            var eduYearCode = await _context.EduYears.Where(ey => ey.RootCode == rootCode && ey.IsActive)
                .Select(ey => ey.EduCode)
                .FirstOrDefaultAsync();

            DateOnly subDate = DateOnly.FromDateTime(DateTime.Today);
            DateOnly expiryDate = subDate.AddMonths((int)plan.ExpiryMonths);

            var studentPlan = new StudentPlan
            {
                SubscriptionPlanCode = plan.SubPlanCode,
                StudentCode = student.StudentCode,
                EduYearCode = (int)eduYearCode,
                SubDate = subDate,
                IsActive = true,
                InsertUser = userCode.Value,
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

            var (userCode, groupCode, rootCode) = GetSessionContext();
          
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

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.SubPlanCode == dto.SubPlanCode);
            if (plan == null)
                return NotFound("Subscription plan not found.");

            var (userCode, groupCode, rootCode) = GetSessionContext();
            if (!userCode.HasValue)
                return Unauthorized();

            plan.SubPlanName = dto.SubPlanName;
            plan.Price = dto.Price;
            plan.TotalCount = dto.Subjects.Sum(s => s.Count);
            plan.ExpiryMonths = dto.ExpiryMonths;
            plan.Description = dto.Description;
            plan.LastUpdateUser = userCode.Value;
            plan.LastUpdateTime = DateTime.Now;

            var oldSubjects = await _context.PlanSubjects.Where(ps => ps.SubscribtionPlanCode == plan.SubPlanCode && ps.IsActive).ToListAsync();
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

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(sp => sp.SubPlanCode == dto.SubPlanCode);
            if (plan == null)
                return NotFound("Subscription plan not found.");

            var (userCode, groupCode, rootCode) = GetSessionContext();
            if (!userCode.HasValue)
                return Unauthorized();

            plan.IsActive = false;
            plan.LastUpdateUser = userCode.Value;
            plan.LastUpdateTime = DateTime.Now;

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
        public class BuyStudentPlanDto
        {
            public int SubscriptionPlanCode { get; set; }
            public int StudentCode { get; set; }
        }
    }
}