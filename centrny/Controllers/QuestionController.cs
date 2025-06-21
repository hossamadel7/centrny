using Microsoft.AspNetCore.Mvc;
using centrny.Models;  // Changed from centrny1
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;
using centrny.Attributes; // Add this for RequirePageAccess

namespace centrny.Controllers  // Changed from centrny1
{
    [RequirePageAccess("Question")] // Add page access requirement
    public class QuestionController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<QuestionController> _logger; // Add logging

        public QuestionController(CenterContext context, ILogger<QuestionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var (userRootCode, rootName, isCenter) = GetUserContext();

            if (!userRootCode.HasValue)
            {
                ViewBag.Error = "Unable to determine your root assignment. Please contact administrator.";
                return View();
            }

            ViewBag.CurrentUserRootCode = userRootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            _logger.LogInformation("Loading Question index for user {Username} (Root: {RootCode})",
                User.Identity?.Name, userRootCode);

            return View();
        }

        // --- CLAIMS-BASED USER RETRIEVAL HELPERS (Updated from Session-based) ---

        private int? GetCurrentUserRootCode()
        {
            var rootCodeClaim = User.FindFirst("RootCode");
            if (rootCodeClaim != null && int.TryParse(rootCodeClaim.Value, out int rootCode))
            {
                return rootCode;
            }
            _logger.LogWarning("User {Username} missing or invalid RootCode claim", User.Identity?.Name);
            return null;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("NameIdentifier");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return 1; // Fallback
        }

        private bool IsCurrentUserCenter()
        {
            var isCenterClaim = User.FindFirst("IsCenter");
            return isCenterClaim?.Value == "True";
        }

        private (int? rootCode, string rootName, bool isCenter) GetUserContext()
        {
            var rootCode = GetCurrentUserRootCode();
            var rootName = User.FindFirst("RootName")?.Value ?? "Unknown";
            var isCenter = IsCurrentUserCenter();
            return (rootCode, rootName, isCenter);
        }

        // =========================
        // CHAPTERS, LESSONS, QUESTIONS
        // =========================

        [HttpGet]
        public JsonResult GetChaptersWithLessonsAndQuestions(int page = 1, int pageSize = 5)
        {
            var rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new { chapters = new List<object>(), totalCount = 0 });

            var query = _context.Lessons
                .Where(l => l.ChapterCode == null && l.RootCode == rootCode.Value);

            int totalCount = query.Count();

            var chapters = query
                .OrderBy(l => l.LessonCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(chapter => new
                {
                    chapterName = chapter.LessonName,
                    chapterCode = chapter.LessonCode,
                    rootCode = chapter.RootCode,
                    yearCode = chapter.YearCode,
                    eduYearCode = chapter.EduYearCode,
                    teacherCode = chapter.TeacherCode,
                    subjectCode = chapter.SubjectCode,
                    lessons = _context.Lessons
                        .Where(l => l.ChapterCode == chapter.LessonCode && l.RootCode == rootCode.Value)
                        .Select(l => new
                        {
                            lessonName = l.LessonName,
                            lessonCode = l.LessonCode,
                            questions = _context.Questions
                                .Where(q => q.LessonCode == l.LessonCode)
                                .Select(q => new
                                {
                                    questionCode = q.QuestionCode,
                                    questionContent = q.QuestionContent,
                                    examCode = q.ExamCode,
                                    lessonCode = q.LessonCode
                                }).ToList()
                        }).ToList()
                }).ToList();

            return Json(new { chapters, totalCount });
        }

