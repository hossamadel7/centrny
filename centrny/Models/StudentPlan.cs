using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class StudentPlan
{
    public int SubscriptionPlanCode { get; set; }

    public int StudentCode { get; set; }

    public int EduYearCode { get; set; }

    public DateOnly SubDate { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastInsertUser { get; set; }

    public DateTime? LastInsertTime { get; set; }

    public int? Price { get; set; }

    public DateOnly ExpiryDate { get; set; }

    public bool IsExpired { get; set; }

    public virtual EduYear EduYearCodeNavigation { get; set; } = null!;

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastInsertUserNavigation { get; set; }

    public virtual Student StudentCodeNavigation { get; set; } = null!;

    public virtual SubscriptionPlan SubscriptionPlanCodeNavigation { get; set; } = null!;
}
