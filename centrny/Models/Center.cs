using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Center
{
    public int CenterCode { get; set; }

    public string CenterName { get; set; } = null!;

    public bool IsActive { get; set; }

    public string CenterPhone { get; set; } = null!;

    public string CenterAddress { get; set; } = null!;

    public string? OwnerName { get; set; }

    public int RootCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastInsertUser { get; set; }

    public DateTime? LastInsertTime { get; set; }

    public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastInsertUserNavigation { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
