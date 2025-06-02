using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class WalletExam
{
    public int WalletExamCode { get; set; }

    public int Amount { get; set; }

    public int Count { get; set; }

    public int OriginalCount { get; set; }

    public DateOnly ExpireDate { get; set; }

    public DateOnly DateStart { get; set; }

    public bool IsActive { get; set; }

    public int RootCode { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
