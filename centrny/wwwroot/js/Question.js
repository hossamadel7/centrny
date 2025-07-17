console.log("=== Question.js file is being loaded ===");

// Use camelCase for all keys to match jQuery .data() parsing
function getJsString(key) {
    // Convert dash-case to camelCase for jQuery .data()
    key = key.replace(/-([a-z])/g, function (g) { return g[1].toUpperCase(); });
    return $('#js-localization').data(key);
}

// Insert all localized labels/text in static UI and modals
function setModalLabels() {
    $('#chapter-modal-title').text(getJsString('addChapterBtn'));
    $('#chapter-name-label').text(getJsString('chapterName'));
    $('#education-year-label').text(getJsString('educationYear'));
    $('#teacher-label').text(getJsString('teacher'));
    $('#subject-label').text(getJsString('subject'));
    $('#save-chapter-btn').text(getJsString('addChapterBtn'));
    $('#cancel-chapter-btn').text(getJsString('cancelBtn'));

    $('#lesson-modal-title').text(getJsString('addLessonBtn'));
    $('#lesson-name-label').text(getJsString('lessonName'));
    $('#save-lesson-btn').text(getJsString('addLessonBtn'));
    $('#cancel-lesson-btn').text(getJsString('cancelBtn'));

    $('#question-modal-title').text(getJsString('addQuestionBtn'));
    $('#question-content-label').text(getJsString('questionContent'));
    $('#exam-code-label').text(getJsString('examCode'));
    $('#save-question-btn').text(getJsString('addQuestionBtn'));
    $('#cancel-question-btn').text(getJsString('cancelBtn'));

    $('#answers-modal-title').text(getJsString('addAnswersBtn'));
    $('#add-more-answer-btn').text(getJsString('addAnotherAnswer'));
    $('#save-answers-btn').text(getJsString('addAnswersBtn'));
    $('#cancel-answers-btn').text(getJsString('cancelBtn'));

    $('#edit-answer-modal-title').text(getJsString('editBtn'));
    $('#edit-answer-content-label').text(getJsString('answerContentPlaceholder'));
    $('#edit-answer-istrue-label').text(getJsString('isCorrectAnswer'));
    $('#save-edit-answer-btn').text(getJsString('editBtn'));
    $('#cancel-edit-answer-btn').text(getJsString('cancelBtn'));
}

