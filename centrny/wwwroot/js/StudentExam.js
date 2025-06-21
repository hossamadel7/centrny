// Student Exam System - Integrated with existing design system
// Handles exam loading, taking, and submission functionality

// Global Variables
let studentCode = 21; // Default student code
let examDuration = 0;
let timerInterval;
let timeLeftSeconds = 0;
let currentExamCode = null;
let currentExamDurationMinutes = 0;

// Configuration
const CONFIG = {
    endpoints: {
        getExams: '/StudentExam/GetStudentExams',
        getQuestions: '/StudentExam/GetExamQuestions',
        submitExam: '/StudentExam/SubmitExam'
    },
    timers: {
        warningThreshold: 300, // 5 minutes
        criticalThreshold: 60   // 1 minute
    }
};

// Utility Functions
function showError(msg) {
    const errDiv = document.getElementById('examErrorMsg');
    if (errDiv) {
        errDiv.textContent = msg;
        errDiv.style.display = 'block';
        errDiv.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    } else if (typeof showToast === 'function') {
        showToast('Error', msg, 'error');
    } else {
        alert('Error: ' + msg);
    }
}

function clearError() {
    const errDiv = document.getElementById('examErrorMsg');
    if (errDiv) {
        errDiv.textContent = '';
        errDiv.style.display = 'none';
    }
}

function showSuccess(msg) {
    if (typeof showToast === 'function') {
        showToast('Success', msg, 'success');
    } else {
        alert('Success: ' + msg);
    }
}

function showLoading(element, text = 'Loading...') {
    if (element) {
        element.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>${text}`;
        element.disabled = true;
        element.classList.add('btn-loading');
    }
}

function hideLoading(element, originalText) {
    if (element) {
        element.innerHTML = originalText;
        element.disabled = false;
        element.classList.remove('btn-loading');
    }
}

// Main Functions
function loadStudentExams() {
    const tbody = document.querySelector("#examsTable tbody");
    if (!tbody) {
        console.error('Exams table not found');
        return;
    }

    tbody.innerHTML = '<tr><td colspan="7" class="text-center py-4"><div class="spinner-border spinner-border-sm me-2 text-primary" role="status" aria-hidden="true"></div><span class="text-muted">Loading exams...</span></td></tr>';

    fetch(`${CONFIG.endpoints.getExams}?studentCode=${studentCode}`)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: Failed to load exams`);
            }
            return response.json();
        })
        .then(data => {
            tbody.innerHTML = "";

            if (!data || data.length === 0) {
                tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4">
                    <i class="fas fa-info-circle me-2 text-muted"></i>
                    <span class="text-muted">No exams found for this student.</span>
                </td></tr>`;
                return;
            }

            data.forEach(exam => {
                const tr = document.createElement("tr");
                let actionCell = '';

                if (exam.alreadyTaken) {
                    const percentage = exam.maxDegree > 0 ? ((exam.degree / exam.maxDegree) * 100).toFixed(1) : 0;
                    const badgeClass = percentage >= 50 ? 'student-exam-score-pass' : 'student-exam-score-fail';
                    const icon = percentage >= 50 ? 'fa-check-circle' : 'fa-times-circle';

                    actionCell = `<span class="badge ${badgeClass} student-exam-score-badge">
                        <i class="fas ${icon} me-1"></i>Score: ${exam.degree}/${exam.maxDegree} (${percentage}%)
                    </span>`;
                } else {
                    actionCell = `<button class="btn student-exam-btn-primary" onclick="StudentExam.attendExam('${exam.examCode}', '${exam.examName}', ${exam.examDurationMinutes})">
                        <i class="fas fa-pencil-alt me-1"></i>Take Exam
                    </button>`;
                }

                tr.innerHTML = `
                    <td><strong>${exam.examCode}</strong></td>
                    <td><strong>${exam.examName}</strong></td>
                    <td><span class="badge student-exam-duration-badge">${exam.examDurationMinutes} min</span></td>
                    <td>${exam.subjectCode}</td>
                    <td>${exam.teacherCode}</td>
                    <td>${exam.eduYearCode}</td>
                    <td>${actionCell}</td>
                `;
                tbody.appendChild(tr);
            });

            // Update student code label
            const label = document.getElementById('currentStudentCodeLabel');
            if (label) {
                label.innerHTML = `<i class="fas fa-user-graduate me-2"></i>Student Code: ${studentCode}`;
            }
        })
        .catch(err => {
            console.error('Error loading exams:', err);
            showError(err.message);
            tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4 text-danger">
                <i class="fas fa-exclamation-triangle me-2"></i>Error: ${err.message}
            </td></tr>`;
        });
}

