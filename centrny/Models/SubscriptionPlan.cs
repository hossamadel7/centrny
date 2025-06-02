using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class SubscriptionPlan
{
    public int SubPlanCode { get; set; }

    public int Price { get; set; }

    public int TotalCount { get; set; }

    public double ExpiryMonths { get; set; }

    public int RootCode { get; set; }

    public string Description { get; set; } = null!;

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public string SubPlanName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual ICollection<PlanSubject> PlanSubjects { get; set; } = new List<PlanSubject>();

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual ICollection<StudentPlan> StudentPlans { get; set; } = new List<StudentPlan>();

    public virtual ICollection<StudentSubjectPlan> StudentSubjectPlans { get; set; } = new List<StudentSubjectPlan>();
}
