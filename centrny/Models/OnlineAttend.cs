using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class OnlineAttend
{
    public int StudentCode { get; set; }

    public int LessonCode { get; set; }

    public int PinCode { get; set; }

    public int Views { get; set; }

    public bool Status { get; set; }

    public int RootCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public virtual Lesson LessonCodeNavigation { get; set; } = null!;

    public virtual Pin PinCodeNavigation { get; set; } = null!;

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual Student StudentCodeNavigation { get; set; } = null!;
}
