console.log("=== Question.js file is being loaded ===");

// Simple test to verify jQuery is working
if (typeof $ === 'undefined') {
    console.error("jQuery is not loaded!");
} else {
    console.log("jQuery is available");
}

// Use camelCase for all keys to match jQuery .data() parsing
function getJsString(key) {
    key = key.replace(/-([a-z])/g, function (g) { return g[1].toUpperCase(); });
    return $('#js-localization').data(key) || key;
}

// Test function that runs immediately
function testAPIs() {
    console.log("=== Testing APIs immediately ===");

    // Test the debug endpoint
    $.ajax({
        url: '/Question/DebugUserAndChapters',
        type: 'GET',
        success: function (response) {
            console.log("=== DEBUG RESPONSE ===");
            console.log("User Info:", response.userInfo);
            console.log("Database Info:", response.databaseInfo);
            console.log("=== END DEBUG ===");
        },
        error: function (xhr, status, error) {
            console.error("Debug API failed:", error);
            console.error("Status:", status);
            console.error("Response:", xhr.responseText);
        }
    });

    // Test chapters endpoint
    $.ajax({
        url: '/Question/GetChaptersForDropdown', // FIXED: Use consistent name
        type: 'GET',
        success: function (response) {
            console.log("=== CHAPTERS RESPONSE ===");
            console.log("Response:", response);
            console.log("Response type:", typeof response);
            console.log("Is array:", Array.isArray(response));
            console.log("Length:", response ? response.length : 'N/A');
            console.log("=== END CHAPTERS ===");
        },
        error: function (xhr, status, error) {
            console.error("Chapters API failed:", error);
            console.error("Status:", status);
            console.error("Response:", xhr.responseText);
        }
    });
}

// Run tests immediately when script loads
console.log("Running immediate API tests...");
setTimeout(testAPIs, 1000); // Give it 1 second for page to be ready

