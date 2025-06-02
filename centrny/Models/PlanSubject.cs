using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class PlanSubject
{
    public int PlanCode { get; set; }

    public int SubjectCode { get; set; }

    public int Count { get; set; }

    public int YearCode { get; set; }

    public int SubscribtionPlanCode { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastInsertUser { get; set; }

    public DateTime? LastInsertTime { get; set; }

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastInsertUserNavigation { get; set; }

    public virtual Subject SubjectCodeNavigation { get; set; } = null!;

    public virtual SubscriptionPlan SubscribtionPlanCodeNavigation { get; set; } = null!;

    public virtual Year YearCodeNavigation { get; set; } = null!;
}
