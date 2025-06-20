using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Learn
{
    public int StudentCode { get; set; }

    public int SubjectCode { get; set; }

    public int TeacherCode { get; set; }

    public int ScheduleCode { get; set; }

    public int EduYearCode { get; set; }

    public int BranchCode { get; set; }

    public int RootCode { get; set; }

    public int? YearCode { get; set; }

    public bool IsOnline { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public int? StudentFee { get; set; }

    public virtual Branch BranchCodeNavigation { get; set; } = null!;

    public virtual EduYear EduYearCodeNavigation { get; set; } = null!;

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual Schedule ScheduleCodeNavigation { get; set; } = null!;

    public virtual Student StudentCodeNavigation { get; set; } = null!;

    public virtual Subject SubjectCodeNavigation { get; set; } = null!;

    public virtual Teacher TeacherCodeNavigation { get; set; } = null!;

    public virtual Year? YearCode1 { get; set; }

    public virtual Lesson? YearCodeNavigation { get; set; }
}
