using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Schedule
{
    public int ScheduleCode { get; set; }

    public string ScheduleName { get; set; } = null!;

    public int? HallCode { get; set; }

    public decimal? ScheduleAmount { get; set; }

    public int? RootCode { get; set; }

    public int? EduYearCode { get; set; }

    public int? CenterCode { get; set; }

    public int? BranchCode { get; set; }

    public int? TeacherCode { get; set; }

    public int? SubjectCode { get; set; }

    public string? DayOfWeek { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int? YearCode { get; set; }

    public int? InsertUser { get; set; }

    public DateTime? InsertTime { get; set; }

    public virtual ICollection<Attend> Attends { get; set; } = new List<Attend>();

    public virtual Branch? BranchCodeNavigation { get; set; }

    public virtual Center? CenterCodeNavigation { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual EduYear? EduYearCodeNavigation { get; set; }

    public virtual Hall? HallCodeNavigation { get; set; }

    public virtual User? InsertUserNavigation { get; set; }

    public virtual ICollection<Learn> Learns { get; set; } = new List<Learn>();

    public virtual Root? RootCodeNavigation { get; set; }

    public virtual Subject? SubjectCodeNavigation { get; set; }

    public virtual Teacher? TeacherCodeNavigation { get; set; }

    public virtual Year? YearCodeNavigation { get; set; }
}