        [HttpGet]
        public JsonResult GetAnswersByQuestion(int questionCode)
        {
            var answers = _context.Answers
                .Where(a => a.QuestionCode == questionCode)
                .Select(a => new
                {
                    answerCode = a.AnswerCode,
                    answerContent = a.AnswerContent,
                    isTrue = a.IsTrue,
                    questionCode = a.QuestionCode,
                    isActive = a.IsActive
                }).ToList();

            return Json(answers);
        }

        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddAnswers([FromForm] List<string> AnswerContent, [FromForm] List<bool> IsTrue, [FromForm] int QuestionCode)
        {
            try
            {
                if (AnswerContent == null || IsTrue == null || AnswerContent.Count != IsTrue.Count)
                    return Json(new { success = false, message = "Invalid answer data." });

                int correctCount = IsTrue.Count(x => x);
                if (correctCount > 1)
                    return Json(new { success = false, message = "Only one correct answer is allowed per question." });

                bool alreadyCorrectInDb = _context.Answers.Any(a => a.QuestionCode == QuestionCode && a.IsTrue);
                if (alreadyCorrectInDb && correctCount > 0)
                    return Json(new { success = false, message = "A correct answer already exists. Only one correct answer is allowed per question." });

                for (int i = 0; i < AnswerContent.Count; i++)
                {
                    var ans = new Answer
                    {
                        AnswerContent = AnswerContent[i],
                        IsTrue = IsTrue[i],
                        QuestionCode = QuestionCode,
                        InsertUser = GetCurrentUserId(),
                        InsertTime = DateTime.Now,
                        IsActive = true
                    };
                    _context.Answers.Add(ans);
                }
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding answers for question {QuestionCode}", QuestionCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "update")]
        public JsonResult EditAnswer(int AnswerCode, string AnswerContent, bool IsTrue)
        {
            try
            {
                var answer = _context.Answers.FirstOrDefault(a => a.AnswerCode == AnswerCode);
                if (answer == null)
                    return Json(new { success = false, message = "Answer not found." });

                if (IsTrue && !_context.Answers.Where(a => a.QuestionCode == answer.QuestionCode && a.AnswerCode != AnswerCode).All(a => !a.IsTrue))
                {
                    return Json(new { success = false, message = "Only one correct answer is allowed per question." });
                }

                answer.AnswerContent = AnswerContent;
                answer.IsTrue = IsTrue;
                answer.LastUpdateUser = GetCurrentUserId();
                answer.LastUpdateTime = DateTime.Now;
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing answer {AnswerCode}", AnswerCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "delete")]
        public JsonResult DeleteAnswer(int AnswerCode)
        {
            try
            {
                var answer = _context.Answers.FirstOrDefault(a => a.AnswerCode == AnswerCode);
                if (answer == null)
                    return Json(new { success = false, message = "Answer not found." });

                _context.Answers.Remove(answer);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting answer {AnswerCode}", AnswerCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddQuestion(string QuestionContent, int LessonCode, int? ExamCode)
        {
            try
            {
                var q = new Question
                {
                    QuestionContent = QuestionContent,
                    LessonCode = LessonCode,
                    ExamCode = ExamCode,
                    InsertUser = GetCurrentUserId(),
                    InsertTime = DateTime.Now
                };
                _context.Questions.Add(q);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding question to lesson {LessonCode}", LessonCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "update")]
        public JsonResult EditQuestion(int QuestionCode, string QuestionContent, int LessonCode, int? ExamCode)
        {
            try
            {
                var question = _context.Questions.FirstOrDefault(x => x.QuestionCode == QuestionCode);
                if (question == null)
                    return Json(new { success = false, message = "Question not found." });

                question.QuestionContent = QuestionContent;
                question.LessonCode = LessonCode;
                question.ExamCode = ExamCode;
                question.LastUpdateUser = GetCurrentUserId();
                question.LastUpdateTime = DateTime.Now;
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing question {QuestionCode}", QuestionCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "delete")]
        public JsonResult DeleteQuestion(int QuestionCode)
        {
            try
            {
                var question = _context.Questions.FirstOrDefault(x => x.QuestionCode == QuestionCode);
                if (question == null)
                    return Json(new { success = false, message = "Question not found." });
                _context.Questions.Remove(question);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting question {QuestionCode}", QuestionCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================
        // CHAPTER (Add Chapter)
        // =========================
        [HttpGet]
        public JsonResult GetEduYearsByRoot()
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new List<object>());

            var years = (from teach in _context.Teaches
                         join eduyear in _context.EduYears on teach.EduYearCode equals eduyear.EduCode
                         where teach.RootCode == rootCode.Value
                         select new { teach.EduYearCode, eduyear.EduName })
                        .Distinct()
                        .OrderBy(y => y.EduYearCode)
                        .ToList();

            return Json(years.Select(y => new { eduYearCode = y.EduYearCode, eduYearName = y.EduName }));
        }

        [HttpGet]
        public JsonResult GetTeachersByRoot()
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new List<object>());

            var teachers = _context.Teaches
                .Where(t => t.RootCode == rootCode.Value)
                .Select(t => t.TeacherCode)
                .Distinct()
                .ToList();
            var teacherList = teachers.Select(t => new {
                teacherCode = t,
                teacherName = _context.Teachers.FirstOrDefault(x => x.TeacherCode == t)?.TeacherName ?? "N/A"
            }).ToList();
            return Json(teacherList);
        }

        [HttpGet]
        public JsonResult GetSubjectsByTeacherYear(int teacherCode, int eduYearCode)
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new List<object>());

            var subjects = _context.Teaches
                .Where(t => t.TeacherCode == teacherCode && t.EduYearCode == eduYearCode && t.RootCode == rootCode.Value)
                .Select(t => t.SubjectCode)
                .Distinct()
                .ToList();
            var subjectList = subjects.Select(s => new {
                subjectCode = s,
                subjectName = _context.Subjects.FirstOrDefault(x => x.SubjectCode == s)?.SubjectName ?? "N/A"
            }).ToList();
            return Json(subjectList);
        }

        [HttpGet]
        public JsonResult IsUserCenter()
        {
            bool isCenter = IsCurrentUserCenter();
            return Json(new { isCenter = isCenter });
        }

        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddChapter(string LessonName, int EduYearCode, int TeacherCode, int SubjectCode)
        {
            try
            {
                int? userRootCode = GetCurrentUserRootCode();
                if (!userRootCode.HasValue)
                    return Json(new { success = false, message = "Unable to determine root assignment." });

                var teach = _context.Teaches.FirstOrDefault(t =>
                    t.TeacherCode == TeacherCode &&
                    t.EduYearCode == EduYearCode &&
                    t.SubjectCode == SubjectCode &&
                    t.RootCode == userRootCode.Value
                );

                if (teach == null)
                    return Json(new { success = false, message = "Matching Teach record not found." });

                var lesson = new Lesson
                {
                    LessonName = LessonName,
                    RootCode = userRootCode.Value,
                    EduYearCode = EduYearCode,
                    TeacherCode = TeacherCode,
                    SubjectCode = SubjectCode,
                    ChapterCode = null,
                    YearCode = teach.YearCode,
                    IsActive = true,
                    InsertUser = GetCurrentUserId(),
                    InsertTime = DateTime.Now
                };

                _context.Lessons.Add(lesson);
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding chapter {LessonName}", LessonName);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddLesson(string LessonName, int RootCode, int TeacherCode, int SubjectCode, int EduYearCode, int? ChapterCode, int? YearCode)
        {
            try
            {
                var lesson = new Lesson
                {
                    LessonName = LessonName,
                    RootCode = RootCode,
                    TeacherCode = TeacherCode,
                    SubjectCode = SubjectCode,
                    EduYearCode = EduYearCode,
                    ChapterCode = ChapterCode,
                    YearCode = YearCode,
                    IsActive = true,
                    InsertUser = GetCurrentUserId(),
                    InsertTime = DateTime.Now
                };
                _context.Lessons.Add(lesson);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding lesson {LessonName}", LessonName);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult SearchQuestions(string term)
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue || string.IsNullOrWhiteSpace(term))
                return Json(new List<object>());

            var questions = (from q in _context.Questions
                             join l in _context.Lessons on q.LessonCode equals l.LessonCode
                             where q.QuestionContent.Contains(term) && l.RootCode == rootCode.Value
                             select new
                             {
                                 questionCode = q.QuestionCode,
                                 questionContent = q.QuestionContent,
                                 lessonCode = q.LessonCode,
                                 lessonName = l.LessonName,
                                 examCode = q.ExamCode
                             }).ToList();

            return Json(questions);
        }

        // ---- For Info Box (User, Root, Teacher) ----
        [HttpGet]
        public JsonResult GetUserRootTeacherInfo()
        {
            var (userRootCode, rootName, isCenter) = GetUserContext();

            if (!userRootCode.HasValue)
            {
                return Json(new
                {
                    userCode = 0,
                    rootCode = 0,
                    teacherName = "N/A",
                    isCenter = false
                });
            }

            var teacher = _context.Teachers.FirstOrDefault(t => t.RootCode == userRootCode.Value);
            var teacherName = teacher != null ? teacher.TeacherName : "N/A";

            return Json(new
            {
                userCode = GetCurrentUserId(),
                rootCode = userRootCode.Value,
                teacherName = teacherName,
                isCenter = isCenter
            });
        }
    }
}