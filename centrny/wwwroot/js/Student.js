
function getJsString(key) {
    const el = document.getElementById('js-localization');
    return el ? (el.getAttribute('data-' + key) || '') : '';
}

function localizeFiltersAndButtons() {
    $('#yearFilter').empty().append($('<option>', { value: '', text: getJsString('all-years') }));
    $('#subjectFilter').empty().append($('<option>', { value: '', text: getJsString('all-subjects') }));
    $('#searchBtn').text(getJsString('search'));
    $('#searchInput').attr('placeholder', getJsString('search-placeholder'));
}

/* ---------------- Main Script ---------------- */
$(function () {

    let currentPage = 1;
    let pageSize = 10;
    let totalCount = 0;
    let studentSearchTimeout = null;
    let lastStudentSearchVal = '';

    localizeFiltersAndButtons();

    /* ---------- Utilities ---------- */
    function formatTime(t) {
        if (!t) return '';
        // Accepts "HH:mm:ss", "HH:mm", ISO or Date
        if (typeof t === 'string') {
            // Already HH:mm
            if (/^\d{2}:\d{2}$/.test(t)) return t;
            // HH:mm:ss
            if (/^\d{2}:\d{2}:\d{2}$/.test(t)) return t.substring(0, 5);
            // Try parse date
            const d = new Date(t);
            if (!isNaN(d.getTime())) {
                return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            }
            return t;
        }
        if (t instanceof Date && !isNaN(t.getTime())) {
            return t.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }
        return '';
    }

    function logAjaxError(prefix, xhr) {
        console.error(prefix + ' | status:', xhr.status, 'response:', xhr.responseText);
    }

    /* ---------- Filters ---------- */
    function loadFilters() {
        $.get('/StudentLearn/GetFilterOptions')
            .done(function (res) {
                $('#yearFilter').empty().append($('<option>', { value: '', text: getJsString('all-years') }));
                $('#subjectFilter').empty().append($('<option>', { value: '', text: getJsString('all-subjects') }));
                (res.years || []).forEach(y =>
                    $('#yearFilter').append($('<option>', { value: y.yearCode, text: y.yearName }))
                );
                (res.subjects || []).forEach(s =>
                    $('#subjectFilter').append($('<option>', { value: s.subjectCode, text: s.subjectName }))
                );
            })
            .fail(xhr => logAjaxError('[Filters] Load failed', xhr));
    }

    /* ---------- Data Table (Custom – not DataTables) ---------- */
    function loadTable(page = 1) {
        const yearCode = $('#yearFilter').val();
        const subjectCode = $('#subjectFilter').val();
        const search = $('#searchInput').val();

        $.get('/StudentLearn/GetGroupedLearnData', {
            page: page,
            pageSize: pageSize,
            yearCode: yearCode || null,
            subjectCode: subjectCode || null,
            search: search || null
        })
            .done(function (res) {
                totalCount = res.totalCount || 0;
                renderGroupedTable(res.data || []);
                renderPagination(page, Math.ceil(totalCount / pageSize));
            })
            .fail(xhr => logAjaxError('[Learn] GetGroupedLearnData failed', xhr));
    }

    function renderGroupedTable(data) {
        let html = '<div class="table-card w-100 p-0" style="box-shadow:none;">';
        // RESPONSIVE WRAPPER (only new part)
        html += '<div class="learn-responsive-wrapper">';
        html += '<table class="gradient-table">';
        html += `<thead><tr>
            <th>#</th>
            <th>${getJsString('student')}</th>
            <th>${getJsString('year')}</th>
            <th>${getJsString('subject')}</th>
            <th>${getJsString('teacher')}</th>
            <th>${getJsString('branch')}</th>
            <th>${getJsString('schedule')}</th>
            <th>${getJsString('online')}</th>
            <th>${getJsString('active')}</th>
            <th>${getJsString('actions')}</th>
        </tr></thead><tbody>`;

        if (!data.length) {
            html += `<tr><td colspan="10" class="text-center">${getJsString('no-records')}</td></tr>`;
        } else {
            data.forEach((student, idx) => {
                const rowCount = (student.subjects || []).length;
                if (rowCount) {
                    student.subjects.forEach((subj, sIdx) => {
                        html += '<tr>';
                        if (sIdx === 0) {
                            html += `<td rowspan="${rowCount}">${idx + 1 + ((currentPage - 1) * pageSize)}</td>`;
                            html += `<td rowspan="${rowCount}" class="bold-rendered">${student.studentName || ''}</td>`;
                            html += `<td rowspan="${rowCount}">${student.yearName || ''}</td>`;
                        }

                        const subjText = (subj.subjectName && subj.subjectName.toLowerCase().includes('philosophy'))
                            ? `<span class="subject-text">${subj.subjectName}</span>`
                            : `<span class="badge">${subj.subjectName || ''}</span>`;

                        html += `<td>${subjText}</td>`;
                        html += `<td style="font-weight:600;color:var(--text-muted);">${subj.teacherName || ''}</td>`;
                        html += `<td style="color:var(--text-muted);">${subj.branchName || ''}</td>`;
                        html += `<td style="color:var(--text-dark);">${subj.scheduleDay || ''} ${formatTime(subj.scheduleStart)}-${formatTime(subj.scheduleEnd)}</td>`;

                        if (sIdx === 0) {
                            html += `<td rowspan="${rowCount}">${student.isOnline ? getJsString('yes') : getJsString('no')}</td>`;
                            html += `<td rowspan="${rowCount}">${student.isActive ? getJsString('yes') : getJsString('no')}</td>`;
                            html += `<td rowspan="${rowCount}">
                                <button class="modern-btn add addSubjectBtn"
                                        style="background: var(--primary-gradient) !important; margin-bottom:6px;"
                                        data-studentcode="${student.studentCode}"
                                        data-yearcode="${student.yearCode}">
                                    <i class="fas fa-plus"></i> ${getJsString('add-subject')}
                                </button><br>
                                <button class="modern-btn edit-btn editLearnAllBtn"
                                        style="background:linear-gradient(135deg,#55a3ff 0%,#00b894 100%) !important; padding:.3em 1em; font-size:.95em; margin-top:3px;"
                                        data-studentcode="${student.studentCode}"
                                        data-yearcode="${student.yearCode}">
                                    <i class="fas fa-clock"></i> ${getJsString('change-time')}
                                </button>
                            </td>`;
                        }
                        html += '</tr>';
                    });
                } else {
                    html += '<tr>';
                    html += `<td>${idx + 1 + ((currentPage - 1) * pageSize)}</td>`;
                    html += `<td class="bold-rendered">${student.studentName || ''}</td>`;
                    html += `<td>${student.yearName || ''}</td>`;
                    html += `<td colspan="4" class="text-center text-muted">${getJsString('no-subjects')}</td>`;
                    html += `<td>${student.isOnline ? getJsString('yes') : getJsString('no')}</td>`;
                    html += `<td>${student.isActive ? getJsString('yes') : getJsString('no')}</td>`;
                    html += `<td>
                        <button class="modern-btn add addSubjectBtn"
                                style="background: var(--primary-gradient) !important;"
                                data-studentcode="${student.studentCode}"
                                data-yearcode="${student.yearCode}">
                            <i class="fas fa-plus"></i> ${getJsString('add-subject')}
                        </button>
                    </td>`;
                    html += '</tr>';
                }
            });
        }

        html += '</tbody></table></div></div>';
        $('#learnTableContainer').html(html);
    }

    function renderPagination(current, total) {
        if (total <= 1) {
            $('#learnPagination').html('');
            return;
        }
        let html = '';
        for (let i = 1; i <= total; i++) {
            html += `<li class="page-item ${i === current ? 'active' : ''}">
                        <a class="page-link" href="#" data-page="${i}">${i}</a>
                     </li>`;
        }
        $('#learnPagination').html(html);
    }

    /* ---------- Pagination Events ---------- */
    $('#learnPagination').on('click', 'a', function (e) {
        e.preventDefault();
        const p = parseInt($(this).data('page'), 10);
        if (p && p !== currentPage) {
            currentPage = p;
            loadTable(currentPage);
        }
    });

    /* ---------- Search & Filter Events ---------- */
    $('#searchBtn').on('click', function () {
        currentPage = 1;
        loadTable(currentPage);
    });

    $('#yearFilter, #subjectFilter').on('change', function () {
        currentPage = 1;
        loadTable(currentPage);
    });

    /* ---------- Autocomplete Student Search ---------- */
    $('#studentFilter').parent().remove(); // remove legacy if exists

    $('#searchInput').on('input', function () {
        const val = $(this).val();
        if (val === lastStudentSearchVal) return;
        lastStudentSearchVal = val;

        clearTimeout(studentSearchTimeout);
        if (!val) {
            $('#studentSearchDropdown').remove();
            return;
        }

        studentSearchTimeout = setTimeout(function () {
            $.get('/StudentLearn/SearchStudents', { term: val })
                .done(function (students) {
                    $('#studentSearchDropdown').remove();
                    if (students && students.length) {
                        const dd = $('<div id="studentSearchDropdown" class="autocomplete-dropdown"></div>');
                        students.forEach(stu => {
                            dd.append(`<div class="autocomplete-item" data-code="${stu.studentCode}">${stu.studentName}</div>`);
                        });
                        const off = $('#searchInput').offset();
                        dd.css({
                            position: 'absolute',
                            left: off.left,
                            top: off.top + $('#searchInput').outerHeight(),
                            width: $('#searchInput').outerWidth(),
                            'z-index': 10000,
                            background: '#fff',
                            border: '1px solid #ccc',
                            'max-height': '220px',
                            overflow: 'auto'
                        });
                        $('body').append(dd);
                    }
                })
                .fail(xhr => logAjaxError('[SearchStudents] failed', xhr));
        }, 250);
    });

    $(document).on('click', '.autocomplete-item', function () {
        $('#searchInput').val($(this).text());
        $('#studentSearchDropdown').remove();
        currentPage = 1;
        loadTable(currentPage);
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('#searchInput, #studentSearchDropdown').length) {
            $('#studentSearchDropdown').remove();
        }
    });

    $('#searchInput').on('keypress', function (e) {
        if (e.which === 13) {
            $('#studentSearchDropdown').remove();
            currentPage = 1;
            loadTable(currentPage);
        }
    });

    /* ---------- Add Subject Flow ---------- */
    $(document).on('click', '.addSubjectBtn', function () {
        const studentCode = $(this).data('studentcode');
        const yearCode = $(this).data('yearcode');
        openAddSubjectModal(studentCode, yearCode);
    });

    function openAddSubjectModal(studentCode, yearCode) {
        $.get('/StudentLearn/GetAddLearnFormData', { studentCode, yearCode })
            .done(function (res) {
                $('#addSubjectModal input[name=StudentCode]').val(studentCode);
                $('#addSubjectModal input[name=YearCode]').val(yearCode);
                $('#addSubjectModal input[name=EduYearCode]').val(res.eduYearCode);
                $('#addSubjectModal input[name=RootCode]').val(res.rootCode);

                const $sub = $('#addSubjectModal select[name=SubjectCode]');
                $sub.empty().append($('<option>', { value: '', text: getJsString('choose-subject') }));
                (res.subjects || []).forEach(s => $sub.append($('<option>', { value: s.subjectCode, text: s.subjectName })));

                $('#addSubjectModal select[name=TeacherCode]').empty().append($('<option>', { value: '', text: getJsString('choose-teacher') }));
                $('#addSubjectModal select[name=BranchCode]').empty().append($('<option>', { value: '', text: getJsString('choose-branch') }));
                $('#addSubjectModal select[name=ScheduleCode]').empty().append($('<option>', { value: '', text: getJsString('choose-schedule') }));
                $('#addSubjectModal input[name=IsOnline]').prop('checked', false);

                $('#addSubjectModal').modal('show');
            })
            .fail(xhr => logAjaxError('[AddLearnFormData] failed', xhr));
    }

    $('#addSubjectModal').on('change', 'select[name=SubjectCode]', function () {
        const subjectCode = $(this).val();
        const yearCode = $('#addSubjectModal input[name=YearCode]').val();
        if (!subjectCode || !yearCode) return;

        $.get('/StudentLearn/GetTeachersForSubject', { subjectCode, yearCode })
            .done(function (res) {
                const $teacher = $('#addSubjectModal select[name=TeacherCode]');
                $teacher.empty().append($('<option>', { value: '', text: getJsString('choose-teacher') }));
                (res.teachers || []).forEach(t => $teacher.append($('<option>', { value: t.teacherCode, text: t.teacherName })));
                $('#addSubjectModal select[name=BranchCode]').empty().append($('<option>', { value: '', text: getJsString('choose-branch') }));
                $('#addSubjectModal select[name=ScheduleCode]').empty().append($('<option>', { value: '', text: getJsString('choose-schedule') }));
            })
            .fail(xhr => logAjaxError('[GetTeachersForSubject] failed', xhr));
    });

    $('#addSubjectModal').on('change', 'select[name=TeacherCode]', function () {
        const subjectCode = $('#addSubjectModal select[name=SubjectCode]').val();
        const teacherCode = $(this).val();
        const yearCode = $('#addSubjectModal input[name=YearCode]').val();
        if (!subjectCode || !teacherCode || !yearCode) return;

        $.get('/StudentLearn/GetBranchesForSubjectTeacher', { subjectCode, teacherCode, yearCode })
            .done(function (res) {
                const $branch = $('#addSubjectModal select[name=BranchCode]');
                $branch.empty().append($('<option>', { value: '', text: getJsString('choose-branch') }));
                (res.branches || []).forEach(b => $branch.append($('<option>', { value: b.branchCode, text: b.branchName })));
                $('#addSubjectModal select[name=ScheduleCode]').empty().append($('<option>', { value: '', text: getJsString('choose-schedule') }));
            })
            .fail(xhr => logAjaxError('[GetBranchesForSubjectTeacher] failed', xhr));
    });

    $('#addSubjectModal').on('change', 'select[name=BranchCode]', function () {
        const subjectCode = $('#addSubjectModal select[name=SubjectCode]').val();
        const teacherCode = $('#addSubjectModal select[name=TeacherCode]').val();
        const branchCode = $(this).val();
        if (!subjectCode || !teacherCode) return;

        $.get('/StudentLearn/GetSchedulesForSubjectTeacherBranch', { subjectCode, teacherCode, branchCode })
            .done(function (res) {
                const $sched = $('#addSubjectModal select[name=ScheduleCode]');
                $sched.empty().append($('<option>', { value: '', text: getJsString('choose-schedule') }));
                (res.schedules || []).forEach(s => {
                    const start = formatTime(s.startTime);
                    const end = formatTime(s.endTime);
                    $sched.append($('<option>', {
                        value: s.scheduleCode,
                        text: `${s.dayOfWeek || ''} ${start}-${end}`
                    }));
                });
            })
            .fail(xhr => logAjaxError('[GetSchedulesForSubjectTeacherBranch] failed', xhr));
    });

    $('#addLearnForm').on('submit', function (e) {
        e.preventDefault();
        $.post('/StudentLearn/AddLearn', $(this).serialize())
            .done(function (res) {
                if (res && res.success) {
                    $('#addSubjectModal').modal('hide');
                    loadTable(currentPage);
                } else {
                    alert((res && res.message) || getJsString('failed-to-add'));
                }
            })
            .fail(xhr => logAjaxError('[AddLearn] failed', xhr));
    });

    /* ---------- Bulk Edit ---------- */
    $(document).on('click', '.editLearnAllBtn', function () {
        const studentCode = $(this).data('studentcode');
        const yearCode = $(this).data('yearcode');
        $.get('/StudentLearn/GetStudentSubjectsForYear', { studentCode, yearCode })
            .done(function (res) {
                const subjects = res.subjects || [];
                let html = '<form id="editAllSchedulesForm">';
                html += `<input type="hidden" name="StudentCode" value="${studentCode}"/>`;
                html += `<input type="hidden" name="YearCode" value="${yearCode}"/>`;
                html += `<table class="table table-bordered"><thead><tr>
                            <th>${getJsString('subject')}</th>
                            <th>${getJsString('current-schedule') || 'Current'}</th>
                            <th>${getJsString('new-schedule') || 'New'}</th>
                         </tr></thead><tbody>`;
                subjects.forEach(subj => {
                    html += '<tr>';
                    html += `<td>${subj.subjectName}<input type="hidden" name="SubjectCodes[]" value="${subj.subjectCode}"/></td>`;
                    html += `<td>${subj.currentScheduleName || ''}</td>`;
                    html += `<td><select name="NewScheduleCodes[]">`;
                    (subj.availableSchedules || []).forEach(sch => {
                        html += `<option value="${sch.scheduleCode}" ${(sch.scheduleCode === subj.currentScheduleCode) ? 'selected' : ''}>${sch.scheduleName}</option>`;
                    });
                    html += `</select></td></tr>`;
                });
                html += '</tbody></table>';
                html += `<button type="submit" class="modern-btn edit-btn">${getJsString('save')}</button>`;
                html += '</form>';

                $('#editLearnModalBody').html(html);
                $('#editLearnModal').modal('show');
            })
            .fail(xhr => logAjaxError('[GetStudentSubjectsForYear] failed', xhr));
    });

    $(document).on('submit', '#editAllSchedulesForm', function (e) {
        e.preventDefault();
        $.post('/StudentLearn/UpdateStudentSchedules', $(this).serialize())
            .done(function (res) {
                if (res && res.success) {
                    $('#editLearnModal').modal('hide');
                    loadTable(currentPage);
                } else {
                    alert((res && res.message) || getJsString('failed-to-update'));
                }
            })
            .fail(xhr => logAjaxError('[UpdateStudentSchedules] failed', xhr));
    });

    /* ---------- Single Edit (Legacy) ---------- */
    $(document).on('click', '.editLearnBtn', function () {
        const studentCode = $(this).data('studentcode');
        const subjectCode = $(this).data('subjectcode');
        const yearCode = $(this).data('yearcode');

        $.get('/StudentLearn/GetEditLearnFormData', { studentCode, subjectCode, yearCode })
            .done(function (res) {
                if (res.success === false) {
                    alert(res.message || 'Not found');
                    return;
                }
                let html = '';
                html += `<input type="hidden" name="StudentCode" value="${studentCode}"/>`;
                html += `<input type="hidden" name="SubjectCode" value="${subjectCode}"/>`;
                html += `<input type="hidden" name="YearCode" value="${yearCode}"/>`;

                if (res.isCenter) {
                    html += `<div class="form-group mb-2">
                        <label>${getJsString('teacher')}</label>
                        <select name="TeacherCode" class="form-control modern-input styled-select" required>
                            ${(res.teachers || []).map(t => `<option value="${t.teacherCode}" ${t.teacherCode == res.selectedTeacher ? 'selected' : ''}>${t.teacherName}</option>`).join('')}
                        </select>
                    </div>`;
                } else {
                    // Provide hidden teacher if not center
                    html += `<input type="hidden" name="TeacherCode" value="${res.selectedTeacher}"/>`;
                }

                html += `<div class="form-group mb-2">
                    <label>${getJsString('schedule')}</label>
                    <select name="ScheduleCode" class="form-control modern-input styled-select" required>
                        ${(res.schedules || []).map(s =>
                    `<option value="${s.scheduleCode}" ${s.scheduleCode == res.selectedSchedule ? 'selected' : ''}>
                                ${(s.dayOfWeek || '')} ${formatTime(s.startTime)}-${formatTime(s.endTime)}
                             </option>`).join('')}
                    </select>
                </div>`;

                // Insert form wrapper to allow submit using existing hidden form
                html = `<form id="editLearnFormInner">${html}
                        <div class="text-end">
                           <button type="submit" class="modern-btn edit-btn" style="margin-top:8px;">
                               ${getJsString('save')}
                           </button>
                        </div>
                        </form>`;

                $('#editLearnModalBody').html(html);
                $('#editLearnModal').modal('show');
            })
            .fail(xhr => logAjaxError('[GetEditLearnFormData] failed', xhr));
    });

    // On-the-fly teacher change inside single edit: refresh schedules
    $(document).on('change', '#editLearnModalBody select[name=TeacherCode]', function () {
        const teacherCode = $(this).val();
        const subjectCode = $('#editLearnModalBody input[name=SubjectCode]').val();
        const yearCode = $('#editLearnModalBody input[name=YearCode]').val();
        if (!teacherCode || !subjectCode || !yearCode) return;

        $.get('/StudentLearn/GetSchedulesForSubjectTeacherBranch', {
            subjectCode,
            teacherCode,
            branchCode: null
        })
            .done(function (res) {
                const $sched = $('#editLearnModalBody select[name=ScheduleCode]');
                $sched.empty();
                (res.schedules || []).forEach(s => {
                    $sched.append($('<option>', {
                        value: s.scheduleCode,
                        text: `${s.dayOfWeek || ''} ${formatTime(s.startTime)}-${formatTime(s.endTime)}`
                    }));
                });
            })
            .fail(xhr => logAjaxError('[Teacher change -> GetSchedules] failed', xhr));
    });

    // Single edit submit
    $(document).on('submit', '#editLearnFormInner', function (e) {
        e.preventDefault();
        $.post('/StudentLearn/EditLearn', $(this).serialize())
            .done(function (res) {
                if (res && res.success) {
                    $('#editLearnModal').modal('hide');
                    loadTable(currentPage);
                } else {
                    alert((res && res.message) || getJsString('failed-to-update'));
                }
            })
            .fail(xhr => logAjaxError('[EditLearn] failed', xhr));
    });

    /* ---------- Initial Load ---------- */
    loadFilters();
    loadTable(currentPage);

    // Expose for debugging
    window.studentLearnReload = function () { loadTable(currentPage); };
});