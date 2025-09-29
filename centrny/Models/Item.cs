using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Item
{
    public int ItemCode { get; set; }

    public int? StudentCode { get; set; }

    public int ItemTypeKey { get; set; }

    public string? ItemKey { get; set; }

    public int RootCode { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public int? EduYear { get; set; }

    public int? SerialNumber { get; set; }

    public virtual EduYear? EduYearNavigation { get; set; }

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual ItemType ItemTypeKeyNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual Student? StudentCodeNavigation { get; set; }
}
