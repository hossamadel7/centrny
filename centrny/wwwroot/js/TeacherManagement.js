console.log("TeacherManagement.js loaded");

$(function () {
    // Localized JS strings
    const deleteTeacherConfirm = getJsString('delete-teacher-confirm');
    const deleteTeachConfirm = getJsString('delete-teach-confirm');
    const noSubjectsFound = getJsString('no-subjects-found');
    const fillAllFields = getJsString('fill-all-fields');
    const processingText = getJsString('processing');
    const submitText = getJsString('submit');
    const saveChangesText = getJsString('save-changes') || "Save Changes";
    const addTeacherText = getJsString('add-teacher-btn') || "Add Teacher";
    const addTeachingSubjectBtn = getJsString('add-teaching-subject-btn');
    const showSubjectsBtn = getJsString('show-subjects-btn');
    const editBtnText = getJsString('edit-btn');
    const deleteBtnText = getJsString('delete-btn');
    const noEduYear = getJsString('no-educational-year');
    const userCodeText = getJsString('usercode');
    const userNameText = getJsString('username');
    const rootNameText = getJsString('rootname');
    const phoneText = getJsString('phone');
    const addressText = getJsString('address');

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
            }
        });
    }

    // Style for button row (matches unified theme)
    $('<style>').text(`
        .teacher-btn-row {
            display: flex;
            gap: 0.6em;
            flex-wrap: wrap;
            justify-content: flex-start;
            align-items: center;
            margin-bottom: 0.5em;
        }
        .teacher-card .modern-btn {
            min-width: 120px;
        }
        .teacher-card .show-subjects-btn {
            background: var(--primary-gradient-light) !important;
            color: var(--primary-color) !important;
            font-weight: 600 !important;
        }
        .teacher-card .add-teachsubject-btn {
            background: var(--secondary-gradient) !important;
            color: #fff !important;
        }
        .teacher-card .edit-teacher-btn {
            background: linear-gradient(135deg, #55a3ff 0%, #00b894 100%) !important;
            color: #fff !important;
        }
        .teacher-card .delete-teacher-btn {
            background: var(--danger-gradient) !important;
            color: #fff !important;
        }
        .teacher-card .modern-btn:hover, .teacher-card .modern-btn:focus {
            box-shadow: var(--shadow-lg) !important;
            transform: scale(1.05) !important;
        }
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
        $.ajax({
            url: '/TeacherManagement/AddTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherData),
            success: function (res) {
                $('#addTeacherModal').modal('hide');
                loadTeachers(loggedInUserRootCode);
                alert(res.message);
                resetSubmitButton(addTeacherSubmitBtn, addTeacherText);
            },
            error: function (xhr) {
                alert("Failed to add teacher: " + xhr.responseText);
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
                resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
            },
            error: function (xhr) {
                alert("Failed to edit teacher: " + xhr.responseText);
                resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
            }
        });
    });

    $('#editTeacherModal').on('hidden.bs.modal', function () {
        resetSubmitButton(editTeacherSubmitBtn, saveChangesText);
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
            ${s.subjectName} <span class="text-secondary">(${s.yearName} - ${s.branchName})</span>
        </span>
        ${canDelete ? `<button class="modern-btn delete-btn delete-teach-btn"
            data-teacher="${s.teacherCode}" data-subject="${s.subjectCode}" style="padding: 0.3em 1em; font-size: 0.9em;">${deleteBtnText}</button>` : ''}
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