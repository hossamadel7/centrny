using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class OnlineAttendLog
{
    public int StudentCode { get; set; }

    public int LessonCode { get; set; }

    public int PinCode { get; set; }

    public int? Views { get; set; }

    public int Status { get; set; }

    public int RootCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public DateTime? TimeLog { get; set; }
}
