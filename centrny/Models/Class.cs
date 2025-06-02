using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Class
{
    public int ClassCode { get; set; }

    public string ClassName { get; set; } = null!;

    public int NoOfStudents { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal? TeacherAmount { get; set; }

    public decimal? CenterAmount { get; set; }

    public int RootCode { get; set; }

    public int TeacherCode { get; set; }

    public int SubjectCode { get; set; }

    public int BranchCode { get; set; }

    public int ScheduleCode { get; set; }

    public int EduYearCode { get; set; }

    public int HallCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public decimal TeacherSubAmount { get; set; }

    public int? YearCode { get; set; }

    public virtual ICollection<Attend> Attends { get; set; } = new List<Attend>();

    public virtual Branch BranchCodeNavigation { get; set; } = null!;

    public virtual EduYear EduYearCodeNavigation { get; set; } = null!;

    public virtual Hall HallCodeNavigation { get; set; } = null!;

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual Schedule ScheduleCodeNavigation { get; set; } = null!;

    public virtual Subject SubjectCodeNavigation { get; set; } = null!;

    public virtual Teacher TeacherCodeNavigation { get; set; } = null!;

    public virtual Year? YearCodeNavigation { get; set; }
}
