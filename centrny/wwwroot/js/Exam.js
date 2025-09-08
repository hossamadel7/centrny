$(document).ready(function () {
    var examItemsPerPage = 5;
    var examCurrentPage = 1;
    var examTotalPages = 1;
    // =============================
    // Global Variables and Configuration
    // =============================

    const examData = window.examPageData || {};
    const isCenterUser = examData.isCenterUser || false;
    const rootCode = examData.rootCode || 0;
    const rootName = examData.rootName || 'Unknown';
    const userName = examData.userName || 'User';

    var $addModal = $('#examModal');
    var addModal = new bootstrap.Modal($addModal[0]);
    var $form = $('#examForm');

    var $editModal = $('#editExamModal');
    var editModal = new bootstrap.Modal($editModal[0]);
    var $editForm = $('#editExamForm');
    var editingExamId = null;

    // Questions management variables
    var chosenQuestions = [];
    var availableQuestions = [];
    var currentExamCode = null;
    var drake = null;
    var availableCurrentPage = 1;
    var chosenCurrentPage = 1;
    var itemsPerPage = 10;
    var availableGroupedData = [];
    var chosenGroupedData = [];
    var chapterExpanded = {};
    var lessonExpanded = {};

    // =============================
    // Utility Functions
    // =============================

    function isMobile() {
        return window.innerWidth <= 576;
    }

    // =============================
    // Localization Helper
    // =============================

    function getJsString(key) {
        const kebabKey = key.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
        let val = $('#js-localization').data(kebabKey);
        if (typeof val === 'undefined' || val === null) {
            val = key;
        }
        return val;
    }

    // =============================
    // Utility Functions
    // =============================

    function showError(containerId, message) {
        const $container = $(`#${containerId}`);
        $container.text(message).show();
        setTimeout(() => $container.fadeOut(), 5000);
    }

    function showSuccess(message) {
        if (typeof showToast === 'function') {
            showToast('success', message);
        } else {
            alert(message);
        }
    }

    function validateExamForm(isCenter) {
        const examName = $('#ExamName').val().trim();
        const examTimer = $('#ExamTimer').val().trim();
        const eduYearCode = $('#EduYearCode').val();
        const subjectCode = $('#SubjectCode').val();
        const yearCode = $('#YearCode').val();

        if (!examName) {
            showError('examError', getJsString('ExamNameRequired'));
            return false;
        }

        if (!examTimer || !examTimer.match(/^([0-1]?\d|2[0-3]):[0-5]\d$/)) {
            showError('examError', getJsString('ExamTimerRequired'));
            return false;
        }

        if (!eduYearCode) {
            showError('examError', getJsString('EduYearRequired'));
            return false;
        }

        if (!subjectCode) {
            showError('examError', getJsString('SubjectRequired'));
            return false;
        }

        if (!yearCode) {
            showError('examError', getJsString('YearRequired'));
            return false;
        }

        if (isCenter) {
            const teacherCode = $('#TeacherCode').val();
            if (!teacherCode) {
                showError('examError', getJsString('TeacherRequired'));
                return false;
            }
        } else {
            const teacherCode = $('#AddExamTeacherCode').val();
            const centerCode = $('#AddExamCenterCode').val();
            const branchCode = $('#AddExamBranchCode').val();

            if (!teacherCode) {
                showError('examError', getJsString('NoTeacherFound'));
                return false;
            }
            if (!centerCode) {
                showError('examError', getJsString('CenterRequired'));
                return false;
            }
        }

        return true;
    }

    function setRequiredInGroup(groupSelector, required) {
        $(groupSelector).find('select, input').each(function () {
            if (required) {
                $(this).attr('required', true);
            } else {
                $(this).removeAttr('required');
            }
        });
    }

    // =============================
    // API Functions
    // =============================

    function fetchAndPopulateCenters($centerDropdown, callback) {
        $centerDropdown.empty().append($('<option>').val('').text(getJsString('SelectCenterOption')));
        $.get('/Exam/GetCentersByRootCode')
            .done(function (centers) {
                centers.forEach(function (c) {
                    $centerDropdown.append($('<option>').val(c.value).text(c.text));
                });
                if (callback) callback();
            })
            .fail(function () {
                console.error(getJsString('FailedToLoadCenters'));
            });
    }

    function fetchAndPopulateBranches($branchDropdown, centerCode) {
        $branchDropdown.empty().append($('<option>').val('').text(getJsString('SelectBranchOption')));
        if (!centerCode) return;

        $.get(`/Exam/GetBranchesByCenter?centerCode=${centerCode}`)
            .done(function (branches) {
                branches.forEach(function (b) {
                    $branchDropdown.append($('<option>').val(b.value).text(b.text));
                });
            })
            .fail(function () {
                console.error(getJsString('FailedToLoadBranches'));
            });
    }

    function fetchAndDisplayTeacher($teacherContainer) {
        $.get('/Exam/GetTeacherByRoot')
            .done(function (teacher) {
                if (teacher && teacher.value) {
                    $teacherContainer.html(`
                        <div class="alert alert-info mb-3">
                            <strong><i class="fas fa-user-tie me-2"></i>${getJsString('TeacherLabel')}:</strong> ${teacher.text}
                        </div>
                    `);
                    $('#AddExamTeacherCode').val(teacher.value);
                } else {
                    $teacherContainer.html(`
                        <div class="alert alert-warning mb-3">
                            <i class="fas fa-exclamation-triangle me-2"></i>${getJsString('NoTeacherFound')}
                        </div>
                    `);
                    $('#AddExamTeacherCode').val('');
                }
            })
            .fail(function () {
                $teacherContainer.html(`
                    <div class="alert alert-danger mb-3">
                        <i class="fas fa-times me-2"></i>${getJsString('ErrorLoadingTeacher')}
                    </div>
                `);
            });
    }

    function loadEduYears(selected) {
        $.get(`/Exam/GetEduYears?rootCode=${rootCode}`)
            .done(function (data) {
                var $eduYear = $('#EduYearCode');
                $eduYear.empty().append($('<option>').val('').text(getJsString('SelectEduYearOption')));

                data.forEach(function (item) {
                    $eduYear.append($('<option>').val(item.value).text(item.text));
                });

                if (selected) {
                    $eduYear.val(selected);
                } else {
                    $eduYear.val('');
                }
            })
            .fail(function () {
                console.error(getJsString('FailedToLoadEduYears'));
            });
    }

    // =============================
    // Exam Management Functions
    // =============================
    let allExams = [];

    function populateSubjectFilter(data) {
        const $subject = $('#filterSubject');
        $subject.empty().append($('<option>').val('').text('All'));
        const uniqueSubjects = {};
        data.forEach(exam => {
            if (exam.subjectCode && exam.subjectName) {
                uniqueSubjects[exam.subjectCode] = exam.subjectName;
            }
        });
        Object.entries(uniqueSubjects).forEach(([code, name]) => {
            $subject.append($('<option>').val(code).text(name));
        });
    }

    function populateEduYearFilter(data) {
        const $eduYear = $('#filterEduYear');
        $eduYear.empty().append($('<option>').val('').text('All'));
        const uniqueEduYears = {};
        data.forEach(exam => {
            if (exam.eduYearCode && exam.eduYearName) {
                uniqueEduYears[exam.eduYearCode] = exam.eduYearName;
            }
        });
        Object.entries(uniqueEduYears).forEach(([code, name]) => {
            $eduYear.append($('<option>').val(code).text(name));
        });
    }

    function populateYearFilter(data) {
        const $year = $('#filterYear');
        $year.empty().append($('<option>').val('').text('All'));
        const uniqueYears = {};
        data.forEach(exam => {
            if (exam.yearCode && exam.yearName) {
                uniqueYears[exam.yearCode] = exam.yearName;
            }
        });
        Object.entries(uniqueYears).forEach(([code, name]) => {
            $year.append($('<option>').val(code).text(name));
        });
    }

    function filterExamsAndRender() {
        let filtered = allExams.slice();
        const subjectVal = $('#filterSubject').val();
        if (subjectVal) {
            filtered = filtered.filter(exam => String(exam.subjectCode) === String(subjectVal));
        }
        const yearVal = $('#filterYear').val();
        if (yearVal) {
            filtered = filtered.filter(exam => String(exam.yearCode) === String(yearVal));
        }
        const typeVal = $('#filterExamType').val();
        if (typeVal) {
            filtered = filtered.filter(exam => typeVal === 'exam' ? exam.isExam : !exam.isExam);
        }

        examTotalPages = Math.max(1, Math.ceil(filtered.length / examItemsPerPage));
        if (examCurrentPage > examTotalPages) examCurrentPage = examTotalPages;
        if (examCurrentPage < 1) examCurrentPage = 1;

        const paginated = filtered.slice((examCurrentPage - 1) * examItemsPerPage, examCurrentPage * examItemsPerPage);

        renderExamsTable(paginated, filtered.length);
        renderExamPagination(filtered.length, examTotalPages, examCurrentPage);
    }

    function renderExamPagination(totalItems, totalPages, currentPage) {
        const container = $('#exam-pagination');
        container.empty();
        if (totalPages <= 1) return;

        let html = '<nav><ul class="pagination justify-content-center pagination-sm">';

        html += `<li class="page-item${currentPage === 1 ? " disabled" : ""}">
        <a class="page-link" href="#" data-page="${currentPage - 1}">&laquo;</a>
    </li>`;

        let start = Math.max(1, currentPage - 2);
        let end = Math.min(totalPages, currentPage + 2);
        for (let i = start; i <= end; i++) {
            html += `<li class="page-item${i === currentPage ? " active" : ""}">
            <a class="page-link" href="#" data-page="${i}">${i}</a>
        </li>`;
        }

        html += `<li class="page-item${currentPage === totalPages ? " disabled" : ""}">
        <a class="page-link" href="#" data-page="${currentPage + 1}">&raquo;</a>
    </li>`;

        html += '</ul></nav>';
        container.html(html);

        container.find('a.page-link').on('click', function (e) {
            e.preventDefault();
            const page = parseInt($(this).data('page'));
            if (!isNaN(page) && page >= 1 && page <= totalPages && page !== currentPage) {
                examCurrentPage = page;
                filterExamsAndRender();
            }
        });
    }

    function loadExams() {
        $('#exam-details').html(`
        <div class="text-center py-4">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">${getJsString('Loading')}</span>
            </div>
            <p class="mt-2 text-muted">${getJsString('Loading')}</p>
        </div>
    `);

        $.get('/Exam/GetAllExams')
            .done(function (data) {
                allExams = data || [];
                populateSubjectFilter(data);
                populateYearFilter(data);
                filterExamsAndRender();
            })
            .fail(function (xhr) {
                $('#exam-details').html(`
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-triangle me-2"></i>
                    ${getJsString('ErrorLoadingExams')}: ${xhr.responseJSON?.error || getJsString('UnknownError')}
                </div>
            `);
            });
    }

    function renderExamsTable(data, filteredLength) {
        if (!isCenterUser && data.length > 0) {
            var teacherName = data[0].teacherName || '';
            $('#exam-for-teacher').show().html(
                `<h2 class="mb-0"><strong>${getJsString('ExamsFor')}</strong><span class="text-primary">${teacherName}</span></h2>`
            );
        } else {
            $('#exam-for-teacher').hide();
        }

        if (data.length === 0) {
            $('#exam-details').html(`
            <div class="text-center py-5">
                <i class="fas fa-inbox display-1 text-muted"></i>
                <h5 class="mt-3 text-muted">${getJsString('NoExamsFound')}</h5>
                <p class="text-muted">${getJsString('GetStartedMsg')}</p>
            </div>
        `);
            $('#exam-table-info').html('');
            $('#exam-pagination').empty();
            return;
        }

        $('#exam-table-info').html(
            `<div class="text-muted" style="margin-bottom:8px;">
        Showing ${data.length} of ${filteredLength ?? data.length} exams.
        </div>`
        );

        var html = '<div class="table-responsive"><table class="gradient-table exam-index-table align-middle mb-0">';
        html += '<thead><tr>';

        if (isMobile()) {
            // Mobile view - show only first 3 columns + expand button
            html += `<th>${getJsString('NameHeader')}</th>`;
            html += `<th>${getJsString('ModeHeader')}</th>`;
            html += `<th>${getJsString('TypeHeader')}</th>`;
            html += `<th></th>`; // + button column
            html += '</tr></thead><tbody>';

            data.forEach(function (exam, index) {
                html += `<tr>
                <td>${exam.examName ?? ''}</td>
                <td>
                    <span class="badge exam-mode-${exam.isOnline ? 'online' : 'offline'}">
                        ${exam.isOnline ? getJsString('Online') : getJsString('Offline')}
                    </span>
                </td>
                <td>
                    <span class="badge exam-type-${exam.isExam ? 'exam' : 'assignment'}">
                        ${exam.isExam ? getJsString('Exam') : getJsString('Assignment')}
                    </span>
                </td>
                <td>
                    <button class="btn btn-sm btn-primary show-details-btn" data-row="${index}" title="Show Details">
                        <i class="fas fa-plus"></i>
                    </button>
                </td>
            </tr>
            <tr class="details-row" style="display:none" data-details="${index}">
                <td colspan="4">
                    <div class="p-3 bg-light rounded">
                        <div class="row">
                            <div class="col-6 mb-2">
                                <strong>${getJsString('StatusHeader')}:</strong><br>
                                <span class="badge exam-status-${exam.isDone ? 'done' : 'pending'}">
                                    ${exam.isDone ? getJsString('Done') : getJsString('Pending')}
                                </span>
                            </div>
                            <div class="col-6 mb-2">
                                <strong>${getJsString('BranchHeader')}:</strong><br>
                                ${exam.branchName ?? 'N/A'}
                            </div>
                            <div class="col-6 mb-2">
                                <strong>${getJsString('SubjectHeader')}:</strong><br>
                                ${exam.subjectName ?? 'N/A'}
                            </div>
                            <div class="col-6 mb-2">
                                <strong>${getJsString('YearHeader')}:</strong><br>
                                ${exam.yearName ?? 'N/A'}
                            </div>
                            <div class="col-6 mb-2">
                                <strong>${getJsString('EduYearHeader')}:</strong><br>
                                ${exam.eduYearName ?? 'N/A'}
                            </div>
                            <div class="col-6 mb-2">
                                <strong>${getJsString('DurationHeader')}:</strong><br>
                                <span class="badge exam-duration">${exam.examTimer ?? '00:00'}</span>
                            </div>
                            <div class="col-12 mb-3">
                                <strong>${getJsString('DegreeHeader')}:</strong>
                                <span class="badge exam-degree">${exam.examDegree ?? '0'}</span>
                            </div>
                            <div class="col-12">
                                <strong>${getJsString('ActionsHeader')}:</strong><br>
                                <div class="d-flex flex-column gap-2 mt-2">
                                    <button class="btn-table modules exam-index-btn-questions btn-sm shadow-sm add-questions"
                                            data-id="${exam.examCode}" title="${getJsString('QuestionsBtn')}">
                                        <i class="fas fa-list-check"></i> ${getJsString('QuestionsBtn')}
                                    </button>
                                    <button class="btn-table edit exam-index-btn-edit btn-sm shadow-sm edit-exam"
                                            data-id="${exam.examCode}" title="${getJsString('EditBtn')}">
                                        <i class="fas fa-pencil"></i> ${getJsString('EditBtn')}
                                    </button>
                                    <button class="btn-table delete exam-index-btn-delete btn-sm shadow-sm delete-exam"
                                            data-id="${exam.examCode}" title="${getJsString('DeleteBtn')}">
                                        <i class="fas fa-trash"></i> ${getJsString('DeleteBtn')}
                                    </button>
                                    <button class="btn-table stats btn-sm view-exam-stats"
                                            data-id="${exam.examCode}" title="${getJsString('StatsBtn')}">
                                        <i class="fas fa-chart-bar"></i> ${getJsString('StatsBtn')}
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </td>
            </tr>`;
            });

        } else {
            // Desktop view - show all columns
            html += `<th>${getJsString('NameHeader')}</th>`;
            html += `<th>${getJsString('ModeHeader')}</th>`;
            html += `<th>${getJsString('TypeHeader')}</th>`;
            html += `<th>${getJsString('StatusHeader')}</th>`;
            html += `<th>${getJsString('BranchHeader')}</th>`;
            html += `<th>${getJsString('SubjectHeader')}</th>`;
            html += `<th>${getJsString('YearHeader')}</th>`;
            html += `<th>${getJsString('EduYearHeader')}</th>`;
            html += `<th>${getJsString('DurationHeader')}</th>`;
            html += `<th>${getJsString('DegreeHeader')}</th>`;
            html += `<th>${getJsString('ActionsHeader')}</th>`;
            html += '</tr></thead><tbody>';

            data.forEach(function (exam) {
                html += `<tr>
                <td>${exam.examName ?? ''}</td>
                <td>
                    <span class="badge exam-mode-${exam.isOnline ? 'online' : 'offline'}">
                        ${exam.isOnline ? getJsString('Online') : getJsString('Offline')}
                    </span>
                </td>
                <td>
                    <span class="badge exam-type-${exam.isExam ? 'exam' : 'assignment'}">
                        ${exam.isExam ? getJsString('Exam') : getJsString('Assignment')}
                    </span>
                </td>
                <td>
                    <span class="badge exam-status-${exam.isDone ? 'done' : 'pending'}">
                        ${exam.isDone ? getJsString('Done') : getJsString('Pending')}
                    </span>
                </td>
                <td>${exam.branchName ?? ''}</td>
                <td>${exam.subjectName ?? ''}</td>
                <td>${exam.yearName ?? ''}</td>
                <td>${exam.eduYearName ?? ''}</td>
                <td>
                    <span class="badge exam-duration">${exam.examTimer ?? '00:00'}</span>
                </td>
                <td>
                    <span class="badge exam-degree">${exam.examDegree ?? '0'}</span>
                </td>
                <td>
                    <div class="d-flex flex-column gap-1">
                        <button class="btn-table modules exam-index-btn-questions btn-sm shadow-sm add-questions"
                                data-id="${exam.examCode}" title="${getJsString('QuestionsBtn')}">
                            <i class="fas fa-list-check"></i> ${getJsString('QuestionsBtn')}
                        </button>
                        <button class="btn-table edit exam-index-btn-edit btn-sm shadow-sm edit-exam"
                                data-id="${exam.examCode}" title="${getJsString('EditBtn')}">
                            <i class="fas fa-pencil"></i> ${getJsString('EditBtn')}
                        </button>
                        <button class="btn-table delete exam-index-btn-delete btn-sm shadow-sm delete-exam"
                                data-id="${exam.examCode}" title="${getJsString('DeleteBtn')}">
                            <i class="fas fa-trash"></i> ${getJsString('DeleteBtn')}
                        </button>
                        <button class="btn-table stats btn-sm view-exam-stats"
                                data-id="${exam.examCode}" title="${getJsString('StatsBtn')}">
                            <i class="fas fa-chart-bar"></i> ${getJsString('StatsBtn')}
                        </button>
                    </div>
                </td>
            </tr>`;
            });
        }

        html += '</tbody></table></div>';
        $('#exam-details').html(html);

        // Add handler for mobile expand/collapse buttons
        $('.show-details-btn').on('click', function () {
            var row = $(this).data('row');
            var detailsRow = $(`tr.details-row[data-details="${row}"]`);
            var icon = $(this).find('i');

            detailsRow.toggle();
            icon.toggleClass('fa-plus fa-minus');

            if (detailsRow.is(':visible')) {
                $(this).attr('title', 'Hide Details');
            } else {
                $(this).attr('title', 'Show Details');
            }
        });
    }

    function loadExamStats(examCode) {
        $('#examStatsContent').html(`
        <div class="text-center py-4">
            <div class="spinner-border text-primary"></div>
            <p class="mt-2">${getJsString('LoadingStatistics')}</p>
        </div>
    `);

        var statsModal = new bootstrap.Modal(document.getElementById('examStatsModal'));
        statsModal.show();

        $.get(`/Exam/GetExamStats?examCode=${examCode}`)
            .done(function (res) {
                if (res.success) {
                    $('#examStatsContent').html(`
                    <div class="row">
                        <div class="col-md-12 mb-3">
                            <div class="card bg-light">
                                <div class="card-body">
                                    <h6 class="card-title">
                                        <i class="fas fa-clipboard-check me-2"></i>${res.examName}
                                    </h6>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="card border-success">
                                <div class="card-body text-center">
                                    <i class="fas fa-check-circle display-4 text-success"></i>
                                    <h4 class="mt-2 text-success">${res.numberTookExam}</h4>
                                    <p class="text-muted mb-0">${getJsString('StudentsCompleted')}</p>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="card border-warning">
                                <div class="card-body text-center">
                                    <i class="fas fa-clock display-4 text-warning"></i>
                                    <h4 class="mt-2 text-warning">${res.numberDidNotTakeExam}</h4>
                                    <p class="text-muted mb-0">${getJsString('StudentsPending')}</p>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-12 text-center mt-3">
                            <div class="row justify-content-center">
                                <div class="col-auto">
                                    <span class="badge exam-degree fs-5">
                                        ${getJsString('SuccessHeader')}: 
                                        ${res.examPercentage ?? '0'}%
                                    </span>
                                </div>
                                <div class="col-auto">
                                    <span class="badge exam-degree fs-5">
                                        ${getJsString('AvgMarksHeader')}: 
                                        ${res.averageMarks !== undefined ? res.averageMarks.toFixed(1) : '0.0'}
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                `);
                } else {
                    $('#examStatsContent').html(`
                    <div class="alert alert-warning">
                        <i class="fas fa-exclamation-triangle me-2"></i>
                        ${res.error || getJsString('ErrorLoadingStatistics')}
                    </div>
                `);
                }
            })
            .fail(function () {
                $('#examStatsContent').html(`
                <div class="alert alert-danger">
                    <i class="fas fa-times me-2"></i>
                    ${getJsString('FailedToLoadExamStatistics')}
                </div>
            `);
            });
    }

    // =============================
    // Event Handlers
    // =============================

    $(document).on('click', '.view-exam-stats', function () {
        var examCode = $(this).data('id');
        loadExamStats(examCode);
    });

    $('#addExamBtn').on('click', function () {
        $('#examError').hide();
        $form[0].reset();
        $form.find('select').val('').empty().append($('<option>').val('').text(getJsString('SelectOption')));
        $form.find('button[type="submit"]').html('<i class="fas fa-save me-2"></i>' + getJsString('SaveExamBtn')).prop('disabled', false);

        ['#teacherDropdownGroup', '#teacherDisplayGroup', '#centerDropdownGroup', '#branchDropdownGroup', '#rootBranchDropdownGroup'].forEach(function (sel) {
            $(sel).hide();
            setRequiredInGroup(sel, false);
        });

        if (isCenterUser) {
            $('#teacherDropdownGroup').show();
            $('#branchDropdownGroup').show();
            setRequiredInGroup('#teacherDropdownGroup', true);
            setRequiredInGroup('#branchDropdownGroup', false);
        } else {
            $('#teacherDisplayGroup').show();
            $('#centerDropdownGroup').show();
            $('#rootBranchDropdownGroup').show();
            setRequiredInGroup('#teacherDisplayGroup', true);
            setRequiredInGroup('#centerDropdownGroup', true);
            setRequiredInGroup('#rootBranchDropdownGroup', false);
            fetchAndDisplayTeacher($('#teacherDisplayContainer'));
            fetchAndPopulateCenters($('#AddExamCenterCode'));
        }

        loadEduYears();
        $('#examModalLabel').text(getJsString('AddExamBtn'));
        addModal.show();
    });

    $('#AddExamCenterCode').on('change', function () {
        var centerCode = $(this).val();
        fetchAndPopulateBranches($('#AddExamBranchCode'), centerCode);
    });

    $(document).off('submit', '#examForm');
    $(document).on('submit', '#examForm', function (e) {
        e.preventDefault();
        $('#examError').hide();

        if (!validateExamForm(isCenterUser)) {
            return;
        }

        var branchVal = isCenterUser ? $('#BranchCode').val() : $('#AddExamBranchCode').val();

        var data = {
            ExamName: $('#ExamName').val().trim(),
            ExamTimer: $('#ExamTimer').val().trim(),
            ExamDegree: "0",
            ExamResult: "0",
            ExamPercentage: "0",
            IsExam: $('#IsExam').is(':checked'),
            IsOnline: $('#IsOnline').is(':checked'),
            TeacherCode: parseInt(isCenterUser ? $('#TeacherCode').val() : $('#AddExamTeacherCode').val()) || 0,
            CenterCode: isCenterUser ? null : parseInt($('#AddExamCenterCode').val()) || null,
            BranchCode: branchVal ? parseInt(branchVal) : null,
            YearCode: parseInt($('#YearCode').val()) || 0,
            SubjectCode: parseInt($('#SubjectCode').val()) || 0,
            EduYearCode: parseInt($('#EduYearCode').val()) || 0
        };

        const $submitBtn = $(this).find('button[type="submit"]');
        const originalText = $submitBtn.html();
        $submitBtn.html('<i class="fas fa-hourglass-half me-2"></i>' + getJsString('Saving')).prop('disabled', true);

        $.ajax({
            url: '/Exam/AddExam',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (res) {
                if (res.success) {
                    addModal.hide();
                    loadExams();
                    showSuccess(getJsString('ExamCreatedSuccess'));
                } else {
                    showError('examError', res.error || getJsString('ErrorAddingExam'));
                }
            },
            error: function (xhr) {
                var err = getJsString('UnknownError');
                if (xhr.responseJSON && xhr.responseJSON.error) {
                    err = xhr.responseJSON.error;
                } else if (xhr.responseText) {
                    try {
                        err = JSON.parse(xhr.responseText).error;
                    } catch (e) {
                        err = getJsString('ServerErrorOccurred');
                    }
                }
                showError('examError', err);
            },
            complete: function () {
                $submitBtn.html(originalText).prop('disabled', false);
            }
        });
    });

    $(document).on('click', '.edit-exam', function () {
        editingExamId = $(this).data('id');
        $editForm[0].reset();
        $('#editExamError').hide();
        $editForm.find('button[type="submit"]').html('<i class="fas fa-save me-2"></i>' + getJsString('UpdateExamBtn')).prop('disabled', false);

        $.get(`/Exam/GetExam?id=${editingExamId}`)
            .done(function (exam) {
                $editForm.find('[name="ExamCode"]').val(exam.examCode);
                $editForm.find('[name="ExamName"]').val(exam.examName);
                $editForm.find('[name="ExamTimer"]').val(exam.examTimer);
                $editForm.find('[name="IsExam"]').prop('checked', exam.isExam);
                $editForm.find('[name="IsOnline"]').prop('checked', exam.isOnline);
                $editForm.find('[name="IsDone"]').prop('checked', exam.isDone);

                $editForm.find('[name="TeacherCode"]').val(exam.teacherCode);
                $editForm.find('[name="SubjectCode"]').val(exam.subjectCode);
                $editForm.find('[name="YearCode"]').val(exam.yearCode);
                $editForm.find('[name="BranchCode"]').val(exam.branchCode);
                $editForm.find('[name="EduYearCode"]').val(exam.eduYearCode);

                editModal.show();
            })
            .fail(function () {
                showError('editExamError', getJsString('FailedToLoadExamDetails'));
            });
    });

    $editForm.on('submit', function (e) {
        e.preventDefault();
        $('#editExamError').hide();

        var data = {
            ExamCode: parseInt($editForm.find('[name="ExamCode"]').val()),
            ExamName: $editForm.find('[name="ExamName"]').val().trim(),
            ExamTimer: $editForm.find('[name="ExamTimer"]').val().trim(),
            IsExam: $editForm.find('[name="IsExam"]').is(':checked'),
            IsOnline: $editForm.find('[name="IsOnline"]').is(':checked'),
            TeacherCode: parseInt($editForm.find('[name="TeacherCode"]').val()),
            SubjectCode: parseInt($editForm.find('[name="SubjectCode"]').val()),
            YearCode: parseInt($editForm.find('[name="YearCode"]').val()),
            BranchCode: parseInt($editForm.find('[name="BranchCode"]').val()),
            EduYearCode: parseInt($editForm.find('[name="EduYearCode"]').val()),
            IsDone: $editForm.find('[name="IsDone"]').is(':checked')
        };

        const $submitBtn = $(this).find('button[type="submit"]');
        const originalText = $submitBtn.html();
        $submitBtn.html('<i class="fas fa-hourglass-half me-2"></i>' + getJsString('Updating')).prop('disabled', true);

        $.ajax({
            url: '/Exam/EditExam',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (res) {
                if (res.success) {
                    editModal.hide();
                    loadExams();
                    showSuccess(getJsString('ExamUpdatedSuccess'));
                } else {
                    showError('editExamError', res.error || getJsString('ErrorUpdatingExam'));
                }
            },
            error: function (xhr) {
                var err = xhr.responseJSON?.error || getJsString('ErrorUpdatingExam');
                showError('editExamError', err);
            },
            complete: function () {
                $submitBtn.html(originalText).prop('disabled', false);
            }
        });
    });

    $(document).on('click', '.delete-exam', function () {
        var id = $(this).data('id');
        if (!confirm(getJsString('ConfirmDeleteExam'))) return;

        $.ajax({
            url: '/Exam/DeleteExam',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(id),
            success: function (res) {
                if (res.success) {
                    loadExams();
                    showSuccess(getJsString('ExamDeletedSuccess'));
                } else {
                    alert(getJsString('Error') + ': ' + (res.error || getJsString('ErrorDeletingExam')));
                }
            },
            error: function (xhr) {
                alert(getJsString('Error') + ': ' + (xhr.responseJSON?.error || getJsString('ErrorDeletingExam')));
            }
        });
    });

    $form.on('change', '[name="EduYearCode"]', function () {
        var eduYearCode = $(this).val();

        $form.find('[name="SubjectCode"]').empty().append($('<option>').val('').text(getJsString('SelectSubjectOption')));
        $form.find('[name="YearCode"]').empty().append($('<option>').val('').text(getJsString('SelectYearOption')));

        if (!eduYearCode) return;

        if (isCenterUser) {
            var $teacher = $form.find('[name="TeacherCode"]');
            $teacher.empty().append($('<option>').val('').text(getJsString('SelectTeacherOption')));

            $.get(`/Exam/GetTeachersByEduYear?eduYearCode=${eduYearCode}`)
                .done(function (data) {
                    data.forEach(function (item) {
                        $teacher.append($('<option>').val(item.value).text(item.text));
                    });
                    $teacher.prop('disabled', false);
                })
                .fail(function () {
                    console.error(getJsString('FailedToLoadTeachers'));
                });
        } else {
            var teacherCode = $('#AddExamTeacherCode').val();
            if (!teacherCode) return;

            $.get(`/Exam/GetSubjectsByTeacherAndEduYear?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}`)
                .done(function (data) {
                    var $subject = $form.find('[name="SubjectCode"]');
                    $subject.empty().append($('<option>').val('').text(getJsString('SelectSubjectOption')));
                    data.forEach(function (item) {
                        $subject.append($('<option>').val(item.value).text(item.text));
                    });
                    $subject.prop('disabled', false);
                })
                .fail(function () {
                    console.error(getJsString('FailedToLoadSubjects'));
                });
        }
    });

    $form.on('change', '[name="TeacherCode"]', function () {
        var eduYearCode = $form.find('[name="EduYearCode"]').val();
        var teacherCode = $(this).val();
        var $subject = $form.find('[name="SubjectCode"]');

        $subject.empty().append($('<option>').val('').text(getJsString('SelectSubjectOption')));
        $form.find('[name="YearCode"]').empty().append($('<option>').val('').text(getJsString('SelectYearOption')));

        if (!teacherCode || !eduYearCode) return;

        $.get(`/Exam/GetSubjectsByTeacherAndEduYear?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}`)
            .done(function (data) {
                data.forEach(function (item) {
                    $subject.append($('<option>').val(item.value).text(item.text));
                });
                $subject.prop('disabled', false);
            })
            .fail(function () {
                console.error(getJsString('FailedToLoadSubjects'));
            });
    });

    $form.on('change', '[name="SubjectCode"]', function () {
        var eduYearCode = $form.find('[name="EduYearCode"]').val();
        var teacherCode = isCenterUser ? $form.find('[name="TeacherCode"]').val() : $('#AddExamTeacherCode').val();
        var subjectCode = $(this).val();
        var $year = $form.find('[name="YearCode"]');

        $year.empty().append($('<option>').val('').text(getJsString('SelectYearOption')));

        if (!subjectCode || !teacherCode || !eduYearCode) return;

        $.get(`/Exam/GetYearsByTeacherEduYearSubject?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}&subjectCode=${subjectCode}`)
            .done(function (data) {
                data.forEach(function (item) {
                    $year.append($('<option>').val(item.value).text(item.text));
                });
                $year.prop('disabled', false);
            })
            .fail(function () {
                console.error(getJsString('FailedToLoadYears'));
            });
    });

    $form.on('change', '[name="YearCode"]', function () {
        var eduYearCode = $form.find('[name="EduYearCode"]').val();
        var teacherCode = isCenterUser ? $form.find('[name="TeacherCode"]').val() : $('#AddExamTeacherCode').val();
        var subjectCode = $form.find('[name="SubjectCode"]').val();
        var yearCode = $(this).val();

        if (isCenterUser) {
            var $branch = $('#BranchCode');
            $branch.empty().append($('<option>').val('').text(getJsString('SelectBranchOption')));

            if (!yearCode || !teacherCode || !eduYearCode || !subjectCode) return;

            $.get(`/Exam/GetBranchesByAll?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}&subjectCode=${subjectCode}&yearCode=${yearCode}`)
                .done(function (data) {
                    data.forEach(function (item) {
                        $branch.append($('<option>').val(item.value).text(item.text));
                    });
                    $branch.prop('disabled', false);
                })
                .fail(function () {
                    console.error(getJsString('FailedToLoadBranches'));
                });
        }
    });

    // =============================
    // Questions Management System
    // =============================

    $(document).on('click', '.add-questions', function () {
        var examCode = $(this).data('id');
        currentExamCode = examCode;
        $('#questionsExamCode').val(examCode);
        availableCurrentPage = 1;
        chosenCurrentPage = 1;
        chosenQuestions = [];
        availableQuestions = [];
        availableGroupedData = [];
        chosenGroupedData = [];
        chapterExpanded = {};
        lessonExpanded = {};

        var exam = allExams.find(e => String(e.examCode) === String(examCode));
        if (!exam) {
            alert(getJsString('ErrorLoadingExamDetails'));
            return;
        }

        const questionsModal = new bootstrap.Modal(document.getElementById('questionsModal'));

        $.get(`/Exam/GetExamQuestions?examCode=${examCode}&teacherCode=${exam.teacherCode}&subjectCode=${exam.subjectCode}&yearCode=${exam.yearCode}`)
            .done(function (data) {
                if (data.chosenFlat && Array.isArray(data.chosenFlat)) {
                    data.chosenFlat.forEach(function (q) {
                        chosenQuestions.push({
                            questionCode: q.questionCode,
                            questionContent: q.questionContent,
                            questionDegree: q.questionDegree || 1,
                            lessonCode: q.lessonCode,
                            lessonName: q.lessonName,
                            chapterCode: q.chapterCode,
                            chapterName: q.chapterName
                        });
                    });
                }

                if (data.availableFlat && Array.isArray(data.availableFlat)) {
                    data.availableFlat.forEach(function (q) {
                        availableQuestions.push({
                            questionCode: q.questionCode,
                            questionContent: q.questionContent,
                            lessonCode: q.lessonCode,
                            lessonName: q.lessonName,
                            chapterCode: q.chapterCode,
                            chapterName: q.chapterName
                        });
                    });
                }

                rebuildGroupedData();
                renderQuestionsLists();
                questionsModal.show();
            })
            .fail(function () {
                alert(getJsString('FailedToLoadExamQuestions'));
            });
    });

    $('#examQuestionSearchBtn').on('click', function () {
        var term = $('#examQuestionSearchInput').val().trim();
        if (!term) return;
        doExamQuestionSearch(term);
    });

    $('#examQuestionSearchInput').on('keypress', function (e) {
        if (e.which === 13) $('#examQuestionSearchBtn').click();
    });

    $('#examQuestionSearchClearBtn').on('click', function () {
        $('#examQuestionSearchInput').val('');
        $('#exam-question-search-results').hide().html('');
        $('#availableQuestions').show();
        $('#availablePaginationTop, #availablePagination').show();
        $(this).hide();
    });

    function doExamQuestionSearch(term) {
        $('#exam-question-search-results').html('<div class="p-2">' + getJsString('Searching') + '</div>').show();
        $('#availableQuestions, #availablePaginationTop, #availablePagination').hide();
        $('#examQuestionSearchClearBtn').show();

        $.get('/Exam/SearchQuestions', { term })
            .done(function (data) {
                let html = '';
                if (!data || data.length === 0) {
                    html = `<div class="p-2 text-muted">${getJsString('NoQuestionsFoundFor')} "<b>${$('<div/>').text(term).html()}</b>".</div>`;
                } else {
                    html = '<ul class="list-group">';
                    data.forEach(function (q) {
                        html += `<li class="list-group-item d-flex justify-content-between align-items-center">
                            <span>
                                <b>${$('<div/>').text(q.questionContent).html()}</b><br>
                                <small class="text-secondary">${getJsString('LessonLabel')}: ${q.lessonName || '-'}</small>
                            </span>
                            <button type="button" class="btn-table edit add-question-from-search"
                                    data-id="${q.questionCode}"
                                    data-content="${$('<div/>').text(q.questionContent).html()}"
                                    data-lessonname="${$('<div/>').text(q.lessonName).html()}">
                                ${getJsString('AddBtn')}
                            </button>
                        </li>`;
                    });
                    html += '</ul>';
                }
                html += `<button id="examQuestionSearchBackBtn" type="button" class="btn-table delete mt-2">${getJsString('BackBtn')}</button>`;
                $('#exam-question-search-results').html(html);
            })
            .fail(function () {
                $('#exam-question-search-results').html('<div class="alert alert-danger">' + getJsString('ErrorSearchingQuestions') + '</div>');
            });
    }

    $(document).on('click', '#examQuestionSearchBackBtn', function () {
        $('#exam-question-search-results').hide().html('');
        $('#availableQuestions, #availablePaginationTop, #availablePagination').show();
        $('#examQuestionSearchInput').val('');
        $('#examQuestionSearchClearBtn').hide();
    });

    $(document).on('click', '.add-question-from-search', function (e) {
        e.preventDefault();
        var questionCode = parseInt($(this).data('id'));
        if (chosenQuestions.some(q => q.questionCode === questionCode)) return;

        chosenQuestions.push({
            questionCode: questionCode,
            questionContent: $(this).data('content'),
            questionDegree: 1,
            lessonName: $(this).data('lessonname'),
        });

        availableQuestions = availableQuestions.filter(q => q.questionCode !== questionCode);
        rebuildGroupedData();
        renderQuestionsLists();
    });

    $('#questionsForm').on('submit', function (e) {
        e.preventDefault();

        $('#chosenQuestions .question-item').each(function () {
            var questionCode = parseInt($(this).data('id'));
            var degree = parseInt($(this).find('.question-degree').val()) || 1;
            var q = chosenQuestions.find(x => x.questionCode === questionCode);
            if (q) q.questionDegree = degree;
        });

        if (!chosenQuestions.length) {
            alert(getJsString('SelectAtLeastOneQuestion'));
            return;
        }

        var examCode = parseInt($('#questionsExamCode').val());
        var submitData = {
            ExamCode: examCode,
            InsertUserCode: 1,
            Questions: chosenQuestions.map(q => ({
                QuestionCode: q.questionCode,
                QuestionDegree: q.questionDegree || 1
            }))
        };

        const $submitBtn = $(this).find('button[type="submit"]');
        const originalText = $submitBtn.html();
        $submitBtn.html('<i class="fas fa-hourglass-half me-2"></i>' + getJsString('Saving')).prop('disabled', true);

        $.ajax({
            url: '/Exam/SetExamQuestions',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(submitData),
            success: function (res) {
                if (res.success) {
                    bootstrap.Modal.getInstance(document.getElementById('questionsModal')).hide();
                    loadExams();
                    showSuccess(res.message || getJsString('QuestionsSavedSuccess'));
                } else {
                    alert(getJsString('Error') + ': ' + (res.error || getJsString('UpdateFailed')));
                }
            },
            error: function (xhr) {
                alert(getJsString('ErrorSavingQuestions') + ': ' + (xhr.responseJSON?.error || getJsString('UnknownError')));
            },
            complete: function () {
                $submitBtn.html(originalText).prop('disabled', false);
            }
        });
    });

    function groupQuestionsByChapterLesson(questionsList) {
        if (!questionsList || !Array.isArray(questionsList)) return [];

        var grouped = {};
        questionsList.forEach(function (question) {
            var chapterCode = question.chapterCode || 0;
            var lessonCode = question.lessonCode;

            if (!grouped[chapterCode]) {
                grouped[chapterCode] = {
                    chapterCode: chapterCode,
                    chapterName: question.chapterName || getJsString('Chapter') + ` ${chapterCode}`,
                    lessons: {}
                };
            }

            if (!grouped[chapterCode].lessons[lessonCode]) {
                grouped[chapterCode].lessons[lessonCode] = {
                    lessonCode: lessonCode,
                    lessonName: question.lessonName || getJsString('Lesson') + ` ${lessonCode}`,
                    questions: []
                };
            }

            grouped[chapterCode].lessons[lessonCode].questions.push({
                questionCode: question.questionCode,
                questionContent: question.questionContent,
                questionDegree: question.questionDegree
            });
        });

        var result = [];
        Object.keys(grouped).forEach(function (chapterCode) {
            var chapter = grouped[chapterCode];
            var lessonsArray = [];
            Object.keys(chapter.lessons).forEach(function (lessonCode) {
                lessonsArray.push(chapter.lessons[lessonCode]);
            });
            result.push({
                chapterCode: chapter.chapterCode,
                chapterName: chapter.chapterName,
                lessons: lessonsArray
            });
        });

        return result;
    }

    function flattenGroupedData(groupedData) {
        var flattened = [];
        if (!groupedData || !Array.isArray(groupedData)) return flattened;

        groupedData.forEach(function (chapter) {
            flattened.push({
                type: 'chapter',
                chapterCode: chapter.chapterCode,
                chapterName: chapter.chapterName,
                id: 'chapter-' + chapter.chapterCode
            });

            if (chapter.lessons && Array.isArray(chapter.lessons)) {
                chapter.lessons.forEach(function (lesson) {
                    flattened.push({
                        type: 'lesson',
                        lessonCode: lesson.lessonCode,
                        lessonName: lesson.lessonName,
                        chapterCode: chapter.chapterCode,
                        id: 'lesson-' + lesson.lessonCode
                    });

                    if (lesson.questions && Array.isArray(lesson.questions)) {
                        lesson.questions.forEach(function (question) {
                            flattened.push({
                                type: 'question',
                                questionCode: question.questionCode,
                                questionContent: question.questionContent,
                                questionDegree: question.questionDegree,
                                lessonCode: lesson.lessonCode,
                                chapterCode: chapter.chapterCode,
                                id: 'question-' + question.questionCode
                            });
                        });
                    }
                });
            }
        });

        return flattened;
    }

    function paginateArray(array, page, itemsPerPage) {
        var offset = (page - 1) * itemsPerPage;
        return array.slice(offset, offset + itemsPerPage);
    }

    function getTotalPages(array, itemsPerPage) {
        return Math.max(1, Math.ceil(array.length / itemsPerPage));
    }

    function createPaginationControls(containerId, currentPage, totalPages, onPageChange) {
        var container = $(`#${containerId}`);
        container.empty();
        if (totalPages <= 1) return;

        var pagination = $('<nav><ul class="pagination pagination-sm justify-content-center"></ul></nav>');
        var ul = pagination.find('ul');

        var prevDisabled = currentPage === 1 ? 'disabled' : '';
        ul.append(`<li class="page-item ${prevDisabled}"><a class="page-link" href="#" data-page="${currentPage - 1}">${getJsString('Previous')}</a></li>`);

        var startPage = Math.max(1, currentPage - 2);
        var endPage = Math.min(totalPages, currentPage + 2);

        if (startPage > 1) {
            ul.append(`<li class="page-item"><a class="page-link" href="#" data-page="1">1</a></li>`);
            if (startPage > 2) {
                ul.append('<li class="page-item disabled"><span class="page-link">...</span></li>');
            }
        }

        for (var i = startPage; i <= endPage; i++) {
            var active = i === currentPage ? 'active' : '';
            ul.append(`<li class="page-item ${active}"><a class="page-link" href="#" data-page="${i}">${i}</a></li>`);
        }

        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                ul.append('<li class="page-item disabled"><span class="page-link">...</span></li>');
            }
            ul.append(`<li class="page-item"><a class="page-link" href="#" data-page="${totalPages}">${totalPages}</a></li>`);
        }

        var nextDisabled = currentPage === totalPages ? 'disabled' : '';
        ul.append(`<li class="page-item ${nextDisabled}"><a class="page-link" href="#" data-page="${currentPage + 1}">${getJsString('Next')}</a></li>`);

        container.append(pagination);

        container.find('a.page-link').on('click', function (e) {
            e.preventDefault();
            var page = parseInt($(this).data('page'));
            if (!isNaN(page) && page >= 1 && page <= totalPages) {
                onPageChange(page);
            }
        });
    }

    function rebuildGroupedData() {
        availableGroupedData = groupQuestionsByChapterLesson(availableQuestions);
        chosenGroupedData = groupQuestionsByChapterLesson(chosenQuestions);
    }

    function renderQuestionsLists() {
        var availableFlattened = flattenGroupedData(availableGroupedData);
        var chosenFlattened = flattenGroupedData(chosenGroupedData);

        var availablePaginated = paginateArray(availableFlattened, availableCurrentPage, itemsPerPage);
        var chosenPaginated = paginateArray(chosenFlattened, chosenCurrentPage, itemsPerPage);

        var $available = $('#availableQuestions').empty();
        availablePaginated.forEach(function (item) {
            if (item.type === 'chapter') {
                var expanded = chapterExpanded['available-' + item.chapterCode] === true;
                $available.append(`
                    <li class="list-group-item bg-primary text-white fw-bold chapter-header"
                        data-chapter="${item.chapterCode}" data-list="available" style="cursor: pointer;">
                        <i class="fas fa-chevron-${expanded ? "down" : "right"} chapter-arrow me-2"></i>
                        <i class="fas fa-book me-2"></i>${item.chapterName}
                    </li>
                `);
            } else if (item.type === 'lesson') {
                var expanded = lessonExpanded['available-' + item.lessonCode] === true;
                $available.append(`
                    <li class="list-group-item bg-light fw-semibold ps-4 lesson-header"
                        data-lesson="${item.lessonCode}" data-chapter="${item.chapterCode}"
                        data-list="available" style="cursor: pointer;">
                        <i class="fas fa-chevron-${expanded ? "down" : "right"} lesson-arrow me-2"></i>
                        <i class="fas fa-journal-whills me-2"></i>${item.lessonName}
                    </li>
                `);
            } else if (item.type === 'question') {
                var show = (chapterExpanded['available-' + item.chapterCode] === true) &&
                    (lessonExpanded['available-' + item.lessonCode] === true);
                $available.append(`
                    <li class="list-group-item ps-5 question-item"
                        data-id="${item.questionCode}" data-chapter="${item.chapterCode}"
                        data-lesson="${item.lessonCode}" style="${show ? "" : "display:none;"}">
                        ${item.questionContent}
                    </li>
                `);
            }
        });

        var $chosen = $('#chosenQuestions').empty();
        chosenPaginated.forEach(function (item) {
            if (item.type === 'chapter') {
                var expanded = chapterExpanded['chosen-' + item.chapterCode] === true;
                $chosen.append(`
                    <li class="list-group-item bg-success text-white fw-bold chapter-header"
                        data-chapter="${item.chapterCode}" data-list="chosen" style="cursor: pointer;">
                        <i class="fas fa-chevron-${expanded ? "down" : "right"} chapter-arrow me-2"></i>
                        <i class="fas fa-book me-2"></i>${item.chapterName}
                    </li>
                `);
            } else if (item.type === 'lesson') {
                var expanded = lessonExpanded['chosen-' + item.lessonCode] === true;
                $chosen.append(`
        <li class="list-group-item bg-light fw-semibold ps-4 lesson-header"
            data-lesson="${item.lessonCode}" data-chapter="${item.chapterCode}"
            data-list="chosen" style="cursor: pointer;">
            <i class="fas fa-chevron-${expanded ? "down" : "right"} lesson-arrow me-2"></i>
            <i class="fas fa-journal-whills me-2"></i>${item.lessonName}
        </li>
    `);
            } else if (item.type === 'question') {
                var show = (chapterExpanded['chosen-' + item.chapterCode] === true) &&
                    (lessonExpanded['chosen-' + item.lessonCode] === true);
                $chosen.append(`
                <li class="list-group-item ps-5 d-flex align-items-center question-item"
                    data-id="${item.questionCode}" data-chapter="${item.chapterCode}"
                    data-lesson="${item.lessonCode}" style="${show ? "" : "display:none;"}">
                    <span class="flex-grow-1">${item.questionContent}</span>
                    <input type="number" class="form-control form-control-sm ms-2 question-degree"
                           style="width:90px" placeholder="${getJsString('DegreeLabel')}" value="${item.questionDegree || 1}"
                           min="1" max="100" required>
                </li>
            `);
            }
        });

        var availableTotalPages = getTotalPages(availableFlattened, itemsPerPage);
        var chosenTotalPages = getTotalPages(chosenFlattened, itemsPerPage);

        createPaginationControls('availablePaginationTop', availableCurrentPage, availableTotalPages, function (page) {
            availableCurrentPage = page;
            renderQuestionsLists();
            $('#availableQuestions').parent().scrollTop($('#availableQuestions').parent()[0].scrollHeight);
        });

        createPaginationControls('availablePagination', availableCurrentPage, availableTotalPages, function (page) {
            availableCurrentPage = page;
            renderQuestionsLists();
            $('#availableQuestions').parent().scrollTop($('#availableQuestions').parent()[0].scrollHeight);
        });

        createPaginationControls('chosenPaginationTop', chosenCurrentPage, chosenTotalPages, function (page) {
            chosenCurrentPage = page;
            renderQuestionsLists();
            $('#chosenQuestions').parent().scrollTop($('#chosenQuestions').parent()[0].scrollHeight);
        });

        createPaginationControls('chosenPagination', chosenCurrentPage, chosenTotalPages, function (page) {
            chosenCurrentPage = page;
            renderQuestionsLists();
            $('#chosenQuestions').parent().scrollTop($('#chosenQuestions').parent()[0].scrollHeight);
        });

        $('#availableInfo').text(`${getJsString('AvailableQuestions')}: ${availableQuestions.length} (${getJsString('Page')} ${availableCurrentPage} ${getJsString('Of')} ${availableTotalPages})`);
        $('#chosenInfo').text(`${getJsString('ChosenQuestions')}: ${chosenQuestions.length} (${getJsString('Page')} ${chosenCurrentPage} ${getJsString('Of')} ${chosenTotalPages})`);

        if (drake && drake.destroy) drake.destroy();
        drake = dragula([document.getElementById('chosenQuestions'), document.getElementById('availableQuestions')], {
            accepts: function (el, target, source, sibling) {
                return $(el).hasClass('question-item');
            }
        });

        drake.on('drop', function (el, target, source) {
            var questionCode = parseInt($(el).data('id'));
            if (!target || !source || target === source) return;

            if (target.id === "chosenQuestions" && source.id === "availableQuestions") {
                var questionIndex = availableQuestions.findIndex(q => q.questionCode === questionCode);
                if (questionIndex !== -1) {
                    var question = availableQuestions.splice(questionIndex, 1)[0];
                    question.questionDegree = 1;
                    chosenQuestions.push(question);
                    rebuildGroupedData();
                    renderQuestionsLists();
                }
            } else if (target.id === "availableQuestions" && source.id === "chosenQuestions") {
                var questionIndex = chosenQuestions.findIndex(q => q.questionCode === questionCode);
                if (questionIndex !== -1) {
                    var question = chosenQuestions.splice(questionIndex, 1)[0];
                    delete question.questionDegree;
                    availableQuestions.push(question);
                    rebuildGroupedData();
                    renderQuestionsLists();
                }
            }
        });

        $('#availableQuestions').parent().scrollTop($('#availableQuestions').parent()[0].scrollHeight);
        $('#chosenQuestions').parent().scrollTop($('#chosenQuestions').parent()[0].scrollHeight);
    }

    $(document).on('click', '.chapter-header', function () {
        var chapterCode = $(this).data('chapter');
        var whichList = $(this).data('list');
        var key = whichList + '-' + chapterCode;
        chapterExpanded[key] = !(chapterExpanded[key] !== false);
        renderQuestionsLists();
    });

    $(document).on('click', '.lesson-header', function () {
        var lessonCode = $(this).data('lesson');
        var whichList = $(this).data('list');
        var key = whichList + '-' + lessonCode;
        lessonExpanded[key] = !(lessonExpanded[key] !== false);
        renderQuestionsLists();
    });

    $(document).on('input change blur', '.question-degree', function () {
        var $input = $(this);
        var questionCode = parseInt($input.closest('li').data('id'));
        var degree = parseInt($input.val()) || 1;

        if (degree < 1) {
            degree = 1;
            $input.val(degree);
        }
        if (degree > 100) {
            degree = 100;
            $input.val(degree);
        }

        var questionIndex = chosenQuestions.findIndex(q => q.questionCode === questionCode);
        if (questionIndex !== -1) {
            chosenQuestions[questionIndex].questionDegree = degree;
        }
    });

    $('#filterSubject').on('change', function () {
        examCurrentPage = 1;
        filterExamsAndRender();
    });

    $('#filterYear').on('change', function () {
        examCurrentPage = 1;
        filterExamsAndRender();
    });

    $('#filterExamType').on('change', function () {
        examCurrentPage = 1;
        filterExamsAndRender();
    });
    // =============================
    // Window Resize Handler
    // =============================

    $(window).on('resize', function () {
        // Re-render table if screen size changes
        if (allExams && allExams.length > 0) {
            filterExamsAndRender();
        }
    });

    // =============================
    // Initialize Page
    // =============================

    loadExams();

    console.log(getJsString('ExamManagementInitializedFor'), {
        isCenter: isCenterUser,
        rootCode: rootCode,
        rootName: rootName,
        userName: userName
    });

});