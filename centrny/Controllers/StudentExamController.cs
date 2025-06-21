using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Collections.Generic;
using centrny.Models;
using Microsoft.EntityFrameworkCore;
using centrny.Attributes;

namespace centrny.Controllers
{
    [RequirePageAccess("StudentExam")]
    public class StudentExamController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<StudentExamController> _logger;

        public StudentExamController(CenterContext context, ILogger<StudentExamController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET: StudentExam - Main view for student exams
        /// </summary>
        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("Loading StudentExam index page for user {Username}", User.Identity?.Name);
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading StudentExam index page for user {Username}", User.Identity?.Name);
                TempData["Error"] = "An error occurred while loading the page.";
                return View();
            }
        }

        /// <summary>
        /// GET: /StudentExam/GetStudentExams?studentCode=21
        /// API endpoint to get exams available for a specific student
        /// </summary>
        [HttpGet]
        public JsonResult GetStudentExams(int studentCode)
        {
            try
            {
                _logger.LogInformation("Getting exams for student {StudentCode} by user {Username}", studentCode, User.Identity?.Name);

                var subjectCodes = _context.Learns
                    .Where(l => l.StudentCode == studentCode && l.IsActive == true)
                    .Select(l => l.SubjectCode)
                    .Distinct()
                    .ToList();

                if (!subjectCodes.Any())
                {
                    _logger.LogWarning("No active subjects found for student {StudentCode}", studentCode);
                    return Json(new List<object>());
                }

                var exams = _context.Exams
                    .Where(e => subjectCodes.Contains(e.SubjectCode))
                    .Select(e => new
                    {
                        examCode = e.ExamCode,
                        examName = e.ExamName,
                        examTimer = e.ExamTimer,
                        // Add duration in minutes
                        examDurationMinutes = e.ExamTimer.Hour * 60 + e.ExamTimer.Minute,
                        subjectCode = e.SubjectCode,
                        teacherCode = e.TeacherCode,
                        eduYearCode = e.EduYearCode,
                        studentExam = _context.StudentExams
                            .Where(se => se.StudentCode == studentCode && se.ExamCode == e.ExamCode && se.IsActive == true)
                            .Select(se => new { se.StudentResult, se.ExamDegree })
                            .FirstOrDefault()
                    })
                    .ToList()
                    .Select(e => new
                    {
                        examCode = e.examCode,
                        examName = e.examName,
                        examTimer = e.examTimer,
                        examDurationMinutes = e.examDurationMinutes,
                        subjectCode = e.subjectCode,
                        teacherCode = e.teacherCode,
                        eduYearCode = e.eduYearCode,
                        alreadyTaken = e.studentExam != null,
                        degree = e.studentExam?.StudentResult ?? 0,
                        maxDegree = e.studentExam?.ExamDegree ?? 0
                    })
                    .ToList();

                _logger.LogInformation("Found {Count} exams for student {StudentCode}", exams.Count, studentCode);
                return Json(exams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exams for student {StudentCode}", studentCode);
                Response.StatusCode = 500;
                return Json(new { message = "Error retrieving exams: " + ex.Message });
            }
        }

        /// <summary>
        /// GET: /StudentExam/GetExamQuestions?examCode=123
        /// API endpoint to get questions for a specific exam
        /// </summary>
        [HttpGet]
        public JsonResult GetExamQuestions(int examCode)
        {
            try
            {
                _logger.LogInformation("Getting questions for exam {ExamCode} by user {Username}", examCode, User.Identity?.Name);

                var questions = _context.ExamQuestions
                    .Where(eq => eq.ExamCode == examCode)
                    .Join(_context.Questions,
                        eq => eq.QuestionCode,
                        q => q.QuestionCode,
                        (eq, q) => new
                        {
                            questionCode = q.QuestionCode,
                            questionText = q.QuestionContent,
                            degree = eq.QuestionDegree,
                            answers = _context.Answers
                                .Where(a => a.QuestionCode == q.QuestionCode)
                                .Select(a => new
                                {
                                    answerCode = a.AnswerCode,
                                    answerText = a.AnswerContent
                                })
                                .ToList()
                        }
                    ).ToList();

                _logger.LogInformation("Found {Count} questions for exam {ExamCode}", questions.Count, examCode);
                return Json(questions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting questions for exam {ExamCode}", examCode);
                Response.StatusCode = 500;
                return Json(new { message = "Error retrieving questions: " + ex.Message });
            }
        }

        /// <summary>
        /// POST: /StudentExam/SubmitExam
        /// API endpoint to submit exam answers
        /// </summary>
        [HttpPost]
        [RequirePageAccess("StudentExam", "insert")]
        public JsonResult SubmitExam([FromBody] ExamSubmission submission)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    _logger.LogInformation("Submitting exam {ExamCode} for student {StudentCode} by user {Username}",
                        submission.ExamCode, submission.StudentCode, User.Identity?.Name);

                    // Prevent duplicate StudentExam records
                    var alreadyExamExists = _context.StudentExams.Any(se =>
                        se.StudentCode == submission.StudentCode &&
                        se.ExamCode == submission.ExamCode &&
                        se.IsActive == true);

                    if (alreadyExamExists)
                    {
                        _logger.LogWarning("Student {StudentCode} attempted to retake exam {ExamCode}",
                            submission.StudentCode, submission.ExamCode);
                        Response.StatusCode = 400;
                        return Json(new { message = "You have already submitted this exam." });
                    }

                    // Check for duplicate answers in submission
                    var duplicateCheck = submission.Answers
                        .GroupBy(a => a.QuestionCode)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicateCheck.Any())
                    {
                        Response.StatusCode = 400;
                        return Json(new { message = "Duplicate answers for questions: " + string.Join(", ", duplicateCheck) });
                    }

                    // Get exam questions and calculate scores
                    var examQuestions = _context.ExamQuestions
                        .Where(eq => eq.ExamCode == submission.ExamCode)
                        .ToList();

                    var questions = _context.Questions
                        .Where(q => examQuestions.Select(eq => eq.QuestionCode).Contains(q.QuestionCode))
                        .ToList();

                    int totalDegree = examQuestions.Sum(eq => eq.QuestionDegree);
                    int studentDegree = 0;
                    int correctAnswers = 0;

                    // Insert StudentExam record
                    var studExam = new StudentExam
                    {
                        StudentCode = submission.StudentCode,
                        ExamCode = submission.ExamCode,
                        ExamDegree = totalDegree,
                        StudentResult = 0, // Will be updated after answer evaluation
                        IsActive = true,
                        InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1"),
                        InsertTime = DateTime.Now
                    };
                    _context.StudentExams.Add(studExam);
                    _context.SaveChanges();

                    // Process answers and update StudentAnswers
                    foreach (var ans in submission.Answers)
                    {
                        if (!int.TryParse(ans.QuestionCode, out int qCode) ||
                            !int.TryParse(ans.AnswerCode, out int aCode))
                        {
                            continue;
                        }

                        var eq = examQuestions.FirstOrDefault(x => x.QuestionCode == qCode);
                        var q = questions.FirstOrDefault(x => x.QuestionCode == qCode);
                        if (eq == null || q == null) continue;

                        var correctAnswer = _context.Answers
                            .FirstOrDefault(a => a.QuestionCode == qCode && a.IsTrue);
                        bool isCorrect = (correctAnswer != null && correctAnswer.AnswerCode == aCode);

                        int questionDegree = eq.QuestionDegree;
                        int studentQDegree = isCorrect ? questionDegree : 0;

                        if (isCorrect)
                        {
                            studentDegree += questionDegree;
                            correctAnswers++;
                        }

                        // Check if StudentAnswer exists (created by trigger)
                        var studentAnswerRow = _context.StudentAnswers.FirstOrDefault(
                            sa => sa.StudentCode == submission.StudentCode
                                && sa.ExamCode == submission.ExamCode
                                && sa.QuestionCode == qCode
                        );

                        if (studentAnswerRow != null)
                        {
                            studentAnswerRow.StudentAnswerCode = aCode;
                            studentAnswerRow.StudentDegree = studentQDegree;
                        }
                        else
                        {
                            // If trigger didn't create it, create manually
                            var newStudentAnswer = new StudentAnswer
                            {
                                StudentCode = submission.StudentCode,
                                ExamCode = submission.ExamCode,
                                QuestionCode = qCode,
                                StudentAnswerCode = aCode,
                                StudentDegree = studentQDegree,
                                RightAnswerCode = correctAnswer?.AnswerCode
                            };
                            _context.StudentAnswers.Add(newStudentAnswer);
                        }
                    }

                    // Update StudentResult and calculate percentage
                    studExam.StudentResult = studentDegree;
                    if (totalDegree > 0)
                    {
                        studExam.StudentPercentage = (double)studentDegree / totalDegree * 100;
                    }

                    _context.SaveChanges();
                    transaction.Commit();

                    _logger.LogInformation("Successfully submitted exam {ExamCode} for student {StudentCode}. Score: {StudentDegree}/{TotalDegree}",
                        submission.ExamCode, submission.StudentCode, studentDegree, totalDegree);

                    string resultMsg = $"Exam submitted! Score: {studentDegree}/{totalDegree} ({studExam.StudentPercentage:F1}%). Correct Answers: {correctAnswers}";
                    return Json(new { message = resultMsg });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Error submitting exam {ExamCode} for student {StudentCode}",
                        submission?.ExamCode, submission?.StudentCode);
                    Response.StatusCode = 500;
                    return Json(new { message = "Error submitting exam: " + ex.Message });
                }
            }
        }

        /// <summary>
        /// GET: /StudentExam/GetStudentExamHistory?studentCode=21
        /// API endpoint to get exam history for a student
        /// </summary>
        [HttpGet]
        public JsonResult GetStudentExamHistory(int studentCode)
        {
            try
            {
                var examHistory = _context.StudentExams
                    .Where(se => se.StudentCode == studentCode && se.IsActive == true)
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.SubjectCodeNavigation)
                    .Include(se => se.ExamCodeNavigation)
                        .ThenInclude(e => e.TeacherCodeNavigation)
                    .Select(se => new
                    {
                        examCode = se.ExamCode,
                        examName = se.ExamCodeNavigation.ExamName,
                        subjectName = se.ExamCodeNavigation.SubjectCodeNavigation.SubjectName,
                        teacherName = se.ExamCodeNavigation.TeacherCodeNavigation.TeacherName,
                        examDegree = se.ExamDegree,
                        studentResult = se.StudentResult,
                        studentPercentage = se.StudentPercentage,
                        submissionDate = se.InsertTime.HasValue ? se.InsertTime.Value.ToString("yyyy-MM-dd HH:mm") : "N/A",
                        passed = se.StudentPercentage >= 50
                    })
                    .OrderByDescending(se => se.submissionDate)
                    .ToList();

                return Json(examHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam history for student {StudentCode}", studentCode);
                Response.StatusCode = 500;
                return Json(new { message = "Error retrieving exam history: " + ex.Message });
            }
        }

        // Data Transfer Objects
        public class ExamSubmission
        {
            public int StudentCode { get; set; }
            public int ExamCode { get; set; }
            public List<AnswerObj> Answers { get; set; } = new List<AnswerObj>();
        }

        public class AnswerObj
        {
            public string QuestionCode { get; set; } = string.Empty;
            public string AnswerCode { get; set; } = string.Empty;
        }
    }
}