$(document).ready(function () {
    console.log("=== DOM is ready ===");

    let currentChapterCode = null;
    let answerIndex = 2;
    let isEditMode = false;
    let editingQuestionId = null;

    console.log("Setting up event handlers...");

    // Load chapters on page load
    console.log("About to call loadChapters...");
    loadChapters();

    // Chapter selection handler
    $('#chapter-dropdown').change(function () {
        console.log("Chapter dropdown changed to:", $(this).val());
        const selectedChapterCode = $(this).val();
        if (selectedChapterCode) {
            currentChapterCode = selectedChapterCode;
            loadQuestionsForChapter(selectedChapterCode);
            $('#questions-display').show();
            $('#selected-chapter-title').html(`<i class="fas fa-question-circle"></i> ${$(this).find('option:selected').text()} - Questions`);
        } else {
            $('#questions-display').hide();
            $('#add-question-section').hide();
        }
    });

    // Add question button handler
    $('#add-question-btn').click(function () {
        console.log("Add question button clicked, currentChapterCode:", currentChapterCode);
        if (currentChapterCode) {
            showAddQuestionForm();
        }
    });

    // Close question form
    $('#close-question-form, #cancel-question-btn').click(function () {
        hideAddQuestionForm();
    });

    // Add answer button
    $('#add-answer-btn').click(function () {
        addAnswerField();
    });

    // Question form submission
    $('#question-form').submit(function (e) {
        e.preventDefault();
        if (isEditMode) {
            updateQuestion();
        } else {
            saveQuestion();
        }
    });

    function loadChapters() {
        console.log("=== loadChapters function called ===");

        // Check if dropdown exists
        if ($('#chapter-dropdown').length === 0) {
            console.error("Chapter dropdown element not found!");
            return;
        }

        // Show loading state
        $('#chapter-dropdown').html('<option value="">Loading...</option>');
        console.log("Set dropdown to loading state");

        // Test multiple endpoints
        console.log("Testing /Question/DebugUserAndChapters...");
        $.ajax({
            url: '/Question/DebugUserAndChapters',
            type: 'GET',
            success: function (response) {
                console.log("=== DEBUG SUCCESS ===");
                console.log(response);
                console.log("=== END DEBUG ===");

                // Now try to load actual chapters
                loadActualChapters();
            },
            error: function (xhr, status, error) {
                console.error("Debug endpoint failed:", error);
                console.error("XHR:", xhr);
                console.error("Status:", status);

                // Try to load chapters anyway
                loadActualChapters();
            }
        });
    }

    function loadActualChapters() {
        console.log("=== loadActualChapters function called ===");

        $.ajax({
            url: '/Question/GetChaptersForDropdown', // FIXED: Use consistent name
            type: 'GET',
            success: function (response) {
                console.log("=== CHAPTERS SUCCESS ===");
                console.log("Response:", response);
                console.log("Type:", typeof response);
                console.log("Is Array:", Array.isArray(response));

                const dropdown = $('#chapter-dropdown');
                dropdown.empty();
                dropdown.append('<option value="">Select Chapter...</option>');

                if (response && Array.isArray(response) && response.length > 0) {
                    console.log("Adding", response.length, "chapters to dropdown");
                    response.forEach(function (chapter, index) {
                        console.log(`Adding chapter ${index}:`, chapter);
                        dropdown.append(`<option value="${chapter.chapterCode}">${chapter.chapterName}</option>`);
                    });
                    console.log("Successfully populated dropdown");
                } else {
                    console.log("No chapters found, trying alternative method...");
                    dropdown.append('<option value="">No chapters available</option>');

                    // Try alternative method
                    tryAlternativeMethod();
                }
            },
            error: function (xhr, status, error) {
                console.error("=== CHAPTERS ERROR ===");
                console.error("Error:", error);
                console.error("Status:", status);
                console.error("Response Text:", xhr.responseText);
                console.error("Status Code:", xhr.status);

                $('#chapter-dropdown').html('<option value="">Error loading chapters</option>');

                // Try alternative method
                tryAlternativeMethod();
            }
        });
    }

    function tryAlternativeMethod() {
        console.log("=== Trying alternative method ===");

        $.ajax({
            url: '/Question/GetChaptersWithLessonsAndQuestions',
            type: 'GET',
            data: {
                page: 1,
                pageSize: 50,
                lessonPages: '{}',
                questionPages: '{}'
            },
            success: function (response) {
                console.log("=== ALTERNATIVE SUCCESS ===");
                console.log("Response:", response);

                const dropdown = $('#chapter-dropdown');
                dropdown.empty();
                dropdown.append('<option value="">Select Chapter...</option>');

                if (response && response.chapters && response.chapters.length > 0) {
                    console.log("Adding", response.chapters.length, "chapters from alternative method");
                    response.chapters.forEach(function (chapter, index) {
                        console.log(`Adding alternative chapter ${index}:`, chapter);
                        dropdown.append(`<option value="${chapter.chapterCode}">${chapter.chapterName}</option>`);
                    });
                } else {
                    dropdown.append('<option value="">No chapters found</option>');
                    console.log("Alternative method also found no chapters");
                }
            },
            error: function (xhr, status, error) {
                console.error("=== ALTERNATIVE ERROR ===");
                console.error("Error:", error);
                console.error("Status:", status);
                console.error("Response:", xhr.responseText);

                $('#chapter-dropdown').html('<option value="">Error occurred</option>');
            }
        });
    }

    function loadQuestionsForChapter(chapterCode) {
        console.log("=== loadQuestionsForChapter called with:", chapterCode, "===");

        const container = $('#questions-container');
        container.html('<div class="loading-state"><i class="fas fa-spinner fa-spin"></i> Loading...</div>');

        $.ajax({
            url: '/Question/GetQuestionsByChapter',
            type: 'GET',
            data: { chapterCode: chapterCode },
            success: function (response) {
                console.log("=== QUESTIONS SUCCESS ===");
                console.log("Questions response:", response);
                container.empty();

                if (response && response.length > 0) {
                    response.forEach(function (question, index) {
                        console.log(`Adding question ${index}:`, question);
                        const questionHtml = createQuestionHtml(question);
                        container.append(questionHtml);
                    });
                } else {
                    container.html(`
                        <div class="empty-state">
                            <i class="fas fa-question-circle"></i>
                            <h4>No Questions</h4>
                            <p>Start by adding your first question to this chapter.</p>
                        </div>
                    `);
                }
            },
            error: function (xhr, status, error) {
                console.error("=== QUESTIONS ERROR ===");
                console.error("Error:", error);
                console.error("Status:", status);
                console.error("Response:", xhr.responseText);
                container.html('<div class="alert alert-danger">Error loading questions</div>');
            }
        });
    }

    function createQuestionHtml(question) {
        let answersHtml = '';
        if (question.answers && question.answers.length > 0) {
            question.answers.forEach(function (answer) {
                const correctClass = answer.isCorrect ? 'correct' : '';
                const correctIcon = answer.isCorrect ? '<i class="fas fa-check-circle text-success me-2"></i>' : '<i class="fas fa-circle text-muted me-2"></i>';
                answersHtml += `
                    <div class="answer-item ${correctClass}">
                        ${correctIcon}${escapeHtml(answer.content)}
                    </div>
                `;
            });
        }

        return `
            <div class="question-item" data-question-id="${question.questionCode}">
                <div class="question-content">${escapeHtml(question.content || question.questionContent)}</div>
                <div class="answers-container">
                    ${answersHtml}
                </div>
                <div class="question-actions mt-3">
                    <button class="modern-btn secondary-btn btn-sm" onclick="editQuestion(${question.questionCode})">
                        <i class="fas fa-edit"></i> Edit
                    </button>
                    <button class="modern-btn danger-btn btn-sm" onclick="deleteQuestion(${question.questionCode})">
                        <i class="fas fa-trash"></i> Delete
                    </button>
                </div>
            </div>
        `;
    }


    function showAddQuestionForm() {
        console.log("=== showAddQuestionForm called ===");
        isEditMode = false;
        editingQuestionId = null;

        const formSection = $('#add-question-section');

        // Show the form with animation
        formSection.show();
        setTimeout(() => {
            formSection.addClass('show');
        }, 10);

        resetQuestionForm();
        $('.question-form-header h3').html('<i class="fas fa-plus-circle"></i> Add Question');
        $('#save-question-btn').html('<i class="fas fa-save"></i> Save');

        // Smooth scroll to the form
        $('html, body').animate({
            scrollTop: formSection.offset().top - 20
        }, 300);

        // Focus on the question content input
        setTimeout(() => {
            $('#question-content-input').focus();
        }, 400);
    }
    function hideAddQuestionForm() {
        const formSection = $('#add-question-section');

        // Hide with animation
        formSection.removeClass('show');
        setTimeout(() => {
            formSection.hide();
        }, 300);

        resetQuestionForm();
        isEditMode = false;
        editingQuestionId = null;
    }

    function addAnswerField() {
        const container = $('#answers-container');
        const newAnswerHtml = `
            <div class="answer-group" data-answer-index="${answerIndex}">
                <input type="text" class="answer-input" placeholder="Answer Content" required>
                <div class="correct-checkbox">
                    <input type="radio" name="correct-answer" value="${answerIndex}" id="correct-${answerIndex}">
                    <label for="correct-${answerIndex}">Correct</label>
                </div>
                <button type="button" class="remove-answer-btn" onclick="removeAnswer(${answerIndex})">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        `;
        container.append(newAnswerHtml);
        answerIndex++;
        updateRemoveButtons();
    }

    window.removeAnswer = function (index) {
        $(`.answer-group[data-answer-index="${index}"]`).remove();
        updateRemoveButtons();
    };

    function updateRemoveButtons() {
        const answerGroups = $('.answer-group');
        answerGroups.each(function (i) {
            const removeBtn = $(this).find('.remove-answer-btn');
            if (answerGroups.length <= 2) {
                removeBtn.hide();
            } else {
                removeBtn.show();
            }
        });
    }

    function resetQuestionForm() {
        $('#question-content-input').val('');
        $('#answers-container').html(`
            <div class="answer-group" data-answer-index="0">
                <input type="text" class="answer-input" placeholder="Answer Content" required>
                <div class="correct-checkbox">
                    <input type="radio" name="correct-answer" value="0" id="correct-0">
                    <label for="correct-0">Correct</label>
                </div>
                <button type="button" class="remove-answer-btn" onclick="removeAnswer(0)" style="display: none;">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
            <div class="answer-group" data-answer-index="1">
                <input type="text" class="answer-input" placeholder="Answer Content" required>
                <div class="correct-checkbox">
                    <input type="radio" name="correct-answer" value="1" id="correct-1">
                    <label for="correct-1">Correct</label>
                </div>
                <button type="button" class="remove-answer-btn" onclick="removeAnswer(1)" style="display: none;">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        `);
        answerIndex = 2;
        $('#question-message').empty();
        updateRemoveButtons();
    }

    function saveQuestion() {
        console.log("=== saveQuestion called ===");
        const questionContent = $('#question-content-input').val().trim();
        const correctAnswerIndex = $('input[name="correct-answer"]:checked').val();

        if (!questionContent) {
            showMessage('Please fill all fields', 'error');
            return;
        }

        const answers = [];
        let hasValidAnswers = true;

        $('.answer-group').each(function () {
            const answerContent = $(this).find('.answer-input').val().trim();
            if (!answerContent) {
                hasValidAnswers = false;
                return false;
            }
            answers.push(answerContent);
        });

        if (!hasValidAnswers) {
            showMessage('Please fill all fields', 'error');
            return;
        }

        if (answers.length < 2) {
            showMessage('Add at least one answer', 'error');
            return;
        }

        if (correctAnswerIndex === undefined) {
            showMessage('Select correct answer', 'error');
            return;
        }

        const saveBtn = $('#save-question-btn');
        saveBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Processing');

        console.log("Saving question:", { questionContent, chapterCode: currentChapterCode, answers, correctAnswerIndex });

        $.ajax({
            url: '/Question/AddQuestionWithAnswers',
            type: 'POST',
            data: {
                questionContent: questionContent,
                chapterCode: currentChapterCode,
                answers: answers,
                correctAnswerIndex: parseInt(correctAnswerIndex)
            },
            success: function (response) {
                console.log("Save response:", response);
                if (response.success) {
                    showMessage('Saved successfully', 'success');
                    setTimeout(function () {
                        hideAddQuestionForm();
                        loadQuestionsForChapter(currentChapterCode);
                    }, 1500);
                } else {
                    showMessage(response.message || 'Error occurred', 'error');
                }
            },
            error: function (xhr, status, error) {
                console.error("Save error:", error);
                console.error("Response:", xhr.responseText);
                showMessage('Error occurred', 'error');
            },
            complete: function () {
                saveBtn.prop('disabled', false).html('<i class="fas fa-save"></i> Save');
            }
        });
    }

    function showMessage(message, type) {
        const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
        const iconClass = type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle';

        $('#question-message').html(`
            <div class="alert ${alertClass}">
                <i class="fas ${iconClass}"></i> ${message}
            </div>
        `);

        if (type === 'success') {
            setTimeout(function () {
                $('#question-message').empty();
            }, 3000);
        }
    }

    function escapeHtml(text) {
        if (!text) return '';
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, function (m) { return map[m]; });
    }

    // Global functions for question actions
    window.editQuestion = function (questionId) {
        console.log("Edit question:", questionId);

        $.ajax({
            url: '/Question/GetQuestionForEdit',
            type: 'GET',
            data: { questionCode: questionId },
            success: function (response) {
                if (response.success && response.question) {
                    const question = response.question;

                    // Set edit mode
                    isEditMode = true;
                    editingQuestionId = questionId;

                    // Show form
                    const formSection = $('#add-question-section');
                    formSection.show();
                    setTimeout(() => {
                        formSection.addClass('show');
                    }, 10);

                    // Update form header
                    $('.question-form-header h3').html('<i class="fas fa-edit"></i> Edit Question');
                    $('#save-question-btn').html('<i class="fas fa-save"></i> Save Changes');

                    // Fill question content
                    $('#question-content-input').val(question.questionContent);

                    // Clear existing answers
                    $('#answers-container').empty();
                    answerIndex = 0;

                    // Add answers
                    if (question.answers && question.answers.length > 0) {
                        question.answers.forEach(function (answer, index) {
                            const answerHtml = `
                            <div class="answer-group" data-answer-index="${index}">
                                <input type="text" class="answer-input" placeholder="Answer Content" value="${escapeHtml(answer.content)}" required>
                                <div class="correct-checkbox">
                                    <input type="radio" name="correct-answer" value="${index}" id="correct-${index}" ${answer.isCorrect ? 'checked' : ''}>
                                    <label for="correct-${index}">Correct</label>
                                </div>
                                <button type="button" class="remove-answer-btn" onclick="removeAnswer(${index})">
                                    <i class="fas fa-trash"></i>
                                </button>
                            </div>
                        `;
                            $('#answers-container').append(answerHtml);
                            answerIndex++;
                        });
                    } else {
                        // Add default two empty answers
                        resetQuestionForm();
                    }

                    updateRemoveButtons();
                    $('#question-message').empty();

                    // Smooth scroll to the form
                    $('html, body').animate({
                        scrollTop: formSection.offset().top - 20
                    }, 300);

                    // Focus on the question content input
                    setTimeout(() => {
                        $('#question-content-input').focus();
                    }, 400);

                } else {
                    alert(response.message || 'Error occurred');
                }
            },
            error: function () {
                alert('Error occurred');
            }
        });
    };

    window.deleteQuestion = function (questionId) {
        console.log("Delete question:", questionId);
        if (confirm('Are you sure you want to delete this question and all its answers?')) {
            $.ajax({
                url: '/Question/DeleteQuestion',
                type: 'POST',
                data: { QuestionCode: questionId },
                success: function (response) {
                    if (response.success) {
                        loadQuestionsForChapter(currentChapterCode);
                    } else {
                        alert(response.message || 'Error occurred');
                    }
                },
                error: function () {
                    alert('Error occurred');
                }
            });
        }
    };

    console.log("=== All event handlers set up ===");
});

console.log("=== End of Question.js file ===");