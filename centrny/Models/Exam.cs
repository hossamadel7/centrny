using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Exam
{
    public int ExamCode { get; set; }

    public string ExamName { get; set; } = null!;

    public string ExamDegree { get; set; } = null!;

    public string? ExamAverageMark { get; set; }

    public string? ExamSuccessPercent { get; set; }

    public bool IsDone { get; set; }

    public bool IsExam { get; set; }

    public bool IsOnline { get; set; }

    public TimeOnly ExamTimer { get; set; }

    public int EduYearCode { get; set; }

    public int TeacherCode { get; set; }

    public int SubjectCode { get; set; }

    public int? BranchCode { get; set; }

    public int? YearCode { get; set; }

    public int? LessonCode { get; set; }

    public bool? IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InserTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public int? SortOrder { get; set; }

    public virtual Branch? BranchCodeNavigation { get; set; }

    public virtual EduYear EduYearCodeNavigation { get; set; } = null!;

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<File> Files { get; set; } = new List<File>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Lesson? LessonCodeNavigation { get; set; }

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual ICollection<StudentExam> StudentExams { get; set; } = new List<StudentExam>();

    public virtual Subject SubjectCodeNavigation { get; set; } = null!;

    public virtual Teacher TeacherCodeNavigation { get; set; } = null!;

    public virtual Year? YearCodeNavigation { get; set; }
}
