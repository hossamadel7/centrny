$(function () {
    var table = $('#expensesTable').DataTable({
        ajax: {
            url: '/Expenses/GetExpenses',
            dataSrc: 'data'
        },
        columns: [
            { data: 'expensesReason' },
            { data: 'expensesAmount' },
            { data: 'employeeName' },
            { data: 'expenseTime' },
            {
                data: null,
                render: function (data, type, row) {
                    return `
            <button class="btn-table edit editExpenseBtn" data-id="${row.expensesCode}" title="Edit"><i class="bi bi-pencil"></i></button>
            <button class="btn-table delete deleteExpenseBtn" data-id="${row.expensesCode}" title="Delete"><i class="bi bi-trash"></i></button>
        `;
                },
                orderable: false
            }
        ]
    });

    // Open Add Modal
    $('#addExpenseBtn').on('click', function () {
        $('#expenseForm')[0].reset();
        $('#ExpensesCode').val(''); // <--- FIXED: use ExpensesCode not ExpenseCode
        $('#expenseModalLabel').text('Add Expense');
        $('#expenseModal').modal('show');
    });

    // Submit Add/Edit
    $('#expenseForm').on('submit', function (e) {
        e.preventDefault();
        var isEdit = $('#ExpensesCode').val() !== ''; // <--- FIXED: use ExpensesCode
        var url = isEdit ? '/Expenses/EditExpense' : '/Expenses/AddExpense';
        $.ajax({
            url: url,
            type: 'POST',
            data: $(this).serialize(),
            success: function (res) {
                if (res.success) {
                    $('#expenseModal').modal('hide');
                    table.ajax.reload(null, false);
                } else {
                    alert('Operation failed.');
                }
            }
        });
    });

    // Open Edit Modal
    $('#expensesTable').on('click', '.editExpenseBtn', function () {
        var id = $(this).data('id');
        $.get('/Expenses/GetExpense', { id: id }, function (exp) {
            $('#ExpensesCode').val(exp.expensesCode); // <--- FIXED: use ExpensesCode
            $('#ExpensesReason').val(exp.expensesReason);
            $('#ExpensesAmount').val(exp.expensesAmount);
            $('#EmployeeCode').val(exp.employeeCode);
            $('#ExpenseTime').val(exp.expenseTime);
            $('#expenseModalLabel').text('Edit Expense');
            $('#expenseModal').modal('show');
        });
    });

    // Delete
    $('#expensesTable').on('click', '.deleteExpenseBtn', function () {
        if (confirm('Are you sure you want to delete this expense?')) {
            var id = $(this).data('id');
            $.post('/Expenses/DeleteExpense', { id: id }, function (res) {
                if (res.success) {
                    table.ajax.reload(null, false);
                } else {
                    alert('Delete failed.');
                }
            });
        }
    });
});