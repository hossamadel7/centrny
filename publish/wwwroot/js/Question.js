console.log("=== Enhanced Question.js file is being loaded ===");

// Global state variables (REQUIRED)
let isCenter = false;
let selectedSubjectCode = null;
let selectedYearCode = null;
let selectedLessonCode = null;
let selectedLessonName = '';
let currentPage = 1;
let pageSize = 10;

// User context (from backend/session)
window.userRootCode = null;
window.userTeacherCode = null;
window.userEduYearCode = null;

// Enhanced modal variables
let answerCounter = 0;
let isEditMode = false;

// For passing selected year to Add Chapter
let lastSelectedYearCode = null;

// =========================
// CORE UTILITY FUNCTIONS (REQUIRED)
// =========================

function getJsString(key) {
    key = key.replace(/-([a-z])/g, function (g) { return g[1].toUpperCase(); });
    return $('#js-localization').data(key) || key;
}

function setButtonProcessing($btn, processingText) {
    if (!$btn.data('original-text')) {
        $btn.data('original-text', $btn.text());
    }
    $btn.html(processingText).prop('disabled', true);
}

function resetButton($btn) {
    $btn.html($btn.data('original-text') || getJsString('save')).prop('disabled', false);
}

function showModal(modalId) {
    $(modalId).css('display', 'flex');
}

function hideModal(modalId) {
    $(modalId).css('display', 'none');
}

// =========================
// ENHANCED ANSWER FIELD MANAGEMENT
// =========================

function initializeAnswerFields() {
    $('#answers-container').empty();
    answerCounter = 0;
    addAnswerField();
    addAnswerField();
    setTimeout(() => {
        $('input[name="correctAnswer"]:first').prop('checked', true);
        updateCorrectAnswerVisuals();
    }, 100);
}

function addAnswerField(content = '', isCorrect = false) {
    answerCounter++;
    // Use localized placeholder
    let placeholder = getJsString('enterAnswerOption');
    if (placeholder && placeholder.includes('{0}')) {
        placeholder = placeholder.replace('{0}', answerCounter);
    } else {
        placeholder = `Enter answer option ${answerCounter}...`;
    }
    const answerHtml = `
        <div class="answer-field" data-answer-id="${answerCounter}">
            <div class="answer-number">${answerCounter}</div>
            <input type="text" 
                   class="answer-input" 
                   name="answer_${answerCounter}" 
                   placeholder="${placeholder}" 
                   value="${content}"
                   required>
            <input type="radio" 
                   class="correct-radio" 
                   name="correctAnswer" 
                   value="${answerCounter}"
                   ${isCorrect ? 'checked' : ''}
                   title="${getJsString('isCorrectAnswer') || 'Mark as correct answer'}">
            <button type="button" class="remove-answer-btn" onclick="removeAnswerField(${answerCounter})">
                <i class="fas fa-trash"></i>
            </button>
        </div>
    `;
    $('#answers-container').append(answerHtml);
    updateAnswerNumbers();
    updateCorrectAnswerVisuals();
    setTimeout(() => {
        $(`input[name="answer_${answerCounter}"]`).focus();
    }, 100);
}

function removeAnswerField(answerId) {
    const $field = $(`.answer-field[data-answer-id="${answerId}"]`);
    const wasCorrect = $field.find('input[name="correctAnswer"]').is(':checked');
    if ($('.answer-field').length <= 2) {
        showValidationMessage(getJsString('atLeastTwoAnswersRequired') || 'You must have at least 2 answer options', 'error');
        return;
    }
    $field.fadeOut(300, function () {
        $(this).remove();
        updateAnswerNumbers();
        if (wasCorrect) {
            $('input[name="correctAnswer"]:first').prop('checked', true);
        }
        updateCorrectAnswerVisuals();
    });
}

function updateAnswerNumbers() {
    $('.answer-field').each(function (index) {
        // Use localized placeholder
        let placeholder = getJsString('enterAnswerOption');
        if (placeholder && placeholder.includes('{0}')) {
            placeholder = placeholder.replace('{0}', index + 1);
        } else {
            placeholder = `Enter answer option ${index + 1}...`;
        }
        $(this).find('.answer-number').text(index + 1);
        $(this).find('.answer-input').attr('placeholder', placeholder);
    });
}

