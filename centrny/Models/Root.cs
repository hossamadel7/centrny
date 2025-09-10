using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Root
{
    public int RootCode { get; set; }

    public string RootOwner { get; set; } = null!;

    public string RootName { get; set; } = null!;

    public string? RootDomain { get; set; }

    public string RootPhone { get; set; } = null!;

    public string RootEmail { get; set; } = null!;

    public decimal RootFees { get; set; }

    public string RootAddress { get; set; } = null!;

    public bool IsActive { get; set; }

    public int NoOfCenter { get; set; }

    public int NoOfUser { get; set; }

    public DateTime StartTime { get; set; }

    public int? RootParentCode { get; set; }

    public bool IsCenter { get; set; }

    public bool? IsParent { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public string? RootBodyColor { get; set; }

    public string? RootButtonColor { get; set; }

    public string? RootBodyFont { get; set; }

    public string? RootButtonFontColor { get; set; }

    public string? RootButtonFontColor2 { get; set; }

    public string? RootBackgroundColor { get; set; }

    public virtual ICollection<Attend> Attends { get; set; } = new List<Attend>();

    public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();

    public virtual ICollection<Center> Centers { get; set; } = new List<Center>();

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Content> Contents { get; set; } = new List<Content>();

    public virtual ICollection<EduYear> EduYears { get; set; } = new List<EduYear>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public virtual ICollection<File> Files { get; set; } = new List<File>();

    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    public virtual ICollection<Hall> Halls { get; set; } = new List<Hall>();

    public virtual ICollection<Income> Incomes { get; set; } = new List<Income>();

    public virtual ICollection<Root> InverseRootParentCodeNavigation { get; set; } = new List<Root>();

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    public virtual ICollection<Learn> Learns { get; set; } = new List<Learn>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<Level> Levels { get; set; } = new List<Level>();

    public virtual ICollection<OnlineAttend> OnlineAttends { get; set; } = new List<OnlineAttend>();

    public virtual ICollection<Pin> Pins { get; set; } = new List<Pin>();

    public virtual ICollection<PositionType> PositionTypes { get; set; } = new List<PositionType>();

    public virtual ICollection<RootModule> RootModules { get; set; } = new List<RootModule>();

    public virtual Root? RootParentCodeNavigation { get; set; }

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();

    public virtual ICollection<SubscriptionPlan> SubscriptionPlans { get; set; } = new List<SubscriptionPlan>();

    public virtual ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();

    public virtual ICollection<Teach> Teaches { get; set; } = new List<Teach>();

    public virtual ICollection<WalletCode> WalletCodes { get; set; } = new List<WalletCode>();

    public virtual ICollection<Year> Years { get; set; } = new List<Year>();
}
