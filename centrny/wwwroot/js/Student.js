$(document).ready(function () {
    let currentPage = 1;
    let pageSize = 10;
    let totalCount = 0;

    function formatTime(dateStr) {
        if (!dateStr) return '';
        let dt = new Date(dateStr);
        if (!isNaN(dt)) {
            return dt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }
        if (dateStr.length === 8 && dateStr[2] === ':' && dateStr[5] === ':') {
            return dateStr.substring(0, 5);
        }
        return dateStr;
    }

    function loadFilters() {
        $.get('/StudentLearn/GetFilterOptions', function (res) {
            $('#yearFilter').empty().append($('<option>', { value: '', text: 'All Years' }));
            $('#subjectFilter').empty().append($('<option>', { value: '', text: 'All Subjects' }));
            $('#studentFilter').empty().append($('<option>', { value: '', text: 'All Students' }));

            res.years.forEach(function (y) {
                $('#yearFilter').append($('<option>', { value: y.yearCode, text: y.yearName }));
            });
            res.subjects.forEach(function (s) {
                $('#subjectFilter').append($('<option>', { value: s.subjectCode, text: s.subjectName }));
            });
            res.students.forEach(function (s) {
                $('#studentFilter').append($('<option>', { value: s.studentCode, text: s.studentName }));
            });
        });
    }

    function loadTable(page = 1) {
        let yearCode = $('#yearFilter').val();
        let subjectCode = $('#subjectFilter').val();
        let studentCode = $('#studentFilter').val();
        let search = $('#searchInput').val();

        $.get('/StudentLearn/GetGroupedLearnData', {
            page: page,
            pageSize: pageSize,
            yearCode: yearCode || null,
            subjectCode: subjectCode || null,
            studentCode: studentCode || null,
            search: search || null
        }, function (res) {
            totalCount = res.totalCount;
            renderGroupedTable(res.data);
            renderPagination(page, Math.ceil(totalCount / pageSize));
        });
    }

    function renderGroupedTable(data) {
        let html = '<div class="table-card w-100 p-0" style="box-shadow: none;">';
        html += '<table class="gradient-table">';
        html += `<thead><tr>
            <th>#</th>
            <th>Student Code</th>
            <th>Student</th>
            <th>Year</th>
            <th>Subject</th>
            <th>Teacher</th>
            <th>Branch</th>
            <th>Schedule</th>
            <th>Online</th>
            <th>Active</th>
            <th>Actions</th>
        </tr></thead><tbody>`;
        if (data.length === 0) {
            html += '<tr><td colspan="11" class="text-center">No records found.</td></tr>';
        } else {
            data.forEach(function (student, idx) {
                if (student.subjects && student.subjects.length > 0) {
                    student.subjects.forEach(function (subj, sIdx) {
                        html += '<tr>';
                        if (sIdx === 0) {
                            html += `<td rowspan="${student.subjects.length}">${idx + 1 + ((currentPage - 1) * pageSize)}</td>`;
                            html += `<td rowspan="${student.subjects.length}">${student.studentCode || ''}</td>`;
                            html += `<td rowspan="${student.subjects.length}" class="bold-rendered">${student.studentName || ''}</td>`;
                            html += `<td rowspan="${student.subjects.length}">${student.yearName || ''}</td>`;
                        }
                        // Subject as plain black text if subject is philosophy (any case), otherwise badge
                        let subjText = (subj.subjectName && subj.subjectName.toLowerCase().includes("philosophy"))
                            ? `<span class="subject-text">${subj.subjectName}</span>`
                            : `<span class="badge">${subj.subjectName || ''}</span>`;
                        html += `<td>${subjText}</td>`;
                        html += `<td style="font-weight: 600; color: var(--text-muted);">${subj.teacherName || ''}</td>`;
                        html += `<td style="color: var(--text-muted);">${subj.branchName || ''}</td>`;
                        html += `<td style="color: var(--text-dark);">${subj.scheduleDay || ''} ${formatTime(subj.scheduleStart)}-${formatTime(subj.scheduleEnd)}</td>`;

                        if (sIdx === 0) {
                            html += `<td rowspan="${student.subjects.length}">${student.isOnline ? "Yes" : "No"}</td>`;
                            html += `<td rowspan="${student.subjects.length}">${student.isActive ? "Yes" : "No"}</td>`;
                            // Actions cell: only once per student
                            html += `<td rowspan="${student.subjects.length}">`;
                            // Add Subject button
                            html += `<button class="modern-btn add addSubjectBtn" style="background: var(--primary-gradient) !important; margin-bottom:6px;" data-studentcode="${student.studentCode}" data-yearcode="${student.yearCode}">
                                        <i class="fas fa-plus"></i> Add Subject
                                    </button>`;
                            // Change time for all subjects (one button per student)
                            html += `<br><button class="modern-btn edit-btn editLearnAllBtn" style="background: linear-gradient(135deg, #55a3ff 0%, #00b894 100%) !important; padding: 0.3em 1em; font-size: 0.95em; margin-top:3px;"
                                data-studentcode="${student.studentCode}" 
                                data-yearcode="${student.yearCode}">
                                    <i class="fas fa-clock"></i> Change time
                            </button>`;
                            html += `</td>`;
                        }
                        html += '</tr>';
                    });
                } else {
                    html += '<tr>';
                    html += `<td>${idx + 1 + ((currentPage - 1) * pageSize)}</td>`;
                    html += `<td>${student.studentCode || ''}</td>`;
                    html += `<td class="bold-rendered">${student.studentName || ''}</td>`;
                    html += `<td>${student.yearName || ''}</td>`;
                    html += '<td colspan="4" class="text-center text-muted">No subjects</td>';
                    html += `<td>${student.isOnline ? "Yes" : "No"}</td>`;
                    html += `<td>${student.isActive ? "Yes" : "No"}</td>`;
                    html += `<td>
                        <button class="modern-btn add addSubjectBtn" style="background: var(--primary-gradient) !important;" data-studentcode="${student.studentCode}" data-yearcode="${student.yearCode}">
                            <i class="fas fa-plus"></i> Add Subject
                        </button>
                    </td>`;
                    html += '</tr>';
                }
            });
        }
        html += '</tbody></table></div>';
        $('#learnTableContainer').html(html);
    }

    function renderPagination(current, total) {
        let html = '';
        if (total <= 1) {
            $('#learnPagination').html('');
            return;
        }
        for (let i = 1; i <= total; i++) {
            html += `<li class="page-item ${i === current ? 'active' : ''}"><a class="page-link" href="#">${i}</a></li>`;
        }
        $('#learnPagination').html(html);
    }

    $('#learnPagination').on('click', 'a', function (e) {
        e.preventDefault();
        let page = parseInt($(this).text());
        if (page !== currentPage) {
            currentPage = page;
            loadTable(currentPage);
        }
    });

    $('#searchBtn').click(function () {
        currentPage = 1;
        loadTable(currentPage);
    });

    $('#yearFilter, #subjectFilter, #studentFilter').change(function () {
        currentPage = 1;
        loadTable(currentPage);
    });

    $('#searchInput').keypress(function (e) {
        if (e.which === 13) {
            currentPage = 1;
            loadTable(currentPage);
        }
    });

    // Add Subject Modal logic
    $(document).on('click', '.addSubjectBtn', function () {
        let studentCode = $(this).data('studentcode');
        let yearCode = $(this).data('yearcode');
        openAddSubjectModal(studentCode, yearCode);
    });

    function openAddSubjectModal(studentCode, yearCode) {
        $.get('/StudentLearn/GetAddLearnFormData', { studentCode: studentCode, yearCode: yearCode }, function (res) {
            $('#addSubjectModal input[name=StudentCode]').val(studentCode);
            $('#addSubjectModal input[name=YearCode]').val(yearCode);
            $('#addSubjectModal input[name=EduYearCode]').val(res.eduYearCode);
            $('#addSubjectModal input[name=RootCode]').val(res.rootCode);

            let $subject = $('#addSubjectModal select[name=SubjectCode]');
            $subject.empty().append($('<option>', { value: '', text: 'Choose Subject' }));
            res.subjects.forEach(function (s) {
                $subject.append($('<option>', { value: s.subjectCode, text: s.subjectName }));
            });

            $('#addSubjectModal select[name=TeacherCode]').empty().append($('<option>', { value: '', text: 'Choose Teacher' }));
            $('#addSubjectModal select[name=BranchCode]').empty().append($('<option>', { value: '', text: 'Choose Branch' }));
            $('#addSubjectModal select[name=ScheduleCode]').empty().append($('<option>', { value: '', text: 'Choose Schedule' }));

            $('#addSubjectModal input[name=IsOnline]').prop('checked', false);

            $('#addSubjectModal').modal('show');
        });
    }

    $('#addSubjectModal').on('change', 'select[name=SubjectCode]', function () {
        let subjectCode = $(this).val();
        let studentCode = $('#addSubjectModal input[name=StudentCode]').val();
        let yearCode = $('#addSubjectModal input[name=YearCode]').val();

        $.get('/StudentLearn/GetTeachersForSubject', { subjectCode: subjectCode, yearCode: yearCode }, function (res) {
            let $teacher = $('#addSubjectModal select[name=TeacherCode]');
            $teacher.empty().append($('<option>', { value: '', text: 'Choose Teacher' }));
            res.teachers.forEach(function (t) {
                $teacher.append($('<option>', { value: t.teacherCode, text: t.teacherName }));
            });
            $('#addSubjectModal select[name=BranchCode]').empty().append($('<option>', { value: '', text: 'Choose Branch' }));
            $('#addSubjectModal select[name=ScheduleCode]').empty().append($('<option>', { value: '', text: 'Choose Schedule' }));
        });
    });

    $('#addSubjectModal').on('change', 'select[name=TeacherCode]', function () {
        let subjectCode = $('#addSubjectModal select[name=SubjectCode]').val();
        let teacherCode = $(this).val();
        let yearCode = $('#addSubjectModal input[name=YearCode]').val();

        $.get('/StudentLearn/GetBranchesForSubjectTeacher', { subjectCode: subjectCode, teacherCode: teacherCode, yearCode: yearCode }, function (res) {
            let $branch = $('#addSubjectModal select[name=BranchCode]');
            $branch.empty().append($('<option>', { value: '', text: 'Choose Branch' }));
            res.branches.forEach(function (b) {
                $branch.append($('<option>', { value: b.branchCode, text: b.branchName }));
            });
            $('#addSubjectModal select[name=ScheduleCode]').empty().append($('<option>', { value: '', text: 'Choose Schedule' }));
        });
    });

    $('#addSubjectModal').on('change', 'select[name=BranchCode]', function () {
        let subjectCode = $('#addSubjectModal select[name=SubjectCode]').val();
        let teacherCode = $('#addSubjectModal select[name=TeacherCode]').val();
        let branchCode = $(this).val();

        $.get('/StudentLearn/GetSchedulesForSubjectTeacherBranch', { subjectCode: subjectCode, teacherCode: teacherCode, branchCode: branchCode }, function (res) {
            let $schedule = $('#addSubjectModal select[name=ScheduleCode]');
            $schedule.empty().append($('<option>', { value: '', text: 'Choose Schedule' }));
            res.schedules.forEach(function (s) {
                $schedule.append($('<option>', {
                    value: s.scheduleCode,
                    text: (s.dayOfWeek || '') + " " + formatTime(s.startTime) + "-" + formatTime(s.endTime)
                }));
            });
        });
    });

    $('#addLearnForm').submit(function (e) {
        e.preventDefault();
        let formData = $(this).serialize();
        $.post('/StudentLearn/AddLearn', formData, function (res) {
            if (res.success) {
                $('#addSubjectModal').modal('hide');
                loadTable(currentPage);
            } else {
                alert(res.message || "Failed to add subject.");
            }
        });
    });

    // --- Edit Learn Logic For ALL subjects of a student in current year ---
    $(document).on('click', '.editLearnAllBtn', function () {
        let studentCode = $(this).data('studentcode');
        let yearCode = $(this).data('yearcode');
        // Fetch all subjects and their schedules for this student in this year
        $.get('/StudentLearn/GetStudentSubjectsForYear', { studentCode, yearCode }, function (res) {
            let html = '<form id="editAllSchedulesForm">';
            html += '<input type="hidden" name="StudentCode" value="' + studentCode + '"/>';
            html += '<input type="hidden" name="YearCode" value="' + yearCode + '"/>';
            html += '<table class="table table-bordered"><thead><tr><th>Subject</th><th>Current Schedule</th><th>New Schedule</th></tr></thead><tbody>';
            (res.subjects || []).forEach(function (subj) {
                html += '<tr>';
                html += '<td>' + subj.subjectName + '<input type="hidden" name="SubjectCodes[]" value="' + subj.subjectCode + '"/></td>';
                html += '<td>' + (subj.currentScheduleName || '') + '</td>';
                html += '<td><select name="NewScheduleCodes[]">';
                (subj.availableSchedules || []).forEach(function (sched) {
                    html += '<option value="' + sched.scheduleCode + '"' +
                        (sched.scheduleCode === subj.currentScheduleCode ? ' selected' : '') + '>' +
                        sched.scheduleName + '</option>';
                });
                html += '</select></td>';
                html += '</tr>';
            });
            html += '</tbody></table>';
            html += '<button type="submit" class="modern-btn edit-btn">Save</button>';
            html += '</form>';
            $('#editLearnModalBody').html(html);
            $('#editLearnModal').modal('show');
        });
    });

    $(document).on('submit', '#editAllSchedulesForm', function (e) {
        e.preventDefault();
        $.post('/StudentLearn/UpdateStudentSchedules', $(this).serialize(), function (res) {
            if (res.success) {
                $('#editLearnModal').modal('hide');
                loadTable(currentPage);
            } else {
                alert(res.message || "Failed to update schedules.");
            }
        });
    });

    // --- Edit Learn Logic for a single subject (legacy, in case you need it) ---
    $(document).on('click', '.editLearnBtn', function () {
        let studentCode = $(this).data('studentcode');
        let subjectCode = $(this).data('subjectcode');
        let yearCode = $(this).data('yearcode');
        $.get('/StudentLearn/GetEditLearnFormData', { studentCode, subjectCode, yearCode }, function (res) {
            let html = '';
            html += `<input type="hidden" name="StudentCode" value="${studentCode}"/>
                     <input type="hidden" name="SubjectCode" value="${subjectCode}"/>
                     <input type="hidden" name="YearCode" value="${yearCode}"/>`;

            if (res.isCenter) {
                html += `<div class="form-group">
                    <label>Teacher</label>
                    <select name="TeacherCode" class="form-control modern-input styled-select" required>
                        ${res.teachers.map(t => `<option value="${t.teacherCode}" ${t.teacherCode == res.selectedTeacher ? 'selected' : ''}>${t.teacherName}</option>`).join('')}
                    </select>
                </div>
                <div class="form-group">
                    <label>Schedule</label>
                    <select name="ScheduleCode" class="form-control modern-input styled-select" required>
                        ${res.schedules.map(s => `<option value="${s.scheduleCode}" ${s.scheduleCode == res.selectedSchedule ? 'selected' : ''}>${(s.dayOfWeek || '')} ${formatTime(s.startTime)}-${formatTime(s.endTime)}</option>`).join('')}
                    </select>
                </div>`;
            } else {
                html += `<input type="hidden" name="TeacherCode" value="${res.selectedTeacher}"/>
                <div class="form-group">
                    <label>Schedule</label>
                    <select name="ScheduleCode" class="form-control modern-input styled-select" required>
                        ${res.schedules.map(s => `<option value="${s.scheduleCode}" ${s.scheduleCode == res.selectedSchedule ? 'selected' : ''}>${(s.dayOfWeek || '')} ${formatTime(s.startTime)}-${formatTime(s.endTime)}</option>`).join('')}
                    </select>
                </div>`;
            }

            $('#editLearnModalBody').html(html);
            $('#editLearnModal').modal('show');
        });
    });

    $('#editLearnModal').on('change', 'select[name=TeacherCode]', function () {
        let teacherCode = $(this).val();
        let subjectCode = $('#editLearnModal input[name=SubjectCode]').val();
        let yearCode = $('#editLearnModal input[name=YearCode]').val();
        $.get('/StudentLearn/GetSchedulesForSubjectTeacherBranch', { subjectCode, teacherCode, branchCode: null }, function (res) {
            let $schedule = $('#editLearnModal select[name=ScheduleCode]');
            $schedule.empty();
            res.schedules.forEach(function (s) {
                $schedule.append($('<option>', {
                    value: s.scheduleCode,
                    text: (s.dayOfWeek || '') + " " + formatTime(s.startTime) + "-" + formatTime(s.endTime)
                }));
            });
        });
    });

    $('#editLearnForm').submit(function (e) {
        e.preventDefault();
        let formData = $(this).serialize();
        $.post('/StudentLearn/EditLearn', formData, function (res) {
            if (res.success) {
                $('#editLearnModal').modal('hide');
                loadTable(currentPage);
            } else {
                alert(res.message || "Failed to update.");
            }
        });
    });

    loadFilters();
    loadTable(currentPage);

    // Expose loadTable globally for debugging or external calls
    window.loadTable = loadTable;
});