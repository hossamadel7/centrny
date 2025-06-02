using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Group
{
    public int GroupCode { get; set; }

    public string GroupName { get; set; } = null!;

    public string? GroupDesc { get; set; }

    public int RootCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual ICollection<GroupPage> GroupPages { get; set; } = new List<GroupPage>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