function attendExam(examCode, examName, examDurationMinutes) {
    clearError();
    currentExamCode = examCode;
    currentExamDurationMinutes = examDurationMinutes || 30;

    const modal = document.getElementById('attendExamModal');
    const modalInstance = new bootstrap.Modal(modal);

    document.getElementById('examTitle').textContent = `${examName} (Code: ${examCode})`;

    // Show modal
    modalInstance.show();

    // Load questions
    loadExamQuestions(examCode);
}

function loadExamQuestions(examCode) {
    const container = document.getElementById('questionsContainer');
    container.innerHTML = '<div class="text-center py-5"><div class="spinner-border me-2 text-primary" role="status" aria-hidden="true"></div><span class="text-muted">Loading questions...</span></div>';

    fetch(`${CONFIG.endpoints.getQuestions}?examCode=${examCode}`)
        .then(res => {
            if (!res.ok) {
                throw new Error(`HTTP ${res.status}: Failed to load questions`);
            }
            return res.json();
        })
        .then(data => {
            renderQuestions(data);
            startExamTimer();
        })
        .catch(err => {
            console.error('Error loading questions:', err);
            showError('Loading questions failed: ' + err.message);
        });
}

function renderQuestions(data) {
    const container = document.getElementById('questionsContainer');
    container.innerHTML = '';

    if (!data || data.length === 0) {
        container.innerHTML = `<div class="exam-error-message text-center">
            <i class="fas fa-exclamation-triangle me-2"></i>No questions found for this exam.
        </div>`;
        return;
    }

    data.forEach((q, idx) => {
        const qDiv = document.createElement('div');
        qDiv.classList.add('exam-question-card');
        qDiv.innerHTML = `
            <div class="exam-question-header">
                <div class="d-flex align-items-start flex-grow-1">
                    <span class="exam-question-number">Q${idx + 1}</span>
                    <span class="exam-question-title">${q.questionText}</span>
                </div>
                <span class="exam-question-points">${q.degree} pts</span>
            </div>
            <div class="exam-question-answers">
                ${(q.answers || []).map((ans, ansIdx) => `
                    <div class="exam-answer-option">
                        <input type="radio" name="q_${q.questionCode}" 
                               value="${ans.answerCode}" id="ans_${ans.answerCode}_${idx}" 
                               onchange="StudentExam.onAnswerSelected(this)" required>
                        <label for="ans_${ans.answerCode}_${idx}">
                            <span class="exam-answer-letter">${String.fromCharCode(65 + ansIdx)}</span>
                            ${ans.answerText}
                        </label>
                    </div>
                `).join('')}
            </div>
        `;
        container.appendChild(qDiv);
    });
}

function onAnswerSelected(radioElement) {
    const card = radioElement.closest('.exam-question-card');
    if (card) {
        card.classList.add('exam-question-answered');

        // Update progress
        updateProgress();
    }
}

function updateProgress() {
    const totalQuestions = document.querySelectorAll('.exam-question-card').length;
    const answeredQuestions = document.querySelectorAll('.exam-question-answered').length;

    // You can add a progress indicator here if needed
    console.log(`Progress: ${answeredQuestions}/${totalQuestions} questions answered`);
}

function startExamTimer() {
    examDuration = currentExamDurationMinutes;
    timeLeftSeconds = examDuration * 60;
    updateTimerDisplay();

    if (timerInterval) clearInterval(timerInterval);
    timerInterval = setInterval(() => {
        timeLeftSeconds--;
        updateTimerDisplay();
        if (timeLeftSeconds <= 0) {
            clearInterval(timerInterval);
            submitExam(true); // Auto-submit when time is up
        }
    }, 1000);
}

function updateTimerDisplay() {
    const disp = document.getElementById('examTimer');
    if (!disp) return;

    let min = Math.floor(timeLeftSeconds / 60);
    let sec = timeLeftSeconds % 60;
    disp.textContent = `${min}:${sec.toString().padStart(2, '0')}`;

    const alertDiv = disp.closest('.exam-timer-alert');

    if (timeLeftSeconds <= CONFIG.timers.criticalThreshold) {
        alertDiv.classList.remove('alert-warning');
        alertDiv.classList.add('alert-danger');
        disp.classList.add('timer-critical');
    } else if (timeLeftSeconds <= CONFIG.timers.warningThreshold) {
        alertDiv.classList.remove('alert-danger');
        alertDiv.classList.add('alert-warning');
        disp.classList.remove('timer-critical');
    }

    if (timeLeftSeconds <= 0) {
        disp.textContent = "Time's up!";
        disp.classList.remove('timer-critical');
    }
}

