$(document).ready(function () {
    // =============================
    // Global Variables and Configuration
    // =============================

    // Get data from server
    const examData = window.examPageData || {};
    const isCenterUser = examData.isCenterUser || false;
    const rootCode = examData.rootCode || 0;
    const rootName = examData.rootName || 'Unknown';
    const userName = examData.userName || 'User';

    // Modal and Form Variables
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

    function showError(containerId, message) {
        const $container = $(`#${containerId}`);
        $container.text(message).show();
        setTimeout(() => $container.fadeOut(), 5000);
    }

    function showSuccess(message) {
        // You can integrate with your existing notification system
        console.log('Success:', message);
        // Or use a toast notification if available
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
            showError('examError', 'Exam name is required.');
            return false;
        }

        if (!examTimer || !examTimer.match(/^([0-1]?\d|2[0-3]):[0-5]\d$/)) {
            showError('examError', 'Please enter a valid time in HH:MM format.');
            return false;
        }

        if (!eduYearCode) {
            showError('examError', 'Educational year is required.');
            return false;
        }

        if (!subjectCode) {
            showError('examError', 'Subject is required.');
            return false;
        }

        if (!yearCode) {
            showError('examError', 'Year is required.');
            return false;
        }

        if (isCenter) {
            const teacherCode = $('#TeacherCode').val();
            const branchCode = $('#BranchCode').val();

            if (!teacherCode) {
                showError('examError', 'Teacher is required.');
                return false;
            }
            if (!branchCode) {
                showError('examError', 'Branch is required.');
                return false;
            }
        } else {
            const teacherCode = $('#AddExamTeacherCode').val();
            const centerCode = $('#AddExamCenterCode').val();
            const branchCode = $('#AddExamBranchCode').val();

            if (!teacherCode) {
                showError('examError', 'No teacher found for your root.');
                return false;
            }
            if (!centerCode) {
                showError('examError', 'Center is required.');
                return false;
            }
            if (!branchCode) {
                showError('examError', 'Branch is required.');
                return false;
            }
        }

        return true;
    }

    // Patch: Helper to set/remove required for select/input in a group
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
        $centerDropdown.empty().append($('<option>').val('').text('-- Select Center --'));

        $.get('/Exam/GetCentersByRootCode')
            .done(function (centers) {
                centers.forEach(function (c) {
                    $centerDropdown.append($('<option>').val(c.value).text(c.text));
                });
                if (callback) callback();
            })
            .fail(function () {
                console.error('Failed to load centers');
            });
    }

    function fetchAndPopulateBranches($branchDropdown, centerCode) {
        $branchDropdown.empty().append($('<option>').val('').text('-- Select Branch --'));
        if (!centerCode) return;

        $.get(`/Exam/GetBranchesByCenter?centerCode=${centerCode}`)
            .done(function (branches) {
                branches.forEach(function (b) {
                    $branchDropdown.append($('<option>').val(b.value).text(b.text));
                });
            })
            .fail(function () {
                console.error('Failed to load branches');
            });
    }

    function fetchAndDisplayTeacher($teacherContainer) {
        $.get('/Exam/GetTeacherByRoot')
            .done(function (teacher) {
                if (teacher && teacher.value) {
                    $teacherContainer.html(`
                        <div class="alert alert-info mb-3">
                            <strong><i class="bi bi-person-badge me-2"></i>Teacher:</strong> ${teacher.text}
                        </div>
                    `);
                    $('#AddExamTeacherCode').val(teacher.value);
                } else {
                    $teacherContainer.html(`
                        <div class="alert alert-warning mb-3">
                            <i class="bi bi-exclamation-triangle me-2"></i>No teacher found for this root.
                        </div>
                    `);
                    $('#AddExamTeacherCode').val('');
                }
            })
            .fail(function () {
                $teacherContainer.html(`
                    <div class="alert alert-danger mb-3">
                        <i class="bi bi-x-circle me-2"></i>Error loading teacher information.
                    </div>
                `);
            });
    }

    function loadEduYears(selected) {
        $.get(`/Exam/GetEduYears?rootCode=${rootCode}`)
            .done(function (data) {
                var $eduYear = $('#EduYearCode');
                $eduYear.empty().append($('<option>').val('').text('-- Select Educational Year --'));

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
                console.error('Failed to load education years');
            });
    }

    // =============================
    // Exam Management Functions
    // =============================

    function loadExams() {
        $('#exam-details').html(`
            <div class="text-center py-4">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p class="mt-2 text-muted">Loading exams...</p>
            </div>
        `);

        $.get('/Exam/GetAllExams')
            .done(function (data) {
                renderExamsTable(data);
            })
            .fail(function (xhr) {
                $('#exam-details').html(`
                    <div class="alert alert-danger">
                        <i class="bi bi-exclamation-triangle me-2"></i>
                        Error loading exams: ${xhr.responseJSON?.error || 'Unknown error'}
                    </div>
                `);
            });
    }

    function renderExamsTable(data) {
        if (!isCenterUser && data.length > 0) {
            var teacherName = data[0].teacherName || '';
            $('#exam-for-teacher').show().html(
                `<h2 class="mb-0"><strong>Exams for: </strong><span class="text-primary">${teacherName}</span></h2>`
            );
        } else {
            $('#exam-for-teacher').hide();
        }

        if (data.length === 0) {
            $('#exam-details').html(`
                <div class="text-center py-5">
                    <i class="bi bi-inbox display-1 text-muted"></i>
                    <h5 class="mt-3 text-muted">No exams found</h5>
                    <p class="text-muted">Click "Add Exam" to create your first exam.</p>
                </div>
            `);
            return;
        }

        var html = '<table class="table exam-index-table align-middle mb-0">';
        html += '<thead><tr>';
        html += '<th>Code</th><th>Name</th><th>Degree</th><th>Avg Marks</th><th>Success %</th>';
        html += '<th>Duration</th><th>Edu Year</th>';
        if (isCenterUser) html += '<th>Teacher</th>';
        html += '<th>Year</th><th>Subject</th><th>Branch</th>';
        html += '<th>Status</th><th>Type</th><th>Mode</th><th>Actions</th>';
        html += '</tr></thead><tbody>';

        data.forEach(function (exam) {
            html += `<tr>
                <td class="fw-bold text-primary">${exam.examCode ?? ''}</td>
                <td class="fw-semibold">${exam.examName ?? ''}</td>
                <td><span class="badge bg-secondary">${exam.examDegree ?? '0'}</span></td>
                <td>${exam.averageMarks !== undefined ? exam.averageMarks.toFixed(1) : '0.0'}</td>
                <td>${exam.examPercentage ?? '0'}%</td>
                <td><span class="badge bg-info">${exam.examTimer ?? '00:00'}</span></td>
                <td>${exam.eduYearName ?? ''}</td>`;

            if (isCenterUser) {
                html += `<td>${exam.teacherName ?? ''}</td>`;
            }

            html += `<td>${exam.yearName ?? ''}</td>
                <td>${exam.subjectName ?? ''}</td>
                <td>${exam.branchName ?? ''}</td>
                <td><span class="badge bg-${exam.isDone ? 'success' : 'warning'}">${exam.isDone ? 'Done' : 'Pending'}</span></td>
                <td><span class="badge bg-${exam.isExam ? 'primary' : 'secondary'}">${exam.isExam ? 'Exam' : 'Quiz'}</span></td>
                <td><span class="badge bg-${exam.isOnline ? 'info' : 'dark'}">${exam.isOnline ? 'Online' : 'Offline'}</span></td>
                <td>
                    <div class="d-flex flex-column gap-1">
                        <button class="btn exam-index-btn-questions btn-sm shadow-sm add-questions" 
                                data-id="${exam.examCode}" title="Manage Questions">
                            <i class="bi bi-list-check"></i> Questions
                        </button>
                        <button class="btn exam-index-btn-edit btn-sm shadow-sm edit-exam" 
                                data-id="${exam.examCode}" title="Edit Exam">
                            <i class="bi bi-pencil"></i> Edit
                        </button>
                        <button class="btn exam-index-btn-delete btn-sm shadow-sm delete-exam" 
                                data-id="${exam.examCode}" title="Delete Exam">
                            <i class="bi bi-trash"></i> Delete
                        </button>
                        <button class="btn btn-info btn-sm view-exam-stats" 
                                data-id="${exam.examCode}" title="View Statistics">
                            <i class="bi bi-bar-chart-line"></i> Stats
                        </button>
                    </div>
                </td>
            </tr>`;
        });

        html += '</tbody></table>';
        $('#exam-details').html(html);
    }

    function loadExamStats(examCode) {
        $('#examStatsContent').html(`
            <div class="text-center py-4">
                <div class="spinner-border text-primary"></div>
                <p class="mt-2">Loading statistics...</p>
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
                                            <i class="bi bi-clipboard-check me-2"></i>${res.examName}
                                        </h6>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="card border-success">
                                    <div class="card-body text-center">
                                        <i class="bi bi-check-circle display-4 text-success"></i>
                                        <h4 class="mt-2 text-success">${res.numberTookExam}</h4>
                                        <p class="text-muted mb-0">Students Completed</p>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="card border-warning">
                                    <div class="card-body text-center">
                                        <i class="bi bi-clock display-4 text-warning"></i>
                                        <h4 class="mt-2 text-warning">${res.numberDidNotTakeExam}</h4>
                                        <p class="text-muted mb-0">Students Pending</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    `);
                } else {
                    $('#examStatsContent').html(`
                        <div class="alert alert-warning">
                            <i class="bi bi-exclamation-triangle me-2"></i>
                            ${res.error || 'Error loading statistics'}
                        </div>
                    `);
                }
            })
            .fail(function () {
                $('#examStatsContent').html(`
                    <div class="alert alert-danger">
                        <i class="bi bi-x-circle me-2"></i>
                        Failed to load exam statistics.
                    </div>
                `);
            });
    }

    // =============================
    // Event Handlers
    // =============================

    // View exam stats
    $(document).on('click', '.view-exam-stats', function () {
        var examCode = $(this).data('id');
        loadExamStats(examCode);
    });

    // Add exam button
    $('#addExamBtn').on('click', function () {
        $('#examError').hide();
        $form[0].reset();
        $form.find('select').val('').empty().append($('<option>').val('').text('-- Select --'));

        // Patch: Hide all groups and remove required, then show needed and re-add required
        ['#teacherDropdownGroup', '#teacherDisplayGroup', '#centerDropdownGroup', '#branchDropdownGroup', '#rootBranchDropdownGroup'].forEach(function (sel) {
            $(sel).hide();
            setRequiredInGroup(sel, false);
        });

        if (isCenterUser) {
            $('#teacherDropdownGroup').show();
            $('#branchDropdownGroup').show();
            setRequiredInGroup('#teacherDropdownGroup', true);
            setRequiredInGroup('#branchDropdownGroup', true);
        } else {
            $('#teacherDisplayGroup').show();
            $('#centerDropdownGroup').show();
            $('#rootBranchDropdownGroup').show();
            setRequiredInGroup('#teacherDisplayGroup', true);
            setRequiredInGroup('#centerDropdownGroup', true);
            setRequiredInGroup('#rootBranchDropdownGroup', true);
            fetchAndDisplayTeacher($('#teacherDisplayContainer'));
            fetchAndPopulateCenters($('#AddExamCenterCode'));
        }

        loadEduYears();
        $('#examModalLabel').text('Add Exam');
        addModal.show();
    });

    // Center change handler for teacher users
    $('#AddExamCenterCode').on('change', function () {
        var centerCode = $(this).val();
        fetchAndPopulateBranches($('#AddExamBranchCode'), centerCode);
    });

    // Form submission
    $(document).off('submit', '#examForm');
    $(document).on('submit', '#examForm', function (e) {
        console.log('Form submitted!');
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
            BranchCode: parseInt(branchVal) || 0,
            YearCode: parseInt($('#YearCode').val()) || 0,
            SubjectCode: parseInt($('#SubjectCode').val()) || 0,
            EduYearCode: parseInt($('#EduYearCode').val()) || 0
        };

        // Disable submit button
        const $submitBtn = $(this).find('button[type="submit"]');
        const originalText = $submitBtn.html();
        $submitBtn.html('<i class="bi bi-hourglass-split me-2"></i>Saving...').prop('disabled', true);

        $.ajax({
            url: '/Exam/AddExam',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (res) {
                if (res.success) {
                    addModal.hide();
                    loadExams();
                    showSuccess('Exam created successfully!');
                } else {
                    showError('examError', res.error || 'Error adding exam');
                }
            },
            error: function (xhr) {
                var err = "Unknown error";
                if (xhr.responseJSON && xhr.responseJSON.error) {
                    err = xhr.responseJSON.error;
                } else if (xhr.responseText) {
                    try {
                        err = JSON.parse(xhr.responseText).error;
                    } catch (e) {
                        err = 'Server error occurred';
                    }
                }
                showError('examError', err);
            },
            complete: function () {
                $submitBtn.html(originalText).prop('disabled', false);
            }
        });
    });

    // Edit exam
    $(document).on('click', '.edit-exam', function () {
        editingExamId = $(this).data('id');
        $editForm[0].reset();
        $('#editExamError').hide();

        $.get(`/Exam/GetExam?id=${editingExamId}`)
            .done(function (exam) {
                $editForm.find('[name="ExamCode"]').val(exam.examCode);
                $editForm.find('[name="ExamName"]').val(exam.examName);
                $editForm.find('[name="ExamTimer"]').val(exam.examTimer);
                $editForm.find('[name="IsExam"]').prop('checked', exam.isExam);
                $editForm.find('[name="IsOnline"]').prop('checked', exam.isOnline);

                // Set hidden fields
                $editForm.find('[name="TeacherCode"]').val(exam.teacherCode);
                $editForm.find('[name="SubjectCode"]').val(exam.subjectCode);
                $editForm.find('[name="YearCode"]').val(exam.yearCode);
                $editForm.find('[name="BranchCode"]').val(exam.branchCode);
                $editForm.find('[name="EduYearCode"]').val(exam.eduYearCode);

                editModal.show();
            })
            .fail(function () {
                showError('editExamError', 'Failed to load exam details');
            });
    });

    // Edit form submission
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
            EduYearCode: parseInt($editForm.find('[name="EduYearCode"]').val())
        };

        const $submitBtn = $(this).find('button[type="submit"]');
        const originalText = $submitBtn.html();
        $submitBtn.html('<i class="bi bi-hourglass-split me-2"></i>Updating...').prop('disabled', true);

        $.ajax({
            url: '/Exam/EditExam',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (res) {
                if (res.success) {
                    editModal.hide();
                    loadExams();
                    showSuccess('Exam updated successfully!');
                } else {
                    showError('editExamError', res.error || 'Error updating exam');
                }
            },
            error: function (xhr) {
                var err = xhr.responseJSON?.error || 'Error updating exam';
                showError('editExamError', err);
            },
            complete: function () {
                $submitBtn.html(originalText).prop('disabled', false);
            }
        });
    });

    // Delete exam
    $(document).on('click', '.delete-exam', function () {
        var id = $(this).data('id');
        if (!confirm('Are you sure you want to delete this exam? This action cannot be undone.')) return;

        $.ajax({
            url: '/Exam/DeleteExam',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(id),
            success: function (res) {
                if (res.success) {
                    loadExams();
                    showSuccess('Exam deleted successfully!');
                } else {
                    alert('Error: ' + (res.error || 'Error deleting exam'));
                }
            },
            error: function (xhr) {
                alert('Error: ' + (xhr.responseJSON?.error || 'Error deleting exam'));
            }
        });
    });

    // =============================
    // Dependent Dropdowns for Add Exam
    // =============================

    $form.on('change', '[name="EduYearCode"]', function () {
        var eduYearCode = $(this).val();

        $form.find('[name="SubjectCode"]').empty().append($('<option>').val('').text('-- Select Subject --'));
        $form.find('[name="YearCode"]').empty().append($('<option>').val('').text('-- Select Year --'));

        if (!eduYearCode) return;

        if (isCenterUser) {
            var $teacher = $form.find('[name="TeacherCode"]');
            $teacher.empty().append($('<option>').val('').text('-- Select Teacher --'));

            $.get(`/Exam/GetTeachersByEduYear?eduYearCode=${eduYearCode}`)
                .done(function (data) {
                    data.forEach(function (item) {
                        $teacher.append($('<option>').val(item.value).text(item.text));
                    });
                    $teacher.prop('disabled', false);
                })
                .fail(function () {
                    console.error('Failed to load teachers');
                });
        } else {
            var teacherCode = $('#AddExamTeacherCode').val();
            if (!teacherCode) return;

            $.get(`/Exam/GetSubjectsByTeacherAndEduYear?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}`)
                .done(function (data) {
                    var $subject = $form.find('[name="SubjectCode"]');
                    $subject.empty().append($('<option>').val('').text('-- Select Subject --'));
                    data.forEach(function (item) {
                        $subject.append($('<option>').val(item.value).text(item.text));
                    });
                    $subject.prop('disabled', false);
                })
                .fail(function () {
                    console.error('Failed to load subjects');
                });
        }
    });

    $form.on('change', '[name="TeacherCode"]', function () {
        var eduYearCode = $form.find('[name="EduYearCode"]').val();
        var teacherCode = $(this).val();
        var $subject = $form.find('[name="SubjectCode"]');

        $subject.empty().append($('<option>').val('').text('-- Select Subject --'));
        $form.find('[name="YearCode"]').empty().append($('<option>').val('').text('-- Select Year --'));

        if (!teacherCode || !eduYearCode) return;

        $.get(`/Exam/GetSubjectsByTeacherAndEduYear?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}`)
            .done(function (data) {
                data.forEach(function (item) {
                    $subject.append($('<option>').val(item.value).text(item.text));
                });
                $subject.prop('disabled', false);
            })
            .fail(function () {
                console.error('Failed to load subjects');
            });
    });

    $form.on('change', '[name="SubjectCode"]', function () {
        var eduYearCode = $form.find('[name="EduYearCode"]').val();
        var teacherCode = isCenterUser ? $form.find('[name="TeacherCode"]').val() : $('#AddExamTeacherCode').val();
        var subjectCode = $(this).val();
        var $year = $form.find('[name="YearCode"]');

        $year.empty().append($('<option>').val('').text('-- Select Year --'));

        if (!subjectCode || !teacherCode || !eduYearCode) return;

        $.get(`/Exam/GetYearsByTeacherEduYearSubject?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}&subjectCode=${subjectCode}`)
            .done(function (data) {
                data.forEach(function (item) {
                    $year.append($('<option>').val(item.value).text(item.text));
                });
                $year.prop('disabled', false);
            })
            .fail(function () {
                console.error('Failed to load years');
            });
    });

    $form.on('change', '[name="YearCode"]', function () {
        var eduYearCode = $form.find('[name="EduYearCode"]').val();
        var teacherCode = isCenterUser ? $form.find('[name="TeacherCode"]').val() : $('#AddExamTeacherCode').val();
        var subjectCode = $form.find('[name="SubjectCode"]').val();
        var yearCode = $(this).val();

        if (isCenterUser) {
            var $branch = $('#BranchCode');
            $branch.empty().append($('<option>').val('').text('-- Select Branch --'));

            if (!yearCode || !teacherCode || !eduYearCode || !subjectCode) return;

            $.get(`/Exam/GetBranchesByAll?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}&subjectCode=${subjectCode}&yearCode=${yearCode}`)
                .done(function (data) {
                    data.forEach(function (item) {
                        $branch.append($('<option>').val(item.value).text(item.text));
                    });
                    $branch.prop('disabled', false);
                })
                .fail(function () {
                    console.error('Failed to load branches');
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

        const questionsModal = new bootstrap.Modal(document.getElementById('questionsModal'));

        $.get(`/Exam/GetExamQuestions?examCode=${examCode}`)
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
                alert('Failed to load exam questions.');
            });
    });

    // Question search functionality
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
        $('#exam-question-search-results').html('<div class="p-2">Searching...</div>').show();
        $('#availableQuestions, #availablePaginationTop, #availablePagination').hide();
        $('#examQuestionSearchClearBtn').show();

        $.get('/Exam/SearchQuestions', { term })
            .done(function (data) {
                let html = '';
                if (!data || data.length === 0) {
                    html = `<div class="p-2 text-muted">No questions found for "<b>${$('<div/>').text(term).html()}</b>".</div>`;
                } else {
                    html = '<ul class="list-group">';
                    data.forEach(function (q) {
                        html += `<li class="list-group-item d-flex justify-content-between align-items-center">
                            <span>
                                <b>${$('<div/>').text(q.questionContent).html()}</b><br>
                                <small class="text-secondary">Lesson: ${q.lessonName || '-'}</small>
                            </span>
                            <button type="button" class="btn btn-success btn-sm add-question-from-search" 
                                    data-id="${q.questionCode}" 
                                    data-content="${$('<div/>').text(q.questionContent).html()}" 
                                    data-lessonname="${$('<div/>').text(q.lessonName).html()}">
                                Add
                            </button>
                        </li>`;
                    });
                    html += '</ul>';
                }
                html += '<button id="examQuestionSearchBackBtn" type="button" class="btn btn-secondary btn-sm mt-2">Back</button>';
                $('#exam-question-search-results').html(html);
            })
            .fail(function () {
                $('#exam-question-search-results').html('<div class="alert alert-danger">Error searching questions.</div>');
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

    // Question form submission
    $('#questionsForm').on('submit', function (e) {
        e.preventDefault();

        $('#chosenQuestions .question-item').each(function () {
            var questionCode = parseInt($(this).data('id'));
            var degree = parseInt($(this).find('.question-degree').val()) || 1;
            var q = chosenQuestions.find(x => x.questionCode === questionCode);
            if (q) q.questionDegree = degree;
        });

        if (!chosenQuestions.length) {
            alert('Please select at least one question for the exam.');
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
        $submitBtn.html('<i class="bi bi-hourglass-split me-2"></i>Saving...').prop('disabled', true);

        $.ajax({
            url: '/Exam/SetExamQuestions',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(submitData),
            success: function (res) {
                if (res.success) {
                    bootstrap.Modal.getInstance(document.getElementById('questionsModal')).hide();
                    loadExams();
                    showSuccess(res.message || 'Questions saved successfully!');
                } else {
                    alert('Error: ' + (res.error || 'Update failed'));
                }
            },
            error: function (xhr) {
                alert('Error saving questions: ' + (xhr.responseJSON?.error || 'Unknown error'));
            },
            complete: function () {
                $submitBtn.html(originalText).prop('disabled', false);
            }
        });
    });

    // =============================
    // Questions Helper Functions
    // =============================

    function groupQuestionsByChapterLesson(questionsList) {
        if (!questionsList || !Array.isArray(questionsList)) return [];

        var grouped = {};
        questionsList.forEach(function (question) {
            var chapterCode = question.chapterCode || 0;
            var lessonCode = question.lessonCode;

            if (!grouped[chapterCode]) {
                grouped[chapterCode] = {
                    chapterCode: chapterCode,
                    chapterName: question.chapterName || `Chapter ${chapterCode}`,
                    lessons: {}
                };
            }

            if (!grouped[chapterCode].lessons[lessonCode]) {
                grouped[chapterCode].lessons[lessonCode] = {
                    lessonCode: lessonCode,
                    lessonName: question.lessonName || `Lesson ${lessonCode}`,
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
        ul.append(`<li class="page-item ${prevDisabled}"><a class="page-link" href="#" data-page="${currentPage - 1}">Previous</a></li>`);

        var startPage = Math.max(1, currentPage - 2);
        var endPage = Math.min(totalPages, currentPage + 2);

        if (startPage > 1) {
            ul.append('<li class="page-item"><a class="page-link" href="#" data-page="1">1</a></li>');
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
        ul.append(`<li class="page-item ${nextDisabled}"><a class="page-link" href="#" data-page="${currentPage + 1}">Next</a></li>`);

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

        // Render available questions
        var $available = $('#availableQuestions').empty();
        availablePaginated.forEach(function (item) {
            if (item.type === 'chapter') {
                var expanded = chapterExpanded['available-' + item.chapterCode] !== false;
                $available.append(`
                    <li class="list-group-item bg-primary text-white fw-bold chapter-header" 
                        data-chapter="${item.chapterCode}" data-list="available" style="cursor: pointer;">
                        <i class="bi bi-chevron-${expanded ? "down" : "right"} chapter-arrow me-2"></i>
                        <i class="bi bi-book me-2"></i>${item.chapterName}
                    </li>
                `);
            } else if (item.type === 'lesson') {
                var expanded = lessonExpanded['available-' + item.lessonCode] !== false;
                $available.append(`
                    <li class="list-group-item bg-light fw-semibold ps-4 lesson-header" 
                        data-lesson="${item.lessonCode}" data-chapter="${item.chapterCode}" 
                        data-list="available" style="cursor: pointer;">
                        <i class="bi bi-chevron-${expanded ? "down" : "right"} lesson-arrow me-2"></i>
                        <i class="bi bi-journal-text me-2"></i>${item.lessonName}
                    </li>
                `);
            } else if (item.type === 'question') {
                var show = (chapterExpanded['available-' + item.chapterCode] !== false) &&
                    (lessonExpanded['available-' + item.lessonCode] !== false);
                $available.append(`
                    <li class="list-group-item ps-5 question-item" 
                        data-id="${item.questionCode}" data-chapter="${item.chapterCode}" 
                        data-lesson="${item.lessonCode}" style="${show ? "" : "display:none;"}">
                        ${item.questionContent}
                    </li>
                `);
            }
        });

        // Render chosen questions
        var $chosen = $('#chosenQuestions').empty();
        chosenPaginated.forEach(function (item) {
            if (item.type === 'chapter') {
                var expanded = chapterExpanded['chosen-' + item.chapterCode] !== false;
                $chosen.append(`
                    <li class="list-group-item bg-success text-white fw-bold chapter-header" 
                        data-chapter="${item.chapterCode}" data-list="chosen" style="cursor: pointer;">
                        <i class="bi bi-chevron-${expanded ? "down" : "right"} chapter-arrow me-2"></i>
                        <i class="bi bi-book me-2"></i>${item.chapterName}
                    </li>
                `);
            } else if (item.type === 'lesson') {
                var expanded = lessonExpanded['chosen-' + item.lessonCode] !== false;
                $chosen.append(`
                    <li class="list-group-item bg-light fw-semibold ps-4 lesson-header" 
                        data-lesson="${item.lessonCode}" data-chapter="${item.chapterCode}" 
                        data-list="chosen" style="cursor: pointer;">
                        <i class="bi bi-chevron-${expanded ? "down" : "right"} lesson-arrow me-2"></i>
                        <i class="bi bi-journal-text me-2"></i>${item.lessonName}
                    </li>
                `);
            } else if (item.type === 'question') {
                var show = (chapterExpanded['chosen-' + item.chapterCode] !== false) &&
                    (lessonExpanded['chosen-' + item.lessonCode] !== false);
                $chosen.append(`
                    <li class="list-group-item ps-5 d-flex align-items-center question-item" 
                        data-id="${item.questionCode}" data-chapter="${item.chapterCode}" 
                        data-lesson="${item.lessonCode}" style="${show ? "" : "display:none;"}">
                        <span class="flex-grow-1">${item.questionContent}</span>
                        <input type="number" class="form-control form-control-sm ms-2 question-degree" 
                               style="width:90px" placeholder="Degree" value="${item.questionDegree || 1}" 
                               min="1" max="100" required>
                    </li>
                `);
            }
        });

        // Update pagination
        var availableTotalPages = getTotalPages(availableFlattened, itemsPerPage);
        var chosenTotalPages = getTotalPages(chosenFlattened, itemsPerPage);

        createPaginationControls('availablePaginationTop', availableCurrentPage, availableTotalPages, function (page) {
            availableCurrentPage = page;
            renderQuestionsLists();
            $('#availableQuestions').parent().scrollTop(0);
        });

        createPaginationControls('availablePagination', availableCurrentPage, availableTotalPages, function (page) {
            availableCurrentPage = page;
            renderQuestionsLists();
            $('#availableQuestions').parent().scrollTop(0);
        });

        createPaginationControls('chosenPaginationTop', chosenCurrentPage, chosenTotalPages, function (page) {
            chosenCurrentPage = page;
            renderQuestionsLists();
            $('#chosenQuestions').parent().scrollTop(0);
        });

        createPaginationControls('chosenPagination', chosenCurrentPage, chosenTotalPages, function (page) {
            chosenCurrentPage = page;
            renderQuestionsLists();
            $('#chosenQuestions').parent().scrollTop(0);
        });

        $('#availableInfo').text(`Available Questions: ${availableQuestions.length} (Page ${availableCurrentPage} of ${availableTotalPages})`);
        $('#chosenInfo').text(`Chosen Questions: ${chosenQuestions.length} (Page ${chosenCurrentPage} of ${chosenTotalPages})`);

        // Initialize Dragula for drag and drop
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

        $('#availableQuestions').parent().scrollTop(0);
        $('#chosenQuestions').parent().scrollTop(0);
    }

    // Chapter/lesson expand/collapse handlers
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

    // Question degree input handler
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

    // =============================
    // Initialize Page
    // =============================

    // Load exams on page load
    loadExams();

    console.log('Exam management initialized for:', {
        isCenter: isCenterUser,
        rootCode: rootCode,
        rootName: rootName,
        userName: userName
    });
});