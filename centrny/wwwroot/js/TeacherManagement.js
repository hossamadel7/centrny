let loggedInUserCode = null;
let loggedInUserRootCode = null;
let currentTeacherCode = null;

function getJsString(key) {
    return $('#js-localization').data(key);
}

$(function () {
    // Localized JS strings
    const deleteTeacherConfirm = getJsString('delete-teacher-confirm');
    const deleteTeachConfirm = getJsString('delete-teach-confirm');
    const noSubjectsFound = getJsString('no-subjects-found');
    const fillAllFields = getJsString('fill-all-fields');
    const processingText = getJsString('processing');
    const submitText = getJsString('submit');
    const userCodeText = getJsString('usercode');
    const userNameText = getJsString('username');
    const rootNameText = getJsString('rootname');
    const phoneText = getJsString('phone');
    const addressText = getJsString('address');
    const showSubjectsBtn = getJsString('show-subjects-btn');
    const addTeachingSubjectBtn = getJsString('add-teaching-subject-btn');
    const editBtn = getJsString('edit-btn');
    const deleteBtn = getJsString('delete-btn');
    const noEduYear = getJsString('no-educational-year');

    const addTeacherSubmitBtn = $('#teacherForm button[type="submit"]');
    const editTeacherSubmitBtn = $('#editTeacherForm button[type="submit"]');
    const teachSubjectSubmitBtn = $('#teachSubjectForm button[type="submit"]');

    function resetSubmitButton($btn, defaultText) {
        $btn.text(defaultText).prop('disabled', false);
    }

    $.ajax({
        url: '/TeacherManagement/GetUserRootInfo',
        method: 'GET',
        success: function (data) {
            if (data.error) {
                $('#user-info').text(data.error);
                return;
            }
            loggedInUserCode = data.user_code;
            loggedInUserRootCode = data.user_root_code;
            $('#user-info').html(
                userCodeText + ': <b>' + data.user_code + '</b> | ' +
                userNameText + ': <b>' + data.user_name + '</b> | ' +
                rootNameText + ': <b>' + data.root_name + '</b>'
            );
            loadTeachers(loggedInUserRootCode);
        }
    });

    function loadTeachers(rootCode) {
        $.ajax({
            url: '/TeacherManagement/GetTeachersByRoot?rootCode=' + rootCode,
            method: 'GET',
            success: function (teachers) {
                let html = teachers.map(teacher => `
                <div class="card mb-3">
                    <div class="card-body">
                        <div><b>${teacher.teacherName}</b></div>
                        <div>${phoneText}: ${teacher.teacherPhone}</div>
                        <div>${addressText}: ${teacher.teacherAddress || ''}</div>
                        <div>Status: ${teacher.isActive ? 'Active' : 'Inactive'}</div>
                        <div class="mt-2">
                            <button class="btn btn-info btn-sm show-subjects-btn" data-teacher="${teacher.teacherCode}">${showSubjectsBtn}</button>
                            <button class="btn btn-success btn-sm add-teachsubject-btn" data-teacher="${teacher.teacherCode}">${addTeachingSubjectBtn}</button>
                            <button class="btn btn-warning btn-sm edit-teacher-btn" data-teacher="${teacher.teacherCode}">${editBtn}</button>
                            <button class="btn btn-danger btn-sm delete-teacher-btn" data-teacher="${teacher.teacherCode}">${deleteBtn}</button>
                        </div>
                        <div class="mt-2 subjects-container" id="subjects-for-teacher-${teacher.teacherCode}" style="display:none;"></div>
                    </div>
                </div>
                `).join('');
                $('#teachers-list').html(html);
            }
        });
    }

    $(document).on('click', '#openAddTeacher', function () {
        $('#teacherForm')[0].reset();
        resetSubmitButton(addTeacherSubmitBtn, submitText);
        $('#addTeacherModal').modal('show');
    });

    $('#teacherForm').on('submit', function (e) {
        e.preventDefault();
        addTeacherSubmitBtn.text(processingText).prop('disabled', true);
        var teacherData = {
            TeacherName: $('#teacherName').val(),
            TeacherPhone: $('#teacherPhone').val(),
            TeacherAddress: $('#teacherAddress').val(),
            RootCode: loggedInUserRootCode,
            InsertUser: loggedInUserCode
        };
        $.ajax({
            url: '/TeacherManagement/AddTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherData),
            success: function (res) {
                $('#addTeacherModal').modal('hide');
                loadTeachers(loggedInUserRootCode);
                alert(res.message);
                resetSubmitButton(addTeacherSubmitBtn, submitText);
            },
            error: function (xhr) {
                alert("Failed to add teacher: " + xhr.responseText);
                resetSubmitButton(addTeacherSubmitBtn, submitText);
            }
        });
    });

    $('#addTeacherModal').on('hidden.bs.modal', function () {
        resetSubmitButton(addTeacherSubmitBtn, submitText);
    });

    $(document).on('click', '.edit-teacher-btn', function () {
        let teacherCode = $(this).data('teacher');
        $.get('/TeacherManagement/GetTeacherById?teacherCode=' + teacherCode, function (teacher) {
            $('#editTeacherCode').val(teacher.teacherCode);
            $('#editTeacherName').val(teacher.teacherName);
            $('#editTeacherPhone').val(teacher.teacherPhone);
            $('#editTeacherAddress').val(teacher.teacherAddress);
            resetSubmitButton(editTeacherSubmitBtn, submitText);
            $('#editTeacherModal').modal('show');
        });
    });

    $('#editTeacherForm').on('submit', function (e) {
        e.preventDefault();
        editTeacherSubmitBtn.text(processingText).prop('disabled', true);
        var teacherEdit = {
            TeacherCode: $('#editTeacherCode').val(),
            TeacherName: $('#editTeacherName').val(),
            TeacherPhone: $('#editTeacherPhone').val(),
            TeacherAddress: $('#editTeacherAddress').val()
        };
        $.ajax({
            url: '/TeacherManagement/EditTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherEdit),
            success: function (res) {
                $('#editTeacherModal').modal('hide');
                loadTeachers(loggedInUserRootCode);
                alert(res.message);
                resetSubmitButton(editTeacherSubmitBtn, submitText);
            },
            error: function (xhr) {
                alert("Failed to edit teacher: " + xhr.responseText);
                resetSubmitButton(editTeacherSubmitBtn, submitText);
            }
        });
    });

    $('#editTeacherModal').on('hidden.bs.modal', function () {
        resetSubmitButton(editTeacherSubmitBtn, submitText);
    });

    $(document).on('click', '.delete-teacher-btn', function () {
        let teacherCode = $(this).data('teacher');
        if (!confirm(deleteTeacherConfirm)) return;
        $.ajax({
            url: '/TeacherManagement/DeleteTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherCode),
            success: function (res) {
                loadTeachers(loggedInUserRootCode);
                alert(res.message);
            }
        });
    });

    $(document).on('click', '.show-subjects-btn', function () {
        let teacherCode = $(this).data('teacher');
        let $container = $(`#subjects-for-teacher-${teacherCode}`);
        if ($container.is(':visible')) {
            $container.slideUp();
            return;
        }
        $.ajax({
            url: `/TeacherManagement/GetSubjectsByTeacher?teacherCode=${teacherCode}&rootCode=${loggedInUserRootCode}`,
            method: 'GET',
            success: function (subjects) {
                let html = '';
                if (!subjects.length) {
                    html = `<span class="text-muted">${noSubjectsFound}</span>`;
                } else {
                    html = '<ul class="list-group">';
                    for (let s of subjects) {
                        html += `
                            <li class="list-group-item d-flex justify-content-between align-items-center">
                                <span>
                                    ${s.subjectName} <span class="text-secondary">(${s.yearName})</span>
                                </span>
                                <button class="btn btn-danger btn-sm ms-2 delete-teach-btn"
                                    data-teacher="${s.teacherCode}" data-subject="${s.subjectCode}">${deleteBtn}</button>
                            </li>`;
                    }
                    html += '</ul>';
                }
                $container.html(html).slideDown();
            }
        });
    });

    $(document).on('click', '.add-teachsubject-btn', function () {
        currentTeacherCode = $(this).data('teacher');
        $('#teachSubjectForm')[0].reset();
        resetSubmitButton(teachSubjectSubmitBtn, submitText);

        $.ajax({
            url: '/TeacherManagement/GetYearsByRoot?rootCode=' + loggedInUserRootCode,
            type: 'GET',
            success: function (years) {
                let $yearSelect = $('#yearSelect');
                $yearSelect.empty();
                for (let year of years) {
                    $yearSelect.append($('<option>', { value: year.yearCode, text: year.yearName }));
                }

                $.ajax({
                    url: '/TeacherManagement/GetActiveEduYearByRoot?rootCode=' + loggedInUserRootCode,
                    type: 'GET',
                    success: function (activeYear) {
                        $('#activeYearDisplay').val(activeYear.eduYearName || noEduYear);
                        $.ajax({
                            url: '/TeacherManagement/GetBranchesForRootWithCenters?rootCode=' + loggedInUserRootCode,
                            type: 'GET',
                            success: function (branches) {
                                let $branchSelect = $('#branchSelect');
                                $branchSelect.empty();
                                for (let branch of branches) {
                                    $branchSelect.append($('<option>', { value: branch.branchCode, text: branch.branchName }));
                                }
                                $.ajax({
                                    url: '/TeacherManagement/GetSubjectsByRoot?rootCode=' + loggedInUserRootCode,
                                    type: 'GET',
                                    success: function (subjects) {
                                        let $subjectSelect = $('#subjectSelect');
                                        $subjectSelect.empty();
                                        for (let subject of subjects) {
                                            $subjectSelect.append($('<option>', { value: subject.subjectCode, text: subject.subjectName }));
                                        }
                                        $('#addTeachSubjectModal').modal('show');
                                    }
                                });
                            }
                        });
                    }
                });
            }
        });
    });

    $('#addTeachSubjectModal').on('hidden.bs.modal', function () {
        resetSubmitButton(teachSubjectSubmitBtn, submitText);
    });

    $('#teachSubjectForm').on('submit', function (e) {
        e.preventDefault();
        teachSubjectSubmitBtn.text(processingText).prop('disabled', true);
        let subjectCode = $('#subjectSelect').val();
        let isPrimary = $('#isPrimary').val() === "true";
        let branchCode = $('#branchSelect').val();
        let yearCode = $('#yearSelect').val();
        if (!subjectCode || !branchCode || !currentTeacherCode || !loggedInUserRootCode || !yearCode) {
            alert(fillAllFields);
            resetSubmitButton(teachSubjectSubmitBtn, submitText);
            return;
        }
        let data = {
            SubjectCode: subjectCode,
            IsPrimary: isPrimary,
            RootCode: loggedInUserRootCode,
            YearCode: yearCode,
            InsertUser: loggedInUserCode,
            BranchCode: branchCode,
            TeacherCode: currentTeacherCode
        };
        $.ajax({
            url: '/TeacherManagement/AddTeachingSubject',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (res) {
                $('#addTeachSubjectModal').modal('hide');
                $(`.show-subjects-btn[data-teacher="${currentTeacherCode}"]`).trigger('click');
                alert(res.message);
                resetSubmitButton(teachSubjectSubmitBtn, submitText);
            },
            error: function (xhr) {
                alert("Failed to add teaching subject: " + xhr.responseText);
                resetSubmitButton(teachSubjectSubmitBtn, submitText);
            }
        });
    });

    $(document).on('click', '.delete-teach-btn', function () {
        let teacherCode = $(this).data('teacher');
        let subjectCode = $(this).data('subject');
        if (!confirm(deleteTeachConfirm)) return;
        $.ajax({
            url: '/TeacherManagement/DeleteTeach',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ TeacherCode: teacherCode, SubjectCode: subjectCode }),
            success: function (res) {
                $(`.show-subjects-btn[data-teacher="${teacherCode}"]`).trigger('click');
                alert(res.message);
            }
        });
    });
});