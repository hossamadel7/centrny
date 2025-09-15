using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Collections.Generic;
using centrny.Models;
using Microsoft.EntityFrameworkCore;
using centrny.Attributes;

namespace centrny.Controllers
{
    public class StudentExamController : Controller
    {
        private readonly CenterContext _context;
        private readonly ILogger<StudentExamController> _logger;

        public StudentExamController(CenterContext context, ILogger<StudentExamController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // --- SESSION HELPERS ---
        private int? GetSessionInt(string key) => HttpContext.Session.GetInt32(key);
        private string GetSessionString(string key) => HttpContext.Session.GetString(key);
        private int GetCurrentUserId() => GetSessionInt("UserCode") ?? 1;

        [HttpGet]
        [Route("StudentExam")]
        [Route("StudentExam/Index")]
        public IActionResult Index(int studentCode, int examCode, string itemKey = null)
        {
            ViewBag.StudentCode = studentCode;
            ViewBag.ExamCode = examCode;
            ViewBag.ItemKey = itemKey ?? "";
            return View();
        }

        /// <summary>
        /// Returns the student's exam start time and duration for the given exam.
        /// Never auto-submits here! Only returns info.
        /// </summary>
        [HttpGet]
        public JsonResult GetStudentExamStartTime(int studentCode, int examCode)
        {
            try
            {
                var studentExam = _context.StudentExams
                    .FirstOrDefault(se => se.StudentCode == studentCode && se.ExamCode == examCode);

                var exam = _context.Exams.FirstOrDefault(e => e.ExamCode == examCode);

                DateTime examStartTime;
                if (studentExam == null)
                {
                    var studentExists = _context.Students.Any(s => s.StudentCode == studentCode);
                    if (!studentExists)
                        return Json(new { error = "Student not found." });

                    if (exam == null)
                        return Json(new { error = "Exam not found." });

                    if (exam.ExamTimer == null)
                        return Json(new { error = "Exam duration not set. Contact admin." });

                    examStartTime = DateTime.UtcNow;
                    studentExam = new StudentExam
                    {
                        StudentCode = studentCode,
                        ExamCode = examCode,
                        ExamTimer = examStartTime,
                        IsActive = false,
                        InsertUser = GetCurrentUserId(),
                        InsertTime = DateTime.Now
                    };
                    _context.StudentExams.Add(studentExam);
                    _context.SaveChanges();
                }
                else
                {
                    if (studentExam.IsActive == true)
                        return Json(new { error = "You have already submitted this exam." });

                    if (!studentExam.ExamTimer.HasValue)
                        return Json(new { error = "Exam timer not set for this exam attempt." });

                    examStartTime = studentExam.ExamTimer.Value;
                }

                exam = _context.Exams.FirstOrDefault(e => e.ExamCode == examCode);
                if (exam == null)
                    return Json(new { error = "Exam not found." });

                if (exam.ExamTimer == null)
                    return Json(new { error = "Exam duration not set. Contact admin." });

                int durationSeconds = (int)exam.ExamTimer.ToTimeSpan().TotalSeconds;
                var elapsed = (DateTime.UtcNow - examStartTime).TotalSeconds;
                var timeLeft = durationSeconds - elapsed;

                if (timeLeft <= 0)
                {
                    return Json(new
                    {
                        error = "Time is up for this exam.",
                        examStartTime = examStartTime.ToString("o"),
                        durationSeconds = durationSeconds,
                        timeExpired = true,
                        timeLeft = 0
                    });
                }

                return Json(new
                {
                    examStartTime = examStartTime.ToString("o"),
                    durationSeconds = durationSeconds,
                    timeLeft = (int)timeLeft
                });
            }
            catch (Exception ex)
            {
                string innerMsg = ex.InnerException != null ? ex.InnerException.Message : "";
                _logger.LogError(ex, "Error in GetStudentExamStartTime");
                return Json(new { error = "Server error while fetching exam timer.", details = ex.Message, inner = innerMsg, stack = ex.StackTrace });
            }
        }

        /// <summary>
        /// API: Get info for a single exam for a student (for attend exam)
        /// </summary>
        [HttpGet]
        public JsonResult GetSingleExam(int studentCode, int examCode)
        {
            var learn = _context.Learns
                .Where(l => l.StudentCode == studentCode && l.IsActive == true)
                .ToList();
            var exam = _context.Exams
                .Include(e => e.SubjectCodeNavigation)
                .Include(e => e.TeacherCodeNavigation)
                .FirstOrDefault(e => e.ExamCode == examCode);

            if (exam == null)
                return Json(new { error = "Exam not found." });

            var match = learn.Any(l =>
                l.EduYearCode == exam.EduYearCode &&
                l.SubjectCode == exam.SubjectCode &&
                l.TeacherCode == exam.TeacherCode &&
                ((exam.BranchCode == null) || (l.BranchCode == exam.BranchCode))
            );
            if (!match)
                return Json(new { error = "You are not allowed to take this exam." });

            var alreadyTaken = _context.StudentExams.Any(se =>
                se.StudentCode == studentCode &&
                se.ExamCode == examCode &&
                se.IsActive == true);

            return Json(new
            {
                examCode = exam.ExamCode,
                examName = exam.ExamName,
                examTimer = exam.ExamTimer,
                examDurationMinutes = (exam.ExamTimer.Hour * 60 + exam.ExamTimer.Minute),
                subjectName = exam.SubjectCodeNavigation?.SubjectName,
                teacherName = exam.TeacherCodeNavigation?.TeacherName,
                alreadyTaken = alreadyTaken,
                isExam = exam.IsExam
            });
        }

        /// <summary>
        /// API: Get questions for a specific exam
        /// </summary>
        [HttpGet]
        public JsonResult GetExamQuestions(int examCode)
        {
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

            return Json(questions);
        }

        /// <summary>
        /// API: Submit exam answers
        /// </summary>
        [HttpPost]
        public JsonResult SubmitExam([FromBody] ExamSubmission submission)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // Logging for debugging
                    _logger.LogInformation("SubmitExam: studentCode={StudentCode}, examCode={ExamCode}", submission.StudentCode, submission.ExamCode);

                    // Try to find existing StudentExam record
                    var studExam = _context.StudentExams
                        .FirstOrDefault(se => se.StudentCode == submission.StudentCode && se.ExamCode == submission.ExamCode);

                    _logger.LogInformation("Before insert, studExam exists? {Exists}", studExam != null);

                    // If not found, create it
                    if (studExam == null)
                    {
                        studExam = new StudentExam
                        {
                            StudentCode = submission.StudentCode,
                            ExamCode = submission.ExamCode,
                            ExamTimer = DateTime.UtcNow,
                            IsActive = false, // Important: default to false
                            InsertUser = GetCurrentUserId(),
                            InsertTime = DateTime.Now
                        };
                        _context.StudentExams.Add(studExam);
                        _context.SaveChanges();

                        // Defensive: re-fetch it
                        studExam = _context.StudentExams
                            .FirstOrDefault(se => se.StudentCode == submission.StudentCode && se.ExamCode == submission.ExamCode);

                        _logger.LogInformation("After insert, studExam exists? {Exists}", studExam != null);
                        if (studExam == null)
                        {
                            _logger.LogError("Failed to create StudentExam row for StudentCode={StudentCode}, ExamCode={ExamCode}", submission.StudentCode, submission.ExamCode);
                            transaction.Rollback();
                            return Json(new { message = "Internal error: could not create a StudentExam record." });
                        }
                    }

                    _logger.LogInformation("studExam.IsActive={IsActive}", studExam.IsActive);

                    // Block double submissions
                    if (studExam.IsActive == true)
                        return Json(new { message = "You have already submitted this exam or assignment." });

                    var exam = _context.Exams.FirstOrDefault(e => e.ExamCode == submission.ExamCode);
                    if (exam == null)
                        return Json(new { message = "Exam/Assignment not found." });

                    bool isAssignment = !exam.IsExam; // Assignments have IsExam == false

                    int totalDegree = 0;
                    int studentDegree = 0;
                    int correctAnswers = 0;

                    if (!isAssignment)
                    {
                        // Exam: enforce timer logic
                        if (exam.ExamTimer == null)
                            return Json(new { message = "Exam duration not set. Contact admin." });

                        if (!studExam.ExamTimer.HasValue)
                            return Json(new { message = "Exam start time is missing." });

                        var durationSeconds = (int)exam.ExamTimer.ToTimeSpan().TotalSeconds;
                        var elapsed = (DateTime.UtcNow - studExam.ExamTimer.Value).TotalSeconds;
                        var timeLeft = durationSeconds - elapsed;
                        if (timeLeft <= 0)
                        {
                            studExam.IsActive = true;
                            _context.SaveChanges();
                            transaction.Commit();
                            return Json(new { message = "Time is up for this exam. Submission not accepted." });
                        }

                        // Exam grading logic
                        var examQuestions = _context.ExamQuestions
                            .Where(eq => eq.ExamCode == submission.ExamCode)
                            .ToList();

                        totalDegree = examQuestions.Sum(eq => eq.QuestionDegree);

                        foreach (var ans in submission.Answers)
                        {
                            if (!int.TryParse(ans.QuestionCode, out int qCode) ||
                                !int.TryParse(ans.AnswerCode, out int aCode))
                            {
                                continue;
                            }

                            var eq = examQuestions.FirstOrDefault(x => x.QuestionCode == qCode);
                            if (eq == null) continue;

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
                    }
                    else
                    {
                        // Assignment: no timer, no grading logic unless you want to add it here
                        // If you want to save answers, you can do so here as well
                    }

                    // Save result and mark as submitted
                    if (!isAssignment)
                    {
                        studExam.ExamDegree = totalDegree;
                        studExam.StudentResult = studentDegree;
                        if (totalDegree > 0)
                        {
                            studExam.StudentPercentage = (double)studentDegree / totalDegree * 100;
                        }
                    }
                    studExam.IsActive = true; // Mark as submitted
                    studExam.InsertUser = GetCurrentUserId();
                    studExam.InsertTime = DateTime.Now;
                    _context.SaveChanges();
                    transaction.Commit();

                    string resultMsg = isAssignment
                        ? "Assignment submitted successfully!"
                        : $"Exam submitted! Score: {studentDegree}/{totalDegree} ({studExam.StudentPercentage:F1}%). Correct Answers: {correctAnswers}";
                    return Json(new { message = resultMsg });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Error in SubmitExam");
                    return Json(new { message = "Error submitting: " + ex.Message });
                }
            }
        }

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