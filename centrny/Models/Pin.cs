using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Pin
{
    public int PinCode { get; set; }

    public string Watermark { get; set; } = null!;

    public bool Type { get; set; }

    public int Times { get; set; }

    public int RootCode { get; set; }

    public int Status { get; set; }

    public int IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public int? StudentCode { get; set; }

    public virtual ICollection<OnlineAttend> OnlineAttends { get; set; } = new List<OnlineAttend>();

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual Student? StudentCodeNavigation { get; set; }
}
