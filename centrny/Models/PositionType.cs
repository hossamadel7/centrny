using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class PositionType
{
    public int PositionCode { get; set; }

    public string PositionName { get; set; } = null!;

    public int RootCode { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
