// --- Exam timer: server-based, never resets, secure ---
// --- StudentExam record is created on first load, timer is from DB! ---

let examCode = (new URLSearchParams(window.location.search)).get('examCode');
let studentCode = (new URLSearchParams(window.location.search)).get('studentCode');
let itemKey = (new URLSearchParams(window.location.search)).get('itemKey');
let examDurationSeconds = null;
let timer = null;
let timeLeft = null;
let submitted = false;
let isExam = true;

const STORAGE_KEY = `exam_${studentCode}_${examCode}_progress`;

window.onbeforeunload = null;
window.history.pushState(null, null, window.location.href);
window.onpopstate = function () { };

document.addEventListener('DOMContentLoaded', function () {
    loadExamInfo();
    const examForm = document.getElementById('examForm');
    if (examForm) {
        examForm.onsubmit = function (e) {
            e.preventDefault();
            submitExam(false);
        };
    }
});

function loadExamInfo() {
    fetch(`/StudentExam/GetSingleExam?studentCode=${studentCode}&examCode=${examCode}`)
        .then(res => res.json())
        .then(data => {
            if (data.error) {
                showError(data.error);
                return;
            }
            if (data.alreadyTaken) {
                clearProgress();
                showError('You have already taken this exam.');
                return;
            }
            const examTitle = document.getElementById('examTitle');
            if (examTitle)
                examTitle.textContent = data.examName ;

            isExam = (typeof data.isExam !== "undefined") ? data.isExam : true;

            if (!isExam) {
                const timerDiv = document.getElementById('examTimer');
                if (timerDiv) timerDiv.style.display = 'none';
                loadQuestions();
                return;
            }

            const timerDiv = document.getElementById('examTimer');
            if (timerDiv) timerDiv.style.display = '';
            fetch(`/StudentExam/GetStudentExamStartTime?studentCode=${studentCode}&examCode=${examCode}`)
                .then(res => res.json())
                .then(timerData => {
                    console.log("TimerData response from backend:", timerData);
                    if (timerData.error) {
                        showError(timerData.error || 'Timer information missing.');
                        return;
                    }
                    if (!timerData.durationSeconds || !timerData.examStartTime) {
                        showError('Timer information missing.');
                        return;
                    }
                    // If backend provides timeLeft, use it; otherwise, calculate
                    if (typeof timerData.timeLeft === "number") {
                        timeLeft = timerData.timeLeft;
                    } else {
                        examDurationSeconds = timerData.durationSeconds;
                        let now = new Date();
                        let serverStart = new Date(timerData.examStartTime);
                        let elapsed = Math.floor((now.getTime() - serverStart.getTime()) / 1000);
                        timeLeft = examDurationSeconds - elapsed;
                    }

                    // Debug info for diagnosis
                    console.log('now:', new Date().toISOString(), 'examStart:', timerData.examStartTime, 'timeLeft:', timeLeft);

                    if (timeLeft <= 0) {
                        timeLeft = 0;
                        loadQuestions(true);
                        return;
                    }
                    loadQuestions();
                })
                .catch((err) => {
                    console.error('Fetch error:', err);
                    showError('Error loading timer info.');
                });

        });
}

function showError(msg) {
    const errDiv = document.getElementById('examErrorMsg');
    if (errDiv) {
        errDiv.textContent = msg || 'Unknown error';
        errDiv.style.display = 'block';
    }
    const qContainer = document.getElementById('questionsContainer');
    if (qContainer) qContainer.innerHTML = '';
    const submitBtn = document.getElementById('submitExamBtn');
    if (submitBtn) submitBtn.disabled = true;

    // Also log in console for debugging
    console.error("Exam Error:", msg);
}

function loadQuestions(autoSubmitIfTimeUp = false) {
    fetch(`/StudentExam/GetExamQuestions?examCode=${examCode}`)
        .then(res => res.json())
        .then(data => {
            let progress = getSavedProgress();
            let answers = {};
            if (progress) {
                answers = progress.answers || {};
            } else {
                answers = {};
                saveProgress({ answers });
            }

            if (!isExam) {
                renderQuestions(data, answers);
                return;
            }

            if (typeof timeLeft !== 'number' || timeLeft <= 0) {
                timeLeft = 0;
                renderQuestions(data, answers);

                // Double-check with server if exam is already submitted before attempting auto-submit
                fetch(`/StudentExam/GetSingleExam?studentCode=${studentCode}&examCode=${examCode}`)
                    .then(res => res.json())
                    .then(singleExam => {
                        if (singleExam.alreadyTaken) {
                            clearProgress();
                            showError('You have already taken this exam.');
                            setTimeout(() => {
                                window.onbeforeunload = null;
                                redirectToProfile();
                            }, 2200);
                            return;
                        }
                        // Only auto-submit if there are answers and exam is not submitted
                        if (autoSubmitIfTimeUp && Object.keys(answers).length > 0) {
                            submitExam(true, answers);
                        } else if (autoSubmitIfTimeUp) {
                            showError('Time is up! No answers to submit.');
                            setTimeout(() => {
                                window.onbeforeunload = null;
                                redirectToProfile();
                            }, 2200);
                        }
                    });
                return;
            }

            renderQuestions(data, answers);
            startTimer(answers);
        }).catch(() => {
            showError('Error loading questions.');
        });
}