$(document).ready(function () {
    // Localize static UI elements - all keys must be camelCase!
    $('#questionSearchInput').attr('placeholder', getJsString('searchPlaceholder'));
    $('#questionSearchBtn').text(getJsString('searchBtn'));
    $('#questionSearchClearBtn').text(getJsString('clearBtn'));
    $('#add-chapter-btn').text(getJsString('addChapterBtn'));
    $('#subject-year-filter-bar label b').text(getJsString('teachingLabel'));
    setModalLabels();

    // Helper functions for button processing state
    function setButtonProcessing($btn, processingText) {
        if (!$btn.data('original-text')) {
            $btn.data('original-text', $btn.text());
        }
        $btn.text(processingText).prop('disabled', true);
    }
    function resetButton($btn) {
        $btn.text($btn.data('original-text') || getJsString('save')).prop('disabled', false);
    }

    // User info box
    $.get('/Question/GetUserRootTeacherInfo', function (data) {
        if (data.isCenter === false) {
            let boxHtml = `
                <div style="background:#f5fafd;border:1px solid #c8e1fa;padding:18px 0 18px 24px;margin:18px 0 30px 0;border-radius:8px;color:#29587a;">
                    <b>User Code:</b> ${data.userCode}<br />
                    <b>Root Code:</b> ${data.rootCode}<br />
                    <b>Teacher Name:</b> ${data.teacherName}
                </div>
            `;
            $('#user-root-info-box').html(boxHtml).show();
        } else {
            $('#user-root-info-box').hide();
        }
    });

    // Subject-Year Filter Setup
    let isCenter = false;
    let userTeacherCode = 0;
    let subjectYearPairs = [];
    let currentSubjectCode = null;
    let currentYearCode = null;
    let stickyKey = "subjectYearFilter";
    let currentPage = 1;
    let pageSize = 5;

    $.get('/Question/IsUserCenter', function (res) {
        isCenter = res.isCenter;
        if (!isCenter) {
            $.get('/Question/GetTeachersByRoot', function (teachers) {
                if (teachers.length > 0) {
                    userTeacherCode = teachers[0].teacherCode;
                    $.get('/Question/GetSubjectYearPairsByTeacher', { teacherCode: userTeacherCode }, function (pairs) {
                        subjectYearPairs = pairs;
                        setupSubjectYearFilter();
                    });
                }
            });
        } else {
            $('#subject-year-filter-bar').hide();
            loadChapters();
        }
    });

    function setupSubjectYearFilter() {
        if (subjectYearPairs.length === 1) {
            $('#subject-year-filter-bar').hide();
            currentSubjectCode = subjectYearPairs[0].subjectCode;
            currentYearCode = subjectYearPairs[0].yearCode;
            setStickyFilter(currentSubjectCode, currentYearCode);
            loadChapters();
        } else if (subjectYearPairs.length > 1) {
            $('#subject-year-filter-bar').show();
            let html = `<option value="">${getJsString('selectOption')}</option>`;
            subjectYearPairs.forEach(p => {
                html += `<option value="${p.subjectCode}|${p.yearCode}">${p.subjectName} (${p.yearName})</option>`;
            });
            $('#subjectYearFilter').html(html).addClass('styled-select');

            let sticky = getStickyFilter();
            if (sticky && sticky.subjectCode && sticky.yearCode) {
                $('#subjectYearFilter').val(`${sticky.subjectCode}|${sticky.yearCode}`);
                currentSubjectCode = sticky.subjectCode;
                currentYearCode = sticky.yearCode;
            } else {
                $('#subjectYearFilter').val('');
                currentSubjectCode = null;
                currentYearCode = null;
            }

            if (currentSubjectCode && currentYearCode) loadChapters();

            $('#subjectYearFilter').off().on('change', function () {
                let val = $(this).val();
                if (val) {
                    let [sub, year] = val.split('|');
                    currentSubjectCode = sub;
                    currentYearCode = year;
                    setStickyFilter(currentSubjectCode, currentYearCode);
                    loadChapters();
                } else {
                    currentSubjectCode = null;
                    currentYearCode = null;
                    setStickyFilter(null, null);
                    $('#chapters-container').html('');
                }
            });
        } else {
            $('#subject-year-filter-bar').hide();
            $('#chapters-container').html(`<div style="padding:20px;">${getJsString('noTeachingSubjects')}</div>`);
        }
    }
    function setStickyFilter(subjectCode, yearCode) {
        window.sessionStorage.setItem(stickyKey, JSON.stringify({ subjectCode, yearCode }));
    }
    function getStickyFilter() {
        let raw = window.sessionStorage.getItem(stickyKey);
        if (raw) return JSON.parse(raw);
        return null;
    }

    // ------ MODAL SYSTEM: Universal Show/Hide ------
    function showModal(modalId) {
        $(modalId).css('display', 'flex');
    }
    function hideModal(modalId) {
        $(modalId).css('display', 'none');
    }
    $(document).on('keydown', function (e) {
        if (e.key === "Escape") {
            $('.modal-background').css('display', 'none');
        }
    });
    $('.modal-background').on('click', function (e) {
        if (e.target === this) {
            $(this).css('display', 'none');
        }
    });

    // ------ ADD CHAPTER LOGIC ------
    $('#add-chapter-btn').on('click', function () {
        setModalLabels();
        $('#chapter-lessonname').val('');
        $('#chapter-message').text('');
        $('#chapter-eduyearcode').val('');
        $('#chapter-eduyearcode-view').val('');
        $('#chapter-subjectcode').html(`<option value="">${getJsString('select')}</option>`);
        $('#chapter-teachercode').html('');
        $('#chapter-teachercode-hidden').val('');
        $('#teacher-group').hide();
        $('#chapter-yearcode').val('');
        $('#save-chapter-btn').text(getJsString('addChapterBtn')).prop('disabled', false);

        $.get('/Question/IsUserCenter', function (res) {
            isCenter = res.isCenter;
            loadChapterYears();

            if (isCenter) {
                $('#teacher-group').show();
                loadChapterTeachers();
            } else {
                $('#teacher-group').hide();
                if (subjectYearPairs.length > 1) {
                    $('#chapter-subjectcode').html(`<option value="">${getJsString('select')}</option>`);
                    subjectYearPairs.forEach(p => {
                        $('#chapter-subjectcode').append(
                            `<option value="${p.subjectCode}|${p.yearCode}">${p.subjectName} (${p.yearName})</option>`
                        );
                    });
                    $('#chapter-subjectcode').off().on('change', function () {
                        if ($(this).val() && $(this).val().indexOf('|') !== -1) {
                            let [subject, year] = $(this).val().split('|');
                            $('#chapter-yearcode').val(year);
                            // Do NOT overwrite the value of chapter-subjectcode, keep "subject|year"
                        } else {
                            $('#chapter-yearcode').val('');
                        }
                    });
                    var selected = $('#subjectYearFilter').val();
                    if (selected) {
                        $('#chapter-subjectcode').val(selected);
                        $('#chapter-subjectcode').trigger('change');
                    }
                } else if (subjectYearPairs.length === 1) {
                    $('#chapter-subjectcode').html(
                        `<option value="${subjectYearPairs[0].subjectCode}|${subjectYearPairs[0].yearCode}" selected>
                        ${subjectYearPairs[0].subjectName} (${subjectYearPairs[0].yearName})
                    </option>`
                    ).prop('disabled', true);
                    $('#chapter-subjectcode').val(subjectYearPairs[0].subjectCode + "|" + subjectYearPairs[0].yearCode);
                    $('#chapter-yearcode').val(subjectYearPairs[0].yearCode);
                } else {
                    $('#chapter-subjectcode').html(`<option value="">${getJsString('noTeachingSubjects')}</option>`).prop('disabled', true);
                    $('#chapter-yearcode').val('');
                }
            }
            showModal('#chapter-modal');
        });
    
    });  
    function loadChapterYears() {
        $.get('/Question/GetEduYearsByRoot', function (years) {
            if (years.length === 1) {
                // Only one active EduYear, set hidden value and readonly display field
                $('#chapter-eduyearcode').val(years[0].eduYearCode);
                $('#chapter-eduyearcode-view').val(years[0].eduYearCode + " - " + years[0].eduYearName);
            } else if (years.length > 1) {
                // If multiple, select first and show all (optional: you can remove this if you never want a dropdown)
                $('#chapter-eduyearcode').val(years[0].eduYearCode);
                $('#chapter-eduyearcode-view').val(years[0].eduYearCode + " - " + years[0].eduYearName);
            } else {
                $('#chapter-eduyearcode').val('');
                $('#chapter-eduyearcode-view').val('');
            }
        });
    }
    function loadChapterTeachers() {
        $.get('/Question/GetTeachersByRoot', function (teachers) {
            let html = `<option value="">${getJsString('select')}</option>`;
            teachers.forEach(t => {
                html += `<option value="${t.teacherCode}">${t.teacherName}</option>`;
            });
            $('#chapter-teachercode').html(html);
        });
    }
    function loadChapterSubjects(teacherCode, eduYearCode) {
        if (!teacherCode || !eduYearCode) {
            $('#chapter-subjectcode').html(`<option value="">${getJsString('select')}</option>`);
            return;
        }
        $.get('/Question/GetSubjectsByTeacherYear', { teacherCode, eduYearCode }, function (subjects) {
            let html = `<option value="">${getJsString('select')}</option>`;
            subjects.forEach(s => {
                html += `<option value="${s.subjectCode}">${s.subjectName}</option>`;
            });
            $('#chapter-subjectcode').html(html);
        });
    }
    $('#chapter-eduyearcode').on('change', function () {
        let eduYearCode = $(this).val();
        if (isCenter) {
            let teacherCode = $('#chapter-teachercode').val();
            loadChapterSubjects(teacherCode, eduYearCode);
        } else {
            loadChapterSubjects(userTeacherCode, eduYearCode);
        }
    });
    $('#chapter-teachercode').on('change', function () {
        let eduYearCode = $('#chapter-eduyearcode').val();
        let teacherCode = $(this).val();
        loadChapterSubjects(teacherCode, eduYearCode);
    });
    $('#cancel-chapter-btn').on('click', function () {
        hideModal('#chapter-modal');
        $('#save-chapter-btn').text(getJsString('addChapterBtn')).prop('disabled', false);
    });
    $('#chapter-modal').on('click', function (e) {
        if (e.target === this) {
            hideModal('#chapter-modal');
            $('#save-chapter-btn').text(getJsString('addChapterBtn')).prop('disabled', false);
        }
    });
    $('#chapter-form').on('submit', function (e) {
        e.preventDefault();
        $('#chapter-message').text('');
        let $saveBtn = $('#save-chapter-btn');
        setButtonProcessing($saveBtn, getJsString('processing'));
        let data = {
            LessonName: $('#chapter-lessonname').val(),
            EduYearCode: $('#chapter-eduyearcode').val()
        };

        if (isCenter) {
            data.TeacherCode = $('#chapter-teachercode').val();
            data.SubjectCode = $('#chapter-subjectcode').val();
            // For center users, YearCode may be set in dropdown or inferred by backend, optional:
            let yearVal = $('#chapter-yearcode').val();
            if (yearVal) data.YearCode = yearVal;
        } else {
            // Non-center user (teacher): always send TeacherCode as userTeacherCode
            data.TeacherCode = userTeacherCode;
            let val = $('#chapter-subjectcode').val();
            if (val && val.indexOf('|') !== -1) {
                let [subjectCode, yearCode] = val.split('|');
                data.SubjectCode = subjectCode;
                data.YearCode = yearCode;
            } else {
                data.SubjectCode = '';
                data.YearCode = '';
            }
        }

        // Validation: require all fields
        if (!data.LessonName || !data.SubjectCode || !data.EduYearCode || !data.TeacherCode || !data.YearCode) {
            $('#chapter-message').css('color', '#e74c3c').text(getJsString('pleaseFillAllFields'));
            resetButton($saveBtn);
            return;
        }

        // Debug: log payload
        console.log("AddChapter data:", data);

        $.ajax({
            url: '/Question/AddChapter',
            method: 'POST',
            data: data,
            success: function (result) {
                if (result.success) {
                    $('#chapter-message').css('color', '#27ae60').text(getJsString('saved'));
                    setTimeout(() => {
                        hideModal('#chapter-modal');
                        $('#save-chapter-btn').text(getJsString('addChapterBtn')).prop('disabled', false);
                        loadChapters();
                    }, 700);
                } else {
                    $('#chapter-message').css('color', '#e74c3c').text(
                        (result.message && result.message.value) ? result.message.value : getJsString('failed')
                    );
                }
            },
            error: function () {
                $('#chapter-message').css('color', '#e74c3c').text(getJsString('errorOccurred'));
            },
            complete: function () {
                resetButton($saveBtn);
            }
        });
    });

    // ------ ADD LESSON LOGIC ------
    $(document).on('click', '.add-lesson-btn', function () {
        setModalLabels();
        $('#lesson-name').val('');
        $('#lesson-message').text('');
        $('#lesson-rootcode').val($(this).data('rootcode'));
        $('#lesson-teachercode').val($(this).data('teachercode'));
        if (isCenter) {
            $('#lesson-subjectcode').val($(this).data('subjectcode'));
            $('#lesson-yearcode').val($(this).data('yearcode'));
        } else {
            $('#lesson-subjectcode').val(currentSubjectCode);
            $('#lesson-yearcode').val(currentYearCode);
        }
        $('#lesson-eduyearcode').val($(this).data('eduyearcode'));
        $('#lesson-chaptercode').val($(this).data('chaptercode'));
        $('#save-lesson-btn').text(getJsString('addLessonBtn')).prop('disabled', false);
        showModal('#lesson-modal');
    });
    $('#cancel-lesson-btn').on('click', function () {
        hideModal('#lesson-modal');
        $('#save-lesson-btn').text(getJsString('addLessonBtn')).prop('disabled', false);
    });
    $('#lesson-modal').on('click', function (e) {
        if (e.target === this) {
            hideModal('#lesson-modal');
            $('#save-lesson-btn').text(getJsString('addLessonBtn')).prop('disabled', false);
        }
    });
    $('#lesson-form').on('submit', function (e) {
        e.preventDefault();
        $('#lesson-message').text('');
        let $saveBtn = $('#save-lesson-btn');
        setButtonProcessing($saveBtn, getJsString('processing'));
        var formData = {
            LessonName: $('#lesson-name').val(),
            RootCode: $('#lesson-rootcode').val(),
            TeacherCode: $('#lesson-teachercode').val(),
            SubjectCode: $('#lesson-subjectcode').val(),
            EduYearCode: $('#lesson-eduyearcode').val(),
            ChapterCode: $('#lesson-chaptercode').val(),
            YearCode: $('#lesson-yearcode').val()
        };
        if (!formData.LessonName || !formData.RootCode || !formData.TeacherCode || !formData.SubjectCode || (!isCenter && !formData.YearCode)) {
            $('#lesson-message').css('color', '#e74c3c').text(getJsString('pleaseFillAllFields'));
            resetButton($saveBtn);
            return;
        }
        $.ajax({
            url: '/Question/AddLesson',
            method: 'POST',
            data: formData,
            success: function (result) {
                if (result.success) {
                    $('#lesson-message').css('color', '#27ae60').text(getJsString('saved'));
                    setTimeout(() => {
                        hideModal('#lesson-modal');
                        $('#save-lesson-btn').text(getJsString('addLessonBtn')).prop('disabled', false);
                        loadChapters(currentPage);
                    }, 700);
                } else {
                    $('#lesson-message').css('color', '#e74c3c').text(result.message || getJsString('failed'));
                }
            },
            error: function () {
                $('#lesson-message').css('color', '#e74c3c').text(getJsString('errorOccurred'));
            },
            complete: function () {
                resetButton($saveBtn);
            }
        });
    });

    // ========== SEARCH BAR LOGIC ==========
    $('#questionSearchBtn').on('click', function () {
        let term = $('#questionSearchInput').val().trim();
        if (term.length === 0) return;
        doQuestionSearch(term);
    });
    $('#questionSearchInput').on('keypress', function (e) {
        if (e.which === 13) {
            $('#questionSearchBtn').click();
        }
    });
    $('#questionSearchClearBtn').on('click', function () {
        $('#questionSearchInput').val('');
        $('#question-search-results').hide().html('');
        $('#chapters-container').show();
        $('#pagination-container').show();
        $('#questionSearchClearBtn').hide();
    });
    function doQuestionSearch(term) {
        let req = { term: term };
        if (!isCenter && currentSubjectCode && currentYearCode) {
            req.subjectCode = currentSubjectCode;
            req.yearCode = currentYearCode;
        }
        $('#question-search-results').html('<div style="padding:18px;">' + getJsString('processing') + '</div>').show();
        $('#chapters-container').hide();
        $('#pagination-container').hide();
        $('#questionSearchClearBtn').show();
        $.get('/Question/SearchQuestions', req, function (data) {
            let html = '';
            if (!data || data.length === 0) {
                html = `<div style="padding:18px; color:#888;">${getJsString('noQuestionsFound')} "<b>${$('<div/>').text(term).html()}</b>".</div>`;
            } else {
                html = `<div style="padding-bottom:8px;color:#444;">${getJsString('foundQuestions').replace('{0}', data.length)}</div><ul style="padding-left:18px;">`;
                data.forEach(function (q) {
                    html += '<li style="margin-bottom:8px;">';
                    html += `<span class="question-content">${$('<div/>').text(q.questionContent).html()}</span>`;
                    html += ` <span style="color:#aaa;font-size:13px;">[${getJsString('lesson')}: ${q.lessonName || '-'}]</span>`;
                    html += '</li>';
                });
                html += '</ul>';
            }
            html += `<button id="questionSearchBackBtn" class="modern-btn btn-cancel" style="margin-top:14px;">${getJsString('backBtn')}</button>`;
            $('#question-search-results').html(html).show();
        });
    }
    $(document).on('click', '#questionSearchBackBtn', function () {
        $('#question-search-results').hide().html('');
        $('#chapters-container').show();
        $('#pagination-container').show();
        $('#questionSearchInput').val('');
        $('#questionSearchClearBtn').hide();
    });

    // ========== LOAD CHAPTERS ==========
    function loadChapters(page = 1) {
        currentPage = page;
        let req = { page: currentPage, pageSize: pageSize };
        if (!isCenter && currentSubjectCode && currentYearCode) {
            req.subjectCode = currentSubjectCode;
            req.yearCode = currentYearCode;
        }
        $.ajax({
            url: '/Question/GetChaptersWithLessonsAndQuestions',
            method: 'GET',
            dataType: 'json',
            data: req,
            success: function (result) {
                let data = result.chapters;
                let totalCount = result.totalCount;
                let html = '';
                if (data.length === 0) {
                    html = `<div style="padding:32px;text-align:center;color:#888;">${getJsString('noChapters')}</div>`;
                }
                data.forEach(function (item, idx) {
                    let chapterBlockAddLessonBtn = `<button class="btn-table add add-lesson-btn"
                        data-rootcode="${item.rootCode || ''}"
                        data-chaptercode="${item.chapterCode || ''}"
                        data-yearcode="${item.yearCode || ''}"
                        data-eduyearcode="${item.eduYearCode || ''}"
                        data-teachercode="${item.teacherCode || ''}"
                        data-subjectcode="${item.subjectCode || ''}">
                        <i class="fas fa-plus"></i> ${getJsString('addLessonBtn')}
                    </button>`;

                    html += `<div class="chapter-block">
                        <div class="chapter-header" data-idx="${idx}">
                            <span class="chapter-icon"><i class="fa-solid fa-layer-group"></i></span>
                            <span>${item.chapterName}</span>
                            <span style="margin-left:auto;"><i class="fa-solid fa-chevron-down"></i></span>
                            ${chapterBlockAddLessonBtn}
                        </div>
                        <div class="lessons-list" id="lessons-list-${idx}">`;
                    if (item.lessons.length > 0) {
                        item.lessons.forEach(function (lesson) {
                            html += `<div class="lesson-block" data-lesson="${lesson.lessonCode}">
                                <div class="lesson-header">
                                    <span class="lesson-title"><i class="fa-solid fa-book"></i> ${lesson.lessonName}</span>
                                    <button class="btn-table add add-question-btn" data-lesson="${lesson.lessonCode}" data-lessonname="${lesson.lessonName}">
                                        <i class="fas fa-plus"></i> ${getJsString('addQuestionBtn')}
                                    </button>
                                </div>
                                <div class="questions-list" id="questions-list-${lesson.lessonCode}">`;

                            if (lesson.questions && lesson.questions.length > 0) {
                                lesson.questions.forEach(function (q) {
                                    html += `<div class="question-row" data-question="${q.questionCode}">
                                        <span class="question-content">${q.questionContent}</span>
                                        <div class="question-actions">
                                            <button class="btn-table stats show-answers-btn" data-question="${q.questionCode}">
                                                <i class="fas fa-chevron-down"></i> ${getJsString('showAnswersBtn')}
                                            </button>
                                            <button class="btn-table add add-answers-btn" data-question="${q.questionCode}" data-questioncontent="${q.questionContent}">
                                                <i class="fas fa-plus"></i> ${getJsString('addAnswersBtn')}
                                            </button>
                                            <button class="btn-table edit edit-question-btn" data-question='${JSON.stringify(q)}'>
                                                <i class="fas fa-pencil"></i><span style="display:none;">${getJsString('editBtn')}</span>
                                            </button>
                                            <button class="btn-table delete delete-question-btn" data-question="${q.questionCode}">
                                                <i class="fas fa-trash"></i><span style="display:none;">${getJsString('deleteBtn')}</span>
                                            </button>
                                        </div>
                                        <div class="answers-list" id="answers-list-${q.questionCode}" style="display:none;"></div>
                                    </div>`;
                                });
                            } else {
                                html += `<div class="empty-lessons"><i class="fa-regular fa-circle-xmark"></i> ${getJsString('noQuestions')}</div>`;
                            }

                            html += `</div></div>`;
                        });
                    } else {
                        html += `<div class="empty-lessons"><i class="fa-regular fa-circle-xmark"></i> ${getJsString('noLessons')}</div>`;
                    }
                    html += `</div></div>`;
                });
                $('#chapters-container').html(html);
                renderPagination(totalCount, currentPage, pageSize);
            },
            error: function (xhr, status, error) {
                $('#chapters-container').html('<div style="padding:32px;text-align:center;color:#e74c3c;">' + getJsString('errorOccurred') + '</div>');
            }
        });
    }
    function renderPagination(totalCount, currentPage, pageSize) {
        let totalPages = Math.ceil(totalCount / pageSize);
        if (totalPages <= 1) {
            $('#pagination-container').html('');
            return;
        }
        let html = '';
        if (currentPage > 1) {
            html += `<button class="modern-btn" id="prev-page-btn">${getJsString('previous')}</button>`;
        }
        html += ` ${getJsString('page')} ${currentPage} ${getJsString('of')} ${totalPages} `;
        if (currentPage < totalPages) {
            html += `<button class="modern-btn" id="next-page-btn">${getJsString('next')}</button>`;
        }
        $('#pagination-container').html(html);
        $('#prev-page-btn').off().on('click', function () {
            if (currentPage > 1) loadChapters(currentPage - 1);
        });
        $('#next-page-btn').off().on('click', function () {
            if (currentPage < totalPages) loadChapters(currentPage + 1);
        });
    }

    // ========== TOGGLE LESSONS ==========
    $(document).on('click', '.chapter-header', function () {
        let idx = $(this).data('idx');
        let $lessons = $('#lessons-list-' + idx);
        $lessons.slideToggle(180);
        $(this).find('.fa-chevron-down').toggleClass('fa-rotate-180');
    });

    // Show modal for add question
    $(document).on('click', '.add-question-btn', function (e) {
        e.stopPropagation();
        setModalLabels();
        const lessonCode = $(this).data('lesson');
        const lessonName = $(this).data('lessonname');

        $('#question-id').val('');
        $('#question-lessoncode').val(lessonCode);
        $('#question-content').val('');
        $('#question-examcode').val('');
        $('#question-modal-title').text(getJsString('addQuestionBtn') + ' "' + lessonName + '"');
        $('#question-message').text('');
        $('#save-question-btn').text(getJsString('addQuestionBtn')).prop('disabled', false);
        showModal('#question-modal');
    });

    // Show modal for edit question
    $(document).on('click', '.edit-question-btn', function (e) {
        e.stopPropagation();
        setModalLabels();
        const q = $(this).data('question');
        $('#question-id').val(q.questionCode);
        $('#question-lessoncode').val(q.lessonCode);
        $('#question-content').val(q.questionContent);
        $('#question-examcode').val(q.examCode || '');
        $('#question-modal-title').text(getJsString('editBtn'));
        $('#question-message').text('');
        $('#save-question-btn').text(getJsString('editBtn')).prop('disabled', false);
        showModal('#question-modal');
    });

    // Hide question modal
    $('#cancel-question-btn').on('click', function () {
        hideModal('#question-modal');
        $('#save-question-btn').text(getJsString('addQuestionBtn')).prop('disabled', false);
    });
    $('#question-modal').on('click', function (e) {
        if (e.target === this) {
            hideModal('#question-modal');
            $('#save-question-btn').text(getJsString('addQuestionBtn')).prop('disabled', false);
        }
    });

    // Add/Edit Question Submit
    $('#question-form').on('submit', function (e) {
        e.preventDefault();
        $('#question-message').text('');
        let $saveBtn = $('#save-question-btn');
        setButtonProcessing($saveBtn, getJsString('processing'));

        let formData = {
            QuestionCode: $('#question-id').val(),
            QuestionContent: $('#question-content').val(),
            LessonCode: $('#question-lessoncode').val(),
            ExamCode: $('#question-examcode').val() || null
        };

        let isEdit = !!formData.QuestionCode;
        let url = isEdit ? '/Question/EditQuestion' : '/Question/AddQuestion';

        if (!formData.QuestionContent || !formData.LessonCode) {
            $('#question-message').css('color', '#e74c3c').text(getJsString('pleaseFillRequiredFields'));
            resetButton($saveBtn);
            return;
        }

        $.ajax({
            url: url,
            method: 'POST',
            data: formData,
            success: function (result) {
                if (result.success) {
                    $('#question-message').css('color', '#27ae60').text(getJsString('saved'));
                    setTimeout(() => {
                        hideModal('#question-modal');
                        $('#save-question-btn').text(isEdit ? getJsString('editBtn') : getJsString('addQuestionBtn')).prop('disabled', false);
                        loadChapters(currentPage);
                    }, 800);
                } else {
                    $('#question-message').css('color', '#e74c3c').text(result.message || getJsString('failed'));
                }
            },
            error: function () {
                $('#question-message').css('color', '#e74c3c').text(getJsString('errorOccurred'));
            },
            complete: function () {
                resetButton($saveBtn);
            }
        });
    });

    // Delete Question
    $(document).on('click', '.delete-question-btn', function (e) {
        e.stopPropagation();
        let questionCode = $(this).data('question');
        if (!confirm(getJsString('deleteQuestionConfirm'))) return;

        $.ajax({
            url: '/Question/DeleteQuestion',
            method: 'POST',
            data: { QuestionCode: questionCode },
            success: function (result) {
                if (result.success) {
                    loadChapters(currentPage);
                } else {
                    alert(result.message || getJsString('failed'));
                }
            },
            error: function (xhr, status, error) {
                alert(getJsString('errorOccurred'));
            }
        });
    });

    // Show Answers Toggle
    $(document).on('click', '.show-answers-btn', function (e) {
        e.stopPropagation();
        let $btn = $(this);
        let questionCode = $btn.data('question');
        let $answersList = $('#answers-list-' + questionCode);

        if ($answersList.is(':visible')) {
            $answersList.slideUp(120);
            $btn.find('i').removeClass('fa-rotate-180');
        } else {
            $.ajax({
                url: '/Question/GetAnswersByQuestion',
                method: 'GET',
                data: { questionCode: questionCode },
                success: function (answers) {
                    let html = '';
                    if (answers.length === 0) {
                        html = `<div style="padding:12px;color:#888;">${getJsString('noAnswers')}</div>`;
                    } else {
                        answers.forEach(ans => {
                            html += `
                                <div class="answer-row" data-answer="${ans.answerCode}">
                                    <span class="answer-content">${ans.answerContent}</span>
                                    <span class="answer-istrue" style="color:${ans.isTrue ? '#27ae60' : '#e74c3c'}">
                                        ${ans.isTrue ? '✔️' : '❌'}
                                    </span>
                                    <div class="answer-actions">
                                        <button class="btn-table edit edit-answer-btn" data-answer='${JSON.stringify(ans)}'><i class="fas fa-pencil"></i><span style="display:none;">${getJsString('editBtn')}</span></button>
                                        <button class="btn-table delete delete-answer-btn" data-answer="${ans.answerCode}"><i class="fas fa-trash"></i><span style="display:none;">${getJsString('deleteBtn')}</span></button>
                                    </div>
                                </div>
                            `;
                        });
                    }
                    $answersList.html(html).slideDown(120);
                    $btn.find('i').addClass('fa-rotate-180');
                },
                error: function (xhr, status, error) {
                    $answersList.html('<div style="padding:12px;color:#e74c3c;">' + getJsString('errorOccurred') + '</div>').slideDown(120);
                }
            });
        }
    });

    // Show Add Answers Modal
    $(document).on('click', '.add-answers-btn', function (e) {
        e.stopPropagation();
        setModalLabels();
        const questionCode = $(this).data('question');
        const questionContent = $(this).data('questioncontent');

        $('#answers-questioncode').val(questionCode);
        $('#answers-fields').html('');
        addAnswerField();
        $('#answers-modal-title').text(getJsString('addAnswersBtn') + ': "' + questionContent + '"');
        $('#answers-message').text('');
        $('#save-answers-btn').text(getJsString('addAnswersBtn')).prop('disabled', false);
        showModal('#answers-modal');
    });

    // Hide answers modal
    $('#cancel-answers-btn').on('click', function () {
        hideModal('#answers-modal');
        $('#save-answers-btn').text(getJsString('addAnswersBtn')).prop('disabled', false);
    });
    $('#answers-modal').on('click', function (e) {
        if (e.target === this) {
            hideModal('#answers-modal');
            $('#save-answers-btn').text(getJsString('addAnswersBtn')).prop('disabled', false);
        }
    });

    // Add more answer fields
    $('#add-more-answer-btn').on('click', function (e) {
        e.preventDefault();
        addAnswerField();
    });

    function addAnswerField() {
        let idx = $('#answers-fields .answer-field-block').length;
        let fieldHtml = `
            <div class="answer-field-block" data-idx="${idx}" style="margin-bottom:8px;">
                <input type="text" name="AnswerContent" placeholder="${getJsString('answerContentPlaceholder')}" class="modern-input" required style="margin-right:8px;width:60%;" />
                <label style="margin-right:8px;">
                    <input type="checkbox" name="IsTrue" value="true" style="vertical-align:middle;" /> ${getJsString('isCorrect')}
                </label>
                <button type="button" class="modern-btn btn-cancel remove-answer-field-btn" style="padding:2px 8px;">${getJsString('removeAnswer')}</button>
            </div>
        `;
        $('#answers-fields').append(fieldHtml);
    }

    // Remove answer field
    $(document).on('click', '.remove-answer-field-btn', function () {
        $(this).closest('.answer-field-block').remove();
    });

    // Add Answers Submit
    $('#answers-form').on('submit', function (e) {
        e.preventDefault();
        $('#answers-message').text('');
        let $saveBtn = $('#save-answers-btn');
        setButtonProcessing($saveBtn, getJsString('processing'));

        let $blocks = $('#answers-fields .answer-field-block');
        if ($blocks.length === 0) {
            $('#answers-message').css('color', '#e74c3c').text(getJsString('addAtLeastOneAnswer'));
            resetButton($saveBtn);
            return;
        }

        let correctCount = 0;
        $blocks.each(function () {
            if ($(this).find('input[name="IsTrue"]').is(':checked')) {
                correctCount++;
            }
        });
        if (correctCount > 1) {
            $('#answers-message').css('color', '#e74c3c').text(getJsString('onlyOneCorrectAnswer'));
            resetButton($saveBtn);
            return;
        }

        let formData = new FormData();
        let questionCode = $('#answers-questioncode').val();
        formData.append('QuestionCode', questionCode);

        $blocks.each(function (i, block) {
            let answerContent = $(block).find('input[name="AnswerContent"]').val();
            let isTrue = $(block).find('input[name="IsTrue"]').is(':checked');
            formData.append('AnswerContent', answerContent);
            formData.append('IsTrue', isTrue);
        });

        $.ajax({
            url: '/Question/AddAnswers',
            method: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (result) {
                if (result.success) {
                    $('#answers-message').css('color', '#27ae60').text(getJsString('saved'));
                    setTimeout(() => {
                        hideModal('#answers-modal');
                        $('#save-answers-btn').text(getJsString('addAnswersBtn')).prop('disabled', false);
                        loadChapters(currentPage);
                    }, 800);
                } else {
                    $('#answers-message').css('color', '#e74c3c').text(result.message || getJsString('failed'));
                }
            },
            error: function () {
                $('#answers-message').css('color', '#e74c3c').text(getJsString('errorOccurred'));
            },
            complete: function () {
                resetButton($saveBtn);
            }
        });
    });

    // Show Edit Answer Modal
    $(document).on('click', '.edit-answer-btn', function (e) {
        e.stopPropagation();
        setModalLabels();
        const ans = $(this).data('answer');

        $('#edit-answer-code').val(ans.answerCode);
        $('#edit-answer-questioncode').val(ans.questionCode);
        $('#edit-answer-content').val(ans.answerContent);
        $('#edit-answer-istrue').prop('checked', ans.isTrue);
        $('#edit-answer-message').text('');
        $('#save-edit-answer-btn').text(getJsString('editBtn')).prop('disabled', false);
        showModal('#edit-answer-modal');
    });

    // Hide edit answer modal
    $('#cancel-edit-answer-btn').on('click', function () {
        hideModal('#edit-answer-modal');
        $('#save-edit-answer-btn').text(getJsString('editBtn')).prop('disabled', false);
    });
    $('#edit-answer-modal').on('click', function (e) {
        if (e.target === this) {
            hideModal('#edit-answer-modal');
            $('#save-edit-answer-btn').text(getJsString('editBtn')).prop('disabled', false);
        }
    });

    // Edit Answer Submit
    $('#edit-answer-form').on('submit', function (e) {
        e.preventDefault();
        $('#edit-answer-message').text('');
        let $saveBtn = $('#save-edit-answer-btn');
        setButtonProcessing($saveBtn, getJsString('processing'));

        let formData = {
            AnswerCode: $('#edit-answer-code').val(),
            QuestionCode: $('#edit-answer-questioncode').val(),
            AnswerContent: $('#edit-answer-content').val(),
            IsTrue: $('#edit-answer-istrue').is(':checked')
        };

        if (!formData.AnswerContent) {
            $('#edit-answer-message').css('color', '#e74c3c').text(getJsString('pleaseFillAllFields'));
            resetButton($saveBtn);
            return;
        }

        $.ajax({
            url: '/Question/EditAnswer',
            method: 'POST',
            data: formData,
            success: function (result) {
                if (result.success) {
                    $('#edit-answer-message').css('color', '#27ae60').text(getJsString('saved'));
                    setTimeout(() => {
                        hideModal('#edit-answer-modal');
                        $('#save-edit-answer-btn').text(getJsString('editBtn')).prop('disabled', false);
                        loadChapters(currentPage);
                    }, 800);
                } else {
                    $('#edit-answer-message').css('color', '#e74c3c').text(result.message || getJsString('failed'));
                }
            },
            error: function () {
                $('#edit-answer-message').css('color', '#e74c3c').text(getJsString('errorOccurred'));
            },
            complete: function () {
                resetButton($saveBtn);
            }
        });
    });

    // Delete Answer
    $(document).on('click', '.delete-answer-btn', function (e) {
        e.stopPropagation();
        let answerCode = $(this).data('answer');
        if (!confirm(getJsString('deleteAnswerConfirm'))) return;

        $.ajax({
            url: '/Question/DeleteAnswer',
            method: 'POST',
            data: { AnswerCode: answerCode },
            success: function (result) {
                if (result.success) {
                    loadChapters(currentPage);
                } else {
                    alert(result.message || getJsString('failed'));
                }
            },
            error: function () {
                alert(getJsString('errorOccurred'));
            }
        });
    });

    console.log("=== End of Question.js file ===");
});