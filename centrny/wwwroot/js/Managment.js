let loggedInUserCode = null;
let loggedInUserRootCode = null; // for superuser logic
let selectedRootCode = null;
let selectedRootIsCenter = null;
let isSuperUser = false;
let currentTeacherCode = null;

$(function () {
    // Load user info and setup UI
    $.ajax({
        url: '/Management/GetUserRootInfo',
        method: 'GET',
        success: function (data) {
            if (data.error) {
                $('#header-title').text('Error');
                $('#user-info').text(data.error);
                return;
            }
            loggedInUserCode = data.user_code;
            loggedInUserRootCode = data.user_root_code;
            isSuperUser = (loggedInUserRootCode == 1);

            $('#user-info').html(
                'User Code: <b>' + data.user_code + '</b> | ' +
                'User Name: <b>' + data.user_name + '</b> | ' +
                'Root Name: <b>' + data.root_name + '</b>'
            );

            if (isSuperUser) {
                // Superuser: show dropdown and load roots
                $('#roots-dropdown-container').show();
                $.ajax({
                    url: '/Management/GetRoots',
                    method: 'GET',
                    success: function (roots) {
                        var $dropdown = $('#roots-dropdown');
                        $dropdown.empty();
                        $dropdown.append('<option value="">-- Select Root --</option>');
                        $.each(roots, function (i, root) {
                            $dropdown.append(
                                $('<option>', {
                                    value: root.rootCode,
                                    text: root.rootName,
                                    'data-iscenter': root.isCenter
                                })
                            );
                        });
                    }
                });
                $('#management-content').hide();
            } else {
                // Not superuser: hide dropdown, load only their root
                $('#roots-dropdown-container').hide();
                selectedRootCode = loggedInUserRootCode;
                // Get isCenter for this root
                $.ajax({
                    url: '/Management/GetRoots',
                    method: 'GET',
                    success: function (roots) {
                        var myRoot = roots.find(r => r.rootCode == loggedInUserRootCode);
                        selectedRootIsCenter = myRoot ? myRoot.isCenter : false;
                        $('#management-content').show();
                        renderSections();
                    }
                });
            }
        }
    });

    $('#roots-dropdown').on('change', function () {
        var rootCode = parseInt($(this).val());
        selectedRootCode = rootCode;
        selectedRootIsCenter = $('#roots-dropdown option:selected').data('iscenter');
        // isSuperUser already set (based on logged in user)
        $('#branches-list').empty();
        $('#teachers-list').empty();
        $('#centers-list').empty();
        $('#management-content').hide();

        if (!rootCode) return;

        $('#management-content').show();
        renderSections();
    });

    function renderSections() {
        $('#branches-section').remove();
        $('#centers-section').remove();
        $('#teachers-section').remove();

        if (isSuperUser) {
            renderBranchesSection(true);
            renderCentersSection(true);
            renderTeachersSection(true);
            loadBranches(selectedRootCode);
            loadCentersWithBranches(selectedRootCode);
            loadTeachers(selectedRootCode);
            return;
        }

        // Always show teachers section for all roots, so "Add Teaching Subject" works for both isCenter true and false!
        renderTeachersSection(true);
        loadTeachers(selectedRootCode);

        if (selectedRootIsCenter) {
            renderBranchesSection(false);
            loadBranches(selectedRootCode);
        } else {
            renderCentersSection(false);
            loadCentersWithBranches(selectedRootCode);
        }
    }

    function renderBranchesSection(editable) {
        $('#branches-section').remove();
        let html = `
            <div id="branches-section">
                <div class="d-flex align-items-center mb-2">
                    <h5 class="mb-0">Branches</h5>
                    ${editable ? `<button class="btn btn-success btn-sm ms-2" id="openAddBranch">Add Branch</button>` : ''}
                </div>
                <div id="branches-list"></div>
            </div>
        `;
        $('#management-content').prepend(html);
    }

    function renderCentersSection(editable) {
        $('#centers-section').remove();
        let html = `
            <div id="centers-section">
                <div class="d-flex align-items-center mb-2">
                    <h5 class="mb-0">Centers</h5>
                    ${editable ? `<button class="btn btn-info btn-sm ms-2" id="openAddCenter">Add Center</button>` : ''}
                </div>
                <div id="centers-list"></div>
            </div>
        `;
        $('#management-content').prepend(html);
    }

    function renderTeachersSection(editable) {
        $('#teachers-section').remove();
        let html = `
            <div id="teachers-section" class="mt-3">
                <div class="d-flex align-items-center mb-2">
                    <h5 class="mb-0">Teachers</h5>
                    ${editable ? `<button class="btn btn-primary btn-sm ms-2" id="openAddTeacher">Add Teacher</button>` : ''}
                </div>
                <div id="teachers-list"></div>
            </div>
        `;
        $('#management-content').append(html);
    }

    function loadBranches(rootCode) {
        $.ajax({
            url: '/Management/GetBranchesByRoot?rootCode=' + rootCode,
            method: 'GET',
            success: function (branches) {
                let editable = isSuperUser;
                let html = `
                <div class="plans-grid">
                    ${branches.map(branch => `
                    <div>
                        <div><b>${branch.branchName}</b></div>
                        <div>Address: ${branch.address}</div>
                        <div>Phone: ${branch.phone}</div>
                        <div>Start Time: ${branch.startTime}</div>
                        <div class="mt-2">
                            ${editable
                        ? `
                                <button class="btn btn-warning btn-action edit-branch-btn" data-id="${branch.branchCode}">Edit</button>
                                <button class="btn btn-danger btn-action delete-branch-btn" data-id="${branch.branchCode}">Delete</button>
                                `
                        : ''
                    }
                        </div>
                    </div>
                    `).join('')}
                </div>`;
                $('#branches-list').html(html);
            }
        });
    }

    function loadCentersWithBranches(rootCode) {
        $.ajax({
            url: '/Management/GetCentersByRoot?rootCode=' + rootCode,
            method: 'GET',
            success: function (centers) {
                let editable = isSuperUser;
                let centerHtmlPromises = centers.map(center => {
                    return $.ajax({
                        url: '/Management/GetBranchesByCenter?centerCode=' + center.centerCode,
                        method: 'GET'
                    }).then(function (branches) {
                        let branchesHtml = '';
                        if (branches.length) {
                            branchesHtml = `
                                <div class="ms-3">
                                    <div><b>Branches:</b>
                                        ${isSuperUser
                                    ? `<button class="btn btn-success btn-sm ms-2 add-branch-under-center-btn" data-centercode="${center.centerCode}">Add Branch</button>`
                                    : ''
                                }
                                    </div>
                                    <ul>
                                        ${branches.map(branch => `
                                            <li>
                                                <b>${branch.branchName}</b>
                                                <span> | Address: ${branch.address}</span>
                                                <span> | Phone: ${branch.phone}</span>
                                                <span> | Start Time: ${branch.startTime}</span>
                                                ${isSuperUser
                                        ? `
                                                        <button class="btn btn-warning btn-action btn-sm edit-branch-btn ms-1" data-id="${branch.branchCode}">Edit</button>
                                                        <button class="btn btn-danger btn-action btn-sm delete-branch-btn ms-1" data-id="${branch.branchCode}">Delete</button>
                                                    `
                                        : ''
                                    }
                                            </li>
                                        `).join('')}
                                    </ul>
                                </div>
                            `;
                        } else {
                            branchesHtml = `
                                <div class="ms-3">
                                    <div>
                                        <b>Branches:</b>
                                        ${isSuperUser
                                    ? `<button class="btn btn-success btn-sm ms-2 add-branch-under-center-btn" data-centercode="${center.centerCode}">Add Branch</button>`
                                    : ''
                                }
                                    </div>
                                    <div class="text-muted ms-2">No branches found.</div>
                                </div>
                            `;
                        }
                        return `
                            <div class="mb-3 border p-2 rounded">
                                <div>
                                    <b>${center.centerName}</b>
                                    <span> | Address: ${center.centerAddress || ''}</span>
                                    <span> | Phone: ${center.centerPhone}</span>
                                    ${editable
                                ? `
                                            <button class="btn btn-warning btn-action edit-center-btn ms-2" data-id="${center.centerCode}">Edit</button>
                                            <button class="btn btn-danger btn-action delete-center-btn ms-1" data-id="${center.centerCode}">Delete</button>
                                        `
                                : ''
                            }
                                </div>
                                ${branchesHtml}
                            </div>
                        `;
                    });
                });

                Promise.all(centerHtmlPromises).then(centerHtmlArr => {
                    $('#centers-list').html(`<div class="plans-grid">${centerHtmlArr.join('')}</div>`);
                });
            }
        });
    }

    // --- Add Branch under specific center (superuser only) ---
    $(document).on('click', '.add-branch-under-center-btn', function () {
        if (!isSuperUser) return;
        let centerCode = $(this).data('centercode');
        $('#branchForm')[0].reset();
        $('#branchCenter').empty();
        // Pre-select and lock the center dropdown to this center
        $.ajax({
            url: '/Management/GetCentersByRoot?rootCode=' + selectedRootCode,
            method: 'GET',
            success: function (centers) {
                $.each(centers, function (i, center) {
                    $('#branchCenter').append(
                        $('<option>', { value: center.centerCode, text: center.centerName })
                    );
                });
                $('#branchCenter').val(centerCode);
                $('#branchCenter').prop('disabled', true);
                $('#addBranchModal').modal('show');
            }
        });
    });

    // --- Add Branch (superuser only) ---
    $(document).on('click', '#openAddBranch', function () {
        if (!isSuperUser) return;
        $('#branchForm')[0].reset();
        $('#branchCenter').empty();
        $('#branchCenter').prop('disabled', false);
        $.ajax({
            url: '/Management/GetCentersByRoot?rootCode=' + selectedRootCode,
            method: 'GET',
            success: function (centers) {
                $.each(centers, function (i, center) {
                    $('#branchCenter').append(
                        $('<option>', { value: center.centerCode, text: center.centerName })
                    );
                });
                $('#addBranchModal').modal('show');
            }
        });
    });

    $('#branchForm').on('submit', function (e) {
        e.preventDefault();
        if (!isSuperUser) return;
        var branchData = {
            BranchName: $('#branchName').val(),
            Address: $('#branchAddress').val(),
            Phone: $('#branchPhone').val(),
            StartTime: $('#branchStartTime').val(),
            CenterCode: $('#branchCenter').val(),
            InsertUser: loggedInUserCode,
            RootCode: selectedRootCode,
            IsActive: true
        };
        $.ajax({
            url: '/Management/AddBranch',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(branchData),
            success: function (res) {
                $('#addBranchModal').modal('hide');
                if (selectedRootIsCenter) {
                    loadBranches(selectedRootCode);
                } else {
                    loadCentersWithBranches(selectedRootCode);
                }
                alert(res.message);
            }
        });
    });

    // Edit Branch (superuser only)
    $(document).on('click', '.edit-branch-btn', function () {
        if (!isSuperUser) return;
        let branchId = $(this).data('id');
        let card = $(this).closest('li,div');
        $('#editBranchCode').val(branchId);
        $('#editBranchName').val(card.find('b').first().text());
        let addressText = card.find('span').filter(function () { return $(this).text().indexOf("Address:") !== -1; }).text().replace("Address: ", "");
        let phoneText = card.find('span').filter(function () { return $(this).text().indexOf("Phone:") !== -1; }).text().replace("Phone: ", "");
        let stimeText = card.find('span').filter(function () { return $(this).text().indexOf("Start Time:") !== -1; }).text().replace("Start Time: ", "");
        $('#editBranchAddress').val(addressText);
        $('#editBranchPhone').val(phoneText);
        $('#editBranchStartTime').val(stimeText);
        $('#editBranchModal').modal('show');
    });

    $('#editBranchForm').on('submit', function (e) {
        e.preventDefault();
        if (!isSuperUser) return;
        var branchEdit = {
            BranchCode: $('#editBranchCode').val(),
            BranchName: $('#editBranchName').val(),
            Address: $('#editBranchAddress').val(),
            Phone: $('#editBranchPhone').val(),
            StartTime: $('#editBranchStartTime').val()
        };
        $.ajax({
            url: '/Management/EditBranch',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(branchEdit),
            success: function (res) {
                $('#editBranchModal').modal('hide');
                if (selectedRootIsCenter) {
                    loadBranches(selectedRootCode);
                } else {
                    loadCentersWithBranches(selectedRootCode);
                }
                alert(res.message);
            }
        });
    });

    // Delete Branch (superuser only)
    $(document).on('click', '.delete-branch-btn', function () {
        if (!isSuperUser) return;
        if (!confirm('Are you sure you want to delete this branch?')) return;
        let branchId = $(this).data('id');
        $.ajax({
            url: '/Management/DeleteBranch',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(branchId),
            success: function (res) {
                if (selectedRootIsCenter) {
                    loadBranches(selectedRootCode);
                } else {
                    loadCentersWithBranches(selectedRootCode);
                }
                alert(res.message);
            }
        });
    });

    // --- Teachers (always rendered for all roots!) ---
    function loadTeachers(rootCode) {
        $.ajax({
            url: '/Management/GetTeachersByRoot?rootCode=' + rootCode,
            method: 'GET',
            success: function (teachers) {
                let html = teachers.map(teacher => `
                <div class="mb-2">
                    <div><b>${teacher.teacherName}</b></div>
                    <div>Phone: ${teacher.teacherPhone}</div>
                    <div>Address: ${teacher.teacherAddress || ''}</div>
                    <div>Status: ${teacher.isActive ? 'Active' : 'Inactive'}</div>
                    <div class="mt-2">
                        <button class="btn btn-info btn-action show-subjects-btn" data-teacher="${teacher.teacherCode}">Show Subjects</button>
                        <button class="btn btn-success btn-action ms-1 add-teachsubject-btn" data-teacher="${teacher.teacherCode}">Add Teaching Subject</button>
                        <button class="btn btn-danger btn-action ms-1 delete-teacher-btn" data-teacher="${teacher.teacherCode}">Delete</button>
                    </div>
                    <div class="mt-2 subjects-container" id="subjects-for-teacher-${teacher.teacherCode}" style="display:none;"></div>
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
        if (!selectedRootCode) {
            alert("Please select a root first.");
            return;
        }
        var teacherData = {
            TeacherName: $('#teacherName').val(),
            TeacherPhone: $('#teacherPhone').val(),
            TeacherAddress: $('#teacherAddress').val(),
            RootCode: selectedRootCode,
            InsertUser: loggedInUserCode
        };
        $.ajax({
            url: '/Management/AddTeacher',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(teacherData),
            success: function (res) {
                $('#addTeacherModal').modal('hide');
                loadTeachers(selectedRootCode);
                alert(res.message);
            },
            error: function (xhr) {
                alert("Failed to add teacher: " + xhr.responseText);
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
            url: `/Management/GetSubjectsByTeacher?teacherCode=${teacherCode}&rootCode=${selectedRootCode}`,
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
            url: '/Management/GetYearsByRoot?rootCode=' + selectedRootCode,
            type: 'GET',
            success: function (years) {
                let $yearSelect = $('#yearSelect');
                $yearSelect.empty();
                for (let year of years) {
                    $yearSelect.append($('<option>', { value: year.yearCode, text: year.yearName }));
                }
                // Set educational year display
                $.ajax({
                    url: '/Management/GetActiveEduYearByRoot?rootCode=' + selectedRootCode,
                    type: 'GET',
                    success: function (activeYear) {
                        $('#activeYearDisplay').val(activeYear.eduYearName || "No educational year");
                        $('#activeYearCode').val(activeYear.yearCode || "");
                        if (activeYear.yearCode) $yearSelect.val(activeYear.yearCode);
                        // Now load branches
                        $.ajax({
                            url: '/Management/GetBranchesForRootWithCenters?rootCode=' + selectedRootCode,
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
        if (!subjectName || !branchCode || !currentTeacherCode || !selectedRootCode || !yearCode) {
            alert('Please fill all fields');
            return;
        }
        let data = {
            SubjectName: subjectName,
            IsPrimary: isPrimary,
            RootCode: selectedRootCode,
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
                loadTeachers(selectedRootCode);
                alert(res.message);
            }
        });
    });

    // --- Add Center (superuser only) ---
    $(document).on('click', '#openAddCenter', function () {
        if (!isSuperUser) return;
        $('#centerForm')[0].reset();
        $('#addCenterModal').modal('show');
    });

    $('#centerForm').on('submit', function (e) {
        e.preventDefault();
        if (!isSuperUser) return;
        var centerData = {
            CenterName: $('#centerName').val(),
            CenterPhone: $('#centerPhone').val(),
            CenterAddress: $('#centerAddress').val(),
            InsertUser: loggedInUserCode,
            RootCode: selectedRootCode
        };
        $.ajax({
            url: '/Management/AddCenter',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(centerData),
            success: function (res) {
                loadCentersWithBranches(selectedRootCode);
                $('#addCenterModal').modal('hide');
                alert(res.message);
            }
        });
    });

    // Edit Center (superuser only)
    $(document).on('click', '.edit-center-btn', function () {
        if (!isSuperUser) return;
        let centerId = $(this).data('id');
        let card = $(this).closest('div');
        $('#editCenterCode').val(centerId);
        $('#editCenterName').val(card.find('b').first().text());
        $('#editCenterPhone').val(card.find('span').filter(function () { return $(this).text().indexOf("Phone:") !== -1; }).text().replace("Phone: ", ""));
        $('#editCenterAddress').val(card.find('span').filter(function () { return $(this).text().indexOf("Address:") !== -1; }).text().replace("Address: ", ""));
        $('#editCenterModal').modal('show');
    });

    $('#editCenterForm').on('submit', function (e) {
        e.preventDefault();
        if (!isSuperUser) return;
        var centerEdit = {
            CenterCode: $('#editCenterCode').val(),
            CenterName: $('#editCenterName').val(),
            CenterPhone: $('#editCenterPhone').val(),
            CenterAddress: $('#editCenterAddress').val()
        };
        $.ajax({
            url: '/Management/EditCenter',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(centerEdit),
            success: function (res) {
                $('#editCenterModal').modal('hide');
                loadCentersWithBranches(selectedRootCode);
                alert(res.message);
            }
        });
    });

    // Delete Center (superuser only)
    $(document).on('click', '.delete-center-btn', function () {
        if (!isSuperUser) return;
        if (!confirm('Are you sure you want to delete this center?')) return;
        let centerId = $(this).data('id');
        $.ajax({
            url: '/Management/DeleteCenter',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(centerId),
            success: function (res) {
                loadCentersWithBranches(selectedRootCode);
                alert(res.message);
            }
        });
    });
});