function updateCorrectAnswerVisuals() {
    $('.answer-field').removeClass('correct-answer');
    const selectedRadio = $('input[name="correctAnswer"]:checked');
    if (selectedRadio.length > 0) {
        selectedRadio.closest('.answer-field').addClass('correct-answer');
    }
}

function showValidationMessage(message, type = 'error') {
    const $messageDiv = $('#question-message');
    $messageDiv.removeClass('error success').addClass(type).text(message).show();
    if (type === 'error') {
        $messageDiv.css({
            'background-color': '#f8d7da',
            'color': '#721c24',
            'border': '1px solid #f5c6cb'
        });
        setTimeout(() => {
            $messageDiv.fadeOut();
        }, 4000);
    } else if (type === 'success') {
        $messageDiv.css({
            'background-color': '#d4edda',
            'color': '#155724',
            'border': '1px solid #c3e6cb'
        });
    }
}

function validateQuestionForm() {
    $('.answer-field').removeClass('error');
    $('#question-message').hide();
    const questionContent = $('#question-content').val().trim();
    const answers = [];
    let hasErrors = false;
    if (!questionContent) {
        showValidationMessage(getJsString('questionContentRequired') || 'Question content is required', 'error');
        $('#question-content').focus();
        return false;
    }

    $('.answer-field').each(function () {
        const $field = $(this);
        const answerText = $field.find('.answer-input').val().trim();
        if (!answerText) {
            $field.addClass('error');
            hasErrors = true;
        } else if (answerText.length < 1) {
            $field.addClass('error');
            hasErrors = true;
        } else {
            answers.push(answerText);
        }
    });
    if (hasErrors) {
        showValidationMessage(getJsString('allAnswerFieldsRequired') || 'All answer fields must be filled with meaningful content', 'error');
        return false;
    }
    if (answers.length < 2) {
        showValidationMessage(getJsString('atLeastTwoAnswersRequired') || 'At least 2 answers are required', 'error');
        return false;
    }
    const duplicates = answers.filter((item, index) =>
        answers.findIndex(a => a.toLowerCase() === item.toLowerCase()) !== index
    );
    if (duplicates.length > 0) {
        showValidationMessage(getJsString('duplicateAnswersNotAllowed') || 'Duplicate answers are not allowed', 'error');
        return false;
    }
    if (!$('input[name="correctAnswer"]:checked').length) {
        showValidationMessage(getJsString('selectCorrectAnswer') || 'Please select the correct answer by clicking a radio button', 'error');
        return false;
    }
    return true;
}

function collectFormData() {
    const answers = [];
    let correctAnswerIndex = -1;
    $('.answer-field').each(function (index) {
        const answerText = $(this).find('.answer-input').val().trim();
        const isCorrect = $(this).find('input[name="correctAnswer"]').is(':checked');
        answers.push(answerText);
        if (isCorrect) {
            correctAnswerIndex = index;
        }
    });
    return {
        questionContent: $('#question-content').val().trim(),
        examCode: $('#question-examcode').val() || null,
        lessonCode: $('#question-lessoncode').val(),
        answers: answers,
        correctAnswerIndex: correctAnswerIndex,
        questionCode: $('#question-id').val() || null
    };
}

// =========================
// ENHANCED MODAL FUNCTIONS
// =========================

function showAddQuestionModal() {
    if (!selectedLessonCode) {
        alert(getJsString('lessonSelected') || 'Please select a lesson first');
        return;
    }
    isEditMode = false;
    $('#question-modal-title').html('<i class="fas fa-plus"></i> ' + (getJsString('addQuestionBtn') || 'Add Question') + ' ' + (getJsString('addAnswersBtn') || 'with Answers'));
    $('#question-id').val('');
    $('#question-lessoncode').val(selectedLessonCode);
    $('#question-content').val('');
    $('#question-examcode').val('');
    $('#question-message').hide();
    $('#save-question-btn').html('<i class="fas fa-save"></i> ' + (getJsString('save') || 'Save') + ' ' + (getJsString('addAnswersBtn') || 'Question & Answers')).prop('disabled', false);
    initializeAnswerFields();
    showModal('#question-modal');
    setTimeout(() => {
        $('#question-content').focus();
    }, 300);
}