function submitExam(isAutoSubmit = false) {
    if (timerInterval) clearInterval(timerInterval);
    clearError();

    // Collect answers
    const qDivs = document.querySelectorAll('.exam-question-card');
    const answers = [];
    let allAnswered = true;
    let unansweredQuestions = [];

    qDivs.forEach((div, idx) => {
        const radio = div.querySelector('input[type="radio"]:checked');
        if (radio) {
            const questionCode = radio.name.replace('q_', '');
            const answerCode = radio.value;
            answers.push({ questionCode, answerCode });
        } else {
            allAnswered = false;
            unansweredQuestions.push(idx + 1);
        }
    });

    // Warn about unanswered questions if not auto-submit
    if (!allAnswered && !isAutoSubmit) {
        const proceed = confirm(`You have ${unansweredQuestions.length} unanswered question(s) (Q${unansweredQuestions.join(', Q')}). Do you want to submit anyway?`);
        if (!proceed) return;
    }

    // Show loading state
    const submitBtn = document.getElementById('submitExamBtn');
    const originalBtnText = '<i class="fas fa-paper-plane me-2"></i>Submit Exam';
    showLoading(submitBtn, 'Submitting...');

    // Submit to server
    fetch(CONFIG.endpoints.submitExam, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        },
        body: JSON.stringify({
            studentCode,
            examCode: currentExamCode,
            answers
        })
    })
        .then(async res => {
            if (!res.ok) {
                let errorMsg = "Unknown error occurred";
                const contentType = res.headers.get("content-type");

                if (contentType && contentType.includes("application/json")) {
                    try {
                        let errData = await res.json();
                        errorMsg = errData.message || JSON.stringify(errData);
                    } catch {
                        errorMsg = await res.text();
                    }
                } else {
                    errorMsg = await res.text();
                }
                throw new Error(errorMsg);
            }
            return res.json();
        })
        .then(result => {
            showSuccess(result.message || "Exam submitted successfully!");

            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('attendExamModal'));
            if (modal) modal.hide();

            // Reload exams to show updated status
            setTimeout(() => loadStudentExams(), 500);
        })
        .catch(err => {
            console.error('Submission error:', err);
            showError("Submission failed: " + err.message);
        })
        .finally(() => {
            hideLoading(submitBtn, originalBtnText);
        });
}

// Event Handlers
function initializeEventHandlers() {
    // Student code input handler
    const codeInput = document.getElementById("studentCodeInput");
    if (codeInput) {
        codeInput.addEventListener("change", function () {
            const newCode = parseInt(this.value);
            if (newCode && newCode > 0) {
                studentCode = newCode;
                loadStudentExams();
            }
        });

        codeInput.addEventListener("keypress", function (e) {
            if (e.key === 'Enter') {
                const newCode = parseInt(this.value);
                if (newCode && newCode > 0) {
                    studentCode = newCode;
                    loadStudentExams();
                }
            }
        });
    }

    // Modal event handlers
    const modal = document.getElementById('attendExamModal');
    if (modal) {
        modal.addEventListener('hidden.bs.modal', function () {
            if (timerInterval) clearInterval(timerInterval);
            clearError();
            const container = document.getElementById('questionsContainer');
            if (container) container.innerHTML = '';
        });
    }

    // Button handlers
    const submitBtn = document.getElementById('submitExamBtn');
    if (submitBtn) {
        submitBtn.addEventListener('click', function (e) {
            e.preventDefault();
            submitExam(false);
        });
    }
}

// Public API - Global namespace for HTML onclick handlers
window.StudentExam = {
    attendExam: attendExam,
    loadStudentExams: loadStudentExams,
    onAnswerSelected: onAnswerSelected,
    submitExam: submitExam
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    // Get initial student code from input
    const codeInput = document.getElementById("studentCodeInput");
    if (codeInput && codeInput.value) {
        studentCode = parseInt(codeInput.value) || 21;
    }

    initializeEventHandlers();
    loadStudentExams();
});

// Export for module systems (if needed)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { StudentExam: window.StudentExam };
}