using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace centrny.Models
{
    #region Financial Reports

    public class FinancialSummaryReport
    {
        public List<FinancialSummaryDto> Classes { get; set; } = new List<FinancialSummaryDto>();
        public decimal TotalRevenue { get; set; }
        public decimal TotalTeacherPayments { get; set; }
        public decimal TotalCenterRevenue { get; set; }
        public int TotalStudents { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? BranchCode { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class FinancialSummaryDto
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; }
        public DateOnly? ClassDate { get; set; }
        public string BranchName { get; set; }
        public string SubjectName { get; set; }
        public string TeacherName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TeacherAmount { get; set; }
        public decimal CenterAmount { get; set; }
        public int NoOfStudents { get; set; }
    }

    #endregion

    public class ClassStudentsViewModel
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; }
        public string ClassDate { get; set; }
        public string ClassStartTime { get; set; }
        public string ClassEndTime { get; set; }
        public string TeacherName { get; set; }
        public string SubjectName { get; set; }
        public string BranchName { get; set; }
        public string HallName { get; set; }
        public int EnrolledCount { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public decimal AttendanceRate { get; set; }

        // ADD THESE FINANCIAL PROPERTIES
        public decimal? TotalAmount { get; set; }
        public decimal? TeacherAmount { get; set; }
        public decimal? CenterAmount { get; set; }

        public List<ClassStudentDto> Students { get; set; } = new List<ClassStudentDto>();
    }
    public class ClassStudentDto
    {
        public int StudentCode { get; set; }
        public string StudentName { get; set; }
        public string StudentPhone { get; set; }
        public string StudentParentPhone { get; set; }
        public bool IsPresent { get; set; }
    }

    #region Student Reports

    public class StudentEnrollmentReport
    {
        public List<StudentEnrollmentDto> Students { get; set; } = new List<StudentEnrollmentDto>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int? BranchCode { get; set; }
        public int? SubjectCode { get; set; }
        public int? YearCode { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class StudentEnrollmentDto
    {
        public int StudentCode { get; set; }
        public string StudentName { get; set; }
        public string StudentPhone { get; set; }
        public string StudentParentPhone { get; set; }
        public string BranchName { get; set; }
        public string YearName { get; set; }
        public DateTime? SubscriptionTime { get; set; } // Changed from DateOnly to DateTime?
        public int EnrolledSubjects { get; set; }
        public string SubjectNames { get; set; }
    }
    public class StudentPerformanceReport
    {
        public List<StudentPerformanceDto> Students { get; set; } = new List<StudentPerformanceDto>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int? SubjectCode { get; set; }
        public int? YearCode { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class StudentPerformanceDto
    {
        public int StudentCode { get; set; }
        public string StudentName { get; set; }
        public string StudentPhone { get; set; }
        public string StudentParentPhone { get; set; }
        public string SubjectName { get; set; }
        public string YearName { get; set; }
        public int TotalClasses { get; set; }
        public int AttendedClasses { get; set; }
        public decimal AttendancePercentage { get; set; }
    }

    #endregion

    #region Teacher Reports

    public class TeacherPerformanceReport
    {
        public List<TeacherPerformanceDto> Classes { get; set; } = new List<TeacherPerformanceDto>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int? TeacherCode { get; set; }
        public int? SubjectCode { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class TeacherPerformanceDto
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; }
        public DateOnly? ClassDate { get; set; }
        public string TeacherName { get; set; }
        public string SubjectName { get; set; }
        public string BranchName { get; set; }
        public int ScheduledStudents { get; set; }
        public int AttendedStudents { get; set; }
        public decimal AttendanceRate { get; set; }
        public decimal TeacherAmount { get; set; }
    }

    public class MyClassesReport
    {
        public List<MyClassDto> Classes { get; set; } = new List<MyClassDto>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SubjectCode { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class MyClassDto
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; }
        public DateOnly? ClassDate { get; set; }
        public TimeOnly? ClassStartTime { get; set; }
        public TimeOnly? ClassEndTime { get; set; }
        public string SubjectName { get; set; }
        public string BranchName { get; set; }
        public string HallName { get; set; }
        public int ScheduledStudents { get; set; }
        public int AttendedStudents { get; set; }
        public decimal TeacherAmount { get; set; }
    }

    public class TeacherEarningsReport
    {
        public List<TeacherEarningsDto> Earnings { get; set; } = new List<TeacherEarningsDto>();
        public decimal TotalEarnings { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SubjectCode { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class TeacherEarningsDto
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; }
        public DateOnly? ClassDate { get; set; }
        public string SubjectName { get; set; }
        public string BranchName { get; set; }
        public int StudentCount { get; set; }
        public decimal TeacherAmount { get; set; }
        public decimal TeacherSubAmount { get; set; }
        public decimal TotalEarning { get; set; }
    }

    #endregion

    #region Exam Reports

    public class ExamPerformanceReport
    {
        public List<ExamPerformanceDto> Exams { get; set; } = new List<ExamPerformanceDto>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int? BranchCode { get; set; }
        public int? SubjectCode { get; set; }
        public int? TeacherCode { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class ExamPerformanceDto
    {
        public int ExamCode { get; set; }
        public string ExamName { get; set; }
        public DateTime? ExamDate { get; set; } // Changed from DateTime to DateTime?
        public string BranchName { get; set; }
        public string SubjectName { get; set; }
        public string TeacherName { get; set; }
        public int TotalStudents { get; set; }
        public double AverageScore { get; set; }
        public int PassingStudents { get; set; }
        public decimal PassingRate { get; set; }
    }
    #endregion

    #region Attendance Reports

    public class ClassAttendanceReport
    {
        public List<ClassAttendanceDetailDto> AttendanceDetails { get; set; } = new List<ClassAttendanceDetailDto>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int? TeacherCode { get; set; }
        public int? SubjectCode { get; set; }
        public DateTime? ClassDate { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class ClassAttendanceDetailDto
    {
        public int ClassCode { get; set; }
        public string ClassName { get; set; }
        public DateOnly? ClassDate { get; set; }
        public TimeOnly? ClassStartTime { get; set; }
        public TimeOnly? ClassEndTime { get; set; }
        public string TeacherName { get; set; }
        public string SubjectName { get; set; }
        public string BranchName { get; set; }
        public string HallName { get; set; }
        public int EnrolledCount { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public decimal AttendanceRate { get; set; }
        public List<StudentAttendanceDto> Students { get; set; } = new List<StudentAttendanceDto>();
    }

    public class StudentAttendanceDto
    {
        public int StudentCode { get; set; }
        public string StudentName { get; set; }
        public string StudentPhone { get; set; }
        public string StudentParentPhone { get; set; }
        public bool IsEnrolled { get; set; }
        public bool IsPresent { get; set; }
        public string AttendanceStatus => IsPresent ? "Present" : "Absent";
        public string StatusClass => IsPresent ? "text-success" : "text-danger";
    }

    #endregion

    #region Dashboard Summary

    public class ReportDashboardSummary
    {
        public int TotalStudents { get; set; }
        public int ActiveClasses { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal OverallAttendanceRate { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalBranches { get; set; }
        public int TotalExams { get; set; }
        public decimal AverageExamScore { get; set; }
    }

    public class AttendanceExportDto
    {
        public string ClassName { get; set; }
        public string ClassDate { get; set; }
        public string ClassTime { get; set; }
        public string TeacherName { get; set; }
        public string SubjectName { get; set; }
        public string BranchName { get; set; }
        public string HallName { get; set; }
        public string StudentName { get; set; }
        public string StudentPhone { get; set; }
        public string StudentParentPhone { get; set; }
        public string AttendanceStatus { get; set; }
        public bool IsPresent { get; set; }
    }


    #endregion
}