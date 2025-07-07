let loggedInUserCode = null;
let loggedInUserRootCode = null;
let currentTeacherCode = null;

$(function () {
    // Load user info and setup UI
    $.ajax({
        url: '/Management/GetUserRootInfo',
        method: 'GET',
        success: function (data) {
            if (data.error) {
                $('#user-info').text(data.error);
                return;
            }
            loggedInUserCode = data.user_code;
            loggedInUserRootCode = data.user_root_code;
            $('#user-info').html(
                'User Code: <b>' + data.user_code + '</b> | ' +
                'User Name: <b>' + data.user_name + '</b> | ' +
                'Root Name: <b>' + data.root_name + '</b>'
            );
            loadTeachers(loggedInUserRootCode);
        }
    });

    function loadTeachers(rootCode) {
        $.ajax({
            url: '/Management/GetTeachersByRoot?rootCode=' + rootCode,
            method: 'GET',
            success: function (teachers) {
                let html = teachers.map(teacher => `
                <div class="card mb-3">
                    <div class="card-body">
                        <div><b>${teacher.teacherName}</b></div>
                        <div>Phone: ${teacher.teacherPhone}</div>
                        <div>Address: ${teacher.teacherAddress || ''}</div>
                        <div>Status: ${teacher.isActive ? 'Active' : 'Inactive'}</div>
                        <div class="mt-2">
                            <button class="btn btn-info btn-sm show-subjects-btn" data-teacher="${teacher.teacherCode}">Show Subjects</button>
                            <button class="btn btn-success btn-sm add-teachsubject-btn" data-teacher="${teacher.teacherCode}">Add Teaching Subject</button>
                            <button class="btn btn-warning btn-sm edit-teacher-btn" data-teacher="${teacher.teacherCode}">Edit</button>
                            <button class="btn btn-danger btn-sm delete-teacher-btn" data-teacher="${teacher.teacherCode}">Delete</button>
                        </div>
                        <div class="mt-2 subjects-container" id="subjects-for-teacher-${teacher.teacherCode}" style="display:none;"></div>
                    </div>
                </div>
                `).join('');
                $('#teachers-list').html(html);
            }
        });
    }

    // --- Add Teacher ---
    $(document).on('click', '#openAddTeacher', function () {
        $('#teacherForm')[0].reset();
        $('#addTeacherModal').modal('show');
    });

    $('#teacherForm').on('submit', function (e) {
        e.preventDefault();
        var teacherData = {
            TeacherName: $('#teacherName').val(),
            TeacherPhone: $('#teacherPhone').val(),
            TeacherAddress: $('#teacherAddress').val(),
            RootCode: loggedInUserRootCode,
            InsertUser: loggedInUserCode
        };
        $.ajax({
            url: '/Management/AddTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherData),
            success: function (res) {
                $('#addTeacherModal').modal('hide');
                loadTeachers(loggedInUserRootCode);
                alert(res.message);
            },
            error: function (xhr) {
                alert("Failed to add teacher: " + xhr.responseText);
            }
        });
    });

    // --- Edit Teacher ---
    $(document).on('click', '.edit-teacher-btn', function () {
        let teacherCode = $(this).data('teacher');
        $.get('/Management/GetTeacherById?teacherCode=' + teacherCode, function (teacher) {
            $('#editTeacherCode').val(teacher.teacherCode);
            $('#editTeacherName').val(teacher.teacherName);
            $('#editTeacherPhone').val(teacher.teacherPhone);
            $('#editTeacherAddress').val(teacher.teacherAddress);
            $('#editTeacherModal').modal('show');
        });
    });

    $('#editTeacherForm').on('submit', function (e) {
        e.preventDefault();
        var teacherEdit = {
            TeacherCode: $('#editTeacherCode').val(),
            TeacherName: $('#editTeacherName').val(),
            TeacherPhone: $('#editTeacherPhone').val(),
            TeacherAddress: $('#editTeacherAddress').val()
        };
        $.ajax({
            url: '/Management/EditTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherEdit),
            success: function (res) {
                $('#editTeacherModal').modal('hide');
                loadTeachers(loggedInUserRootCode);
                alert(res.message);
            }
        });
    });

    // --- Delete Teacher ---
    $(document).on('click', '.delete-teacher-btn', function () {
        let teacherCode = $(this).data('teacher');
        if (!confirm('Are you sure you want to delete this teacher? All their teaching subjects will also be deleted.')) return;
        $.ajax({
            url: '/Management/DeleteTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherCode),
            success: function (res) {
                loadTeachers(loggedInUserRootCode);
                alert(res.message);
            }
        });
    });

    // --- Show Subjects for Teacher ---
    $(document).on('click', '.show-subjects-btn', function () {
        let teacherCode = $(this).data('teacher');
        let $container = $(`#subjects-for-teacher-${teacherCode}`);
        if ($container.is(':visible')) {
            $container.slideUp();
            return;
        }
        $.ajax({
            url: `/Management/GetSubjectsByTeacher?teacherCode=${teacherCode}&rootCode=${loggedInUserRootCode}`,
            method: 'GET',
            success: function (subjects) {
                let html = '';
                if (!subjects.length) {
                    html = '<span class="text-muted">No subjects found.</span>';
                } else {
                    html = '<ul class="list-group">';
                    for (let s of subjects) {
                        html += `
                            <li class="list-group-item d-flex justify-content-between align-items-center">
                                ${s.subjectName}
                                <button class="btn btn-danger btn-sm ms-2 delete-teach-btn"
                                    data-teacher="${s.teacherCode}" data-subject="${s.subjectCode}">Delete</button>
                            </li>`;
                    }
                    html += '</ul>';
                }
                $container.html(html).slideDown();
            }
        });
    });

    // --- Add Teaching Subject (open modal and fill years, branches & educational year) ---
    $(document).on('click', '.add-teachsubject-btn', function () {
        currentTeacherCode = $(this).data('teacher');
        // Populate year dropdown
        $.ajax({
            url: '/Management/GetYearsByRoot?rootCode=' + loggedInUserRootCode,
            type: 'GET',
            success: function (years) {
                let $yearSelect = $('#yearSelect');
                $yearSelect.empty();
                for (let year of years) {
                    $yearSelect.append($('<option>', { value: year.yearCode, text: year.yearName }));
                }
                // Set educational year display
                $.ajax({
                    url: '/Management/GetActiveEduYearByRoot?rootCode=' + loggedInUserRootCode,
                    type: 'GET',
                    success: function (activeYear) {
                        $('#activeYearDisplay').val(activeYear.eduYearName || "No educational year");
                        $('#activeYearCode').val(activeYear.yearCode || "");
                        if (activeYear.yearCode) $yearSelect.val(activeYear.yearCode);
                        // Now load branches
                        $.ajax({
                            url: '/Management/GetBranchesForRootWithCenters?rootCode=' + loggedInUserRootCode,
                            type: 'GET',
                            success: function (branches) {
                                let $branchSelect = $('#branchSelect');
                                $branchSelect.empty();
                                for (let branch of branches) {
                                    $branchSelect.append($('<option>', { value: branch.branchCode, text: branch.branchName }));
                                }
                                $('#teachSubjectForm')[0].reset();
                                $('#addTeachSubjectModal').modal('show');
                                if (activeYear.yearCode) $yearSelect.val(activeYear.yearCode);
                                $('#activeYearDisplay').val(activeYear.eduYearName || "No educational year");
                            }
                        });
                    }
                });
            }
        });
    });

    // --- Submit Add Teaching Subject ---
    $('#teachSubjectForm').on('submit', function (e) {
        e.preventDefault();
        let subjectName = $('#subjectName').val();
        let isPrimary = $('#isPrimary').val() === "true";
        let branchCode = $('#branchSelect').val();
        let yearCode = $('#yearSelect').val();
        if (!subjectName || !branchCode || !currentTeacherCode || !loggedInUserRootCode || !yearCode) {
            alert('Please fill all fields');
            return;
        }
        let data = {
            SubjectName: subjectName,
            IsPrimary: isPrimary,
            RootCode: loggedInUserRootCode,
            YearCode: yearCode,
            InsertUser: loggedInUserCode,
            BranchCode: branchCode,
            TeacherCode: currentTeacherCode
        };
        $.ajax({
            url: '/Management/AddTeachingSubject',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (res) {
                $('#addTeachSubjectModal').modal('hide');
                $(`.show-subjects-btn[data-teacher="${currentTeacherCode}"]`).trigger('click');
                alert(res.message);
            },
            error: function (xhr) {
                alert("Failed to add teaching subject: " + xhr.responseText);
            }
        });
    });

    // --- Delete Teach (from teach table) ---
    $(document).on('click', '.delete-teach-btn', function () {
        let teacherCode = $(this).data('teacher');
        let subjectCode = $(this).data('subject');
        if (!confirm('Are you sure you want to delete this teaching subject?')) return;
        $.ajax({
            url: '/Management/DeleteTeach',
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