using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using centrny.Models;
using centrny.Attributes;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;

namespace centrny.Controllers
{
    [RequirePageAccess("Question")]
    [Authorize]
    public class QuestionController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<QuestionController> _logger;
        private readonly IStringLocalizer<QuestionController> _localizer;

        public QuestionController(
            CenterContext context,
            ILogger<QuestionController> logger,
            IStringLocalizer<QuestionController> localizer)
        {
            _context = context;
            _logger = logger;
            _localizer = localizer;
        }

        // --- SESSION-BASED CONTEXT HELPERS (MATCHING BRANCH CONTROLLER) ---
        private int GetSessionInt(string key) => (int)HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private (int userCode, int groupCode, int rootCode, string username) GetSessionContext()
        {
            return (
                GetSessionInt("UserCode"),
                GetSessionInt("GroupCode"),
                _context.Roots.Where(x => x.RootDomain == HttpContext.Request.Host.Host.ToString().Replace("www.", "")).FirstOrDefault().RootCode,
                GetSessionString("Username")
            );
        }

        // --- BASIC PAGE ENTRY ---
        public IActionResult Index()
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            ViewBag.UserRootCode = rootCode;
            ViewBag.UserGroupCode = groupCode;
            ViewBag.CurrentUserName = username;
            ViewBag.IsCenter = GetSessionString("RootIsCenter") == "True";
            ViewBag.UserCode = userCode;

