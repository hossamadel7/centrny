// Employee Management with safe Bootstrap 5 modal helpers

(function boot(factory) {
    if (typeof window.jQuery !== 'undefined') {
        factory(window.jQuery);
    } else {
        document.addEventListener('DOMContentLoaded', function () {
            if (typeof window.jQuery !== 'undefined') {
                factory(window.jQuery);
            } else {
                console.error('Employee.js requires jQuery. Please include jQuery before this script.');
            }
        });
    }
})(function ($) {
    'use strict';

    // Safe Bootstrap modal helpers (v5 with jQuery fallback)
    function safeShowModalById(id) {
        try {
            const el = document.getElementById(id);
            if (!el) return;
            if (window.bootstrap && typeof window.bootstrap.Modal === 'function') {
                const Ctor = window.bootstrap.Modal;
                let instance = (typeof Ctor.getOrCreateInstance === 'function')
                    ? Ctor.getOrCreateInstance(el)
                    : (typeof Ctor.getInstance === 'function' ? Ctor.getInstance(el) : null);
                if (!instance) instance = new Ctor(el);
                if (instance && typeof instance.show === 'function') { instance.show(); return; }
            }
            if (typeof window.$ !== 'undefined' && typeof window.$(el).modal === 'function') {
                window.$(el).modal('show'); return;
            }
            el.style.display = 'block';
            document.body.classList.add('modal-open');
        } catch (e) { console && console.debug && console.debug('safeShowModalById error:', e); }
    }
    function safeHideModalById(id) {
        try {
            const el = document.getElementById(id);
            if (!el) return;
            if (window.bootstrap && typeof window.bootstrap.Modal === 'function') {
                const Ctor = window.bootstrap.Modal;
                let instance = (typeof Ctor.getOrCreateInstance === 'function')
                    ? Ctor.getOrCreateInstance(el)
                    : (typeof Ctor.getInstance === 'function' ? Ctor.getInstance(el) : null);
                if (!instance) instance = new Ctor(el);
                if (instance && typeof instance.hide === 'function') { instance.hide(); return; }
            }
            if (typeof window.$ !== 'undefined' && typeof window.$(el).modal === 'function') {
                window.$(el).modal('hide'); return;
            }
            el.style.display = 'none';
            document.body.classList.remove('modal-open');
        } catch (e) { console && console.debug && console.debug('safeHideModalById error:', e); }
    }

    $(document).ready(function () {
        loadEmployees();
        loadDropdowns();

        function loadEmployees() {
            $.getJSON('/Employee/GetEmployees', function (data) {
                var rows = '';
                if (!data || data.length === 0) {
                    rows = `<tr><td colspan="9" style="text-align:center">${noEmployeesText}</td></tr>`;
                } else {
                    $.each(data, function (i, emp) {
                        rows += `<tr>
                            <td>${emp.employeeName}</td>
                            <td>${emp.employeePhone}</td>
                            <td>${emp.employeeEmail}</td>
                            <td>${emp.employeeSalary}</td>
                            <td>${emp.employeeStartDate}</td>
                            <td>${emp.userCode ?? ''}</td>
                            <td>${emp.branchCode ?? ''}</td>
                            <td>${emp.isActive ? yesText : noText}</td>
                            <td>
                                <button class="modern-btn success-btn edit-employee" data-id="${emp.employeeCode}">
                                    <i class="fas fa-edit"></i> ${editTitle}
                                </button>
                                <button class="modern-btn delete-btn delete-employee" data-id="${emp.employeeCode}">
                                    <i class="fas fa-trash"></i> ${deleteText}
                                </button>
                            </td>
                        </tr>`;
                    });
                }
                $('#employeeTable tbody').html(rows);
            }).fail(function () {
                $('#employeeTable tbody').html(`<tr><td colspan="9" style="text-align:center">${errorLoadingText}</td></tr>`);
            });
        }

        function loadDropdowns() {
            // User dropdown (localized)
            $.getJSON('/Employee/GetUsersForDropdown', function (data) {
                var options = `<option value="">${userSelectText}</option>`;
                $.each(data, function (i, user) {
                    options += `<option value="${user.userCode}">${user.username}</option>`;
                });
                $('#userCode').html(options);
            });
            // Branch dropdown (localized)
            $.getJSON('/Employee/GetBranchesForDropdown', function (data) {
                var options = `<option value="">${branchSelectText}</option>`;
                $.each(data, function (i, branch) {
                    options += `<option value="${branch.branchCode}">${branch.branchName}</option>`;
                });
                $('#branchCode').html(options);
            });
        }

        // Add Employee
        $('#btnAddEmployee').click(function () {
            $('#employeeForm')[0].reset();
            $('#employeeCode').val('');
            $('#isActive').val('true');
            $('#employeeModalLabel').text(addTitle);
            safeShowModalById('employeeModal');
        });

        // Edit Employee (open modal and fill fields)
        $('#employeeTable').on('click', '.edit-employee', function () {
            var id = $(this).data('id');
            $.getJSON('/Employee/GetEmployees', function (data) {
                var emp = data.find(e => e.employeeCode === id);
                if (emp) {
                    $('#employeeCode').val(emp.employeeCode);
                    $('#employeeName').val(emp.employeeName);
                    $('#employeePhone').val(emp.employeePhone);
                    $('#employeeEmail').val(emp.employeeEmail);
                    $('#employeeSalary').val(emp.employeeSalary);
                    // If API returns ISO date, ensure input gets yyyy-MM-dd
                    var sd = (emp.employeeStartDate || '').slice(0, 10);
                    $('#employeeStartDate').val(sd);
                    $('#userCode').val(emp.userCode ?? '');
                    $('#branchCode').val(emp.branchCode ?? '');
                    $('#isActive').val(String(emp.isActive));
                    $('#employeeModalLabel').text(editTitle);
                    safeShowModalById('employeeModal');
                }
            });
        });

        // Save Employee (Add or Edit)
        $('#employeeForm').submit(function (e) {
            e.preventDefault();
            var employee = {
                employeeName: $('#employeeName').val(),
                employeePhone: $('#employeePhone').val(),
                employeeEmail: $('#employeeEmail').val(),
                employeeSalary: parseFloat($('#employeeSalary').val()),
                employeeStartDate: $('#employeeStartDate').val(),
                userCode: $('#userCode').val() || null,
                branchCode: $('#branchCode').val() || null,
                isActive: $('#isActive').val() === "true"
            };
            var id = $('#employeeCode').val();
            if (id) {
                // Edit
                employee.employeeCode = parseInt(id);
                $.ajax({
                    url: '/Employee/EditEmployee',
                    type: 'PUT',
                    contentType: 'application/json',
                    data: JSON.stringify(employee),
                    success: function () {
                        safeHideModalById('employeeModal');
                        loadEmployees();
                    },
                    error: function () {
                        $('#addEmployeeError').text(couldNotEditText);
                    }
                });
            } else {
                var addEmp = Object.assign({}, employee);
                delete addEmp.isActive;
                $.ajax({
                    url: '/Employee/AddEmployee',
                    type: 'POST',
                    contentType: 'application/json',
                    data: JSON.stringify(addEmp),
                    success: function () {
                        safeHideModalById('employeeModal');
                        loadEmployees();
                    },
                    error: function () {
                        $('#addEmployeeError').text(couldNotAddText);
                    }
                });
            }
        });

        // Delete Employee
        $('#employeeTable').on('click', '.delete-employee', function () {
            var id = $(this).data('id');
            if (confirm(deleteConfirmText)) {
                $.ajax({
                    url: '/Employee/DeleteEmployee/' + id,
                    type: 'DELETE',
                    success: function () {
                        loadEmployees();
                    },
                    error: function () {
                        alert(couldNotDeleteText);
                    }
                });
            }
        });
    });
});