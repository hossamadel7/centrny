using centrny.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace centrny.Controllers
{
    [Authorize]
    public class StudentLearnController : Controller
    {
        private readonly CenterContext _context;

        public StudentLearnController(CenterContext context)
        {
            _context = context;
        }

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int? userCode, int? groupCode, int? rootCode, bool isCenter) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                GetSessionInt("RootCode"),
                GetSessionString("RootIsCenter") == "True"
            );
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupedLearnData(
            int page = 1,
            int pageSize = 10,
            int? yearCode = null,
            int? subjectCode = null,
            int? studentCode = null,
            string search = null)
        {
            var (userCode, groupCode, rootCode, _) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var learnsQuery = from l in _context.Learns
                              join s in _context.Students on l.StudentCode equals s.StudentCode
                              join subj in _context.Subjects on l.SubjectCode equals subj.SubjectCode
                              join t in _context.Teachers on l.TeacherCode equals t.TeacherCode
                              join b in _context.Branches on l.BranchCode equals b.BranchCode
                              join y in _context.Years on l.YearCode equals y.YearCode
                              join sc in _context.Schedules on l.ScheduleCode equals sc.ScheduleCode
                              where l.RootCode == rootCode.Value
                              select new
                              {
                                  l.StudentCode,
                                  StudentName = s.StudentName,
                                  l.YearCode,
                                  YearName = y.YearName,
                                  l.IsOnline,
                                  l.IsActive,
                                  l.SubjectCode,
                                  SubjectName = subj.SubjectName,
                                  TeacherName = t.TeacherName,
                                  BranchName = b.BranchName,
                                  ScheduleDay = sc.DayOfWeek,
                                  ScheduleStart = sc.StartTime,
                                  ScheduleEnd = sc.EndTime
                              };

            if (yearCode.HasValue)
                learnsQuery = learnsQuery.Where(l => l.YearCode == yearCode.Value);
            if (subjectCode.HasValue)
                learnsQuery = learnsQuery.Where(l => l.SubjectCode == subjectCode.Value);
            if (studentCode.HasValue)
                learnsQuery = learnsQuery.Where(l => l.StudentCode == studentCode.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                learnsQuery = learnsQuery.Where(l =>
                    l.StudentName.Contains(search) ||
                    l.SubjectName.Contains(search) ||
                    l.TeacherName.Contains(search)
                );
            }

            var groupedRaw = await learnsQuery
                .GroupBy(l => new { l.StudentCode, l.StudentName, l.YearCode, l.YearName })
                .Select(g => new
                {
                    studentCode = g.Key.StudentCode,
                    studentName = g.Key.StudentName,
                    yearCode = g.Key.YearCode,
                    yearName = g.Key.YearName,
                    isOnline = g.Any(x => x.IsOnline),
                    isActive = g.Any(x => x.IsActive),
                    subjects = g.Select(l => new
                    {
                        subjectCode = l.SubjectCode,
                        subjectName = l.SubjectName,
                        teacherName = l.TeacherName,
                        branchName = l.BranchName,
                        scheduleDay = l.ScheduleDay,
                        scheduleStart = l.ScheduleStart,
                        scheduleEnd = l.ScheduleEnd
                    }).ToList()
                })
                .OrderBy(g => g.studentName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var studentSubjectPairs = groupedRaw
                .SelectMany(g => g.subjects.Select(s => new { StudentCode = g.studentCode, SubjectCode = s.subjectCode }))
                .ToList();

            var studentCodes = studentSubjectPairs.Select(x => x.StudentCode).Distinct().ToList();
            var subjectCodes = studentSubjectPairs.Select(x => x.SubjectCode).Distinct().ToList();

            var subscribedPairs = await _context.StudentSubjectPlans
                .Where(ssp => studentCodes.Contains(ssp.StudentCode) && subjectCodes.Contains(ssp.SubjectCode))
                .Select(ssp => new { ssp.StudentCode, ssp.SubjectCode })
                .ToListAsync();

            var subscribedSet = new HashSet<(int, int)>(subscribedPairs.Select(sp => (sp.StudentCode, sp.SubjectCode)));

            var grouped = groupedRaw.Select(g => new
            {
                studentCode = g.studentCode,
                studentName = g.studentName,
                yearCode = g.yearCode,
                yearName = g.yearName,
                isOnline = g.isOnline,
                isActive = g.isActive,
                subjects = g.subjects.Select(s => new
                {
                    subjectCode = s.subjectCode,
                    subjectName = s.subjectName,
                    teacherName = s.teacherName,
                    branchName = s.branchName,
                    scheduleDay = s.scheduleDay,
                    scheduleStart = s.scheduleStart,
                    scheduleEnd = s.scheduleEnd,
                    isSubscribed = subscribedSet.Contains((g.studentCode, s.subjectCode))
                }).ToList()
            }).ToList();

            int totalCount = await learnsQuery
                .Select(l => new { l.StudentCode, l.YearCode })
                .Distinct()
                .CountAsync();

            return Json(new
            {
                data = grouped,
                totalCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetFilterOptions()
        {
            var (userCode, groupCode, rootCode, _) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var eduYear = await _context.EduYears.FirstOrDefaultAsync(e => e.RootCode == rootCode.Value && e.IsActive);
            int? eduYearCode = eduYear?.EduCode;

            var years = await _context.Years
                .Where(y => y.EduYearCode == eduYearCode)
                .OrderBy(y => y.YearName)
                .Select(y => new { y.YearCode, y.YearName })
                .ToListAsync();

            var subjects = await _context.Subjects
                .Where(s => s.RootCode == rootCode.Value)
                .OrderBy(s => s.SubjectName)
                .Select(s => new { s.SubjectCode, s.SubjectName })
                .ToListAsync();

            return Json(new
            {
                years,
                subjects
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchStudents(string term)
        {
            var (userCode, groupCode, rootCode, _) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var students = await _context.Students
                .Where(s => s.RootCode == rootCode.Value && (s.StudentName.Contains(term) || s.StudentCode.ToString().Contains(term)))
                .OrderBy(s => s.StudentName)
                .Select(s => new { s.StudentCode, s.StudentName })
                .Take(15)
                .ToListAsync();

            return Json(students);
        }

        [HttpGet]
        public async Task<IActionResult> GetAddLearnFormData(int studentCode, int yearCode)
        {
            var (userCode, groupCode, rootCode, _) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var subjects = await _context.Subjects
                .Where(s => s.RootCode == rootCode.Value && s.YearCode == yearCode)
                .OrderBy(s => s.SubjectName)
                .Select(s => new { s.SubjectCode, s.SubjectName })
                .ToListAsync();

            var activeEduYear = await _context.EduYears
                .FirstOrDefaultAsync(e => e.RootCode == rootCode.Value && e.IsActive);

            return Json(new
            {
                subjects,
                eduYearCode = activeEduYear?.EduCode ?? 0,
                rootCode = rootCode.Value
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersForSubject(int subjectCode, int yearCode)
        {
            var teachers = await (from teach in _context.Teaches
                                  join t in _context.Teachers on teach.TeacherCode equals t.TeacherCode
                                  where teach.SubjectCode == subjectCode && teach.YearCode == yearCode
                                  select new
                                  {
                                      t.TeacherCode,
                                      t.TeacherName
                                  })
                                 .Distinct()
                                 .OrderBy(t => t.TeacherName)
                                 .ToListAsync();
            return Json(new { teachers });
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesForSubjectTeacher(int subjectCode, int teacherCode, int yearCode)
        {
            var branches = await (from teach in _context.Teaches
                                  join b in _context.Branches on teach.BranchCode equals b.BranchCode
                                  where teach.SubjectCode == subjectCode && teach.TeacherCode == teacherCode && teach.YearCode == yearCode
                                  select new
                                  {
                                      b.BranchCode,
                                      b.BranchName
                                  })
                                 .Distinct()
                                 .OrderBy(b => b.BranchName)
                                 .ToListAsync();
            return Json(new { branches });
        }

        [HttpGet]
        public async Task<IActionResult> GetSchedulesForSubjectTeacherBranch(int subjectCode, int teacherCode, int? branchCode)
        {
            var query = _context.Schedules
                .Where(s => s.SubjectCode == subjectCode && s.TeacherCode == teacherCode);
            if (branchCode.HasValue)
                query = query.Where(s => s.BranchCode == branchCode.Value);

            var schedules = await query
                .Select(s => new
                {
                    s.ScheduleCode,
                    s.DayOfWeek,
                    s.StartTime,
                    s.EndTime
                })
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            return Json(new { schedules });
        }

        [HttpPost]
        public async Task<IActionResult> AddLearn([FromForm] LearnCreateDto dto)
        {
            var (userCode, groupCode, rootCode, _) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Json(new { success = false, message = "Unauthorized" });

            bool exists = await _context.Learns.AnyAsync(l =>
                l.StudentCode == dto.StudentCode &&
                l.SubjectCode == dto.SubjectCode &&
                l.YearCode == dto.YearCode);

            if (exists)
                return Json(new { success = false, message = "This subject is already assigned to this student for this year." });

            var learn = new Learn
            {
                StudentCode = dto.StudentCode,
                SubjectCode = dto.SubjectCode,
                TeacherCode = dto.TeacherCode,
                ScheduleCode = dto.ScheduleCode,
                EduYearCode = dto.EduYearCode,
                BranchCode = dto.BranchCode,
                RootCode = rootCode.Value,
                YearCode = dto.YearCode,
                IsOnline = dto.IsOnline,
                IsActive = true,
                InsertUser = userCode.Value,
                InsertTime = DateTime.Now
            };

            _context.Learns.Add(learn);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetEditLearnFormData(int studentCode, int subjectCode, int yearCode)
        {
            var (userCode, groupCode, rootCode, isCenter) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var learn = await _context.Learns.FirstOrDefaultAsync(l =>
                l.StudentCode == studentCode && l.SubjectCode == subjectCode && l.YearCode == yearCode);
            if (learn == null)
                return Json(new { success = false, message = "Learn record not found" });

            if (isCenter)
            {
                var teachers = await (from teach in _context.Teaches
                                      join t in _context.Teachers on teach.TeacherCode equals t.TeacherCode
                                      where teach.SubjectCode == subjectCode && teach.YearCode == yearCode
                                      select new
                                      {
                                          t.TeacherCode,
                                          t.TeacherName
                                      })
                                     .Distinct()
                                     .OrderBy(t => t.TeacherName)
                                     .ToListAsync();

                var schedules = await _context.Schedules
                    .Where(s => s.SubjectCode == subjectCode && s.TeacherCode == learn.TeacherCode)
                    .Select(s => new
                    {
                        s.ScheduleCode,
                        s.DayOfWeek,
                        s.StartTime,
                        s.EndTime
                    })
                    .OrderBy(s => s.DayOfWeek)
                    .ThenBy(s => s.StartTime)
                    .ToListAsync();

                return Json(new
                {
                    isCenter = true,
                    teachers,
                    schedules,
                    selectedTeacher = learn.TeacherCode,
                    selectedSchedule = learn.ScheduleCode
                });
            }
            else
            {
                var schedules = await _context.Schedules
                    .Where(s => s.SubjectCode == subjectCode && s.TeacherCode == learn.TeacherCode)
                    .Select(s => new
                    {
                        s.ScheduleCode,
                        s.DayOfWeek,
                        s.StartTime,
                        s.EndTime
                    })
                    .OrderBy(s => s.DayOfWeek)
                    .ThenBy(s => s.StartTime)
                    .ToListAsync();

                return Json(new
                {
                    isCenter = false,
                    schedules,
                    selectedTeacher = learn.TeacherCode,
                    selectedSchedule = learn.ScheduleCode
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditLearn([FromForm] LearnEditDto dto)
        {
            var (userCode, groupCode, rootCode, isCenter) = GetSessionContext();
            if (!userCode.HasValue)
                return Json(new { success = false, message = "Unauthorized" });

            var learn = await _context.Learns.FirstOrDefaultAsync(l =>
                l.StudentCode == dto.StudentCode && l.SubjectCode == dto.SubjectCode && l.YearCode == dto.YearCode);

            if (learn == null)
                return Json(new { success = false, message = "Not found" });

            if (isCenter)
                learn.TeacherCode = dto.TeacherCode;

            learn.ScheduleCode = dto.ScheduleCode;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentSubjectsForYear(int studentCode, int yearCode)
        {
            var (userCode, groupCode, rootCode, _) = GetSessionContext();
            if (!userCode.HasValue || !groupCode.HasValue || !rootCode.HasValue)
                return Unauthorized();

            var learns = await _context.Learns
                .Where(l => l.StudentCode == studentCode && l.YearCode == yearCode)
                .ToListAsync();

            var subjectList = new List<object>();

            foreach (var learn in learns)
            {
                var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == learn.SubjectCode);
                string subjectName = subject?.SubjectName ?? "";

                var schedule = await _context.Schedules.FirstOrDefaultAsync(s => s.ScheduleCode == learn.ScheduleCode);
                string scheduleDay = schedule?.DayOfWeek ?? "";
                string startTime = schedule?.StartTime != null ? schedule.StartTime.Value.ToString("HH:mm") : "";
                string endTime = schedule?.EndTime != null ? schedule.EndTime.Value.ToString("HH:mm") : "";

                var scheduleEntities = await _context.Schedules
                    .Where(s => s.SubjectCode == learn.SubjectCode && s.TeacherCode == learn.TeacherCode)
                    .OrderBy(s => s.DayOfWeek)
                    .ThenBy(s => s.StartTime)
                    .ToListAsync();

                var availableSchedules = new List<object>();
                foreach (var s in scheduleEntities)
                {
                    string avStart = s.StartTime != null ? s.StartTime.Value.ToString("HH:mm") : "";
                    string avEnd = s.EndTime != null ? s.EndTime.Value.ToString("HH:mm") : "";

                    availableSchedules.Add(new
                    {
                        scheduleCode = s.ScheduleCode,
                        scheduleName = $"{s.DayOfWeek} {avStart}-{avEnd}"
                    });
                }

                subjectList.Add(new
                {
                    subjectCode = learn.SubjectCode,
                    subjectName = subjectName,
                    currentScheduleCode = learn.ScheduleCode,
                    currentScheduleName = $"{scheduleDay} {startTime}-{endTime}",
                    availableSchedules = availableSchedules
                });
            }

            return Json(new
            {
                subjects = subjectList
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStudentSchedules([FromForm] StudentSchedulesUpdateDto dto)
        {
            var (userCode, groupCode, rootCode, _) = GetSessionContext();
            if (!userCode.HasValue)
                return Json(new { success = false, message = "Unauthorized" });

            if (dto.SubjectCodes == null || dto.NewScheduleCodes == null || dto.SubjectCodes.Count != dto.NewScheduleCodes.Count)
                return Json(new { success = false, message = "Invalid data" });

            for (int i = 0; i < dto.SubjectCodes.Count; i++)
            {
                int subjectCode = dto.SubjectCodes[i];
                int scheduleCode = dto.NewScheduleCodes[i];

                var learn = await _context.Learns.FirstOrDefaultAsync(l =>
                    l.StudentCode == dto.StudentCode &&
                    l.SubjectCode == subjectCode &&
                    l.YearCode == dto.YearCode);

                if (learn != null)
                {
                    learn.ScheduleCode = scheduleCode;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentStatistics(
            int studentCode,
            int yearCode,
            int? subjectCode = null,
            bool? isExamFilter = null
        )
        {
            var learnsQuery = _context.Learns
                .Where(l => l.StudentCode == studentCode && l.YearCode == yearCode);

            if (subjectCode.HasValue)
                learnsQuery = learnsQuery.Where(l => l.SubjectCode == subjectCode.Value);

            var learns = await learnsQuery.ToListAsync();
            var subjects = learns.Select(l => l.SubjectCode).Distinct().ToList();

            var subjectStats = new List<object>();

            foreach (var subjCode in subjects)
            {
                var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.SubjectCode == subjCode);

                var examsQuery = _context.Exams
                    .Where(e => e.SubjectCode == subjCode && e.YearCode == yearCode && e.IsActive == true);

                if (isExamFilter.HasValue)
                    examsQuery = examsQuery.Where(e => e.IsExam == isExamFilter.Value);

                var exams = await examsQuery.OrderBy(e => e.InserTime).ToListAsync();

                var studentExams = await _context.StudentExams
                    .Where(se => se.StudentCode == studentCode)
                    .ToListAsync();

                var studentExamList = new List<StudentExam>();
                foreach (var se in studentExams)
                {
                    var exam = exams.FirstOrDefault(e => e.ExamCode == se.ExamCode);
                    if (exam != null)
                        studentExamList.Add(se);
                }

                int totalExams = exams.Count;
                int attendedExams = studentExamList.Count;
                int missedExams = totalExams - attendedExams;

                double avgMark = attendedExams > 0 ? studentExamList.Average(se => (double?)se.StudentResult ?? 0) : 0;

                var examList = exams.Select(e =>
                {
                    var se = studentExamList.FirstOrDefault(s => s.ExamCode == e.ExamCode);
                    return new
                    {
                        examName = e.ExamName,
                        examDate = e.InserTime.ToString("yyyy-MM-dd"),
                        examDegree = e.ExamDegree,
                        studentDegree = se?.StudentResult,
                        attended = se != null,
                        status = se != null ? "Attended" : "Missed"
                    };
                }).ToList();

                var schedules = learns.Where(l => l.SubjectCode == subjCode).Select(l => l.ScheduleCode).Distinct().ToList();

                var classes = await _context.Classes
                    .Where(c => schedules.Contains((int)c.ScheduleCode) && c.YearCode == yearCode)
                    .OrderBy(c => c.ClassDate)
                    .ToListAsync();

                var classIds = classes.Select(c => c.ClassCode).ToList();

                var attends = await _context.Attends
                    .Where(a => a.StudentId == studentCode && classIds.Contains(a.ClassId))
                    .ToListAsync();

                var attendedClassIds = attends.Select(a => a.ClassId).ToHashSet();

                int totalClasses = classes.Count;
                int attendedClasses = attendedClassIds.Count;
                int missedClasses = totalClasses - attendedClasses;
                double attendanceRate = totalClasses > 0 ? (attendedClasses * 100.0 / totalClasses) : 0;

                var classList = classes.Select(c => new
                {
                    className = c.ClassName,
                    classDate = c.ClassDate.HasValue ? c.ClassDate.Value.ToString("yyyy-MM-dd") : "",
                    classTime = (c.ClassStartTime != null && c.ClassEndTime != null) ?
                        $"{c.ClassStartTime:HH:mm}-{c.ClassEndTime:HH:mm}" : "",
                    attended = attendedClassIds.Contains(c.ClassCode),
                    status = attendedClassIds.Contains(c.ClassCode) ? "Attended" : "Missed"
                }).ToList();

                subjectStats.Add(new
                {
                    subjectCode = subjCode,
                    subjectName = subject?.SubjectName ?? "",
                    exams = new
                    {
                        total = totalExams,
                        attended = attendedExams,
                        missed = missedExams,
                        avgMark = avgMark,
                        list = examList
                    },
                    attendance = new
                    {
                        total = totalClasses,
                        attended = attendedClasses,
                        missed = missedClasses,
                        rate = attendanceRate,
                        list = classList
                    }
                });
            }

            return Json(new
            {
                subjects = subjectStats
            });
        }

        public class LearnCreateDto
        {
            public int StudentCode { get; set; }
            public int SubjectCode { get; set; }
            public int TeacherCode { get; set; }
            public int ScheduleCode { get; set; }
            public int EduYearCode { get; set; }
            public int BranchCode { get; set; }
            public int RootCode { get; set; }
            public int YearCode { get; set; }
            public bool IsOnline { get; set; }
        }

        public class LearnEditDto
        {
            public int StudentCode { get; set; }
            public int SubjectCode { get; set; }
            public int YearCode { get; set; }
            public int TeacherCode { get; set; }
            public int ScheduleCode { get; set; }
        }

        public class StudentSchedulesUpdateDto
        {
            public int StudentCode { get; set; }
            public int YearCode { get; set; }
            public List<int> SubjectCodes { get; set; }
            public List<int> NewScheduleCodes { get; set; }
        }
    }
}