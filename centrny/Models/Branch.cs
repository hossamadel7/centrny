using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Branch
{
    public int BranchCode { get; set; }

    public string BranchName { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public DateOnly StartTime { get; set; }

    public int CenterCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public bool IsActive { get; set; }

    public int RootCode { get; set; }

    public virtual Center CenterCodeNavigation { get; set; } = null!;

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual ICollection<Hall> Halls { get; set; } = new List<Hall>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual ICollection<Learn> Learns { get; set; } = new List<Learn>();

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();

    public virtual ICollection<Teach> Teaches { get; set; } = new List<Teach>();
}
