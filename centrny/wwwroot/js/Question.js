console.log("=== Question.js file is being loaded ===");

$(document).ready(function () {
    console.log("=== jQuery ready function executed ===");

    // ---- USER/ROOT/TEACHER INFO BOX ----
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

    let currentPage = 1;
    let pageSize = 5;

    loadChapters();

    // ------ ADD CHAPTER LOGIC ------
    let isCenter = false;
    let userTeacherCode = 0;

    // Show Add Chapter modal
    $('#add-chapter-btn').on('click', function () {
        // Reset form
        $('#chapter-lessonname').val('');
        $('#chapter-message').text('');
        $('#chapter-eduyearcode').html('');
        $('#chapter-subjectcode').html('<option value="">Select</option>');
        $('#chapter-teachercode').html('');
        $('#chapter-teachercode-hidden').val('');
        $('#teacher-group').hide();

        // Determine if user is center
        $.get('/Question/IsUserCenter', function (res) {
            isCenter = res.isCenter;
            loadChapterYears();

            if (isCenter) {
                $('#teacher-group').show();
                loadChapterTeachers();
            } else {
                $('#teacher-group').hide();
                // Get teacher code for this root (the only teacher)
                $.get('/Question/GetTeachersByRoot', function (teachers) {
                    if (teachers.length > 0) {
                        userTeacherCode = teachers[0].teacherCode;
                        $('#chapter-teachercode-hidden').val(userTeacherCode);
                    }
                });
            }

            $('#chapter-modal').fadeIn(180);
        });
    });

    // Load years for chapter modal
    function loadChapterYears() {
        $.get('/Question/GetEduYearsByRoot', function (years) {
            let html = '<option value="">Select</option>';
            years.forEach(y => {
                html += `<option value="${y.eduYearCode}">${y.eduYearCode} - ${y.eduYearName}</option>`;
            });
            $('#chapter-eduyearcode').html(html);
        });
    }

    // Load teachers for chapter modal (if center)
    function loadChapterTeachers() {
        $.get('/Question/GetTeachersByRoot', function (teachers) {
            let html = '<option value="">Select</option>';
            teachers.forEach(t => {
                html += `<option value="${t.teacherCode}">${t.teacherName}</option>`;
            });
            $('#chapter-teachercode').html(html);
        });
    }

    // Load subjects for chapter modal
    function loadChapterSubjects(teacherCode, eduYearCode) {
        if (!teacherCode || !eduYearCode) {
            $('#chapter-subjectcode').html('<option value="">Select</option>');
            return;
        }
        $.get('/Question/GetSubjectsByTeacherYear', { teacherCode, eduYearCode }, function (subjects) {
            let html = '<option value="">Select</option>';
            subjects.forEach(s => {
                html += `<option value="${s.subjectCode}">${s.subjectName}</option>`;
            });
            $('#chapter-subjectcode').html(html);
        });
    }

    // On EduYear change, reload subjects and (if center) teachers
    $('#chapter-eduyearcode').on('change', function () {
        let eduYearCode = $(this).val();

        if (isCenter) {
            let teacherCode = $('#chapter-teachercode').val();
            loadChapterSubjects(teacherCode, eduYearCode);
        } else {
            loadChapterSubjects(userTeacherCode, eduYearCode);
        }
    });

    // On Teacher change, reload subjects
    $('#chapter-teachercode').on('change', function () {
        let eduYearCode = $('#chapter-eduyearcode').val();
        let teacherCode = $(this).val();
        loadChapterSubjects(teacherCode, eduYearCode);
    });

    // Cancel Add Chapter
    $('#cancel-chapter-btn').on('click', function () {
        $('#chapter-modal').fadeOut(120);
    });

    $('#chapter-modal').on('click', function (e) {
        if (e.target === this) {
            $('#chapter-modal').fadeOut(120);
        }
    });

    // Add Chapter Submit
    $('#chapter-form').on('submit', function (e) {
        e.preventDefault();
        $('#chapter-message').text('');

        let data = {
            LessonName: $('#chapter-lessonname').val(),
            EduYearCode: $('#chapter-eduyearcode').val(),
            TeacherCode: isCenter ? $('#chapter-teachercode').val() : $('#chapter-teachercode-hidden').val(),
            SubjectCode: $('#chapter-subjectcode').val()
        };

        if (!data.LessonName || !data.EduYearCode || !data.TeacherCode || !data.SubjectCode) {
            $('#chapter-message').css('color', '#e74c3c').text('Please fill all fields.');
            return;
        }

        $.post('/Question/AddChapter', data, function (result) {
            if (result.success) {
                $('#chapter-message').css('color', '#27ae60').text('Saved!');
                setTimeout(() => {
                    $('#chapter-modal').fadeOut(120);
                    loadChapters();
                }, 700);
            } else {
                $('#chapter-message').css('color', '#e74c3c').text(result.message || 'Failed.');
            }
        });
    });

    // ------ END ADD CHAPTER LOGIC ------

    // ------ ADD LESSON LOGIC ------
    $(document).on('click', '.add-lesson-btn', function () {
        $('#lesson-name').val('');
        $('#lesson-message').text('');
        $('#lesson-rootcode').val($(this).data('rootcode'));
        $('#lesson-teachercode').val($(this).data('teachercode'));
        $('#lesson-subjectcode').val($(this).data('subjectcode'));
        $('#lesson-eduyearcode').val($(this).data('eduyearcode'));
        $('#lesson-chaptercode').val($(this).data('chaptercode'));
        $('#lesson-yearcode').val($(this).data('yearcode'));
        $('#lesson-modal').fadeIn(180);
    });

    $('#cancel-lesson-btn').on('click', function () {
        $('#lesson-modal').fadeOut(120);
    });
    $('#lesson-modal').on('click', function (e) {
        if (e.target === this) $('#lesson-modal').fadeOut(120);
    });

    $('#lesson-form').on('submit', function (e) {
        e.preventDefault();
        $('#lesson-message').text('');
        var formData = {
            LessonName: $('#lesson-name').val(),
            RootCode: $('#lesson-rootcode').val(),
            TeacherCode: $('#lesson-teachercode').val(),
            SubjectCode: $('#lesson-subjectcode').val(),
            EduYearCode: $('#lesson-eduyearcode').val(),
            ChapterCode: $('#lesson-chaptercode').val(),
            YearCode: $('#lesson-yearcode').val()
        };
        $.post('/Question/AddLesson', formData, function (result) {
            if (result.success) {
                $('#lesson-message').css('color', '#27ae60').text('Saved!');
                setTimeout(() => {
                    $('#lesson-modal').fadeOut(100);
                    loadChapters(currentPage);
                }, 700);
            } else {
                $('#lesson-message').css('color', '#e74c3c').text(result.message || 'Failed.');
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
        $('#question-search-results').html('<div style="padding:18px;">Searching...</div>').show();
        $('#chapters-container').hide();
        $('#pagination-container').hide();
        $('#questionSearchClearBtn').show();

        $.get('/Question/SearchQuestions', { term }, function (data) {
            let html = '';
            if (!data || data.length === 0) {
                html = '<div style="padding:18px; color:#888;">No questions found for "<b>' + $('<div/>').text(term).html() + '</b>".</div>';
            } else {
                html = '<div style="padding-bottom:8px;color:#444;">Found ' + data.length + ' question(s):</div><ul style="padding-left:18px;">';
                data.forEach(function (q) {
                    html += '<li style="margin-bottom:8px;">';
                    html += '<span class="question-content">' + $('<div/>').text(q.questionContent).html() + '</span>';
                    html += ' <span style="color:#aaa;font-size:13px;">[Lesson: ' + (q.lessonName || '-') + ']</span>';
                    html += '</li>';
                });
                html += '</ul>';
            }
            html += '<button id="questionSearchBackBtn" class="modern-btn btn-cancel" style="margin-top:14px;">Back</button>';
            $('#question-search-results').html(html).show();
        });
    }

    // Back/clear from search results
    $(document).on('click', '#questionSearchBackBtn', function () {
        $('#question-search-results').hide().html('');
        $('#chapters-container').show();
        $('#pagination-container').show();
        $('#questionSearchInput').val('');
        $('#questionSearchClearBtn').hide();
    });

    // ========== EXISTING QUESTION/ANSWER UI LOGIC BELOW ==========

    function loadChapters(page = 1) {
        currentPage = page;
        $.ajax({
            url: '/Question/GetChaptersWithLessonsAndQuestions',
            method: 'GET',
            dataType: 'json',
            data: { page: currentPage, pageSize: pageSize },
            success: function (result) {
                let data = result.chapters;
                let totalCount = result.totalCount;
                let html = '';
                if (data.length === 0) {
                    html = `<div style="padding:32px;text-align:center;color:#888;">No chapters found.</div>`;
                }
                data.forEach(function (item, idx) {
                    let chapterBlockAddLessonBtn = `<button class="modern-btn add-lesson-btn"
                        data-rootcode="${item.rootCode || ''}"
                        data-chaptercode="${item.chapterCode || ''}"
                        data-yearcode="${item.yearCode || ''}"
                        data-eduyearcode="${item.eduYearCode || ''}"
                        data-teachercode="${item.teacherCode || ''}"
                        data-subjectcode="${item.subjectCode || ''}">
                        Add Lesson
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
                                    <button class="styled-btn add-question-btn" data-lesson="${lesson.lessonCode}" data-lessonname="${lesson.lessonName}">
                                        <i class="fa-solid fa-plus"></i> Add Question
                                    </button>
                                </div>
                                <div class="questions-list" id="questions-list-${lesson.lessonCode}">`;

                            if (lesson.questions && lesson.questions.length > 0) {
                                lesson.questions.forEach(function (q) {
                                    html += `<div class="question-row" data-question="${q.questionCode}">
                                        <span class="question-content">${q.questionContent}</span>
                                        <div class="question-actions">
                                            <button class="styled-btn show-answers-btn" data-question="${q.questionCode}">
                                                <i class="fa-solid fa-chevron-down"></i> Show Answers
                                            </button>
                                            <button class="styled-btn add-answers-btn" data-question="${q.questionCode}" data-questioncontent="${q.questionContent}">
                                                <i class="fa-solid fa-plus"></i> Add Answers
                                            </button>
                                            <button class="icon-btn edit-question-btn" data-question='${JSON.stringify(q)}'><i class="fa-solid fa-edit"></i></button>
                                            <button class="icon-btn delete-question-btn" data-question="${q.questionCode}"><i class="fa-solid fa-trash"></i></button>
                                        </div>
                                        <div class="answers-list" id="answers-list-${q.questionCode}" style="display:none;"></div>
                                    </div>`;
                                });
                            } else {
                                html += `<div class="empty-lessons"><i class="fa-regular fa-circle-xmark"></i> No questions for this lesson.</div>`;
                            }

                            html += `</div></div>`;
                        });
                    } else {
                        html += `<div class="empty-lessons"><i class="fa-regular fa-circle-xmark"></i> No lessons available for this chapter.</div>`;
                    }
                    html += `</div></div>`;
                });
                $('#chapters-container').html(html);
                renderPagination(totalCount, currentPage, pageSize);
            },
            error: function (xhr, status, error) {
                $('#chapters-container').html('<div style="padding:32px;text-align:center;color:#e74c3c;">Error loading chapters.</div>');
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
            html += `<button class="modern-btn" id="prev-page-btn">Previous</button>`;
        }
        html += ` Page ${currentPage} of ${totalPages} `;
        if (currentPage < totalPages) {
            html += `<button class="modern-btn" id="next-page-btn">Next</button>`;
        }
        $('#pagination-container').html(html);

        $('#prev-page-btn').off().on('click', function () {
            if (currentPage > 1) loadChapters(currentPage - 1);
        });
        $('#next-page-btn').off().on('click', function () {
            if (currentPage < totalPages) loadChapters(currentPage + 1);
        });
    }

    // Toggle lessons dropdown
    $(document).on('click', '.chapter-header', function () {
        let idx = $(this).data('idx');
        let $lessons = $('#lessons-list-' + idx);
        $lessons.slideToggle(180);
        $(this).find('.fa-chevron-down').toggleClass('fa-rotate-180');
    });

    // Show modal for add question
    $(document).on('click', '.add-question-btn', function (e) {
        e.stopPropagation();
        const lessonCode = $(this).data('lesson');
        const lessonName = $(this).data('lessonname');

        $('#question-id').val('');
        $('#question-lessoncode').val(lessonCode);
        $('#question-content').val('');
        $('#question-examcode').val('');
        $('#question-modal-title').text('Add Question to "' + lessonName + '"');
        $('#question-message').text('');
        $('#question-modal').fadeIn(180);
    });

    // Show modal for edit question
    $(document).on('click', '.edit-question-btn', function (e) {
        e.stopPropagation();
        const q = $(this).data('question');

        $('#question-id').val(q.questionCode);
        $('#question-lessoncode').val(q.lessonCode);
        $('#question-content').val(q.questionContent);
        $('#question-examcode').val(q.examCode || '');
        $('#question-modal-title').text('Edit Question');
        $('#question-message').text('');
        $('#question-modal').fadeIn(180);
    });

    // Hide question modal
    $('#cancel-question-btn').on('click', function () {
        $('#question-modal').fadeOut(150);
    });

    $('#question-modal').on('click', function (e) {
        if (e.target === this) {
            $('#question-modal').fadeOut(150);
        }
    });

    // Add/Edit Question Submit
    $('#question-form').on('submit', function (e) {
        e.preventDefault();
        $('#question-message').text('');

        let formData = {
            QuestionCode: $('#question-id').val(),
            QuestionContent: $('#question-content').val(),
            LessonCode: $('#question-lessoncode').val(),
            ExamCode: $('#question-examcode').val() || null
        };

        let url = formData.QuestionCode ? '/Question/EditQuestion' : '/Question/AddQuestion';

        $.ajax({
            url: url,
            method: 'POST',
            data: formData,
            success: function (result) {
                if (result.success) {
                    $('#question-message').css('color', '#27ae60').text('Saved!');
                    setTimeout(() => {
                        $('#question-modal').fadeOut(100);
                        loadChapters(currentPage);
                    }, 800);
                } else {
                    $('#question-message').css('color', '#e74c3c').text(result.message || 'Failed.');
                }
            },
            error: function (xhr, status, error) {
                $('#question-message').css('color', '#e74c3c').text('An error occurred.');
            }
        });
    });

    // Delete Question
    $(document).on('click', '.delete-question-btn', function (e) {
        e.stopPropagation();
        let questionCode = $(this).data('question');
        if (!confirm('Are you sure you want to delete this question?')) return;

        $.ajax({
            url: '/Question/DeleteQuestion',
            method: 'POST',
            data: { QuestionCode: questionCode },
            success: function (result) {
                if (result.success) {
                    loadChapters(currentPage);
                } else {
                    alert(result.message || 'Failed to delete the question.');
                }
            },
            error: function (xhr, status, error) {
                alert('An error occurred while deleting.');
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
                        html = `<div style="padding:12px;color:#888;">No answers for this question.</div>`;
                    } else {
                        answers.forEach(ans => {
                            html += `
                                <div class="answer-row" data-answer="${ans.answerCode}">
                                    <span class="answer-content">${ans.answerContent}</span>
                                    <span class="answer-istrue" style="color:${ans.isTrue ? '#27ae60' : '#e74c3c'}">
                                        ${ans.isTrue ? '✔️' : '❌'}
                                    </span>
                                    <div class="answer-actions">
                                        <button class="icon-btn edit-answer-btn" data-answer='${JSON.stringify(ans)}'><i class="fa-solid fa-edit"></i></button>
                                        <button class="icon-btn delete-answer-btn" data-answer="${ans.answerCode}"><i class="fa-solid fa-trash"></i></button>
                                    </div>
                                </div>
                            `;
                        });
                    }
                    $answersList.html(html).slideDown(120);
                    $btn.find('i').addClass('fa-rotate-180');
                },
                error: function (xhr, status, error) {
                    $answersList.html('<div style="padding:12px;color:#e74c3c;">Error loading answers.</div>').slideDown(120);
                }
            });
        }
    });

    // Show Add Answers Modal
    $(document).on('click', '.add-answers-btn', function (e) {
        e.stopPropagation();
        const questionCode = $(this).data('question');
        const questionContent = $(this).data('questioncontent');

        $('#answers-questioncode').val(questionCode);
        $('#answers-fields').html('');
        addAnswerField();
        $('#answers-modal-title').text('Add Answers to: "' + questionContent + '"');
        $('#answers-message').text('');
        $('#answers-modal').fadeIn(180);
    });

    // Hide answers modal
    $('#cancel-answers-btn').on('click', function () {
        $('#answers-modal').fadeOut(150);
    });

    $('#answers-modal').on('click', function (e) {
        if (e.target === this) {
            $('#answers-modal').fadeOut(150);
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
                <input type="text" name="AnswerContent" placeholder="Answer Content" class="modern-input" required style="margin-right:8px;width:60%;" />
                <label style="margin-right:8px;">
                    <input type="checkbox" name="IsTrue" value="true" style="vertical-align:middle;" /> Is Correct
                </label>
                <button type="button" class="modern-btn btn-cancel remove-answer-field-btn" style="padding:2px 8px;">Remove</button>
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

        let $blocks = $('#answers-fields .answer-field-block');
        if ($blocks.length === 0) {
            $('#answers-message').css('color', '#e74c3c').text('Add at least one answer.');
            return;
        }

        let correctCount = 0;
        $blocks.each(function () {
            if ($(this).find('input[name="IsTrue"]').is(':checked')) {
                correctCount++;
            }
        });
        if (correctCount > 1) {
            $('#answers-message').css('color', '#e74c3c').text('Only one correct answer is allowed per question.');
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
                    $('#answers-message').css('color', '#27ae60').text('Saved!');
                    setTimeout(() => {
                        $('#answers-modal').fadeOut(100);
                        loadChapters(currentPage);
                    }, 800);
                } else {
                    $('#answers-message').css('color', '#e74c3c').text(result.message || 'Failed.');
                }
            },
            error: function () {
                $('#answers-message').css('color', '#e74c3c').text('An error occurred.');
            }
        });
    });

    // Show Edit Answer Modal
    $(document).on('click', '.edit-answer-btn', function (e) {
        e.stopPropagation();
        const ans = $(this).data('answer');

        $('#edit-answer-code').val(ans.answerCode);
        $('#edit-answer-questioncode').val(ans.questionCode);
        $('#edit-answer-content').val(ans.answerContent);
        $('#edit-answer-istrue').prop('checked', ans.isTrue);
        $('#edit-answer-message').text('');
        $('#edit-answer-modal').fadeIn(180);
    });

    // Hide edit answer modal
    $('#cancel-edit-answer-btn').on('click', function () {
        $('#edit-answer-modal').fadeOut(150);
    });

    $('#edit-answer-modal').on('click', function (e) {
        if (e.target === this) {
            $('#edit-answer-modal').fadeOut(150);
        }
    });

    // Edit Answer Submit
    $('#edit-answer-form').on('submit', function (e) {
        e.preventDefault();
        $('#edit-answer-message').text('');

        let formData = {
            AnswerCode: $('#edit-answer-code').val(),
            QuestionCode: $('#edit-answer-questioncode').val(),
            AnswerContent: $('#edit-answer-content').val(),
            IsTrue: $('#edit-answer-istrue').is(':checked')
        };

        $.ajax({
            url: '/Question/EditAnswer',
            method: 'POST',
            data: formData,
            success: function (result) {
                if (result.success) {
                    $('#edit-answer-message').css('color', '#27ae60').text('Saved!');
                    setTimeout(() => {
                        $('#edit-answer-modal').fadeOut(100);
                        loadChapters(currentPage);
                    }, 800);
                } else {
                    $('#edit-answer-message').css('color', '#e74c3c').text(result.message || 'Failed.');
                }
            },
            error: function () {
                $('#edit-answer-message').css('color', '#e74c3c').text('An error occurred.');
            }
        });
    });

    // Delete Answer
    $(document).on('click', '.delete-answer-btn', function (e) {
        e.stopPropagation();
        let answerCode = $(this).data('answer');
        if (!confirm('Are you sure you want to delete this answer?')) return;

        $.ajax({
            url: '/Question/DeleteAnswer',
            method: 'POST',
            data: { AnswerCode: answerCode },
            success: function (result) {
                if (result.success) {
                    loadChapters(currentPage);
                } else {
                    alert(result.message || 'Failed to delete the answer.');
                }
            },
            error: function () {
                alert('An error occurred while deleting.');
            }
        });
    });

    console.log("=== Event handlers attached ===");
});

console.log("=== End of Question.js file ===");