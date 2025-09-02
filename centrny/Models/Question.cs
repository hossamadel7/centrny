using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Question
{
    public int QuestionCode { get; set; }

    public string QuestionContent { get; set; } = null!;

    public string? QuestionImage { get; set; }

    public int? ExamCode { get; set; }

    public int? LessonCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual Exam? ExamCodeNavigation { get; set; }

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Lesson? LessonCodeNavigation { get; set; }
}
