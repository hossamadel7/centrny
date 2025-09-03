console.log("=== Enhanced Question.js file is being loaded ===");

// Global state variables (REQUIRED)
let isCenter = false;
let selectedSubjectCode = null;
let selectedYearCode = null;
let selectedLessonCode = null;  
let selectedLessonName = '';
let currentPage = 1;
let pageSize = 10;

// Enhanced modal variables
let answerCounter = 0;
let isEditMode = false;

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
    $btn.text(processingText).prop('disabled', true);
}

function resetButton($btn) {
    $btn.text($btn.data('original-text') || 'Save').prop('disabled', false);
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

    // Add initial 2 answer fields
    addAnswerField();
    addAnswerField();

    // Set first answer as correct by default
    setTimeout(() => {
        $('input[name="correctAnswer"]:first').prop('checked', true);
        updateCorrectAnswerVisuals();
    }, 100);
}

function addAnswerField(content = '', isCorrect = false) {
    answerCounter++;

    const answerHtml = `
        <div class="answer-field" data-answer-id="${answerCounter}">
            <div class="answer-number">${answerCounter}</div>
            <input type="text" 
                   class="answer-input" 
                   name="answer_${answerCounter}" 
                   placeholder="Enter answer option ${answerCounter}..." 
                   value="${content}"
                   required>
            <input type="radio" 
                   class="correct-radio" 
                   name="correctAnswer" 
                   value="${answerCounter}"
                   ${isCorrect ? 'checked' : ''}
                   title="Mark as correct answer">
            <button type="button" class="remove-answer-btn" onclick="removeAnswerField(${answerCounter})">
                <i class="fas fa-trash"></i>
            </button>
        </div>
    `;

    $('#answers-container').append(answerHtml);
    updateAnswerNumbers();
    updateCorrectAnswerVisuals();

    // Focus on the new answer input
    setTimeout(() => {
        $(`input[name="answer_${answerCounter}"]`).focus();
    }, 100);
}

function removeAnswerField(answerId) {
    const $field = $(`.answer-field[data-answer-id="${answerId}"]`);
    const wasCorrect = $field.find('input[name="correctAnswer"]').is(':checked');

    // Don't allow removing if only 2 answers remain
    if ($('.answer-field').length <= 2) {
        showValidationMessage('You must have at least 2 answer options', 'error');
        return;
    }

    $field.fadeOut(300, function () {
        $(this).remove();
        updateAnswerNumbers();

        // If the removed answer was correct, select the first remaining answer
        if (wasCorrect) {
            $('input[name="correctAnswer"]:first').prop('checked', true);
        }

        updateCorrectAnswerVisuals();
    });
}

function updateAnswerNumbers() {
    $('.answer-field').each(function (index) {
        $(this).find('.answer-number').text(index + 1);
        $(this).find('.answer-input').attr('placeholder', `Enter answer option ${index + 1}...`);
    });
}

function updateCorrectAnswerVisuals() {
    // Remove correct styling from all fields
    $('.answer-field').removeClass('correct-answer');

    // Add correct styling to the selected answer
    const selectedRadio = $('input[name="correctAnswer"]:checked');
    if (selectedRadio.length > 0) {
        selectedRadio.closest('.answer-field').addClass('correct-answer');
    }
}

function showValidationMessage(message, type = 'error') {
    const $messageDiv = $('#question-message');

    $messageDiv.removeClass('error success')
        .addClass(type)
        .text(message)
        .show();

    if (type === 'error') {
        $messageDiv.css({
            'background-color': '#f8d7da',
            'color': '#721c24',
            'border': '1px solid #f5c6cb'
        });

        // Auto-hide after 4 seconds for error messages
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
    // Clear previous validation styles
    $('.answer-field').removeClass('error');
    $('#question-message').hide();

    const questionContent = $('#question-content').val().trim();
    const answers = [];
    let hasErrors = false;

    // Validate question content
    if (!questionContent) {
        showValidationMessage('Question content is required', 'error');
        $('#question-content').focus();
        return false;
    }

    if (questionContent.length < 10) {
        showValidationMessage('Question content should be at least 10 characters long', 'error');
        $('#question-content').focus();
        return false;
    }

    // Validate answers
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
        showValidationMessage('All answer fields must be filled with meaningful content', 'error');
        return false;
    }

    if (answers.length < 2) {
        showValidationMessage('At least 2 answers are required', 'error');
        return false;
    }

    // Check for duplicate answers
    const duplicates = answers.filter((item, index) =>
        answers.findIndex(a => a.toLowerCase() === item.toLowerCase()) !== index
    );
    if (duplicates.length > 0) {
        showValidationMessage('Duplicate answers are not allowed', 'error');
        return false;
    }

    // Validate correct answer selection
    if (!$('input[name="correctAnswer"]:checked').length) {
        showValidationMessage('Please select the correct answer by clicking a radio button', 'error');
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
        alert('Please select a lesson first');
        return;
    }

    isEditMode = false;

    // Reset form
    $('#question-modal-title').html('<i class="fas fa-plus"></i> Add Question with Answers');
    $('#question-id').val('');
    $('#question-lessoncode').val(selectedLessonCode);
    $('#question-content').val('');
    $('#question-examcode').val('');
    $('#question-message').hide();
    $('#save-question-btn').html('<i class="fas fa-save"></i> Save Question & Answers').prop('disabled', false);

    // Initialize answer fields
    initializeAnswerFields();

    showModal('#question-modal');

    // Focus on question content after modal animation
    setTimeout(() => {
        $('#question-content').focus();
    }, 300);
}