function showEditQuestionModal(question) {
    isEditMode = true;
    $('#question-modal-title').html('<i class="fas fa-edit"></i> ' + (getJsString('editBtn') || 'Edit') + ' ' + (getJsString('addAnswersBtn') || 'Question with Answers'));
    $('#question-id').val(question.questionCode);
    $('#question-lessoncode').val(question.lessonCode);
    $('#question-content').val(question.questionContent);
    $('#question-examcode').val(question.examCode || '');
    $('#question-message').hide();
    $('#save-question-btn').html('<i class="fas fa-save"></i> ' + (getJsString('editBtn') || 'Update') + ' ' + (getJsString('addAnswersBtn') || 'Question & Answers')).prop('disabled', false);
    loadQuestionAnswers(question.questionCode);
    showModal('#question-modal');
    setTimeout(() => {
        $('#question-content').focus();
    }, 300);
}

function loadQuestionAnswers(questionCode) {
    $('#answers-container').empty();
    answerCounter = 0;
    $('#answers-container').html('<div style="text-align:center;padding:20px;color:#666;"><i class="fas fa-spinner fa-spin"></i> ' + (getJsString('loadingAnswers') || 'Loading answers...') + '</div>');
    $.get('/Question/GetAnswersByQuestion', { questionCode: questionCode }, function (answers) {
        $('#answers-container').empty();
        answerCounter = 0;
        if (answers && answers.length > 0) {
            answers.forEach(answer => {
                addAnswerField(answer.answerContent, answer.isTrue);
            });
        } else {
            initializeAnswerFields();
        }
        setTimeout(() => {
            updateCorrectAnswerVisuals();
        }, 100);
    }).fail(function () {
        console.error('Failed to load existing answers');
        $('#answers-container').empty();
        answerCounter = 0;
        initializeAnswerFields();
        showValidationMessage(getJsString('couldNotLoadExistingAnswers') || 'Could not load existing answers. You can add new ones.', 'error');
    });
}

function saveQuestionWithAnswers() {
    if (!validateQuestionForm()) {
        return;
    }
    const formData = collectFormData();
    const $saveBtn = $('#save-question-btn');
    setButtonProcessing($saveBtn, '<i class="fas fa-spinner fa-spin"></i> ' + (getJsString('savingQuestionAndAnswers') || 'Saving question and answers...'));
    showValidationMessage(getJsString('savingQuestionAndAnswers') || 'Saving question and answers...', 'success');
    const url = isEditMode ? '/Question/UpdateQuestionWithAnswers' : '/Question/AddQuestionWithAnswers';
    const requestData = {
        questionContent: formData.questionContent,
        examCode: formData.examCode,
        answers: formData.answers,
        correctAnswerIndex: formData.correctAnswerIndex
    };
    if (isEditMode) {
        requestData.questionCode = formData.questionCode;
        requestData.lessonCode = formData.lessonCode;
    } else {
        requestData.chapterCode = formData.lessonCode; // For new questions
    }
    $.ajax({
        url: url,
        method: 'POST',
        data: requestData,
        success: function (result) {
            if (result.success) {
                showValidationMessage(getJsString('questionAnswersSaved') || 'Question and answers saved successfully!', 'success');
                setTimeout(() => {
                    hideModal('#question-modal');
                    if (selectedLessonCode) {
                        loadQuestionsForLesson(selectedLessonCode, currentPage);
                        if (!isEditMode) {
                            const $lessonItem = $(`.lesson-item[data-lesson="${selectedLessonCode}"]`);
                            if ($lessonItem.length > 0) {
                                const currentCount = parseInt($lessonItem.find('.lesson-question-count').text()) || 0;
                                $lessonItem.find('.lesson-question-count').text((currentCount + 1) + ' Q');
                            }
                        }
                    }
                }, 1500);
            } else {
                showValidationMessage(result.message || getJsString('failedToSaveQuestion') || 'Failed to save question', 'error');
            }
        },
        error: function (xhr) {
            console.error('Save error:', xhr);
            let errorMessage = getJsString('errorSavingQuestion') || 'An error occurred while saving. Please try again.';
            if (xhr.responseJSON && xhr.responseJSON.message) {
                errorMessage = xhr.responseJSON.message;
            }
            showValidationMessage(errorMessage, 'error');
        },
        complete: function () {
            resetButton($saveBtn);
        }
    });
}

