using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Employee
{
    public int EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string EmployeePhone { get; set; } = null!;

    public string EmployeeEmail { get; set; } = null!;

    public DateOnly EmployeeStartDate { get; set; }

    public decimal EmployeeSalary { get; set; }

    public bool IsActive { get; set; }

    public int? UserCode { get; set; }

    public int RootCode { get; set; }

    public int? BranchCode { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdatTime { get; set; }

    public virtual Branch? BranchCodeNavigation { get; set; }

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;

    public virtual User? UserCodeNavigation { get; set; }
}
