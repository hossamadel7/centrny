using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class RootModule
{
    public int RootCode { get; set; }

    public int ModuleCode { get; set; }

    public string? ModuleName { get; set; }

    public virtual Module ModuleCodeNavigation { get; set; } = null!;

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