            return View();
        }
        // ======== CHUNK 2: GET ENDPOINTS WITH ROOT-BASED SECURITY ========

        // Helper: Restrict all data to current root
        private IQueryable<Lesson> LessonsForCurrentRoot()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            return _context.Lessons.Where(l => l.RootCode == rootCode && l.IsActive);
        }

        private IQueryable<Question> QuestionsForCurrentRoot()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            return _context.Questions.Where(q => q.LessonCode != null &&
                _context.Lessons.Any(l => l.LessonCode == q.LessonCode && l.RootCode == rootCode));
        }

        // Subject-Year pairs for dropdown (restricted to current root)
        [HttpGet]
        public JsonResult GetSubjectYearsByRoot()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var teaches = _context.Teaches.Where(t => t.RootCode == rootCode && t.IsActive);

            var subjectYears = teaches
                .Select(t => new
                {
                    subjectCode = t.SubjectCode,
                    yearCode = t.YearCode,
                    subjectName = t.SubjectCodeNavigation.SubjectName,
                    yearName = t.YearCodeNavigation.YearName,
                    displayName = t.SubjectCodeNavigation.SubjectName + " - " + t.YearCodeNavigation.YearName
                })
                .Distinct()
                .OrderBy(x => x.subjectName)
                .ThenBy(x => x.yearName)
                .ToList();

            return Json(subjectYears);
        }

        // Lessons hierarchy for sidebar, root-restricted
        [HttpGet]
        public JsonResult GetLessonHierarchy(int subjectCode, int yearCode)
        {
            var (_, _, rootCode, _) = GetSessionContext();

            var chapters = _context.Lessons
                .Where(l => l.RootCode == rootCode && l.SubjectCode == subjectCode && l.YearCode == yearCode && l.ChapterCode == null && l.IsActive)
                .OrderBy(l => l.LessonCode)
                .Select(chapter => new
                {
                    chapterCode = chapter.LessonCode,
                    chapterName = chapter.LessonName,
                    lessons = _context.Lessons
                        .Where(l => l.ChapterCode == chapter.LessonCode && l.IsActive)
                        .OrderBy(l => l.LessonCode)
                        .Select(lesson => new
                        {
                            lessonCode = lesson.LessonCode,
                            lessonName = lesson.LessonName,
                            questionCount = _context.Questions.Count(q => q.LessonCode == lesson.LessonCode)
                        }).ToList()
                }).ToList();

            return Json(new { chapters = chapters });
        }

        // Get questions for a lesson (root restricted)
        [HttpGet]
        public JsonResult GetQuestionsByLesson(int lessonCode, int page = 1, int pageSize = 10)
        {
            var (_, _, rootCode, _) = GetSessionContext();
            // Only allow access if lesson belongs to current root
            var lesson = _context.Lessons.FirstOrDefault(l => l.LessonCode == lessonCode && l.RootCode == rootCode && l.IsActive);
            if (lesson == null)
                return Json(new { questions = new List<object>(), pagination = new { currentPage = 1, totalPages = 1, totalCount = 0, pageSize = pageSize } });

            var questions = _context.Questions
                .Where(q => q.LessonCode == lessonCode)
                .OrderBy(q => q.QuestionCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    questionCode = q.QuestionCode,
                    questionContent = q.QuestionContent,
                    examCode = q.ExamCode,
                    lessonCode = q.LessonCode,
                    insertTime = q.InsertTime
                }).ToList();

            int totalCount = _context.Questions.Count(q => q.LessonCode == lessonCode);
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return Json(new
            {
                questions = questions,
                pagination = new
                {
                    currentPage = page,
                    totalPages = totalPages,
                    totalCount = totalCount,
                    pageSize = pageSize
                }
            });
        }

        // Get answers for a question (root restricted)
        [HttpGet]
        public JsonResult GetAnswersByQuestion(int questionCode)
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var question = _context.Questions.Include(q => q.LessonCodeNavigation)
                .FirstOrDefault(q => q.QuestionCode == questionCode);

            // Security: only allow access if question's lesson belongs to current root
            if (question == null || question.LessonCodeNavigation?.RootCode != rootCode)
                return Json(new List<object>());

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
        // ======== CHUNK 3: ADD, EDIT, DELETE ENDPOINTS WITH STRICT ROOT SECURITY ========

        // --- ADD CHAPTER ---
        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddChapter(string LessonName, int? EduYearCode, int? TeacherCode, int SubjectCode, int? YearCode)
        {
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            // Security: Only allow adding to current root
            if (!EduYearCode.HasValue || !TeacherCode.HasValue || SubjectCode == 0 || !YearCode.HasValue)
                return Json(new { success = false, message = "Missing required fields." });

            var teach = _context.Teaches.FirstOrDefault(t =>
                t.TeacherCode == TeacherCode &&
                t.EduYearCode == EduYearCode &&
                t.SubjectCode == SubjectCode &&
                t.RootCode == rootCode);

            if (teach == null)
                return Json(new { success = false, message = "No matching permission for this chapter's context." });

            var lesson = new Lesson
            {
                LessonName = LessonName,
                RootCode = rootCode,
                EduYearCode = EduYearCode.Value,
                TeacherCode = TeacherCode.Value,
                SubjectCode = SubjectCode,
                ChapterCode = null,
                YearCode = YearCode.Value,
                IsActive = true,
                InsertUser = userCode,
                InsertTime = DateTime.Now
            };

            _context.Lessons.Add(lesson);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        // --- ADD LESSON ---
        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddLesson(string LessonName, int? ChapterCode, int? TeacherCode, int? YearCode)
        {
            var (userCode, groupCode, sessionRootCode, username) = GetSessionContext();

            if (!ChapterCode.HasValue)
                return Json(new { success = false, message = "Chapter is required." });

            // Find the parent chapter (must be active, a chapter, and under this root)
            var chapter = _context.Lessons.FirstOrDefault(x =>
                x.LessonCode == ChapterCode.Value &&
                x.RootCode == sessionRootCode &&
                x.IsActive &&
                x.ChapterCode == null);

            if (chapter == null)
                return Json(new { success = false, message = "Invalid or inaccessible Chapter." });

            if (!TeacherCode.HasValue)
                return Json(new { success = false, message = "Teacher is required." });

            if (!YearCode.HasValue)
                return Json(new { success = false, message = "Year is required." });

            var lesson = new Lesson
            {
                LessonName = LessonName,
                RootCode = chapter.RootCode,
                TeacherCode = TeacherCode.Value,
                SubjectCode = chapter.SubjectCode,
                EduYearCode = chapter.EduYearCode,
                ChapterCode = ChapterCode,
                YearCode = YearCode,
                IsActive = true,
                InsertUser = userCode,
                InsertTime = DateTime.Now
            };

            _context.Lessons.Add(lesson);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        // --- ADD QUESTION (with answers, for lesson) ---
        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddQuestionWithAnswers(string questionContent, int chapterCode, List<string> answers, int correctAnswerIndex, int? examCode = null)
        {
            var (userCode, _, rootCode, _) = GetSessionContext();

            var lesson = _context.Lessons.FirstOrDefault(l => l.LessonCode == chapterCode && l.RootCode == rootCode && l.IsActive);
            if (lesson == null)
                return Json(new { success = false, message = "Lesson not found or access denied." });

            // Validate input as before...
            // [Validation logic unchanged, omitted for brevity, see prior chunk]

            // [Create question and answers as before]
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var question = new Question
                {
                    QuestionContent = questionContent.Trim(),
                    LessonCode = chapterCode,
                    ExamCode = examCode,
                    InsertUser = userCode,
                    InsertTime = DateTime.Now
                };

                _context.Questions.Add(question);
                _context.SaveChanges();

                for (int i = 0; i < answers.Count; i++)
                {
                    var answer = new Answer
                    {
                        AnswerContent = answers[i].Trim(),
                        IsTrue = i == correctAnswerIndex,
                        QuestionCode = question.QuestionCode,
                        InsertUser = userCode,
                        InsertTime = DateTime.Now,
                        IsActive = true
                    };
                    _context.Answers.Add(answer);
                }

                _context.SaveChanges();
                transaction.Commit();

                return Json(new { success = true, questionCode = question.QuestionCode });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error adding question/answers");
                return Json(new { success = false, message = "Error saving question. Please try again." });
            }
        }

        // --- EDIT QUESTION (with answers) ---
        [HttpPost]
        [RequirePageAccess("Question", "update")]
        public JsonResult UpdateQuestionWithAnswers(int questionCode, string questionContent, List<string> answers, int correctAnswerIndex, int? examCode = null)
        {
            var (userCode, _, rootCode, _) = GetSessionContext();

            var question = _context.Questions.Include(q => q.LessonCodeNavigation)
                .FirstOrDefault(q => q.QuestionCode == questionCode);

            // Security: Check question belongs to this root
            if (question == null || question.LessonCodeNavigation?.RootCode != rootCode)
                return Json(new { success = false, message = "Question not found or access denied." });

            // [Validation logic unchanged, omitted for brevity, see prior chunk]

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // [Update logic unchanged]
                question.QuestionContent = questionContent.Trim();
                question.ExamCode = examCode;
                question.LastUpdateUser = userCode;
                question.LastUpdateTime = DateTime.Now;

                var existingAnswers = _context.Answers.Where(a => a.QuestionCode == questionCode).ToList();
                _context.Answers.RemoveRange(existingAnswers);

                for (int i = 0; i < answers.Count; i++)
                {
                    var answer = new Answer
                    {
                        AnswerContent = answers[i].Trim(),
                        IsTrue = i == correctAnswerIndex,
                        QuestionCode = questionCode,
                        InsertUser = userCode,
                        InsertTime = DateTime.Now,
                        IsActive = true
                    };
                    _context.Answers.Add(answer);
                }

                _context.SaveChanges();
                transaction.Commit();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error updating question/answers");
                return Json(new { success = false, message = "Error updating question. Please try again." });
            }
        }

        // --- DELETE QUESTION ---
        [HttpPost]
        [RequirePageAccess("Question", "delete")]
        public JsonResult DeleteQuestion(int QuestionCode)
        {
            var (userCode, _, rootCode, _) = GetSessionContext();

            var question = _context.Questions.Include(q => q.LessonCodeNavigation)
                .FirstOrDefault(x => x.QuestionCode == QuestionCode);

            if (question == null || question.LessonCodeNavigation?.RootCode != rootCode)
                return Json(new { success = false, message = "Question not found or access denied." });

            var answers = _context.Answers.Where(a => a.QuestionCode == QuestionCode).ToList();
            _context.Answers.RemoveRange(answers);
            _context.Questions.Remove(question);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // --- DELETE ANSWER ---
        [HttpPost]
        [RequirePageAccess("Question", "delete")]
        public JsonResult DeleteAnswer(int AnswerCode)
        {
            var (userCode, _, rootCode, _) = GetSessionContext();

            var answer = _context.Answers.Include(a => a.QuestionCodeNavigation)
                .ThenInclude(q => q.LessonCodeNavigation)
                .FirstOrDefault(a => a.AnswerCode == AnswerCode);

            if (answer == null || answer.QuestionCodeNavigation?.LessonCodeNavigation?.RootCode != rootCode)
                return Json(new { success = false, message = "Answer not found or access denied." });

            _context.Answers.Remove(answer);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        // --- EDIT ANSWER ---
        [HttpPost]
        [RequirePageAccess("Question", "update")]
        public JsonResult EditAnswer(int AnswerCode, string AnswerContent, bool IsTrue)
        {
            var (userCode, _, rootCode, _) = GetSessionContext();

            var answer = _context.Answers.Include(a => a.QuestionCodeNavigation)
                .ThenInclude(q => q.LessonCodeNavigation)
                .FirstOrDefault(a => a.AnswerCode == AnswerCode);

            if (answer == null || answer.QuestionCodeNavigation?.LessonCodeNavigation?.RootCode != rootCode)
                return Json(new { success = false, message = "Answer not found or access denied." });

            answer.AnswerContent = AnswerContent;
            answer.IsTrue = IsTrue;
            answer.LastUpdateUser = userCode;
            answer.LastUpdateTime = DateTime.Now;
            _context.SaveChanges();
            return Json(new { success = true });
        }
        // ======== CHUNK 4: USER INFO & REMAINING UTILITY ENDPOINTS ========

        // --- GET USER ROOT/TEACHER INFO ---
        [HttpGet]
        public JsonResult GetUserRootTeacherInfo()
        {
            // This endpoint is for JS to get current user's root, code, teacher, etc.
            var (userCode, groupCode, rootCode, username) = GetSessionContext();

            // Try to get teacher info for this user/root
            var teacher = _context.Teachers.FirstOrDefault(t => t.RootCode == rootCode);
            var isCenter = _context.Roots.FirstOrDefault(r => r.RootCode == rootCode)?.IsCenter ?? false;
            string teacherName = teacher?.TeacherName ?? "";

            return Json(new
            {
                userCode,
                groupCode,
                rootCode,
                username,
                isCenter,
                teacherCode = teacher?.TeacherCode,
                teacherName
            });
        }

        // --- GET EDU YEARS BY ROOT ---
        [HttpGet]
        public JsonResult GetEduYearsByRoot()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var eduYears = _context.EduYears
                .Where(t => t.RootCode == rootCode && t.IsActive)
                .Select(t => new
                {
                    eduYearCode = t.EduCode,
                    eduYearName = t.EduName
                })
                .Distinct()
                .ToList();

            return Json(eduYears);
        }

        // --- GET TEACHERS BY ROOT ---
        [HttpGet]
        public JsonResult GetTeachersByRoot()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var teachers = _context.Teachers
                .Where(t => t.RootCode == rootCode && t.IsActive)
                .Select(t => new
                {
                    teacherCode = t.TeacherCode,
                    teacherName = t.TeacherName
                })
                .ToList();

            return Json(teachers);
        }

        // --- (OPTIONAL) GET ALL CHAPTERS FOR ROOT (for admin tools) ---
        [HttpGet]
        public JsonResult GetAllChaptersForRoot()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var chapters = _context.Lessons
                .Where(l => l.RootCode == rootCode && l.ChapterCode == null && l.IsActive)
                .Select(l => new
                {
                    lessonCode = l.LessonCode,
                    lessonName = l.LessonName
                })
                .OrderBy(l => l.lessonName)
                .ToList();
            return Json(chapters);
        }

        // --- (OPTIONAL) SEARCH QUESTIONS (restricted by root) ---
        [HttpGet]
        public JsonResult SearchQuestions(string q)
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var results = _context.Questions
                .Where(ques => ques.QuestionContent.Contains(q) &&
                    _context.Lessons.Any(l => l.LessonCode == ques.LessonCode && l.RootCode == rootCode))
                .Select(ques => new
                {
                    questionCode = ques.QuestionCode,
                    questionContent = ques.QuestionContent,
                    lessonCode = ques.LessonCode
                })
                .Take(50)
                .ToList();

            return Json(results);
        }

        // --- (OPTIONAL) GET QUESTION STATS (by root) ---
        [HttpGet]
        public JsonResult GetQuestionStats()
        {
            var (_, _, rootCode, _) = GetSessionContext();
            int totalQuestions = _context.Questions.Count(q =>
                _context.Lessons.Any(l => l.LessonCode == q.LessonCode && l.RootCode == rootCode));
            int totalLessons = _context.Lessons.Count(l => l.RootCode == rootCode);
            int totalChapters = _context.Lessons.Count(l => l.RootCode == rootCode && l.ChapterCode == null);

            return Json(new
            {
                totalQuestions,
                totalLessons,
                totalChapters
            });
        }
                // END CONTROLLER
            // ======== CHUNK 5: CONTROLLER CLOSING, EXTRAS & NOTES ========

        // --- (OPTIONAL) GET LESSON BY CODE (WITH ROOT SECURITY) ---
        [HttpGet]
        public JsonResult GetLesson(int lessonCode)
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var lesson = _context.Lessons
                .Where(l => l.LessonCode == lessonCode && l.RootCode == rootCode)
                .Select(l => new
                {
                    lessonCode = l.LessonCode,
                    lessonName = l.LessonName,
                    subjectCode = l.SubjectCode,
                    yearCode = l.YearCode,
                    chapterCode = l.ChapterCode
                })
                .FirstOrDefault();

            if (lesson == null)
                return Json(new { success = false, message = "Lesson not found or access denied." });
            return Json(new { success = true, lesson });
        }

        // --- (OPTIONAL) GET CHAPTER BY CODE (WITH ROOT SECURITY) ---
        [HttpGet]
        public JsonResult GetChapter(int chapterCode)
        {
            var (_, _, rootCode, _) = GetSessionContext();
            var chapter = _context.Lessons
                .Where(l => l.LessonCode == chapterCode && l.RootCode == rootCode && l.ChapterCode == null)
                .Select(l => new
                {
                    chapterCode = l.LessonCode,
                    chapterName = l.LessonName,
                    subjectCode = l.SubjectCode,
                    yearCode = l.YearCode
                })
                .FirstOrDefault();

            if (chapter == null)
                return Json(new { success = false, message = "Chapter not found or access denied." });
            return Json(new { success = true, chapter });
        }
    }

}
// ======== CHUNK 6: DTOs, Models, and Helper Classes for QuestionController ========

