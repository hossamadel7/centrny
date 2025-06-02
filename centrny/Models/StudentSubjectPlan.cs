using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class StudentSubjectPlan
{
    public int StudentCode { get; set; }

    public int SubjectCode { get; set; }

    public int PlanCode { get; set; }

    public int EduYearCode { get; set; }

    public DateOnly SubDate { get; set; }

    public int ClassesCount { get; set; }

    public int Remaining { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastInsertUser { get; set; }

    public DateTime? LastInsertTime { get; set; }

    public decimal? ClassPrice { get; set; }

    public virtual EduYear EduYearCodeNavigation { get; set; } = null!;

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastInsertUserNavigation { get; set; }

    public virtual SubscriptionPlan PlanCodeNavigation { get; set; } = null!;

    public virtual Student StudentCodeNavigation { get; set; } = null!;

    public virtual Subject SubjectCodeNavigation { get; set; } = null!;
}
