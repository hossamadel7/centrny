// --- Exam resume logic with ticking timer, answers, and no auto submit on close! ---

let examCode = (new URLSearchParams(window.location.search)).get('examCode');
let studentCode = (new URLSearchParams(window.location.search)).get('studentCode');
let itemKey = (new URLSearchParams(window.location.search)).get('itemKey');
let examDurationSeconds = null;
let timer = null;
let timeLeft = null;
let submitted = false;
let isExam = true; // default true, will be set by backend

const STORAGE_KEY = `exam_${studentCode}_${examCode}_progress`;

window.onbeforeunload = null; // Do NOT auto-submit on close

window.history.pushState(null, null, window.location.href);
window.onpopstate = function () {
    // Just leave, don't submit, don't redirect
};

document.addEventListener('DOMContentLoaded', function () {
    // Try to load progress and time
    loadExamInfo();
    document.getElementById('examForm').onsubmit = function (e) {
        e.preventDefault();
        submitExam(false);
    };
});

function loadExamInfo() {
    fetch(`/StudentExam/GetSingleExam?studentCode=${studentCode}&examCode=${examCode}`)
        .then(res => res.json())
        .then(data => {
            if (data.error) {
                showError(data.error);
                setTimeout(() => redirectToProfile(), 2100);
                return;
            }
            if (data.alreadyTaken) {
                showError('You have already taken this exam.');
                setTimeout(() => redirectToProfile(), 2100);
                return;
            }
            document.getElementById('examTitle').textContent = data.examName + ` (Code: ${data.examCode})`;

            // ---- NEW LOGIC: Check if this is an assignment (IsExam == false) ----
            isExam = (typeof data.isExam !== "undefined") ? data.isExam : true;

            if (!isExam) {
                // Assignment: Hide timer, student can take unlimited time
                document.getElementById('examTimer').style.display = 'none';
                // Do NOT set examDurationSeconds, timer, etc.
                loadQuestions(); // Just load questions, don't start timer
                return;
            }

            // Normal exam: show timer
            document.getElementById('examTimer').style.display = '';
            examDurationSeconds = (data.examDurationMinutes || 30) * 60;
            loadQuestions();
        }).catch(() => {
            showError('Error loading exam info.');
        });
}

function showError(msg) {
    const errDiv = document.getElementById('examErrorMsg');
    errDiv.textContent = msg;
    errDiv.style.display = 'block';
    document.getElementById('questionsContainer').innerHTML = '';
    document.getElementById('submitExamBtn').disabled = true;
}

function loadQuestions() {
    fetch(`/StudentExam/GetExamQuestions?examCode=${examCode}`)
        .then(res => res.json())
        .then(data => {
            let progress = getSavedProgress();
            let startTimestamp;
            let answers = {};
            if (progress) {
                startTimestamp = progress.startTimestamp;
                answers = progress.answers || {};
            } else {
                startTimestamp = Date.now();
                answers = {};
                saveProgress({ startTimestamp, answers });
            }

            // ---- NEW LOGIC: For assignments, skip timer logic ----
            if (!isExam) {
                renderQuestions(data, answers);
                // Do not start timer, no time limit!
                return;
            }

            // Calculate time left
            let now = Date.now();
            let elapsed = Math.floor((now - startTimestamp) / 1000);
            timeLeft = examDurationSeconds - elapsed;
            if (timeLeft <= 0) {
                // Time is up: auto-submit answers from storage
                timeLeft = 0;
                renderQuestions(data, answers);
                submitExam(true, answers);
                return;
            }

            renderQuestions(data, answers);
            startTimer(startTimestamp, answers);
        }).catch(() => {
            showError('Error loading questions.');
        });
}

function renderQuestions(data, savedAnswers = {}) {
    const container = document.getElementById('questionsContainer');
    if (!data || data.length === 0) {
        container.innerHTML = `<div class="exam-error-message text-center">
            <i class="fas fa-exclamation-triangle me-2"></i>No questions found for this exam.
        </div>`;
        document.getElementById('submitExamBtn').disabled = true;
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

    // Attach listeners for saving answers
    container.querySelectorAll('input[type="radio"]').forEach(radio => {
        radio.addEventListener('change', () => {
            let progress = getSavedProgress();
            if (!progress) return;
            progress.answers[radio.name.replace('q_', '')] = radio.value;
            saveProgress(progress);
        });
    });
}

function startTimer(startTimestamp, answers) {
    updateTimerDisplay();
    timer = setInterval(() => {
        timeLeft--;
        updateTimerDisplay();
        if (timeLeft <= 0) {
            clearInterval(timer);
            submitExam(true, answers); // auto-submit saved answers
        }
    }, 1000);
}
function updateTimerDisplay() {
    let min = Math.floor(timeLeft / 60);
    let sec = timeLeft % 60;
    document.getElementById('examTimer').textContent = `${min}:${sec.toString().padStart(2, '0')}`;
    if (timeLeft <= 60) {
        document.getElementById('examTimer').style.background = '#e46a0a';
    }
    if (timeLeft <= 10) {
        document.getElementById('examTimer').style.background = '#dc3545';
    }
    if (timeLeft <= 0) {
        document.getElementById('examTimer').style.background = '#dc3545';
        document.getElementById('examTimer').textContent = "Time's up!";
    }
}

// submitExam: if answersObj is passed, use it. Otherwise, gather from DOM.
function submitExam(auto = false, answersObj = null) {
    if (submitted) return;
    submitted = true;
    if (timer) clearInterval(timer);
    document.getElementById('submitExamBtn').disabled = true;

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
            // Clear progress after submit
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

// --- Local Storage for Progress ---

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