function renderQuestions(data, savedAnswers = {}) {
    const container = document.getElementById('questionsContainer');
    if (!container) return;
    if (!data || data.length === 0) {
        container.innerHTML = `<div class="exam-error-message text-center">
            <i class="fas fa-exclamation-triangle me-2"></i>No questions found for this exam.
        </div>`;
        const submitBtn = document.getElementById('submitExamBtn');
        if (submitBtn) submitBtn.disabled = true;
        return;
    }
    container.innerHTML = data.map((q, idx) => `
        <div class="exam-question-card">
            <div class="exam-question-header">
                <span>Q${idx + 1}. ${q.questionText}</span>
                <span style="font-size:.95em;color:#666">${q.degree} pts</span>
            </div>
            <div class="exam-question-answers">
                ${(q.answers || []).map((ans, ansIdx) => `
                    <div class="exam-answer-option">
                        <input type="radio" name="q_${q.questionCode}" value="${ans.answerCode}" id="ans_${ans.answerCode}_${idx}" 
                            ${savedAnswers[q.questionCode] == ans.answerCode ? "checked" : ""} required>
                        <label for="ans_${ans.answerCode}_${idx}">
                            <span class="exam-answer-letter">${String.fromCharCode(65 + ansIdx)}</span>
                            ${ans.answerText}
                        </label>
                    </div>
                `).join('')}
            </div>
        </div>
    `).join('');

    container.querySelectorAll('input[type="radio"]').forEach(radio => {
        radio.addEventListener('change', () => {
            let progress = getSavedProgress() || { answers: {} };
            progress.answers[radio.name.replace('q_', '')] = radio.value;
            saveProgress(progress);
        });
    });
}

function startTimer(answers) {
    updateTimerDisplay();
    timer = setInterval(() => {
        timeLeft--;
        updateTimerDisplay();
        if (timeLeft <= 0) {
            clearInterval(timer);
            // Double-check with server before auto-submitting
            fetch(`/StudentExam/GetSingleExam?studentCode=${studentCode}&examCode=${examCode}`)
                .then(res => res.json())
                .then(singleExam => {
                    if (singleExam.alreadyTaken) {
                        clearProgress();
                        showError('You have already taken this exam.');
                        setTimeout(() => {
                            window.onbeforeunload = null;
                            redirectToProfile();
                        }, 2200);
                        return;
                    }
                    if (Object.keys(answers).length > 0) {
                        submitExam(true, answers);
                    } else {
                        showError('Time is up! No answers to submit.');
                        setTimeout(() => {
                            window.onbeforeunload = null;
                            redirectToProfile();
                        }, 2200);
                    }
                });
        }
    }, 1000);
}

function updateTimerDisplay() {
    const timerDiv = document.getElementById('examTimer');
    if (!timerDiv) return;
    let min = Math.floor(timeLeft / 60);
    let sec = timeLeft % 60;
    timerDiv.textContent = `${min}:${sec.toString().padStart(2, '0')}`;
    if (timeLeft <= 60) {
        timerDiv.style.background = '#e46a0a';
    }
    if (timeLeft <= 10) {
        timerDiv.style.background = '#dc3545';
    }
    if (timeLeft <= 0) {
        timerDiv.style.background = '#dc3545';
        timerDiv.textContent = "Time's up!";
    }
}

function submitExam(auto = false, answersObj = null) {
    if (submitted) return;
    submitted = true;
    if (timer) clearInterval(timer);
    const submitBtn = document.getElementById('submitExamBtn');
    if (submitBtn) submitBtn.disabled = true;

    let answers = [];
    if (answersObj) {
        for (const [questionCode, answerCode] of Object.entries(answersObj)) {
            answers.push({ questionCode, answerCode });
        }
    } else {
        const qDivs = document.querySelectorAll('.exam-question-card');
        qDivs.forEach(div => {
            const radio = div.querySelector('input[type="radio"]:checked');
            if (radio) {
                const questionCode = radio.name.replace('q_', '');
                const answerCode = radio.value;
                answers.push({ questionCode, answerCode });
            }
        });
    }

    // Prevent submission if no answers and auto
    if (auto && answers.length === 0) {
        showError('Time is up! No answers to submit.');
        setTimeout(() => {
            window.onbeforeunload = null;
            redirectToProfile();
        }, 2200);
        return;
    }

    fetch('/StudentExam/SubmitExam', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            studentCode: parseInt(studentCode),
            examCode: parseInt(examCode),
            answers: answers
        })
    })
        .then(res => res.json())
        .then(result => {
            clearProgress();
            if (result.message) {
                showError(result.message);
            } else {
                showError('Exam submitted!');
            }
            setTimeout(() => {
                window.onbeforeunload = null;
                redirectToProfile();
            }, 2200);
        })
        .catch(() => {
            showError('Submission failed. Your answers may be lost.');
            setTimeout(() => {
                window.onbeforeunload = null;
                redirectToProfile();
            }, 2400);
        });
}

function redirectToProfile() {
    if (itemKey) {
        window.location.href = '/Student/' + itemKey;
    } else {
        window.location.href = '/Student/' + studentCode;
    }
}

function getSavedProgress() {
    try {
        let raw = localStorage.getItem(STORAGE_KEY);
        if (!raw) return null;
        return JSON.parse(raw);
    } catch {
        return null;
    }
}

function saveProgress(progress) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(progress));
    } catch { }
}

function clearProgress() {
    try {
        localStorage.removeItem(STORAGE_KEY);
    } catch { }
}