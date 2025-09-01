using Microsoft.AspNetCore.Mvc;
using centrny.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;
using centrny.Attributes;
using Microsoft.Extensions.Localization;

namespace centrny.Controllers
{
    [RequirePageAccess("Question")]
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

        public IActionResult Index()
        {
            var (userRootCode, rootName, isCenter) = GetUserContext();

            if (!userRootCode.HasValue)
            {
                ViewBag.Error = _localizer["UnableToDetermineRootAssignment"];
                return View();
            }

            ViewBag.CurrentUserRootCode = userRootCode;
            ViewBag.UserRootName = rootName;
            ViewBag.IsCenter = isCenter;

            _logger.LogInformation("Loading Question index for user {Username} (Root: {RootCode})",
                User.Identity?.Name, userRootCode);

            return View();
        }

        // --- CLAIMS-BASED USER RETRIEVAL HELPERS ---

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
            return 1;
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
        // NEW IMPROVED ENDPOINTS FOR BETTER UX
        // =========================

        [HttpGet]
        public JsonResult GetChaptersForDropdown()
        {
            var rootCode = GetCurrentUserRootCode();
            _logger.LogInformation("User {UserName} requesting chapters, claimed RootCode: {RootCode}", User.Identity?.Name, rootCode);

            try
            {
                // First, let's log what we're looking for
                _logger.LogInformation("Looking for chapters where ChapterCode is NULL, RootCode = {RootCode}, IsActive = true", rootCode);

                // Get all available roots for debugging
                var allRoots = _context.Lessons.Select(l => l.RootCode).Distinct().ToList();
                _logger.LogInformation("Available root codes in database: {Roots}", string.Join(", ", allRoots));

                // If no root code from claims, try to use the first available root for debugging
                if (!rootCode.HasValue)
                {
                    _logger.LogWarning("No root code from user claims, using first available root for debugging");
                    rootCode = allRoots.FirstOrDefault();
                    if (rootCode == 0) rootCode = null;
                }

                if (!rootCode.HasValue)
                {
                    _logger.LogError("Still no root code available");
                    return Json(new List<object>());
                }

                // Get chapters with more detailed logging
                var chaptersQuery = _context.Lessons
                    .Where(l => l.ChapterCode == null && l.RootCode == rootCode.Value);

                var totalChapters = chaptersQuery.Count();
                _logger.LogInformation("Found {Count} total chapters for root {RootCode}", totalChapters, rootCode);

                var activeChapters = chaptersQuery.Where(l => l.IsActive).Count();
                _logger.LogInformation("Found {Count} active chapters for root {RootCode}", activeChapters, rootCode);

                var chapters = chaptersQuery
                    .Where(l => l.IsActive)
                    .OrderBy(l => l.LessonName)
                    .Select(l => new
                    {
                        chapterCode = l.LessonCode,
                        chapterName = l.LessonName,
                        subjectName = _context.Subjects.FirstOrDefault(s => s.SubjectCode == l.SubjectCode).SubjectName ?? "N/A",
                        yearName = _context.Years.FirstOrDefault(y => y.YearCode == l.YearCode).YearName ?? "N/A",
                        rootCode = l.RootCode    // Add for debugging
                    })
                    .ToList();

                _logger.LogInformation("Returning {Count} chapters: {Chapters}",
                    chapters.Count,
                    string.Join(", ", chapters.Select(c => $"{c.chapterName} (ID: {c.chapterCode})")));

                return Json(chapters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chapters for dropdown for root {RootCode}", rootCode);
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public JsonResult GetQuestionsByChapter(int chapterCode)
        {
            var rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new List<object>());

            try
            {
                // Get all lessons under this chapter
                var lessonCodes = _context.Lessons
                    .Where(l => l.ChapterCode == chapterCode && l.RootCode == rootCode.Value)
                    .Select(l => l.LessonCode)
                    .ToList();

                // Add the chapter itself as it can also have questions
                lessonCodes.Add(chapterCode);

                var questions = _context.Questions
                    .Where(q => q.LessonCode.HasValue && lessonCodes.Contains(q.LessonCode.Value))
                    .OrderBy(q => q.QuestionCode)
                    .Select(q => new
                    {
                        questionCode = q.QuestionCode,
                        content = q.QuestionContent,
                        lessonCode = q.LessonCode,
                        examCode = q.ExamCode,
                        lessonName = q.LessonCodeNavigation != null ? q.LessonCodeNavigation.LessonName : "N/A",
                        answers = q.Answers
                            .Where(a => a.IsActive)
                            .OrderBy(a => a.AnswerCode)
                            .Select(a => new
                            {
                                answerCode = a.AnswerCode,
                                content = a.AnswerContent,
                                isCorrect = a.IsTrue
                            }).ToList()
                    })
                    .ToList();

                return Json(questions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading questions for chapter {ChapterCode}", chapterCode);
                return Json(new List<object>());
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "insert")]
        public JsonResult AddQuestionWithAnswers(string questionContent, int chapterCode, List<string> answers, int correctAnswerIndex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(questionContent))
                    return Json(new { success = false, message = "Question content is required" });

                if (answers == null || answers.Count < 2)
                    return Json(new { success = false, message = "At least 2 answers are required" });

                if (correctAnswerIndex < 0 || correctAnswerIndex >= answers.Count)
                    return Json(new { success = false, message = "Invalid correct answer selection" });

                // Validate that answers are not empty
                for (int i = 0; i < answers.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(answers[i]))
                        return Json(new { success = false, message = $"Answer {i + 1} cannot be empty" });
                }

                // Validate that chapter exists and belongs to user
                var rootCode = GetCurrentUserRootCode();
                if (!rootCode.HasValue)
                    return Json(new { success = false, message = "Unable to determine user context" });

                var chapter = _context.Lessons.FirstOrDefault(l => l.LessonCode == chapterCode && l.RootCode == rootCode.Value);
                if (chapter == null)
                    return Json(new { success = false, message = "Chapter not found or access denied" });

                // Start transaction
                using var transaction = _context.Database.BeginTransaction();

                try
                {
                    // Create the question
                    var question = new Question
                    {
                        QuestionContent = questionContent.Trim(),
                        LessonCode = chapterCode,
                        ExamCode = null,
                        InsertUser = GetCurrentUserId(),
                        InsertTime = DateTime.Now
                    };

                    _context.Questions.Add(question);
                    _context.SaveChanges();

                    // Create the answers
                    for (int i = 0; i < answers.Count; i++)
                    {
                        var answer = new Answer
                        {
                            AnswerContent = answers[i].Trim(),
                            IsTrue = i == correctAnswerIndex,
                            QuestionCode = question.QuestionCode,
                            InsertUser = GetCurrentUserId(),
                            InsertTime = DateTime.Now,
                            IsActive = true
                        };
                        _context.Answers.Add(answer);
                    }

                    _context.SaveChanges();
                    transaction.Commit();

                    _logger.LogInformation("Successfully added question {QuestionCode} with {AnswerCount} answers to chapter {ChapterCode}",
                        question.QuestionCode, answers.Count, chapterCode);

                    return Json(new { success = true, questionCode = question.QuestionCode });
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding question with answers to chapter {ChapterCode}", chapterCode);
                return Json(new { success = false, message = "Error saving question. Please try again." });
            }
        }

        [HttpPost]
        [RequirePageAccess("Question", "update")]
        public JsonResult UpdateQuestionWithAnswers(int questionCode, string questionContent, List<string> answers, int correctAnswerIndex)
        {
            try
            {
                var question = _context.Questions.FirstOrDefault(q => q.QuestionCode == questionCode);
                if (question == null)
                    return Json(new { success = false, message = "Question not found" });

                if (string.IsNullOrWhiteSpace(questionContent))
                    return Json(new { success = false, message = "Question content is required" });

                if (answers == null || answers.Count < 2)
                    return Json(new { success = false, message = "At least 2 answers are required" });

                if (correctAnswerIndex < 0 || correctAnswerIndex >= answers.Count)
                    return Json(new { success = false, message = "Invalid correct answer selection" });

                // Validate that answers are not empty
                for (int i = 0; i < answers.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(answers[i]))
                        return Json(new { success = false, message = $"Answer {i + 1} cannot be empty" });
                }

                using var transaction = _context.Database.BeginTransaction();

                try
                {
                    // Update question
                    question.QuestionContent = questionContent.Trim();
                    question.LastUpdateUser = GetCurrentUserId();
                    question.LastUpdateTime = DateTime.Now;

                    // Delete existing answers
                    var existingAnswers = _context.Answers.Where(a => a.QuestionCode == questionCode).ToList();
                    _context.Answers.RemoveRange(existingAnswers);

                    // Create new answers
                    for (int i = 0; i < answers.Count; i++)
                    {
                        var answer = new Answer
                        {
                            AnswerContent = answers[i].Trim(),
                            IsTrue = i == correctAnswerIndex,
                            QuestionCode = questionCode,
                            InsertUser = GetCurrentUserId(),
                            InsertTime = DateTime.Now,
                            IsActive = true
                        };
                        _context.Answers.Add(answer);
                    }

                    _context.SaveChanges();
                    transaction.Commit();

                    _logger.LogInformation("Successfully updated question {QuestionCode} with {AnswerCount} answers",
                        questionCode, answers.Count);

                    return Json(new { success = true });
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating question {QuestionCode}", questionCode);
                return Json(new { success = false, message = "Error updating question. Please try again." });
            }
        }

        [HttpGet]
        public JsonResult GetQuestionForEdit(int questionCode)
        {
            try
            {
                var question = _context.Questions
                    .Include(q => q.Answers.Where(a => a.IsActive))
                    .Where(q => q.QuestionCode == questionCode)
                    .Select(q => new
                    {
                        questionCode = q.QuestionCode,
                        questionContent = q.QuestionContent,
                        lessonCode = q.LessonCode,
                        examCode = q.ExamCode,
                        answers = q.Answers
                            .Where(a => a.IsActive)
                            .OrderBy(a => a.AnswerCode)
                            .Select(a => new
                            {
                                answerCode = a.AnswerCode,
                                content = a.AnswerContent,
                                isCorrect = a.IsTrue
                            }).ToList()
                    })
                    .FirstOrDefault();

                if (question == null)
                    return Json(new { success = false, message = "Question not found" });

                return Json(new { success = true, question = question });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading question {QuestionCode} for edit", questionCode);
                return Json(new { success = false, message = "Error loading question" });
            }
        }

        [HttpGet]
        public JsonResult GetChapterStatistics(int chapterCode)
        {
            try
            {
                var rootCode = GetCurrentUserRootCode();
                if (!rootCode.HasValue)
                    return Json(new { success = false, message = "Unable to determine user context" });

                // Get all lessons under this chapter
                var lessonCodes = _context.Lessons
                    .Where(l => l.ChapterCode == chapterCode && l.RootCode == rootCode.Value)
                    .Select(l => l.LessonCode)
                    .ToList();

                // Add the chapter itself
                lessonCodes.Add(chapterCode);

                var totalQuestions = _context.Questions.Count(q => q.LessonCode.HasValue && lessonCodes.Contains(q.LessonCode.Value));
                var totalAnswers = _context.Answers
                    .Join(_context.Questions, a => a.QuestionCode, q => q.QuestionCode, (a, q) => new { a, q })
                    .Count(joined => joined.a.IsActive && joined.q.LessonCode.HasValue && lessonCodes.Contains(joined.q.LessonCode.Value));
                var questionsWithAnswers = _context.Questions
                    .Where(q => q.LessonCode.HasValue && lessonCodes.Contains(q.LessonCode.Value))
                    .Count(q => q.Answers.Any(a => a.IsActive));

                return Json(new
                {
                    success = true,
                    statistics = new
                    {
                        totalQuestions = totalQuestions,
                        totalAnswers = totalAnswers,
                        questionsWithAnswers = questionsWithAnswers,
                        questionsWithoutAnswers = totalQuestions - questionsWithAnswers
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics for chapter {ChapterCode}", chapterCode);
                return Json(new { success = false, message = "Error loading statistics" });
            }
        }

        // =========================
        // ENHANCED SEARCH FUNCTIONALITY
        // =========================

        [HttpGet]
        public JsonResult SearchQuestionsAdvanced(string term, int? chapterCode = null, int? subjectCode = null, int? yearCode = null, bool includeAnswers = true)
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue || string.IsNullOrWhiteSpace(term))
                return Json(new List<object>());

            try
            {
                var query = from q in _context.Questions
                            join l in _context.Lessons on q.LessonCode equals l.LessonCode
                            where q.QuestionContent.Contains(term) && l.RootCode == rootCode.Value
                            select new { q, l };

                if (chapterCode.HasValue)
                {
                    // Include questions from the chapter and its lessons
                    var lessonCodes = _context.Lessons
                        .Where(l => l.ChapterCode == chapterCode.Value && l.RootCode == rootCode.Value)
                        .Select(l => l.LessonCode)
                        .ToList();
                    lessonCodes.Add(chapterCode.Value);

                    query = query.Where(x => x.q.LessonCode.HasValue && lessonCodes.Contains(x.q.LessonCode.Value));
                }

                if (subjectCode.HasValue)
                    query = query.Where(x => x.l.SubjectCode == subjectCode.Value);

                if (yearCode.HasValue)
                    query = query.Where(x => x.l.YearCode == yearCode.Value);

                // Execute the main query first
                var queryResults = query
                    .OrderBy(x => x.q.QuestionCode)
                    .Take(50) // Limit results for performance
                    .ToList();

                // Then process the results with additional queries
                var results = queryResults.Select(x =>
                {
                    var chapterName = x.l.ChapterCode.HasValue
                        ? _context.Lessons.FirstOrDefault(c => c.LessonCode == x.l.ChapterCode.Value)?.LessonName
                        : null;

                    var answers = includeAnswers ?
                        _context.Answers
                            .Where(a => a.QuestionCode == x.q.QuestionCode && a.IsActive)
                            .OrderBy(a => a.AnswerCode)
                            .Select(a => new
                            {
                                answerCode = a.AnswerCode,
                                content = a.AnswerContent,
                                isCorrect = a.IsTrue
                            }).ToList().Cast<object>().ToList()
                        : new List<object>();

                    return new
                    {
                        questionCode = x.q.QuestionCode,
                        questionContent = x.q.QuestionContent,
                        lessonCode = x.q.LessonCode,
                        lessonName = x.l.LessonName,
                        examCode = x.q.ExamCode,
                        chapterCode = x.l.ChapterCode,
                        chapterName = chapterName,
                        answers = answers
                    };
                }).ToList();

                return Json(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching questions with term '{Term}'", term);
                return Json(new List<object>());
            }
        }
        // =========================
        // EXISTING ENDPOINTS (KEPT FOR COMPATIBILITY)
        // =========================

        [HttpGet]
        public JsonResult GetSubjectYearPairsByTeacher(int teacherCode)
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new List<object>());

            var pairs = (from t in _context.Teaches
                         where t.TeacherCode == teacherCode && t.RootCode == rootCode.Value
                         join s in _context.Subjects on t.SubjectCode equals s.SubjectCode
                         join y in _context.Years on t.YearCode equals y.YearCode
                         select new
                         {
                             subjectCode = t.SubjectCode,
                             yearCode = t.YearCode,
                             subjectName = s.SubjectName,
                             yearName = y.YearName
                         }).Distinct().ToList();

            return Json(pairs);
        }

        [HttpGet]
        public JsonResult GetChaptersWithLessonsAndQuestions(
            int page = 1,
            int pageSize = 5,
            int? subjectCode = null,
            int? yearCode = null,
            string lessonPages = null,
            string questionPages = null,
            int lessonsPageSize = 5,
            int questionsPageSize = 5)
        {
            var rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new { chapters = new List<object>(), totalCount = 0 });

            // Parse lessonPages and questionPages maps
            var lessonPagesDict = !string.IsNullOrEmpty(lessonPages)
                ? JsonSerializer.Deserialize<Dictionary<int, int>>(lessonPages)
                : new Dictionary<int, int>();
            var questionPagesDict = !string.IsNullOrEmpty(questionPages)
                ? JsonSerializer.Deserialize<Dictionary<int, int>>(questionPages)
                : new Dictionary<int, int>();

            var query = _context.Lessons
                .Where(l => l.ChapterCode == null && l.RootCode == rootCode.Value);

            if (subjectCode.HasValue)
                query = query.Where(l => l.SubjectCode == subjectCode.Value);
            if (yearCode.HasValue)
                query = query.Where(l => l.YearCode == yearCode.Value);

            int totalCount = query.Count();

            var chapters = query
                .OrderBy(l => l.LessonCode)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(chapter =>
                {
                    int chapterLessonsPage = lessonPagesDict.ContainsKey(chapter.LessonCode) ? lessonPagesDict[chapter.LessonCode] : 1;

                    // Lessons for this chapter
                    var lessonQuery = _context.Lessons
                        .Where(l => l.ChapterCode == chapter.LessonCode && l.RootCode == rootCode.Value);

                    int lessonTotalCount = lessonQuery.Count();

                    var lessons = lessonQuery
                        .OrderBy(l => l.LessonCode)
                        .Skip((chapterLessonsPage - 1) * lessonsPageSize)
                        .Take(lessonsPageSize)
                        .ToList()
                        .Select(l =>
                        {
                            int lessonQuestionsPage = questionPagesDict.ContainsKey(l.LessonCode) ? questionPagesDict[l.LessonCode] : 1;

                            // Questions for this lesson
                            var questionsQuery = _context.Questions
                                .Where(q => q.LessonCode == l.LessonCode);

                            int questionTotalCount = questionsQuery.Count();

                            var questions = questionsQuery
                                .OrderBy(q => q.QuestionCode)
                                .Skip((lessonQuestionsPage - 1) * questionsPageSize)
                                .Take(questionsPageSize)
                                .Select(q => new
                                {
                                    questionCode = q.QuestionCode,
                                    questionContent = q.QuestionContent,
                                    examCode = q.ExamCode,
                                    lessonCode = q.LessonCode
                                }).ToList();

                            return new
                            {
                                lessonName = l.LessonName,
                                lessonCode = l.LessonCode,
                                questions = new
                                {
                                    items = questions,
                                    totalCount = questionTotalCount,
                                    page = lessonQuestionsPage,
                                    pageSize = questionsPageSize
                                }
                            };
                        }).ToList();

                    return new
                    {
                        chapterName = chapter.LessonName,
                        chapterCode = chapter.LessonCode,
                        rootCode = chapter.RootCode,
                        yearCode = chapter.YearCode,
                        eduYearCode = chapter.EduYearCode,
                        teacherCode = chapter.TeacherCode,
                        subjectCode = chapter.SubjectCode,
                        lessons = new
                        {
                            items = lessons,
                            totalCount = lessonTotalCount,
                            page = chapterLessonsPage,
                            pageSize = lessonsPageSize
                        }
                    };
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
                    return Json(new { success = false, message = _localizer["InvalidAnswerData"] });

                int correctCount = IsTrue.Count(x => x);
                if (correctCount > 1)
                    return Json(new { success = false, message = _localizer["OnlyOneCorrectAnswer"] });

                bool alreadyCorrectInDb = _context.Answers.Any(a => a.QuestionCode == QuestionCode && a.IsTrue);
                if (alreadyCorrectInDb && correctCount > 0)
                    return Json(new { success = false, message = _localizer["CorrectAnswerAlreadyExists"] });

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
                    return Json(new { success = false, message = _localizer["AnswerNotFound"] });

                if (IsTrue && !_context.Answers.Where(a => a.QuestionCode == answer.QuestionCode && a.AnswerCode != AnswerCode).All(a => !a.IsTrue))
                {
                    return Json(new { success = false, message = _localizer["OnlyOneCorrectAnswer"] });
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
                    return Json(new { success = false, message = _localizer["AnswerNotFound"] });

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
                    return Json(new { success = false, message = _localizer["QuestionNotFound"] });

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
                    return Json(new { success = false, message = _localizer["QuestionNotFound"] });

                // Also delete all answers for this question
                var answers = _context.Answers.Where(a => a.QuestionCode == QuestionCode).ToList();
                _context.Answers.RemoveRange(answers);

                _context.Questions.Remove(question);
                _context.SaveChanges();

                _logger.LogInformation("Deleted question {QuestionCode} and {AnswerCount} answers", QuestionCode, answers.Count);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting question {QuestionCode}", QuestionCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =========================
        // CHAPTER MANAGEMENT
        // =========================

        [HttpGet]
        public JsonResult GetEduYearsByRoot()
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue)
                return Json(new List<object>());

            var years = (from teach in _context.Teaches
                         join eduyear in _context.EduYears on teach.EduYearCode equals eduyear.EduCode
                         where teach.RootCode == rootCode.Value && eduyear.IsActive
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
            var teacherList = teachers.Select(t => new
            {
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
            var subjectList = subjects.Select(s => new
            {
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
        public JsonResult AddChapter(string LessonName, int? EduYearCode, int? TeacherCode, int SubjectCode, int? YearCode)
        {
            try
            {
                int? userRootCode = GetCurrentUserRootCode();
                if (!userRootCode.HasValue)
                    return Json(new { success = false, message = _localizer["UnableToDetermineRootAssignment"] });

                bool isCenterUser = IsCurrentUserCenter();
                if (!isCenterUser)
                {
                    TeacherCode = _context.Teachers
                        .Where(t => t.RootCode == userRootCode.Value)
                        .Select(t => (int?)t.TeacherCode)
                        .FirstOrDefault();

                    if (!TeacherCode.HasValue)
                        return Json(new { success = false, message = _localizer["MissingRequiredFields"] });
                }

                int? yearCode = YearCode;
                int? eduYearCode = EduYearCode;

                if (isCenterUser && (!yearCode.HasValue && TeacherCode.HasValue && EduYearCode.HasValue))
                {
                    var teach = _context.Teaches.FirstOrDefault(t =>
                        t.TeacherCode == TeacherCode &&
                        t.EduYearCode == EduYearCode &&
                        t.SubjectCode == SubjectCode &&
                        t.RootCode == userRootCode.Value
                    );
                    if (teach != null)
                        yearCode = teach.YearCode;
                    else
                        return Json(new { success = false, message = _localizer["MatchingTeachNotFound"] });
                }

                if (!eduYearCode.HasValue || !TeacherCode.HasValue || !yearCode.HasValue || SubjectCode == 0)
                    return Json(new { success = false, message = _localizer["MissingRequiredFields"] });

                var lesson = new Lesson
                {
                    LessonName = LessonName,
                    RootCode = userRootCode.Value,
                    EduYearCode = eduYearCode.Value,
                    TeacherCode = TeacherCode.Value,
                    SubjectCode = SubjectCode,
                    ChapterCode = null,
                    YearCode = yearCode.Value,
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
        public JsonResult AddLesson(string LessonName, int? RootCode, int? TeacherCode, int? SubjectCode, int EduYearCode, int? ChapterCode, int? YearCode)
        {
            try
            {
                if (!RootCode.HasValue || !TeacherCode.HasValue || !SubjectCode.HasValue)
                    return Json(new { success = false, message = _localizer["MissingRequiredFields"] });

                var lesson = new Lesson
                {
                    LessonName = LessonName,
                    RootCode = RootCode.Value,
                    TeacherCode = TeacherCode.Value,
                    SubjectCode = SubjectCode.Value,
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
        public JsonResult SearchQuestions(string term, int? subjectCode = null, int? yearCode = null)
        {
            int? rootCode = GetCurrentUserRootCode();
            if (!rootCode.HasValue || string.IsNullOrWhiteSpace(term))
                return Json(new List<object>());

            var questions = (from q in _context.Questions
                             join l in _context.Lessons on q.LessonCode equals l.LessonCode
                             where q.QuestionContent.Contains(term) && l.RootCode == rootCode.Value
                             && (!subjectCode.HasValue || l.SubjectCode == subjectCode.Value)
                             && (!yearCode.HasValue || l.YearCode == yearCode.Value)
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

        [HttpGet]
        public JsonResult DebugUserAndChapters()
        {
            try
            {
                var rootCode = GetCurrentUserRootCode();
                var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

                // Get all chapters regardless of root
                var allChapters = _context.Lessons
                    .Where(l => l.ChapterCode == null)
                    .Select(l => new
                    {
                        lessonCode = l.LessonCode,
                        lessonName = l.LessonName,
                        rootCode = l.RootCode,
                        isActive = l.IsActive,
                        subjectCode = l.SubjectCode,
                        yearCode = l.YearCode
                    })
                    .ToList();

                // Get available roots
                var availableRoots = _context.Lessons.Select(l => l.RootCode).Distinct().ToList();

                return Json(new
                {
                    userInfo = new
                    {
                        userName = User.Identity?.Name,
                        extractedRootCode = rootCode,
                        allClaims = allClaims
                    },
                    databaseInfo = new
                    {
                        availableRoots = availableRoots,
                        totalChapters = allChapters.Count,
                        allChapters = allChapters
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

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