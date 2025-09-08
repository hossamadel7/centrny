using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class File
{
    public int FileCode { get; set; }

    public string FileLocation { get; set; } = null!;

    public int RootCode { get; set; }

    public int LessonCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public virtual Lesson LessonCodeNavigation { get; set; } = null!;

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