// =========================
// MAIN APPLICATION FUNCTIONS (REQUIRED)
// =========================

function initializeUserInfo() {
    $.get('/Question/GetUserRootTeacherInfo', function (data) {
        isCenter = data.isCenter;
        window.userRootCode = data.rootCode;
        window.userTeacherCode = data.teacherCode || data.userCode;
        window.userEduYearCode = data.eduYearCode || '';
    }).fail(function () {
        console.error('Failed to load user info');
    });
}

function loadSubjectYears() {
    $.get('/Question/GetSubjectYearsByRoot', function (data) {
        let html = '<option value="">' + (getJsString('selectOption') || '-- Select Subject & Year --') + '</option>';
        if (data && data.length > 0) {
            data.forEach(item => {
                html += `<option value="${item.subjectCode}|${item.yearCode}">${item.displayName}</option>`;
            });
        } else {
            html += '<option value="" disabled>' + (getJsString('noTeachingSubjects') || 'No subjects available') + '</option>';
        }
        $('#subjectYearSelect').html(html);
    }).fail(function () {
        console.error('Failed to load subject years');
        $('#subjectYearSelect').html('<option value="" disabled>' + (getJsString('errorLoadingSubjects') || 'Error loading subjects') + '</option>');
    });
}

function loadLessonHierarchy(subjectCode, yearCode) {
    if (!subjectCode || !yearCode) {
        $('#lessonsHierarchy').html(`
            <div class="empty-state">
                <i class="fas fa-book-open"></i>
                <div>${getJsString('selectSubjectYear') || 'Select a subject and year to view lessons'}</div>
            </div>
        `);
        return;
    }
    $('#lessonsHierarchy').html('<div style="text-align:center;padding:20px;"><i class="fas fa-spinner fa-spin"></i> ' + (getJsString('loadingLessons') || 'Loading lessons...') + '</div>');
    $.get('/Question/GetLessonHierarchy', { subjectCode: subjectCode, yearCode: yearCode }, function (data) {
        let html = '';
        if (data.chapters && data.chapters.length > 0) {
            data.chapters.forEach((chapter, idx) => {
                html += `
                    <div class="chapter-item">
                        <div class="chapter-header" data-chapter="${chapter.chapterCode}">
                            <i class="fas fa-chevron-right"></i>
                            <span>${chapter.chapterName}</span>
                        </div>
                        <div class="lessons-list" id="lessons-${chapter.chapterCode}">
                            <div class="add-lesson-to-chapter-container" style="display: flex; justify-content: flex-end; margin-bottom: 0.8rem; display:none;">
                                <button class="modern-btn primary-btn add-lesson-to-chapter-btn" data-chapter="${chapter.chapterCode}">
                                    <i class="fas fa-plus"></i> ${getJsString('addLessonBtn') || 'Add Lesson'}
                                </button>
                            </div>
                `;
                if (chapter.lessons && chapter.lessons.length > 0) {
                    chapter.lessons.forEach(lesson => {
                        html += `
                            <div class="lesson-item" data-lesson="${lesson.lessonCode}" data-lesson-name="${lesson.lessonName}">
                                <span><i class="fas fa-book"></i> ${lesson.lessonName}</span>
                                <span class="lesson-question-count">${lesson.questionCount} Q</span>
                            </div>
                        `;
                    });
                } else {
                    html += `<div class="lesson-item" style="opacity:0.6;cursor:default;"><i class="fas fa-info-circle"></i> ${getJsString('noLessons') || 'No lessons in this chapter'}</div>`;
                }
                html += `
                        </div>
                    </div>
                `;
                if (idx === data.chapters.length - 1) {
                    html += `
                        <div style="text-align:center; margin-top: 1.2rem;">
                            <button id="addChapterBtn" class="modern-btn success-btn" style="width:90%">
                                <i class="fas fa-plus"></i> ${getJsString('addChapterBtn') || 'Add Chapter'}
                            </button>
                        </div>
                    `;
                }
            });
        } else {
            html = `
                <div class="empty-state">
                    <i class="fas fa-exclamation-circle"></i>
                    <div>${getJsString('noChapters') || 'No chapters found for selected subject and year'}</div>
                </div>
                <div style="text-align:center; margin-top: 1.2rem;">
                    <button id="addChapterBtn" class="modern-btn success-btn" style="width:90%">
                        <i class="fas fa-plus"></i> ${getJsString('addChapterBtn') || 'Add Chapter'}
                    </button>
                </div>
            `;
        }
        $('#lessonsHierarchy').html(html);
    }).fail(function () {
        console.error('Failed to load lesson hierarchy');
        $('#lessonsHierarchy').html(`
            <div class="empty-state">
                <i class="fas fa-exclamation-triangle"></i>
                <div>${getJsString('errorLoadingLessonsHierarchy') || 'Error loading lessons hierarchy'}</div>
            </div>
            <div style="text-align:center; margin-top: 1.2rem;">
                <button id="addChapterBtn" class="modern-btn success-btn" style="width:90%">
                    <i class="fas fa-plus"></i> ${getJsString('addChapterBtn') || 'Add Chapter'}
                </button>
            </div>
        `);
    });
}

