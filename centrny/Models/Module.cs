using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Module
{
    public int ModuleCode { get; set; }

    public string ModuleName { get; set; } = null!;

    public virtual ICollection<Page> Pages { get; set; } = new List<Page>();

    public virtual ICollection<RootModule> RootModules { get; set; } = new List<RootModule>();
}
