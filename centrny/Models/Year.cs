using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Year
{
    public int YearCode { get; set; }

    public string YearName { get; set; } = null!;

    public int YearSort { get; set; }

    public int LevelCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<EduYear> EduYears { get; set; } = new List<EduYear>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Level LevelCodeNavigation { get; set; } = null!;

    public virtual ICollection<PlanSubject> PlanSubjects { get; set; } = new List<PlanSubject>();

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
