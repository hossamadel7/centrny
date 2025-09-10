using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Income
{
    public int Id { get; set; }

    public int RootCode { get; set; }

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public string? Description { get; set; }

    public DateTime? InsertTime { get; set; }

    public int InsertUserCode { get; set; }

    public virtual User InsertUserCodeNavigation { get; set; } = null!;

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