function loadQuestionsForLesson(lessonCode, page = 1) {
    if (!lessonCode) return;
    currentPage = page;
    $('#questionsContainer').html('<div style="text-align:center;padding:20px;"><i class="fas fa-spinner fa-spin"></i> ' + (getJsString('loadingQuestions') || 'Loading questions...') + '</div>');
    $.get('/Question/GetQuestionsByLesson', {
        lessonCode: lessonCode,
        page: page,
        pageSize: pageSize
    }, function (data) {
        let html = '';
        if (data.questions && data.questions.length > 0) {
            data.questions.forEach(question => {
                html += `
                    <div class="question-item" data-question="${question.questionCode}">
                        <div class="question-content">${question.questionContent}</div>
                        <div class="question-actions">
                            <button class="btn-table stats show-answers-btn" data-question="${question.questionCode}">
                                <i class="fas fa-list"></i> ${getJsString('showAnswersBtn') || 'Show Answers'}
                            </button>
                            <button class="btn-table edit edit-question-btn" data-question='${JSON.stringify(question)}'>
                                <i class="fas fa-edit"></i> 
                            </button>
                            <button class="btn-table delete delete-question-btn" data-question="${question.questionCode}">
                                <i class="fas fa-trash"></i> 
                            </button>
                        </div>
                        <div class="answers-container" id="answers-${question.questionCode}" style="display:none;margin-top:1rem;"></div>
                    </div>
                `;
            });
        } else {
            html = `
                <div class="empty-state">
                    <i class="fas fa-question-circle"></i>
                    <div>${getJsString('noQuestions') || 'No questions found for this lesson'}</div>
                    <div style="margin-top:1rem;">
                        <button class="modern-btn primary-btn" id="addFirstQuestion">
                            <i class="fas fa-plus"></i> ${getJsString('addQuestionBtn') || 'Add First Question'}
                        </button>
                    </div>
                </div>
            `;
        }
        $('#questionsContainer').html(html);
        if (data.pagination && data.pagination.totalPages > 1) {
            renderPagination(data.pagination);
        } else {
            $('#paginationContainer').html('');
        }
    }).fail(function () {
        console.error('Failed to load questions');
        $('#questionsContainer').html(`
            <div class="empty-state">
                <i class="fas fa-exclamation-triangle"></i>
                <div>${getJsString('errorLoadingQuestions') || 'Error loading questions'}</div>
            </div>
        `);
    });
}

function renderPagination(pagination) {
    let html = '';
    if (pagination.currentPage > 1) {
        html += `<button class="modern-btn secondary-btn" id="prevPageBtn">
                    <i class="fas fa-chevron-left"></i> ${getJsString('previous') || 'Previous'}
                </button>`;
    }
    html += ` ${getJsString('page') || 'Page'} ${pagination.currentPage} ${getJsString('of') || 'of'} ${pagination.totalPages} (${pagination.totalCount} ${getJsString('questions') || 'questions'}) `;
    if (pagination.currentPage < pagination.totalPages) {
        html += `<button class="modern-btn secondary-btn" id="nextPageBtn">
                    ${getJsString('next') || 'Next'} <i class="fas fa-chevron-right"></i>
                </button>`;
    }
    $('#paginationContainer').html(html);
}

// =========================
// EVENT HANDLERS
// =========================

