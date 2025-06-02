using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Subject
{
    public int SubjectCode { get; set; }

    public string SubjectName { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public int RootCode { get; set; }

    public int YearCode { get; set; }

    public int? SubjectTotal { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual ICollection<Learn> Learns { get; set; } = new List<Learn>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<PlanSubject> PlanSubjects { get; set; } = new List<PlanSubject>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual ICollection<StudentSubjectPlan> StudentSubjectPlans { get; set; } = new List<StudentSubjectPlan>();

    public virtual ICollection<Teach> Teaches { get; set; } = new List<Teach>();
}
