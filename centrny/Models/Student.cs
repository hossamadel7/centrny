using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Student
{
    public int StudentCode { get; set; }

    public string StudentName { get; set; } = null!;

    public string StudentPhone { get; set; } = null!;

    public string StudentFatherPhone { get; set; } = null!;

    public string? StudentMotherPhone { get; set; }

    public string? StudentFatherJob { get; set; }

    public string? StudentMotherJob { get; set; }

    public int RootCode { get; set; }

    public int? YearCode { get; set; }

    public bool IsActive { get; set; }

    public DateOnly SubscribtionTime { get; set; }

    public DateOnly StudentBirthdate { get; set; }

    public bool? StudentGender { get; set; }

    public int BranchCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastInsertUser { get; set; }

    public DateTime? LastInsertTime { get; set; }

    public string? StudentUsername { get; set; }

    public string? StudentPassword { get; set; }

    public virtual ICollection<Attend> Attends { get; set; } = new List<Attend>();

    public virtual Branch BranchCodeNavigation { get; set; } = null!;

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    public virtual User? LastInsertUserNavigation { get; set; }

    public virtual ICollection<Learn> Learns { get; set; } = new List<Learn>();

    public virtual ICollection<OnlineAttend> OnlineAttends { get; set; } = new List<OnlineAttend>();

    public virtual ICollection<Pin> Pins { get; set; } = new List<Pin>();

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual ICollection<StudentExam> StudentExams { get; set; } = new List<StudentExam>();

    public virtual ICollection<StudentPlan> StudentPlans { get; set; } = new List<StudentPlan>();

    public virtual ICollection<StudentSubjectPlan> StudentSubjectPlans { get; set; } = new List<StudentSubjectPlan>();

    public virtual Year? YearCodeNavigation { get; set; }
}
