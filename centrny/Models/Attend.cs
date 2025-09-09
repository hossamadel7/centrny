using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Attend
{
    public int TeacherCode { get; set; }

    public int ClassId { get; set; }

    public int StudentId { get; set; }

    public int? HallId { get; set; }

    public int? ScheduleCode { get; set; }

    public DateTime AttendDate { get; set; }

    public decimal SessionPrice { get; set; }

    public int RootCode { get; set; }

    public int Type { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Hall? Hall { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual Schedule? ScheduleCodeNavigation { get; set; }

    public virtual Student Student { get; set; } = null!;

    public virtual Teacher TeacherCodeNavigation { get; set; } = null!;

    public virtual Lockup TypeNavigation { get; set; } = null!;
}
