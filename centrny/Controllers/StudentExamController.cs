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

        // GET: /StudentExam?studentCode=123&examCode=456&itemKey=XYZ
        public IActionResult Index(int studentCode, int examCode, string itemKey = null)
        {
            ViewBag.StudentCode = studentCode;
            ViewBag.ExamCode = examCode;
            ViewBag.ItemKey = itemKey ?? "";
            return View();
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

            // Must have a learn record matching teacher/subject/branch/eduyear for this exam
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
                    var alreadyExists = _context.StudentExams.Any(se =>
                        se.StudentCode == submission.StudentCode &&
                        se.ExamCode == submission.ExamCode &&
                        se.IsActive == true);

                    if (alreadyExists)
                        return Json(new { message = "You have already submitted this exam." });

                    var examQuestions = _context.ExamQuestions
                        .Where(eq => eq.ExamCode == submission.ExamCode)
                        .ToList();

                    int totalDegree = examQuestions.Sum(eq => eq.QuestionDegree);
                    int studentDegree = 0;
                    int correctAnswers = 0;

                    var studExam = new StudentExam
                    {
                        StudentCode = submission.StudentCode,
                        ExamCode = submission.ExamCode,
                        ExamDegree = totalDegree,
                        StudentResult = 0,
                        IsActive = true,
                        InsertUser = int.Parse(User.FindFirst("NameIdentifier")?.Value ?? "1"),
                        InsertTime = DateTime.Now
                    };
                    _context.StudentExams.Add(studExam);
                    _context.SaveChanges();

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

                    studExam.StudentResult = studentDegree;
                    if (totalDegree > 0)
                    {
                        studExam.StudentPercentage = (double)studentDegree / totalDegree * 100;
                    }

                    _context.SaveChanges();
                    transaction.Commit();

                    string resultMsg = $"Exam submitted! Score: {studentDegree}/{totalDegree} ({studExam.StudentPercentage:F1}%). Correct Answers: {correctAnswers}";
                    return Json(new { message = resultMsg });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { message = "Error submitting exam: " + ex.Message });
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