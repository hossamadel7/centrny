/* Student Learn page script (jQuery-based) with safe Bootstrap modal helpers */
/* Works with Bootstrap 4 (jQuery plugin) and Bootstrap 5 (JS bundle API) */

'use strict';

/* ---------------- Ensure Bootstrap CSS exists and modal z-index is above theme ---------------- */
(function ensureBootstrapCssAndZIndex() {
    try {
        // If no bootstrap css is linked, inject it so .modal gets hidden by default and styled properly
        const hasBootstrapCss =
            document.querySelector('link[href*="bootstrap"][href$=".css"]') ||
            document.querySelector('link[href*="bootstrap.min.css"]');

        if (!hasBootstrapCss) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css';
            // Prepend so theme overrides can still win where intended
            document.head.prepend(link);
        }

        // Raise modal/backdrop z-index to sit over dashboard theme overlays
        if (!document.getElementById('student-learn-modal-zfix')) {
            const style = document.createElement('style');
            style.id = 'student-learn-modal-zfix';
            style.textContent = `
                .modal { z-index: 10850 !important; }
                .modal-backdrop { z-index: 10840 !important; }
            `;
            document.head.appendChild(style);
        }
    } catch (_) { }
})();

/* ---------------- Localization helpers ---------------- */
function getJsString(key) {
    const el = document.getElementById('js-localization');
    return el ? (el.getAttribute('data-' + key) || '') : '';
}

function localizeFiltersAndButtons($) {
    $('#yearFilter').empty().append($('<option>', { value: '', text: getJsString('all-years') }));
    $('#subjectFilter').empty().append($('<option>', { value: '', text: getJsString('all-subjects') }));
    $('#searchBtn').text(getJsString('search'));
    $('#searchInput').attr('placeholder', getJsString('search-placeholder'));
}

/* ---------------- Safe Bootstrap modal helpers (v4/v5) ---------------- */
function safeShowModalById(id) {
    try {
        const el = document.getElementById(id);
        if (!el) return;

        // Bootstrap 5 API path
        if (window.bootstrap && typeof window.bootstrap.Modal === 'function') {
            const Ctor = window.bootstrap.Modal;
            let instance = (typeof Ctor.getOrCreateInstance === 'function')
                ? Ctor.getOrCreateInstance(el)
                : (typeof Ctor.getInstance === 'function' ? Ctor.getInstance(el) : null);
            if (!instance) instance = new Ctor(el);
            if (instance && typeof instance.show === 'function') { instance.show(); return; }
        }

        // Bootstrap 4 jQuery plugin path
        if (typeof window.$ !== 'undefined' && typeof window.$(el).modal === 'function') {
            window.$(el).modal('show');
            return;
        }

        // Final fallback: toggle visibility (rudimentary) if no Bootstrap JS loaded
        el.style.display = 'block';
        document.body.classList.add('modal-open');
    } catch (e) {
        if (window.console && console.debug) console.debug('safeShowModalById error:', e);
    }
}

function safeHideModalById(id) {
    try {
        const el = document.getElementById(id);
        if (!el) return;

        // Bootstrap 5 API path
        if (window.bootstrap && typeof window.bootstrap.Modal === 'function') {
            const Ctor = window.bootstrap.Modal;
            let instance = (typeof Ctor.getOrCreateInstance === 'function')
                ? Ctor.getOrCreateInstance(el)
                : (typeof Ctor.getInstance === 'function' ? Ctor.getInstance(el) : null);
            if (!instance) instance = new Ctor(el);
            if (instance && typeof instance.hide === 'function') { instance.hide(); return; }
        }

        // Bootstrap 4 jQuery plugin path
        if (typeof window.$ !== 'undefined' && typeof window.$(el).modal === 'function') {
            window.$(el).modal('hide');
            return;
        }

        // Last resort
        el.style.display = 'none';
        document.body.classList.remove('modal-open');
    } catch (e) {
        if (window.console && console.debug) console.debug('safeHideModalById error:', e);
    }
}

/* ---------------- Boot function to ensure jQuery is loaded ---------------- */
(function boot() {
    if (typeof window.jQuery === 'undefined') {
        // Wait for DOM and check again (in case jQuery is injected later in layout)
        document.addEventListener('DOMContentLoaded', function () {
            if (typeof window.jQuery === 'undefined') {
                console.error('Student.js requires jQuery. Please include jQuery before this script.');
                return;
            }
            init(window.jQuery);
        });
    } else {
        init(window.jQuery);
    }
})();