function setupEventHandlers() {
    $('#subjectYearSelect').on('change', function () {
        const value = $(this).val();
        if (value) {
            const [subjectCode, yearCode] = value.split('|');
            selectedSubjectCode = subjectCode;
            selectedYearCode = yearCode;
            lastSelectedYearCode = yearCode;
            $('#mainLayout').show();
            loadLessonHierarchy(subjectCode, yearCode);
            $('#lessonContent').hide();
            $('#noLessonSelected').show();
        } else {
            selectedSubjectCode = null;
            selectedYearCode = null;
            selectedLessonCode = null;
            lastSelectedYearCode = null;
            $('#mainLayout').hide();
        }
    });

    $(document).on('click', '.add-lesson-to-chapter-btn', function (e) {
        e.stopPropagation();
        $('#addLessonForm')[0].reset();
        $('#addLessonMsg').hide();
        const chapterCode = $(this).data('chapter');
        $('[name="ChapterCode"]').val(chapterCode);
        $('[name="TeacherCode"]').val(window.userTeacherCode || '');
        $('[name="YearCode"]').val(selectedYearCode || '');
        showModal('#addLessonModal'); // UPDATED
    });

    $(document).on('click', '.chapter-header', function () {
        const chapterCode = $(this).data('chapter');
        const $lessonsList = $('#lessons-' + chapterCode);
        const $icon = $(this).find('i');
        if ($lessonsList.is(':visible')) {
            $lessonsList.slideUp(200);
            $icon.removeClass('fa-chevron-down').addClass('fa-chevron-right');
            $(this).removeClass('expanded');
            $lessonsList.find('.add-lesson-to-chapter-container').hide();
        } else {
            $lessonsList.slideDown(200);
            $icon.removeClass('fa-chevron-right').addClass('fa-chevron-down');
            $(this).addClass('expanded');
            $lessonsList.find('.add-lesson-to-chapter-container').show();
        }
    });

    $(document).on('click', '.lesson-item', function () {
        if ($(this).find('.fa-info-circle').length > 0) return; // Skip empty state items
        const lessonCode = $(this).data('lesson');
        const lessonName = $(this).data('lesson-name');
        if (lessonCode) {
            $('.lesson-item').removeClass('active');
            $(this).addClass('active');
            selectedLessonCode = lessonCode;
            selectedLessonName = lessonName;
            $('#selectedLessonName').text(lessonName);
            $('#noLessonSelected').hide();
            $('#lessonContent').show();
            loadQuestionsForLesson(lessonCode);
        }
    });

    $(document).on('click', '#addQuestionBtn, #addFirstQuestion', function () {
        showAddQuestionModal();
    });

    $(document).on('click', '.edit-question-btn', function () {
        const question = $(this).data('question');
        showEditQuestionModal(question);
    });

    $(document).on('click', '.delete-question-btn', function () {
        const questionCode = $(this).data('question');
        if (confirm(getJsString('deleteQuestionConfirm') || 'Are you sure you want to delete this question?')) {
            deleteQuestion(questionCode);
        }
    });

    $(document).on('click', '#prevPageBtn', function () {
        if (currentPage > 1) {
            loadQuestionsForLesson(selectedLessonCode, currentPage - 1);
        }
    });

    $(document).on('click', '#nextPageBtn', function () {
        loadQuestionsForLesson(selectedLessonCode, currentPage + 1);
    });

    // SHOW ANSWERS BUTTON HANDLER
    $(document).on('click', '.show-answers-btn', function () {
        const $btn = $(this);
        const questionCode = $btn.data('question');
        const $answersContainer = $('#answers-' + questionCode);

        if ($answersContainer.is(':visible')) {
            $answersContainer.slideUp();
            $btn.removeClass('active');
            return;
        }

        if ($answersContainer.data('loaded')) {
            $answersContainer.slideDown();
            $btn.addClass('active');
            return;
        }

        $answersContainer.html('<div style="text-align:center;"><i class="fas fa-spinner fa-spin"></i> ' + (getJsString('loadingAnswers') || 'Loading answers...') + '</div>').slideDown();
        $btn.addClass('active');

        $.get('/Question/GetAnswersByQuestion', { questionCode: questionCode }, function (answers) {
            let html = '';
            if (answers && answers.length > 0) {
                answers.forEach(ans => {
                    html += `
                        <div class="answer-row${ans.isTrue ? ' correct' : ''}">
                            <span class="answer-content">${ans.answerContent}</span>
                            <span class="answer-status ${ans.isTrue ? 'correct' : 'incorrect'}">
                                ${ans.isTrue ? '<i class="fas fa-check-circle"></i> ' + (getJsString('isCorrect') || 'Correct') : ''}
                            </span>
                        </div>
                    `;
                });
            } else {
                html = '<div style="color:#888;text-align:center;">' + (getJsString('noAnswersFound') || 'No answers found.') + '</div>';
            }
            $answersContainer.html(html);
            $answersContainer.data('loaded', true);
        }).fail(function () {
            $answersContainer.html('<div style="color:red;text-align:center;">' + (getJsString('failed') || 'Failed to load answers.') + '</div>');
        });
    });
}

