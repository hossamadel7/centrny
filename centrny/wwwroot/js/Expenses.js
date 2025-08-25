/**
 * expenses-handler.js
 * Fully aligned with ExpensesController:
 *  Controller returns: { data: [ { expensesCode, expensesReason, expensesAmount, employeeName, expenseTime } ] }
 *
 * Common reasons data might not show:
 * 1. 401/302 (auth redirect) -> HTML instead of JSON (now logged in error callback).
 * 2. Incorrect dataSrc path (fixed & validated).
 * 3. Property name mismatch (now exactly matches controller).
 * 4. DataTables Responsive config used without its JS (now safely feature-detected).
 * 5. JavaScript error before init (wrapped in DOMContentLoaded & try/catch).
 */

(function () {

    function initDataTable() {
        // Safely detect responsive extension
        const hasResponsive = !!($.fn.dataTable && $.fn.dataTable.Responsive);

        const table = $('#expensesTable').DataTable({
            ajax: {
                url: '/Expenses/GetExpenses',
                type: 'GET',
                cache: false,
                dataSrc: function (json) {
                    // Debug logging
                    if (!json) {
                        console.error('[Expenses] Empty / null JSON response.');
                        return [];
                    }
                    if (!Array.isArray(json.data)) {
                        console.warn('[Expenses] Unexpected JSON shape. Expected { data: [...] } got:', json);
                        return [];
                    }
                    console.log(`[Expenses] Loaded ${json.data.length} record(s).`, json.data);
                    return json.data;
                },
                error: function (xhr, status, err) {
                    console.error('[Expenses] AJAX error:', status, err, 'HTTP:', xhr.status, 'Response:', xhr.responseText);
                    alert('Failed to load expenses (HTTP ' + xhr.status + '). Check console for details.');
                }
            },

            processing: true,
            deferRender: true,
            autoWidth: false,
            paging: true,
            pageLength: 10,
            lengthMenu: [5, 10, 25, 50],
            ordering: true,
            order: [[3, 'desc']], // 4th column (expenseTime)
            language: {
                emptyTable: "No expenses found",
                search: "Search:",
                lengthMenu: "Show _MENU_",
                paginate: { previous: "Previous", next: "Next" },
                processing: "Loading..."
            },

            // Only enable if plugin is present
            responsive: hasResponsive ? {
                details: {
                    type: 'inline',
                    target: 'tr',
                    display: $.fn.dataTable.Responsive.display.childRowImmediate
                }
            } : false,

            // Match controller property names EXACTLY
            columns: [
                {
                    data: 'expensesReason',
                    defaultContent: '',
                    className: 'dt-nowrap'
                },
                {
                    data: 'expensesAmount',
                    defaultContent: '',
                    className: 'text-end',
                    render: function (d) {
                        if (d == null || d === '') return '';
                        const num = Number(d);
                        if (isNaN(num)) return d;
                        return num.toLocaleString(undefined, {
                            minimumFractionDigits: 2,
                            maximumFractionDigits: 2
                        });
                    }
                },
                {
                    data: 'employeeName',
                    defaultContent: ''
                },
                {
                    data: 'expenseTime',
                    defaultContent: '',
                    render: function (d) {
                        // Controller already formats yyyy-MM-dd
                        return d || '';
                    }
                },
                {
                    data: null,
                    orderable: false,
                    className: 'text-center',
                    defaultContent: '',
                    render: function (row) {
                        const id = row.expensesCode;
                        return `
                            <button type="button"
                                    class="action-btn action-edit editExpenseBtn"
                                    data-id="${id}" title="Edit">
                                <i class="bi bi-pencil"></i>
                            </button>
                            <button type="button"
                                    class="action-btn action-del deleteExpenseBtn"
                                    data-id="${id}" title="Delete">
                                <i class="bi bi-trash"></i>
                            </button>`;
                    }
                }
            ],

            drawCallback: function () {
                $('#expensesTable').css('width', '100%');
            },

            initComplete: function (settings, json) {
                if (hasResponsive) {
                    console.log('[Expenses] DataTable initialized with Responsive.');
                } else {
                    console.log('[Expenses] DataTable initialized (Responsive not loaded).');
                }
            }
        });

        window.expensesDt = table;
    }

    function openAddModal() {
        const formEl = $('#expenseForm')[0];
        if (formEl) formEl.reset();
        $('#ExpensesCode').val('');
        $('#expenseModalLabel').text('Add Expense');
        new bootstrap.Modal(document.getElementById('expenseModal')).show();
    }

    function submitForm(e) {
        e.preventDefault();
        const isEdit = !!$('#ExpensesCode').val();
        const url = isEdit ? '/Expenses/EditExpense' : '/Expenses/AddExpense';

        $.ajax({
            url: url,
            type: 'POST',
            data: $('#expenseForm').serialize(),
            success: function (res) {
                if (res && res.success) {
                    const el = document.getElementById('expenseModal');
                    const modal = bootstrap.Modal.getInstance(el);
                    if (modal) modal.hide();
                    if (window.expensesDt) window.expensesDt.ajax.reload(null, false);
                } else {
                    alert((res && res.message) || 'Operation failed.');
                }
            },
            error: function (xhr) {
                console.error('[Expenses] Save error:', xhr.status, xhr.responseText);
                alert('Server error while saving (HTTP ' + xhr.status + ').');
            }
        });
    }

    function handleEditClick(e) {
        const id = $(this).data('id');
        if (!id) return;

        $.get('/Expenses/GetExpense', { id: id })
            .done(function (exp) {
                if (!exp) {
                    alert('Expense not found.');
                    return;
                }
                $('#ExpensesCode').val(exp.expensesCode);
                $('#ExpensesReason').val(exp.expensesReason);
                $('#ExpensesAmount').val(exp.expensesAmount);
                $('#EmployeeCode').val(exp.employeeCode);
                $('#ExpenseTime').val(exp.expenseTime); // already yyyy-MM-dd
                $('#expenseModalLabel').text('Edit Expense');
                new bootstrap.Modal(document.getElementById('expenseModal')).show();
            })
            .fail(function (xhr) {
                console.error('[Expenses] GetExpense error:', xhr.status, xhr.responseText);
                alert('Failed to load expense (HTTP ' + xhr.status + ').');
            });
    }

    function handleDeleteClick(e) {
        const id = $(this).data('id');
        if (!id) return;
        if (!confirm('Are you sure you want to delete this expense?')) return;

        $.post('/Expenses/DeleteExpense', { id: id })
            .done(function (res) {
                if (res && res.success) {
                    if (window.expensesDt) window.expensesDt.ajax.reload(null, false);
                } else {
                    alert((res && res.message) || 'Delete failed.');
                }
            })
            .fail(function (xhr) {
                console.error('[Expenses] Delete error:', xhr.status, xhr.responseText);
                alert('Server error while deleting (HTTP ' + xhr.status + ').');
            });
    }

    function bindEvents() {
        $('#addExpenseBtn').on('click', openAddModal);
        $('#expenseForm').on('submit', submitForm);
        $('#expensesTable').on('click', '.editExpenseBtn', handleEditClick);
        $('#expensesTable').on('click', '.deleteExpenseBtn', handleDeleteClick);

        // Adjust column widths on resize
        $(window).on('resize', function () {
            if (window.expensesDt) window.expensesDt.columns.adjust();
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        try {
            if (!$('#expensesTable').length) {
                console.warn('[Expenses] #expensesTable not found in DOM.');
                return;
            }
            initDataTable();
            bindEvents();
        } catch (err) {
            console.error('[Expenses] Initialization error:', err);
            alert('Failed to initialize expenses table. See console.');
        }
    });

})();