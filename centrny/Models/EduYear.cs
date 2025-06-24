using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class EduYear
{
    public int EduCode { get; set; }

    public string EduName { get; set; } = null!;

    public bool IsActive { get; set; }

    public int RootCode { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual ICollection<Learn> Learns { get; set; } = new List<Learn>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual ICollection<StudentPlan> StudentPlans { get; set; } = new List<StudentPlan>();

    public virtual ICollection<StudentSubjectPlan> StudentSubjectPlans { get; set; } = new List<StudentSubjectPlan>();

    public virtual ICollection<Teach> Teaches { get; set; } = new List<Teach>();

    public virtual ICollection<Year> Years { get; set; } = new List<Year>();
}
