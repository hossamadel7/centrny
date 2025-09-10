(function ($) {
    "use strict";
    if (!$) {
        console.error("jQuery not loaded before income.js");
        return;
    }

    /* ================== CONFIG ================== */
    const API = {
        list: '/Income/GetIncomes',
        get: id => `/Income/GetIncome/${id}`,
        add: '/Income/AddIncome',
        edit: '/Income/EditIncome',
        delete: id => `/Income/DeleteIncome/${id}`
    };

    // If you use Anti-Forgery tokens (uncomment and ensure the hidden input exists in layout)
    // const antiForgeryToken = $('input[name="__RequestVerificationToken"]').val();

    const UI = {
        tableSelector: '#incomeTable',
        totalFooter: '#totalAmountFooter',
        totalInline: '#inlineTotalAmount',
        rowCountInline: '#inlineRowCount',
        addBtn: '#btnAddIncome',
        refreshBtn: '#btnRefreshIncome'
    };

    let incomeTable = null;
    let requestLock = false; // Prevent double submit

    /* ================== INIT ================== */
    $(document).ready(function () {
        initDataTable();
        bindEvents();
    });

    /* ================== DATATABLE ================== */
    function initDataTable() {
        incomeTable = $(UI.tableSelector).DataTable({
            ajax: {
                url: API.list,
                type: 'GET',
                dataSrc: function (json) {
                    if (!json || !Array.isArray(json.data)) {
                        return [];
                    }
                    return json.data;
                },
                error: function (xhr) {
                    showAjaxError('Failed to load income list', xhr);
                }
            },
            columns: [
                { data: 'id', width: '6%' },
                {
                    data: 'amount',
                    width: '10%',
                    render: d => safeAmount(d)
                },
                {
                    data: 'paymentDate',
                    width: '12%',
                    render: d => formatDateDisplay(d)
                },
                {
                    data: 'description',
                    width: '25%',
                    render: d => d ? escapeHtml(d) : ''
                },
                { data: 'insertTime', width: '16%' },
                { data: 'insertUserCode', width: '8%' },
                {
                    data: null,
                    width: '15%',
                    orderable: false,
                    render: row => actionButtons(row)
                }
            ],
            order: [[0, 'desc']],
            responsive: true,
            scrollX: true,
            language: {
                emptyTable: 'No income records found.',
                search: 'Search:',
                lengthMenu: 'Show _MENU_',
                info: 'Showing _START_ to _END_ of _TOTAL_',
                infoEmpty: 'No entries',
                paginate: { previous: '«', next: '»' }
            },
            drawCallback: updateTotals
        });

        // DataTables global error (covers JSON parse errors etc.)
        $(UI.tableSelector).on('error.dt', function (e, settings, techNote, message) {
            Swal.fire({
                icon: 'error',
                title: 'Table Error',
                html: `<div style="text-align:left;font-size:.85rem;">${escapeHtml(message)}</div>`,
                footer: `<button type="button" class="swal2-confirm swal2-styled" id="retryLoadTable">Retry</button>`
            });
        });

        // Retry button handler (after failure)
        $(document).on('click', '#retryLoadTable', function () {
            Swal.close();
            reloadTable(false, true);
        });
    }

    function actionButtons(row) {
        return `
            <div class="table-actions">
                <button class="btn-action edit" data-id="${row.id}" title="Edit"><i class="fas fa-edit"></i><span>Edit</span></button>
                <button class="btn-action delete" data-id="${row.id}" title="Delete"><i class="fas fa-trash"></i><span>Delete</span></button>
            </div>`;
    }

    function updateTotals() {
        if (!incomeTable) return;
        const data = incomeTable.column(1, { search: 'applied' }).data();
        let total = 0;
        data.each(v => total += parseFloat(v || 0));
        $(UI.totalFooter).text(total.toFixed(2));
        $(UI.totalInline).text(total.toFixed(2));
        $(UI.rowCountInline).text(data.length);
    }

    function reloadTable(preservePage = false, force = false) {
        if (!incomeTable) return;
        if (force) {
            incomeTable.ajax.reload(updateTotals, preservePage);
        } else {
            incomeTable.ajax.reload(updateTotals, preservePage);
        }
    }

    /* ================== EVENTS ================== */
    function bindEvents() {
        $(document).on('click', UI.addBtn, () =>
            openIncomeForm({ title: 'Add Income', confirmText: 'Save', isEdit: false })
        );
        $(document).on('click', UI.refreshBtn, () => reloadTable());

        $(UI.tableSelector).on('click', '.btn-action.edit', function () {
            const id = $(this).data('id');
            loadIncomeForEdit(id);
        });

        $(UI.tableSelector).on('click', '.btn-action.delete', function () {
            const id = $(this).data('id');
            confirmDelete(id);
        });
    }

    /* ================== CRUD ================== */
    function loadIncomeForEdit(id) {
        Swal.fire({ title: 'Loading...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });
        $.get(API.get(id))
            .done(data => {
                Swal.close();
                if (!data) {
                    Swal.fire({ icon: 'warning', title: 'Not Found', text: 'Income record not found' });
                    return;
                }
                openIncomeForm({
                    title: 'Edit Income',
                    confirmText: 'Update',
                    isEdit: true,
                    data: {
                        id: data.id,
                        amount: data.amount,
                        paymentDate: data.paymentDate,
                        description: data.description
                    }
                });
            })
            .fail(xhr => showAjaxError('Failed to load income', xhr));
    }

    function confirmDelete(id) {
        Swal.fire({
            title: 'Delete Income?',
            text: 'This action cannot be undone.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, delete',
            cancelButtonText: 'Cancel',
            reverseButtons: true
        }).then(res => {
            if (res.isConfirmed) {
                secureAjax({
                    url: API.delete(id),
                    method: 'DELETE',
                    loadingMsg: 'Deleting...',
                    success: resp => {
                        Swal.fire({
                            icon: 'success',
                            title: 'Deleted',
                            text: resp?.message || 'Income deleted',
                            timer: 1200,
                            showConfirmButton: false
                        });
                        reloadTable();
                    }
                });
            }
        });
    }

    /* ================== FORM ================== */
    function openIncomeForm({ title, confirmText, isEdit = false, data = {} }) {
        const today = new Date().toISOString().split('T')[0];

        Swal.fire({
            title,
            html: buildFormHtml({
                id: data.id || '',
                amount: data.amount || '',
                paymentDate: data.paymentDate || today,
                description: data.description || ''
            }),
            showCancelButton: true,
            confirmButtonText: confirmText,
            focusConfirm: false,
            width: 640,
            allowOutsideClick: false,
            didOpen: () => $('#swal-input-amount').trigger('focus'),
            preConfirm: () => {
                const vals = extractFormValues();
                const { valid, errors } = validate(vals);
                if (!valid) {
                    showErrors(errors);
                    return false;
                }
                return vals;
            }
        }).then(res => {
            if (res.isConfirmed && res.value) {
                isEdit ? submitEdit(res.value) : submitAdd(res.value);
            }
        });
    }

    function buildFormHtml(v) {
        return `
            <div class="swal-form-group">
                <label for="swal-input-amount">Amount <span style="color:#d63031;">*</span></label>
                <input type="number" step="0.01" min="0.01" id="swal-input-amount" value="${v.amount}">
                <div class="field-error" data-field="Amount"></div>
            </div>
            <div class="swal-form-group">
                <label for="swal-input-date">Payment Date <span style="color:#d63031;">*</span></label>
                <input type="date" id="swal-input-date" value="${v.paymentDate}">
                <div class="field-error" data-field="PaymentDate"></div>
            </div>
            <div class="swal-form-group">
                <label for="swal-input-desc">Description</label>
                <textarea id="swal-input-desc" rows="3" maxlength="255">${v.description}</textarea>
                <div class="field-error" data-field="Description"></div>
            </div>
            <input type="hidden" id="swal-input-id" value="${v.id}">
        `;
    }

    function extractFormValues() {
        return {
            Id: $('#swal-input-id').val(),
            Amount: $('#swal-input-amount').val(),
            PaymentDate: $('#swal-input-date').val(),
            Description: $('#swal-input-desc').val()
        };
    }

    function validate(v) {
        const errors = {};
        if (!v.Amount || isNaN(parseFloat(v.Amount)) || parseFloat(v.Amount) <= 0)
            errors.Amount = 'Amount must be greater than 0';
        if (!v.PaymentDate)
            errors.PaymentDate = 'Payment Date is required';
        return { valid: Object.keys(errors).length === 0, errors };
    }

    function showErrors(errors) {
        $('.field-error').text('');
        Object.entries(errors).forEach(([field, msg]) => {
            $(`.field-error[data-field="${field}"]`).text(msg);
        });
        Swal.showValidationMessage('Please correct the highlighted errors');
    }

    function submitAdd(v) {
        secureAjax({
            url: API.add,
            method: 'POST',
            payload: {
                Amount: parseFloat(v.Amount),
                PaymentDate: v.PaymentDate,
                Description: v.Description
            },
            loadingMsg: 'Saving...',
            success: resp => {
                Swal.fire({
                    icon: 'success',
                    title: 'Added!',
                    text: resp?.message || 'Income added successfully',
                    timer: 1300,
                    showConfirmButton: false
                });
                reloadTable();
            }
        });
    }

    function submitEdit(v) {
        secureAjax({
            url: API.edit,
            method: 'PUT',
            payload: {
                Id: parseInt(v.Id),
                Amount: parseFloat(v.Amount),
                PaymentDate: v.PaymentDate,
                Description: v.Description
            },
            loadingMsg: 'Updating...',
            success: resp => {
                Swal.fire({
                    icon: 'success',
                    title: 'Updated!',
                    text: resp?.message || 'Income updated successfully',
                    timer: 1300,
                    showConfirmButton: false
                });
                reloadTable(true);
            }
        });
    }

    /* ================== AJAX HELPERS ================== */
    function secureAjax({ url, method, payload, loadingMsg, success }) {
        if (requestLock) return;
        requestLock = true;

        Swal.fire({
            title: loadingMsg || 'Processing...',
            allowOutsideClick: false,
            didOpen: () => Swal.showLoading()
        });

        $.ajax({
            url,
            method,
            contentType: 'application/json',
            data: payload ? JSON.stringify(payload) : null,
            // headers: { 'RequestVerificationToken': antiForgeryToken }, // uncomment if needed
            timeout: 20000
        })
            .done(data => {
                Swal.close();
                success && success(data);
            })
            .fail(xhr => {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    html: `<div style="text-align:left;font-size:.85rem;">${escapeHtml(resolveError(xhr))}</div>`
                });
            })
            .always(() => {
                requestLock = false;
            });
    }

    function showAjaxError(title, xhr) {
        Swal.fire({
            icon: 'error',
            title: title,
            html: `<div style="text-align:left;font-size:.85rem;">${escapeHtml(resolveError(xhr))}</div>`
        });
    }

    /* ================== UTILITIES ================== */
    function safeAmount(val) {
        const num = parseFloat(val);
        return isNaN(num) ? '0.00' : num.toFixed(2);
    }

    function formatDateDisplay(val) {
        // Currently leaving as server-sent "yyyy-MM-dd". If you want another format:
        // return dayjs(val).isValid() ? dayjs(val).format('YYYY-MM-DD') : val;
        return val;
    }

    function escapeHtml(txt) {
        return $('<div/>').text(txt || '').html();
    }

    function resolveError(xhr) {
        if (xhr?.responseJSON?.message) return xhr.responseJSON.message;
        if (xhr?.responseJSON?.error) return xhr.responseJSON.error;
        if (xhr?.responseText) return xhr.responseText;
        if (xhr?.status === 0) return 'Network error or request aborted.';
        return 'Unknown error';
    }

})(window.jQuery);