using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Teacher
{
    public int TeacherCode { get; set; }

    public string TeacherName { get; set; } = null!;

    public string TeacherPhone { get; set; } = null!;

    public string? TeacherAddress { get; set; }

    public bool IsActive { get; set; }

    public bool IsStaff { get; set; }

    public int RootCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual ICollection<Attend> Attends { get; set; } = new List<Attend>();

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual ICollection<Learn> Learns { get; set; } = new List<Learn>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual ICollection<Teach> Teaches { get; set; } = new List<Teach>();
}
