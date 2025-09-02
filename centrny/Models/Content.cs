using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Content
{
    public int ContentCode { get; set; }

    public int RootCode { get; set; }

    public string? Title { get; set; }

    public string? TitleAr { get; set; }

    public string? WebLayoutH { get; set; }

    public string? WebLayoutHAr { get; set; }

    public string? WebLayoutF { get; set; }

    public string? WebLayoutFAr { get; set; }

    public string? AppLayoutH { get; set; }

    public string? AppLayoutHAr { get; set; }

    public string? AppLayoutF { get; set; }

    public string? AppLayoutFAr { get; set; }

    public string? Home { get; set; }

    public string? HomaAr { get; set; }

    public string? About { get; set; }

    public string? AboutAr { get; set; }

    public string? Teacher { get; set; }

    public string? TeacherAr { get; set; }

    public string? Contact { get; set; }

    public string? ContactAr { get; set; }

    public string? Apply { get; set; }

    public string? ApplyAr { get; set; }

    public string? Gallery { get; set; }

    public string? GallerAr { get; set; }

    public string? Login { get; set; }

    public string? LoginAr { get; set; }

    public string? Courses { get; set; }

    public string? CoursesAr { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
