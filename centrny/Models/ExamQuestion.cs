using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class ExamQuestion
{
    public int ExamCode { get; set; }

    public int QuestionCode { get; set; }

    public int QuestionDegree { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual Exam ExamCodeNavigation { get; set; } = null!;

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Question QuestionCodeNavigation { get; set; } = null!;
}
