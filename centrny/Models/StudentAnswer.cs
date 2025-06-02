using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class StudentAnswer
{
    public int StudentCode { get; set; }

    public int ExamCode { get; set; }

    public int QuestionCode { get; set; }

    public int? RightAnswerCode { get; set; }

    public int? StudentAnswerCode { get; set; }

    public double? QuestionDegree { get; set; }

    public double? StudentDegree { get; set; }

    public DateTime? InsertTime { get; set; }

    public DateTime? LastUpdateTime { get; set; }
}