/* ---------------- Main Script (requires jQuery) ---------------- */
function init($) {

    let currentPage = 1;
    let pageSize = 10;
    let totalCount = 0;
    let studentSearchTimeout = null;
    let lastStudentSearchVal = '';

    // For stats modal filter
    window._lastStudentStatsStudentCode = null;
    window._lastStudentStatsYearCode = null;

    localizeFiltersAndButtons($);

    /* ---------- Utilities ---------- */
    function formatTime(t) {
        if (!t) return '';
        if (typeof t === 'string') {
            if (/^\d{2}:\d{2}$/.test(t)) return t;
            if (/^\d{2}:\d{2}:\d{2}$/.test(t)) return t.substring(0, 5);
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
                            html += `<td rowspan="${rowCount}" class="bold-rendered student-stats-trigger" style="cursor:pointer;color:#007bff;" 
                                data-studentcode="${student.studentCode}" data-yearcode="${student.yearCode}">${student.studentName || ''}</td>`;
                            html += `<td rowspan="${rowCount}">${student.yearName || ''}</td>`;
                        }

                        // Show subscribed badge if subj.isSubscribed is true
                        let subjText = `<span class="badge">${subj.subjectName || ''}</span>`;
                        if (subj.isSubscribed) {
                            subjText += ` <span class="badge bg-success ms-1">${getJsString('subscribed') || 'Subscribed'}</span>`;
                        }

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
    $('#studentFilter').parent().remove();

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
        theYearCode = $(this).data('yearcode');
        openAddSubjectModal(studentCode, theYearCode);
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

                safeShowModalById('addSubjectModal');
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
                    safeHideModalById('addSubjectModal');
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
                safeShowModalById('editLearnModal');
            })
            .fail(xhr => logAjaxError('[GetStudentSubjectsForYear] failed', xhr));
    });

    $(document).on('submit', '#editAllSchedulesForm', function (e) {
        e.preventDefault();
        $.post('/StudentLearn/UpdateStudentSchedules', $(this).serialize())
            .done(function (res) {
                if (res && res.success) {
                    safeHideModalById('editLearnModal');
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

                html = `<form id="editLearnFormInner">${html}
                        <div class="text-end">
                           <button type="submit" class="modern-btn edit-btn" style="margin-top:8px;">
                               ${getJsString('save')}
                           </button>
                        </div>
                        </form>`;

                $('#editLearnModalBody').html(html);
                safeShowModalById('editLearnModal');
            })
            .fail(xhr => logAjaxError('[GetEditLearnFormData] failed', xhr));
    });

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

    $(document).on('submit', '#editLearnFormInner', function (e) {
        e.preventDefault();
        $.post('/StudentLearn/EditLearn', $(this).serialize())
            .done(function (res) {
                if (res && res.success) {
                    safeHideModalById('editLearnModal');
                    loadTable(currentPage);
                } else {
                    alert((res && res.message) || getJsString('failed-to-update'));
                }
            })
            .fail(xhr => logAjaxError('[EditLearn] failed', xhr));
    });

    /* ---------- Student Statistics Modal ---------- */
    if ($('#studentStatsModal').length === 0) {
        $('body').append(`
        <div class="modal fade" id="studentStatsModal" tabindex="-1" role="dialog" aria-labelledby="studentStatsModalLabel" aria-hidden="true">
            <div class="modal-dialog modal-xl" role="document">
                <div class="modal-content">
                    <div class="modal-header bg-info text-white">
                        <h5 class="modal-title" id="studentStatsModalLabel">Student Statistics</h5>
                        <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                            <span aria-hidden="true">&times;</span>
                        </button>
                    </div>
                    <div class="modal-body">
                        <div id="studentStatsModalBody"></div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-dismiss="modal">Close</button>
                    </div>
                </div>
            </div>
        </div>`);
    }

    $(document).on('click', '.student-stats-trigger', function () {
        const studentCode = $(this).data('studentcode');
        const yearCode = $(this).data('yearcode');
        window._lastStudentStatsStudentCode = studentCode;
        window._lastStudentStatsYearCode = yearCode;
        showStudentStatisticsModal(studentCode, yearCode, "");
    });

    function showStudentStatisticsModal(studentCode, yearCode, subjectCode) {
        safeShowModalById('studentStatsModal');
        $('#studentStatsModalBody').html('<div class="text-center"><span class="loader"></span> Loading...</div>');
        let isExamFilter = $('#studentStatsTypeFilter').val();
        if (isExamFilter === "") isExamFilter = null;
        else isExamFilter = (isExamFilter === "true");

        $.get('/StudentLearn/GetStudentStatistics', {
            studentCode: studentCode,
            yearCode: yearCode,
            subjectCode: subjectCode || null,
            isExamFilter: isExamFilter
        }).done(function (res) {
            if (!res || !res.subjects || res.subjects.length === 0) {
                $('#studentStatsModalBody').html('<div class="alert alert-warning">No statistics found.</div>');
                return;
            }

            let subjectOptions = '<option value="">All Subjects</option>';
            res.subjects.forEach(sub => {
                subjectOptions += `<option value="${sub.subjectCode}">${sub.subjectName}</option>`;
            });

            let filterHtml = `
    <div class="mb-3 d-flex" style="gap:15px;">
        <div>
            <label><b>Type:</b></label>
            <select id="studentStatsTypeFilter" class="form-control" style="max-width:200px;display:inline-block;margin-left:10px;">
                <option value="">All</option>
                <option value="true">Exams Only</option>
                <option value="false">Assignments Only</option>
            </select>
        </div>
        <div>
            <label><b>Filter by Subject:</b></label>
            <select id="studentStatsSubjectFilter" class="form-control" style="max-width:300px;display:inline-block;margin-left:10px;">
                ${subjectOptions}
            </select>
        </div>
    </div>
`;

            let statsHtml = '';
            res.subjects.forEach(sub => {
                statsHtml += `
                    <div class="card mb-3">
                        <div class="card-header fw-bold">${sub.subjectName}</div>
                        <div class="card-body">
                            <div class="row g-3">
                                <div class="col-md-6">
                                    <h5>Attendance</h5>
                                    <p><strong>Rate:</strong> ${sub.attendance.rate.toFixed(1)}%</p>
                                    <div>
                                        <canvas id="attGraph${sub.subjectCode}" height="80"></canvas>
                                    </div>
                                    <table class="table table-sm mt-2">
                                        <thead>
                                            <tr>
                                                <th>Class</th>
                                                <th>Date</th>
                                                <th>Time</th>
                                                <th>Status</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                          ${sub.attendance.list.map(c => `<tr>
    <td>${c.className}</td>
    <td>${c.classDate}</td>
    <td>${c.classTime}</td>
    <td class="${c.status === 'Attended' ? 'text-success' : 'text-danger'}">${c.status}</td>
</tr>`).join('')}
                                        </tbody>
                                    </table>
                                </div>
                                <div class="col-md-6">
                                    <h5>Exams</h5>
                                    <p><strong>Avg Mark:</strong> ${sub.exams.attended > 0 ? sub.exams.avgMark.toFixed(2) : 'N/A'}</p>
                                    <table class="table table-sm">
                                        <thead>
                                            <tr>
                                               <th>Exam</th>
                                               <th>Date</th>
                                               <th>Status</th>
                                               <th>Student Degree</th>
                                               <th>Exam Degree</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                          ${sub.exams.list.map(e => `<tr>
    <td>${e.examName}</td>
    <td>${e.examDate}</td>
    <td class="${e.status === 'Attended' ? 'text-success' : 'text-danger'}">${e.status}</td>
    <td>${e.studentDegree !== undefined && e.studentDegree !== null ? e.studentDegree : '-'}</td>
    <td>${e.examDegree !== undefined && e.examDegree !== null ? e.examDegree : '-'}</td>
</tr>`).join('')}
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                    </div>
                `;
            });

            $('#studentStatsModalBody').html(filterHtml + statsHtml);

            if (window.Chart) {
                res.subjects.forEach(sub => {
                    let ctx = document.getElementById('attGraph' + sub.subjectCode);
                    if (ctx) {
                        new Chart(ctx, {
                            type: 'doughnut',
                            data: {
                                labels: ['Attended', 'Missed'],
                                datasets: [{
                                    data: [sub.attendance.attended, sub.attendance.missed],
                                    backgroundColor: ['#00b894', '#d63031'],
                                }]
                            },
                            options: {
                                responsive: true,
                                legend: { display: true, position: 'bottom' }
                            }
                        });
                    }
                });
            }
        });
    }

    $(document).on('change', '#studentStatsSubjectFilter', function () {
        const subjectCode = $(this).val();
        const studentCode = window._lastStudentStatsStudentCode;
        const yearCode = window._lastStudentStatsYearCode;
        showStudentStatisticsModal(studentCode, yearCode, subjectCode);
    });

    $(document).on('change', '#studentStatsTypeFilter', function () {
        const subjectCode = $('#studentStatsSubjectFilter').val();
        const studentCode = window._lastStudentStatsStudentCode;
        const yearCode = window._lastStudentStatsYearCode;
        showStudentStatisticsModal(studentCode, yearCode, subjectCode);
    });

    loadFilters();
    loadTable(currentPage);

    window.studentLearnReload = function () { loadTable(currentPage); };
}