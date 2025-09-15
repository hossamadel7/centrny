using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class File
{
    public int FileCode { get; set; }

    public string? FileLocation { get; set; }

    public int RootCode { get; set; }

    public int LessonCode { get; set; }

    public int? ExamCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int FileType { get; set; }

    public string? DisplayName { get; set; }

    public int SortOrder { get; set; }

    public int? VideoProvider { get; set; }

    public string? FileExtension { get; set; }

    public long? FileSizeBytes { get; set; }

    public TimeOnly? Duration { get; set; }

    public bool IsActive { get; set; }

    public bool? IsOnlineLesson { get; set; }

    public virtual Exam? ExamCodeNavigation { get; set; }

    public virtual Lesson LessonCodeNavigation { get; set; } = null!;

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
