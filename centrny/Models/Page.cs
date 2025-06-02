using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Page
{
    public int PageCode { get; set; }

    public string PageName { get; set; } = null!;

    public int PageSort { get; set; }

    public string PagePath { get; set; } = null!;

    public int PageParent { get; set; }

    public bool IsParent { get; set; }

    public int ModuleCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual ICollection<GroupPage> GroupPages { get; set; } = new List<GroupPage>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Module ModuleCodeNavigation { get; set; } = null!;
}
