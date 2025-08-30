using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class StudentExam
{
    public int StudentCode { get; set; }

    public int ExamCode { get; set; }

    public int? PinCode { get; set; }

    public int? ExamDegree { get; set; }

    public double? StudentResult { get; set; }

    public double? StudentPercentage { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? ExamTimer { get; set; }

    public int? InsertUser { get; set; }

    public DateTime? InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual Exam ExamCodeNavigation { get; set; } = null!;

    public virtual Student StudentCodeNavigation { get; set; } = null!;
}
