using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Content
{
    public int ContentCode { get; set; }

    public int RootCode { get; set; }

    public string? Title { get; set; }

    public string? WebLayoutH { get; set; }

    public string? WebLayoutF { get; set; }

    public string? AppLayoutH { get; set; }

    public string? AppLayoutF { get; set; }

    public string? Home { get; set; }

    public string? About { get; set; }

    public string? Coches { get; set; }

    public string? Contact { get; set; }

    public string? Apply { get; set; }

    public string? Gallery { get; set; }

    public string? Login { get; set; }

    public string? First { get; set; }

    public string? Renew { get; set; }

    public string? Client { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