function showEditQuestionModal(question) {
    isEditMode = true;

    $('#question-modal-title').html('<i class="fas fa-edit"></i> Edit Question with Answers');
    $('#question-id').val(question.questionCode);
    $('#question-lessoncode').val(question.lessonCode);
    $('#question-content').val(question.questionContent);
    $('#question-examcode').val(question.examCode || '');
    $('#question-message').hide();
    $('#save-question-btn').html('<i class="fas fa-save"></i> Update Question & Answers').prop('disabled', false);

    // Load existing answers
    loadQuestionAnswers(question.questionCode);

    showModal('#question-modal');

    setTimeout(() => {
        $('#question-content').focus();
    }, 300);
}

function loadQuestionAnswers(questionCode) {
    // Clear existing answers first
    $('#answers-container').empty();
    answerCounter = 0;

    // Show loading state
    $('#answers-container').html('<div style="text-align:center;padding:20px;color:#666;"><i class="fas fa-spinner fa-spin"></i> Loading answers...</div>');

    $.get('/Question/GetAnswersByQuestion', { questionCode: questionCode }, function (answers) {
        $('#answers-container').empty();
        answerCounter = 0;

        if (answers && answers.length > 0) {
            answers.forEach(answer => {
                addAnswerField(answer.answerContent, answer.isTrue);
            });
        } else {
            // No existing answers, add empty fields
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
        showValidationMessage('Could not load existing answers. You can add new ones.', 'error');
    });
}

function saveQuestionWithAnswers() {
    if (!validateQuestionForm()) {
        return;
    }

    const formData = collectFormData();
    const $saveBtn = $('#save-question-btn');

    setButtonProcessing($saveBtn, '<i class="fas fa-spinner fa-spin"></i> Saving...');
    showValidationMessage('Saving question and answers...', 'success');

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
                showValidationMessage('Question and answers saved successfully!', 'success');

                setTimeout(() => {
                    hideModal('#question-modal');

                    // Refresh the questions list
                    if (selectedLessonCode) {
                        loadQuestionsForLesson(selectedLessonCode, currentPage);

                        // Update lesson question count in sidebar
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
                showValidationMessage(result.message || 'Failed to save question', 'error');
            }
        },
        error: function (xhr, status, error) {
            console.error('Save error:', error);
            let errorMessage = 'An error occurred while saving. Please try again.';

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

        if (!isCenter) {
            let boxHtml = `
                <div style="background:#f5fafd;border:1px solid #c8e1fa;padding:18px 0 18px 24px;margin:18px 0 30px 0;border-radius:8px;color:#29587a;">
                    <b>User Code:</b> ${data.userCode}<br />
                    <b>Root Code:</b> ${data.rootCode}<br />
                    <b>Teacher Name:</b> ${data.teacherName}
                </div>
            `;
            $('#user-root-info-box').html(boxHtml).show();
        }
    }).fail(function () {
        console.error('Failed to load user info');
    });
}

function loadSubjectYears() {
    $.get('/Question/GetSubjectYearsByRoot', function (data) {
        let html = '<option value="">-- Select Subject & Year --</option>';

        if (data && data.length > 0) {
            data.forEach(item => {
                html += `<option value="${item.subjectCode}|${item.yearCode}">${item.displayName}</option>`;
            });
        } else {
            html += '<option value="" disabled>No subjects available</option>';
        }

        $('#subjectYearSelect').html(html);
    }).fail(function () {
        console.error('Failed to load subject years');
        $('#subjectYearSelect').html('<option value="" disabled>Error loading subjects</option>');
    });
}

function loadLessonHierarchy(subjectCode, yearCode) {
    if (!subjectCode || !yearCode) {
        $('#lessonsHierarchy').html(`
            <div class="empty-state">
                <i class="fas fa-book-open"></i>
                <div>Select a subject and year to view lessons</div>
            </div>
        `);
        return;
    }

    $('#lessonsHierarchy').html('<div style="text-align:center;padding:20px;"><i class="fas fa-spinner fa-spin"></i> Loading lessons...</div>');

    $.get('/Question/GetLessonHierarchy', { subjectCode: subjectCode, yearCode: yearCode }, function (data) {
        let html = '';

        if (data.chapters && data.chapters.length > 0) {
            data.chapters.forEach(chapter => {
                html += `
                    <div class="chapter-item">
                        <div class="chapter-header" data-chapter="${chapter.chapterCode}">
                            <i class="fas fa-chevron-right"></i>
                            <span>${chapter.chapterName}</span>
                        </div>
                        <div class="lessons-list" id="lessons-${chapter.chapterCode}">
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
                    html += '<div class="lesson-item" style="opacity:0.6;cursor:default;"><i class="fas fa-info-circle"></i> No lessons in this chapter</div>';
                }

                html += `
                        </div>
                    </div>
                `;
            });
        } else {
            html = `
                <div class="empty-state">
                    <i class="fas fa-exclamation-circle"></i>
                    <div>No chapters found for selected subject and year</div>
                </div>
            `;
        }

        $('#lessonsHierarchy').html(html);
    }).fail(function () {
        console.error('Failed to load lesson hierarchy');
        $('#lessonsHierarchy').html(`
            <div class="empty-state">
                <i class="fas fa-exclamation-triangle"></i>
                <div>Error loading lessons hierarchy</div>
            </div>
        `);
    });
}

function loadQuestionsForLesson(lessonCode, page = 1) {
    if (!lessonCode) return;

    currentPage = page;
    $('#questionsContainer').html('<div style="text-align:center;padding:20px;"><i class="fas fa-spinner fa-spin"></i> Loading questions...</div>');

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
                                <i class="fas fa-list"></i> Show Answers
                            </button>
                            <button class="btn-table add add-answers-btn" data-question="${question.questionCode}">
                                <i class="fas fa-plus"></i> Add Answers
                            </button>
                            <button class="btn-table edit edit-question-btn" data-question='${JSON.stringify(question)}'>
                                <i class="fas fa-edit"></i> Edit
                            </button>
                            <button class="btn-table delete delete-question-btn" data-question="${question.questionCode}">
                                <i class="fas fa-trash"></i> Delete
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
                    <div>No questions found for this lesson</div>
                    <div style="margin-top:1rem;">
                        <button class="modern-btn primary-btn" id="addFirstQuestion">
                            <i class="fas fa-plus"></i> Add First Question
                        </button>
                    </div>
                </div>
            `;
        }

        $('#questionsContainer').html(html);

        // Update pagination
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
                <div>Error loading questions</div>
            </div>
        `);
    });
}

function renderPagination(pagination) {
    let html = '';

    if (pagination.currentPage > 1) {
        html += `<button class="modern-btn secondary-btn" id="prevPageBtn">
                    <i class="fas fa-chevron-left"></i> Previous
                </button>`;
    }

    html += ` Page ${pagination.currentPage} of ${pagination.totalPages} (${pagination.totalCount} questions) `;

    if (pagination.currentPage < pagination.totalPages) {
        html += `<button class="modern-btn secondary-btn" id="nextPageBtn">
                    Next <i class="fas fa-chevron-right"></i>
                </button>`;
    }

    $('#paginationContainer').html(html);
}

// =========================
// EVENT HANDLERS
// =========================

function setupEventHandlers() {
    // Subject-Year selection change
    $('#subjectYearSelect').on('change', function () {
        const value = $(this).val();

        if (value) {
            const [subjectCode, yearCode] = value.split('|');
            selectedSubjectCode = subjectCode;
            selectedYearCode = yearCode;

            $('#mainLayout').show();
            loadLessonHierarchy(subjectCode, yearCode);

            // Hide lesson content until a lesson is selected
            $('#lessonContent').hide();
            $('#noLessonSelected').show();
        } else {
            selectedSubjectCode = null;
            selectedYearCode = null;
            selectedLessonCode = null;

            $('#mainLayout').hide();
        }
    });

    // Chapter expand/collapse
    $(document).on('click', '.chapter-header', function () {
        const chapterCode = $(this).data('chapter');
        const $lessonsList = $('#lessons-' + chapterCode);
        const $icon = $(this).find('i');

        if ($lessonsList.is(':visible')) {
            $lessonsList.slideUp(200);
            $icon.removeClass('fa-chevron-down').addClass('fa-chevron-right');
            $(this).removeClass('expanded');
        } else {
            $lessonsList.slideDown(200);
            $icon.removeClass('fa-chevron-right').addClass('fa-chevron-down');
            $(this).addClass('expanded');
        }
    });

    // Lesson selection
    $(document).on('click', '.lesson-item', function () {
        if ($(this).find('.fa-info-circle').length > 0) return; // Skip empty state items

        const lessonCode = $(this).data('lesson');
        const lessonName = $(this).data('lesson-name');

        if (lessonCode) {
            // Update selected lesson
            $('.lesson-item').removeClass('active');
            $(this).addClass('active');

            selectedLessonCode = lessonCode;
            selectedLessonName = lessonName;

            // Update UI
            $('#selectedLessonName').text(lessonName);
            $('#noLessonSelected').hide();
            $('#lessonContent').show();

            // Load questions for this lesson
            loadQuestionsForLesson(lessonCode);
        }
    });

    // Add Question button
    $(document).on('click', '#addQuestionBtn, #addFirstQuestion', function () {
        showAddQuestionModal();
    });

    // Edit Question
    $(document).on('click', '.edit-question-btn', function () {
        const question = $(this).data('question');
        showEditQuestionModal(question);
    });

    // Delete Question
    $(document).on('click', '.delete-question-btn', function () {
        const questionCode = $(this).data('question');

        if (confirm(getJsString('deleteQuestionConfirm') || 'Are you sure you want to delete this question?')) {
            deleteQuestion(questionCode);
        }
    });

    // Pagination
    $(document).on('click', '#prevPageBtn', function () {
        if (currentPage > 1) {
            loadQuestionsForLesson(selectedLessonCode, currentPage - 1);
        }
    });

    $(document).on('click', '#nextPageBtn', function () {
        loadQuestionsForLesson(selectedLessonCode, currentPage + 1);
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
                // Update lesson question count in sidebar
                const $lessonItem = $(`.lesson-item[data-lesson="${selectedLessonCode}"]`);
                if ($lessonItem.length > 0) {
                    const currentCount = parseInt($lessonItem.find('.lesson-question-count').text());
                    const newCount = Math.max(0, currentCount - 1);
                    $lessonItem.find('.lesson-question-count').text(newCount + ' Q');
                }
            } else {
                alert(result.message || 'Failed to delete question');
            }
        },
        error: function () {
            alert('Error occurred while deleting question');
        }
    });
}

// =========================
// ENHANCED MODAL EVENT HANDLERS
// =========================

function setupEnhancedModalHandlers() {
    // Add answer button
    $(document).off('click', '#add-answer-btn').on('click', '#add-answer-btn', function (e) {
        e.preventDefault();
        addAnswerField();
    });

    // Correct answer radio change
    $(document).off('change', 'input[name="correctAnswer"]').on('change', 'input[name="correctAnswer"]', function () {
        updateCorrectAnswerVisuals();
    });

    // Answer input validation on blur
    $(document).off('blur', '.answer-input').on('blur', '.answer-input', function () {
        const $field = $(this).closest('.answer-field');
        if ($(this).val().trim()) {
            $field.removeClass('error');
        }
    });

    // Enhanced form submission
    $('#question-form').off('submit').on('submit', function (e) {
        e.preventDefault();
        saveQuestionWithAnswers();
    });

    // Enhanced cancel button
    $('#cancel-question-btn').off('click').on('click', function () {
        if ($('.answer-input').filter(function () { return $(this).val().trim() !== ''; }).length > 0 ||
            $('#question-content').val().trim() !== '') {
            if (confirm('You have unsaved changes. Are you sure you want to close?')) {
                hideModal('#question-modal');
            }
        } else {
            hideModal('#question-modal');
        }
    });

    // Keyboard shortcuts
    $(document).off('keydown.questionModal').on('keydown.questionModal', function (e) {
        if ($('#question-modal').is(':visible')) {
            // Escape to close modal
            if (e.which === 27) {
                hideModal('#question-modal');
            }
        }
    });
}

// =========================
// INITIALIZATION
// =========================

$(document).ready(function () {
    // Check user type and load initial data
    initializeUserInfo();
    loadSubjectYears();

    // Event handlers
    setupEventHandlers();
    setupEnhancedModalHandlers();
});

// Global function to remove answer (called from HTML)
window.removeAnswerField = removeAnswerField;

console.log("=== End of Enhanced Question.js file ===");