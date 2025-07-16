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
                            <button class="modern-btn success-btn edit-employee" data-id="${emp.employeeCode}"><i class="fas fa-edit"></i> ${editTitle}</button>
                            <button class="modern-btn delete-btn delete-employee" data-id="${emp.employeeCode}"><i class="fas fa-trash"></i> ${deleteText}</button>
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
        $('#employeeModal').modal('show');
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
                $('#employeeStartDate').val(emp.employeeStartDate);
                $('#userCode').val(emp.userCode ?? '');
                $('#branchCode').val(emp.branchCode ?? '');
                $('#isActive').val(emp.isActive.toString());
                $('#employeeModalLabel').text(editTitle);
                $('#employeeModal').modal('show');
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
                    $('#employeeModal').modal('hide');
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
                    $('#employeeModal').modal('hide');
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