function deleteQuestion(questionCode) {
    $.ajax({
        url: '/Question/DeleteQuestion',
        method: 'POST',
        data: { QuestionCode: questionCode },
        success: function (result) {
            if (result.success) {
                loadQuestionsForLesson(selectedLessonCode, currentPage);
                const $lessonItem = $(`.lesson-item[data-lesson="${selectedLessonCode}"]`);
                if ($lessonItem.length > 0) {
                    const currentCount = parseInt($lessonItem.find('.lesson-question-count').text());
                    const newCount = Math.max(0, currentCount - 1);
                    $lessonItem.find('.lesson-question-count').text(newCount + ' Q');
                }
            } else {
                alert(result.message || getJsString('failed') || 'Failed to delete question');
            }
        },
        error: function () {
            alert(getJsString('errorOccurred') || 'Error occurred while deleting question');
        }
    });
}

// =========================
// ENHANCED MODAL EVENT HANDLERS
// =========================

function setupEnhancedModalHandlers() {
    $(document).off('click', '#add-answer-btn').on('click', '#add-answer-btn', function (e) {
        e.preventDefault();
        addAnswerField();
    });

    $(document).off('change', 'input[name="correctAnswer"]').on('change', 'input[name="correctAnswer"]', function () {
        updateCorrectAnswerVisuals();
    });

    $(document).off('blur', '.answer-input').on('blur', '.answer-input', function () {
        const $field = $(this).closest('.answer-field');
        if ($(this).val().trim()) {
            $field.removeClass('error');
        }
    });

    $('#question-form').off('submit').on('submit', function (e) {
        e.preventDefault();
        saveQuestionWithAnswers();
    });

    $('#cancel-question-btn').off('click').on('click', function () {
        if ($('.answer-input').filter(function () { return $(this).val().trim() !== ''; }).length > 0 ||
            $('#question-content').val().trim() !== '') {
            if (confirm(getJsString('areYouSureCancel') || 'You have unsaved changes. Are you sure you want to close?')) {
                hideModal('#question-modal');
            }
        } else {
            hideModal('#question-modal');
        }
    });

    $(document).off('keydown.questionModal').on('keydown.questionModal', function (e) {
        if ($('#question-modal').is(':visible')) {
            if (e.which === 27) {
                hideModal('#question-modal');
            }
        }
    });
}

// =========================
// ADD CHAPTER / LESSON MODALS
// =========================

$('#addLessonForm').on('submit', function (e) {
    e.preventDefault();
    $('#addLessonMsg').hide();
    const $btn = $(this).find('button[type="submit"]');
    setButtonProcessing($btn, '<i class="fas fa-spinner fa-spin"></i> ' + (getJsString('processing') || 'Saving...'));
    $.post('/Question/AddLesson', $(this).serialize(), function (resp) {
        if (resp.success) {
            $('#addLessonMsg').css('color', 'green').text(getJsString('saved') || 'Lesson added!').show();
            setTimeout(() => {
                hideModal('#addLessonModal'); // UPDATED
                if (selectedSubjectCode && selectedYearCode) {
                    loadLessonHierarchy(selectedSubjectCode, selectedYearCode);
                }
            }, 800);
        } else {
            $('#addLessonMsg').css('color', 'red').text(resp.message).show();
        }
    }).fail(function () {
        $('#addLessonMsg').css('color', 'red').text(getJsString('errorOccurred') || 'An error occurred.').show();
    }).always(function () {
        resetButton($btn);
    });
});

