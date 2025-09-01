using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Lesson
{
    public int LessonCode { get; set; }

    public string LessonName { get; set; } = null!;

    public int RootCode { get; set; }

    public int TeacherCode { get; set; }

    public int SubjectCode { get; set; }

    public int EduYearCode { get; set; }

    public int? ChapterCode { get; set; }

    public int? YearCode { get; set; }

    public int? LessonWatchedTimes { get; set; }

    public int? LessonExpireDays { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdatTime { get; set; }

    public virtual Lesson? ChapterCodeNavigation { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual EduYear EduYearCodeNavigation { get; set; } = null!;

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual ICollection<File> Files { get; set; } = new List<File>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual ICollection<Lesson> InverseChapterCodeNavigation { get; set; } = new List<Lesson>();

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual ICollection<OnlineAttend> OnlineAttends { get; set; } = new List<OnlineAttend>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual Subject SubjectCodeNavigation { get; set; } = null!;

    public virtual Teacher TeacherCodeNavigation { get; set; } = null!;

    public virtual Year? YearCodeNavigation { get; set; }
}
