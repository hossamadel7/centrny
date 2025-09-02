using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Answer
{
    public int AnswerCode { get; set; }

    public string AnswerContent { get; set; } = null!;

    public string? AnswerImage { get; set; }

    public bool IsTrue { get; set; }

    public int QuestionCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public bool IsActive { get; set; }

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Question QuestionCodeNavigation { get; set; } = null!;
}