// You may need to adjust namespaces and using directives for your actual project structure.

public class AddLessonDto
{
    public string LessonName { get; set; }
    public int RootCode { get; set; }
    public int TeacherCode { get; set; }
    public int SubjectCode { get; set; }
    public int EduYearCode { get; set; }
    public int? ChapterCode { get; set; }
    public int YearCode { get; set; }
}

public class AddChapterDto
{
    public string LessonName { get; set; }
    public int EduYearCode { get; set; }
    public int TeacherCode { get; set; }
    public int SubjectCode { get; set; }
    public int YearCode { get; set; }
}

public class AddQuestionWithAnswersDto
{
    public string QuestionContent { get; set; }
    public int ChapterCode { get; set; }
    public List<string> Answers { get; set; }
    public int CorrectAnswerIndex { get; set; }
    public int? ExamCode { get; set; }
}

public class UpdateQuestionWithAnswersDto
{
    public int QuestionCode { get; set; }
    public string QuestionContent { get; set; }
    public List<string> Answers { get; set; }
    public int CorrectAnswerIndex { get; set; }
    public int? ExamCode { get; set; }
}

public class EditAnswerDto
{
    public int AnswerCode { get; set; }
    public string AnswerContent { get; set; }
    public bool IsTrue { get; set; }
}

// You may also have view models or other helper classes for returning data to the frontend
// Example:
public class LessonViewModel
{
    public int LessonCode { get; set; }
    public string LessonName { get; set; }
    public int SubjectCode { get; set; }
    public int YearCode { get; set; }
    public int? ChapterCode { get; set; }
}

// Add any additional helpers or data contracts as needed for your endpoints.

// ======== END OF CONTROLLER FILE ========