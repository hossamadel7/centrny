console.log("TeacherManagement.js loaded");

// SweetAlert2 helpers
function swalSuccess(msg) {
    Swal.fire({
        icon: 'success',
        title: $('#js-localization').data('success-title') || '',
        text: msg,
        showConfirmButton: false,
        timer: 1700
    });
}
function swalError(msg) {
    Swal.fire({
        icon: 'error',
        title: $('#js-localization').data('error-title') || '',
        text: msg,
        showConfirmButton: true
    });
}
function swalConfirm(msg) {
    return Swal.fire({
        icon: 'warning',
        title: '',
        text: msg,
        showCancelButton: true,
        confirmButtonText: $('#js-localization').data('submit') || 'OK',
        cancelButtonText: $('#js-localization').data('cancel') || 'Cancel'
    });
}

$(function () {
    // Localized JS strings
    const loc = $('#js-localization');
    const deleteTeacherConfirm = loc.data('delete-teacher-confirm');
    const deleteTeachConfirm = loc.data('delete-teach-confirm');
    const noSubjectsFound = loc.data('no-subjects-found');
    const fillAllFields = loc.data('fill-all-fields');
    const processingText = loc.data('processing');
    const submitText = loc.data('submit');
    const saveChangesText = loc.data('save-changes') || "Save Changes";
    const addTeacherText = loc.data('add-teacher-btn') || "Add Teacher";
    const addTeachingSubjectBtn = loc.data('add-teaching-subject-btn');
    const showSubjectsBtn = loc.data('show-subjects-btn');
    const editBtnText = loc.data('edit-btn');
    const deleteBtnText = loc.data('delete-btn');
    const noEduYear = loc.data('no-educational-year');
    const userCodeText = loc.data('usercode');
    const userNameText = loc.data('username');
    const rootNameText = loc.data('rootname');
    const phoneText = loc.data('phone');
    const addressText = loc.data('address');
    const successAddTeacher = loc.data('success-add-teacher') || "Teacher added!";
    const successEditTeacher = loc.data('success-edit-teacher') || "Teacher updated!";
    const successDeleteTeacher = loc.data('success-delete-teacher') || "Teacher deleted!";
    const successAddTeachSubject = loc.data('success-add-teach-subject') || "Teaching subject added!";
    const successDeleteTeachSubject = loc.data('success-delete-teach-subject') || "Teaching subject deleted!";
    const errorAction = loc.data('error-action') || "Action failed.";
    const errorNetwork = loc.data('error-network') || "Network error.";

    // Permission flags from view
    const jsPerms = $('#js-permissions');
    const canInsert = jsPerms.data('can-insert') === true || jsPerms.data('can-insert') === "True";
    const canUpdate = jsPerms.data('can-update') === true || jsPerms.data('can-update') === "True";
    const canDelete = jsPerms.data('can-delete') === true || jsPerms.data('can-delete') === "True";

    let loggedInUserCode = null;
    let loggedInUserRootCode = null;
    let currentTeacherCode = null;

    const addTeacherSubmitBtn = $('#teacherForm button[type="submit"]');
    const editTeacherSubmitBtn = $('#editTeacherForm button[type="submit"]');
    const teachSubjectSubmitBtn = $('#teachSubjectForm button[type="submit"]');

    function resetSubmitButton($btn, defaultText) {
        $btn.text(defaultText).prop('disabled', false);
    }

    function getJsString(key) {
        return $('#js-localization').data(key);
    }

    $.ajax({
        url: '/TeacherManagement/GetUserRootInfo',
        method: 'GET',
        success: function (data) {
            if (data.error) {
                swalError(data.error);
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
        },
        error: function () { swalError(errorNetwork); }
    });

    function loadTeachers(rootCode) {
        $.ajax({
            url: '/TeacherManagement/GetTeachersByRoot?rootCode=' + rootCode,
            method: 'GET',
            success: function (teachers) {
                let html = teachers.map(teacher => `
                <div class="card teacher-card mb-3" style="border-radius: 20px; box-shadow: var(--shadow-lg); border: none; background: var(--bg-white);">
                    <div class="card-body" style="padding: 1.6em 1.3em;">
                        <div class="fw-bold mb-1" style="font-size: 1.1em; color: var(--primary-color);">${teacher.teacherName}</div>
                        <div style="color: var(--text-muted);">${phoneText}: <span class="fw-bold">${teacher.teacherPhone}</span></div>
                        <div style="color: var(--text-muted);">${addressText}: <span class="fw-bold">${teacher.teacherAddress || ''}</span></div>
                        <div style="color: var(--text-muted);">Status: <span class="fw-bold">${teacher.isActive ? 'Active' : 'Inactive'}</span></div>
                        <div class="mt-3 teacher-btn-row">
                            <button class="modern-btn show-subjects-btn" data-teacher="${teacher.teacherCode}">${showSubjectsBtn}</button>
                            ${canInsert ? `<button class="modern-btn add-teachsubject-btn" data-teacher="${teacher.teacherCode}">${addTeachingSubjectBtn}</button>` : ''}
                            ${canUpdate ? `<button class="modern-btn edit-btn edit-teacher-btn" data-teacher="${teacher.teacherCode}">${editBtnText}</button>` : ''}
                            ${canDelete ? `<button class="modern-btn delete-btn delete-teacher-btn" data-teacher="${teacher.teacherCode}">${deleteBtnText}</button>` : ''}
                        </div>
                        <div class="mt-2 subjects-container" id="subjects-for-teacher-${teacher.teacherCode}" style="display:none;"></div>
                    </div>
                </div>
                `).join('');
                $('#teachers-list').html(html);
            },
            error: function () { swalError(errorNetwork); }
        });
    }

    // Button row style
    $('<style>').text(`
        .teacher-btn-row {
            display: flex;
            gap: 0.6em;
            flex-wrap: wrap;
            justify-content: flex-start;
            align-items: center;
            margin-bottom: 0.5em;
        }
        .teacher-card .modern-btn { min-width: 120px; }
        .teacher-card .show-subjects-btn { background: var(--primary-gradient-light) !important; color: var(--primary-color) !important; font-weight: 600 !important; }
        .teacher-card .add-teachsubject-btn { background: var(--secondary-gradient) !important; color: #fff !important; }
        .teacher-card .edit-teacher-btn { background: linear-gradient(135deg, #55a3ff 0%, #00b894 100%) !important; color: #fff !important; }
        .teacher-card .delete-teacher-btn { background: var(--danger-gradient) !important; color: #fff !important; }
        .teacher-card .modern-btn:hover, .teacher-card .modern-btn:focus { box-shadow: var(--shadow-lg) !important; transform: scale(1.05) !important; }
    `).appendTo('head');

    $(document).on('click', '#openAddTeacher', function () {
        $('#teacherForm')[0].reset();
        resetSubmitButton(addTeacherSubmitBtn, addTeacherText);
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
        if (!teacherData.TeacherName || !teacherData.TeacherPhone) {
            swalError(fillAllFields);
            resetSubmitButton(addTeacherSubmitBtn, addTeacherText);
            return;
        }
        $.ajax({
            url: '/TeacherManagement/AddTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherData),
            success: function (res) {
                $('#addTeacherModal').modal('hide');
                loadTeachers(loggedInUserRootCode);
                swalSuccess(successAddTeacher);
                resetSubmitButton(addTeacherSubmitBtn, addTeacherText);
            },
            error: function (xhr) {
                swalError(errorAction + ": " + xhr.responseText);
                resetSubmitButton(addTeacherSubmitBtn, addTeacherText);
            }
        });
    });

    $('#addTeacherModal').on('hidden.bs.modal', function () {
        resetSubmitButton(addTeacherSubmitBtn, addTeacherText);
    });

    $(document).on('click', '.edit-teacher-btn', function () {
        let teacherCode = $(this).data('teacher');
        $.get('/TeacherManagement/GetTeacherById?teacherCode=' + teacherCode, function (teacher) {
            $('#editTeacherCode').val(teacher.teacherCode);
            $('#editTeacherName').val(teacher.teacherName);
            $('#editTeacherPhone').val(teacher.teacherPhone);
            $('#editTeacherAddress').val(teacher.teacherAddress);
            resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
            $('#editTeacherModal').modal('show');
        }).fail(function () {
            swalError(errorNetwork);
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
        if (!teacherEdit.TeacherName || !teacherEdit.TeacherPhone) {
            swalError(fillAllFields);
            resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
            return;
        }
        $.ajax({
            url: '/TeacherManagement/EditTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherEdit),
            success: function (res) {
                $('#editTeacherModal').modal('hide');
                loadTeachers(loggedInUserRootCode);
                swalSuccess(successEditTeacher);
                resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
            },
            error: function (xhr) {
                swalError(errorAction + ": " + xhr.responseText);
                resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
            }
        });
    });

    $('#editTeacherModal').on('hidden.bs.modal', function () {
        resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
    });

    $(document).on('click', '.delete-teacher-btn', function () {
        let teacherCode = $(this).data('teacher');
        swalConfirm(deleteTeacherConfirm).then(result => {
            if (result.isConfirmed) {
                $.ajax({
                    url: '/TeacherManagement/DeleteTeacher',
                    method: 'POST',
                    contentType: 'application/json',
                    data: JSON.stringify(teacherCode),
                    success: function (res) {
                        loadTeachers(loggedInUserRootCode);
                        swalSuccess(successDeleteTeacher);
                    },
                    error: function (xhr) {
                        swalError(errorAction + ": " + xhr.responseText);
                    }
                });
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
            ${s.subjectName} <span class="text-secondary">(${s.yearName} - ${s.branchName})</span>
        </span>
        ${canDelete ? `<button class="modern-btn delete-btn delete-teach-btn"
            data-teacher="${s.teacherCode}" data-subject="${s.subjectCode}" style="padding: 0.3em 1em; font-size: 0.9em;">${deleteBtnText}</button>` : ''}
    </li>`;
                    }
                    html += '</ul>';
                }
                $container.html(html).slideDown();
            },
            error: function () { swalError(errorNetwork); }
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
                                    },
                                    error: function () { swalError(errorNetwork); }
                                });
                            },
                            error: function () { swalError(errorNetwork); }
                        });
                    },
                    error: function () { swalError(errorNetwork); }
                });
            },
            error: function () { swalError(errorNetwork); }
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
            swalError(fillAllFields);
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
                swalSuccess(successAddTeachSubject);
                resetSubmitButton(teachSubjectSubmitBtn, submitText);
            },
            error: function (xhr) {
                swalError(errorAction + ": " + xhr.responseText);
                resetSubmitButton(teachSubjectSubmitBtn, submitText);
            }
        });
    });

    $(document).on('click', '.delete-teach-btn', function () {
        let teacherCode = $(this).data('teacher');
        let subjectCode = $(this).data('subject');
        swalConfirm(deleteTeachConfirm).then(result => {
            if (result.isConfirmed) {
                $.ajax({
                    url: '/TeacherManagement/DeleteTeach',
                    method: 'POST',
                    contentType: 'application/json',
                    data: JSON.stringify({ TeacherCode: teacherCode, SubjectCode: subjectCode }),
                    success: function (res) {
                        $(`.show-subjects-btn[data-teacher="${teacherCode}"]`).trigger('click');
                        swalSuccess(successDeleteTeachSubject);
                    },
                    error: function (xhr) {
                        swalError(errorAction + ": " + xhr.responseText);
                    }
                });
            }
        });
    });
});