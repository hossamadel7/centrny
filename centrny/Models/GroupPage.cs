using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class GroupPage
{
    public int GroupCode { get; set; }

    public int PageCode { get; set; }

    public bool InsertFlag { get; set; }

    public bool UpdateFlag { get; set; }

    public bool DeleteFlag { get; set; }

    public virtual Group GroupCodeNavigation { get; set; } = null!;

    public virtual Page PageCodeNavigation { get; set; } = null!;
}