$('#addChapterForm').on('submit', function (e) {
    e.preventDefault();
    $('#addChapterMsg').hide();
    const $btn = $(this).find('button[type="submit"]');
    setButtonProcessing($btn, '<i class="fas fa-spinner fa-spin"></i> ' + (getJsString('processing') || 'Saving...'));
    $.post('/Question/AddChapter', $(this).serialize(), function (resp) {
        if (resp.success) {
            $('#addChapterMsg').css('color', 'green').text(getJsString('saved') || 'Chapter added!').show();
            setTimeout(() => {
                hideModal('#addChapterModal'); // UPDATED
                if (selectedSubjectCode && selectedYearCode) {
                    loadLessonHierarchy(selectedSubjectCode, selectedYearCode);
                }
            }, 800);
        } else {
            $('#addChapterMsg').css('color', 'red').text(resp.message).show();
        }
    }).fail(function () {
        $('#addChapterMsg').css('color', 'red').text(getJsString('errorOccurred') || 'An error occurred.').show();
    }).always(function () {
        resetButton($btn);
    });
});

// =========================
// POPULATE DROPDOWNS
// =========================

function populateEduYears() {
    $.get('/Question/GetEduYearsByRoot', function (data) {
        let html = '<option value="">' + (getJsString('selectOption') || '-- Select --') + '</option>';
        let onlyEduYearCode = null;
        if (data.length === 1) {
            onlyEduYearCode = data[0].eduYearCode;
        }
        data.forEach(x => html += `<option value="${x.eduYearCode}">${x.eduYearName}</option>`);
        $('#addChapterForm select[name="EduYearCode"]').html(html);
        if (onlyEduYearCode) {
            $('#addChapterForm select[name="EduYearCode"]').val(onlyEduYearCode);
        }
    });
}

function populateTeachers() {
    $.get('/Question/GetTeachersByRoot', function (data) {
        let html = '<option value="">' + (getJsString('selectOption') || '-- Select --') + '</option>';
        let onlyTeacherCode = null;
        if (data.length === 1) {
            onlyTeacherCode = data[0].teacherCode;
        }
        data.forEach(x => html += `<option value="${x.teacherCode}">${x.teacherName}</option>`);
        $('#addChapterForm select[name="TeacherCode"]').html(html);
        if (onlyTeacherCode) {
            $('#addChapterForm select[name="TeacherCode"]').val(onlyTeacherCode);
        }
    });
}
function populateSubjects() {
    $.get('/Question/GetSubjectYearsByRoot', function (data) {
        let html = '<option value="">' + (getJsString('selectOption') || '-- Select --') + '</option>';
        data.forEach(x => html += `<option value="${x.subjectCode}">${x.subjectName}</option>`);
        $('#addChapterForm select[name="SubjectCode"]').html(html);
    });
}
function populateYears(selectedYearCode) {
    $.get('/Question/GetSubjectYearsByRoot', function (data) {
        let html = '<option value="">' + (getJsString('selectOption') || '-- Select --') + '</option>';
        data.forEach(x => html += `<option value="${x.yearCode}">${x.yearName}</option>`);
        $('#addChapterForm select[name="YearCode"]').html(html);
        if (selectedYearCode) {
            $('#addChapterForm select[name="YearCode"]').val(selectedYearCode);
        }
    });
}

// Optional: Hide modals on overlay click (UX improvement)
$('.modal-background').on('click', function (e) {
    if (e.target === this) hideModal(this); // UPDATED
});

// =========================
// INITIALIZATION
// =========================

$(document).on('click', '#addChapterBtn', function () {
    $('#addChapterForm')[0].reset();
    $('#addChapterMsg').hide();
    populateEduYears();
    populateTeachers();
    populateSubjects();
    populateYears(lastSelectedYearCode); // Pass selected year to modal
    showModal('#addChapterModal'); // UPDATED
});

$(document).ready(function () {
    initializeUserInfo();
    loadSubjectYears();
    setupEventHandlers();
    setupEnhancedModalHandlers();
});

// Global function to remove answer (called from HTML)
window.removeAnswerField = removeAnswerField;

console.log("=== End of Enhanced Question.js file ===");