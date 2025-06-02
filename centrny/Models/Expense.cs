using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Expense
{
    public int ExpensesCode { get; set; }

    public string ExpensesReason { get; set; } = null!;

    public int RootCode { get; set; }

    public decimal ExpensesAmount { get; set; }

    public int? EmployeeCode { get; set; }

    public DateOnly ExpenseTime { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual Employee? EmployeeCodeNavigation { get; set; }

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual User? LastUpdateUserNavigation { get; set; }

    public virtual Root RootCodeNavigation { get; set; } = null!;
}
