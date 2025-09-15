// student-registration2.js (updated)
// Multi-step form logic with scrolling & minor UX improvements

let currentStep = 1;
let selectedSubjects = [];
let availableSubjects = [];
let availableTeachers = [];
let registrationMode = "Offline";
let pinValidated = false;

document.addEventListener('DOMContentLoaded', function () {
    updateStepButtons();
    setupEventListeners();
    modeChangeHandler();
    ensureScrollEnabled();
});

function ensureScrollEnabled() {
    document.documentElement.style.overflowY = 'auto';
    document.body.style.overflowY = 'auto';
}

function setupEventListeners() {
    document.querySelectorAll('input[name="Mode"]').forEach(radio => {
        radio.addEventListener('change', modeChangeHandler);
    });

    const branchSelect = document.getElementById('branchCode');
    if (branchSelect) {
        branchSelect.addEventListener('change', function () {
            resetSubjectsAndTeachers();
            if (this.value && document.getElementById('yearCode').value) {
                loadAvailableSubjects();
            }
        });
    }

    const yearSelect = document.getElementById('yearCode');
    if (yearSelect) {
        yearSelect.addEventListener('change', function () {
            resetSubjectsAndTeachers();
            if (this.value && document.getElementById('branchCode').value) {
                loadAvailableSubjects();
            }
        });
    }

    document.querySelectorAll('input[required], select[required]').forEach(input => {
        input.addEventListener('blur', validateField);
        input.addEventListener('input', clearValidation);
    });

    document.getElementById('prevBtn').addEventListener('click', function () {
        changeStep(-1);
    });
    document.getElementById('nextBtn').addEventListener('click', function () {
        changeStep(1);
    });
    document.getElementById('submitBtn').addEventListener('click', function () {
        submitRegistration();
    });

    const validatePinBtn = document.getElementById('validatePinBtn');
    if (validatePinBtn) validatePinBtn.addEventListener('click', validatePin);

    const usernameInput = document.getElementById('username');
    if (usernameInput) usernameInput.addEventListener('blur', checkUsername);

    const completeOnlineBtn = document.getElementById('completeOnlineBtn');
    if (completeOnlineBtn) completeOnlineBtn.addEventListener('click', submitOnlineRegistration);
}

// MODE HANDLING
function modeChangeHandler() {
    const mode = document.querySelector('input[name="Mode"]:checked').value;
    registrationMode = mode;

    const branchSection = document.getElementById('branchSection');
    const onlinePinSection = document.getElementById('onlinePinSection');

    if (mode === "Online") {
        if (branchSection) branchSection.style.display = "none";
        if (onlinePinSection) onlinePinSection.style.display = "";
        const onlineYearSection = document.getElementById('onlineYearSection');
        if (onlineYearSection) onlineYearSection.style.display = "none";
        document.getElementById('branchCode').required = false;
        document.getElementById('yearCode').required = false;
    } else {
        if (branchSection) branchSection.style.display = "";
        if (onlinePinSection) onlinePinSection.style.display = "none";
        document.getElementById('branchCode').required = true;
        document.getElementById('yearCode').required = true;
    }
    resetSubjectsAndTeachers();
}

function resetSubjectsAndTeachers() {
    selectedSubjects = [];
    availableSubjects = [];
    availableTeachers = [];
    const subjectsDiv = document.getElementById('availableSubjects');
    if (subjectsDiv) subjectsDiv.innerHTML = '';
    const schedulesDiv = document.getElementById('scheduleSelection');
    if (schedulesDiv) schedulesDiv.innerHTML = '';
    const summaryDiv = document.getElementById('selectedSubjectsSummary');
    if (summaryDiv) summaryDiv.style.display = 'none';
}

// STEP NAVIGATION
function changeStep(direction) {
    if (direction === 1 && !validateCurrentStep()) return;

    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (currentStepContent) currentStepContent.classList.remove('active');
    const currentStepIndicator = document.getElementById(`step${currentStep}`);
    if (currentStepIndicator) currentStepIndicator.classList.remove('active');

    currentStep += direction;
    if (currentStep < 1) currentStep = 1;
    if (currentStep > 5) currentStep = 5;

    const newStepContent = document.getElementById(`step${currentStep}Content`);
    if (newStepContent) newStepContent.classList.add('active');
    const newStepIndicator = document.getElementById(`step${currentStep}`);
    if (newStepIndicator) newStepIndicator.classList.add('active');

    if (currentStep === 3) {
        const branchCode = document.getElementById('branchCode')?.value;
        const yearCode = document.getElementById('yearCode')?.value;
        if ((branchCode && yearCode && registrationMode === "Offline") || registrationMode === "Online") {
            loadAvailableSubjects();
        }
    } else if (currentStep === 4) {
        if (registrationMode === "Offline") {
            document.getElementById('scheduleSection').style.display = "";
            document.getElementById('onlineUserSection').style.display = "none";
            loadAvailableSchedules();
        } else {
            document.getElementById('scheduleSection').style.display = "none";
            if (pinValidated) {
                document.getElementById('onlineUserSection').style.display = "";
            }
        }
    } else if (currentStep === 5) {
        if (registrationMode === "Offline") {
            document.getElementById('offlineSummarySection').style.display = "";
            document.getElementById('onlineRegistrationSection').style.display = "none";
            showRegistrationSummary();
        } else {
            document.getElementById('offlineSummarySection').style.display = "none";
            document.getElementById('onlineRegistrationSection').style.display = "";
        }
    }

    updateStepButtons();
    scrollToFormTop();
}

function scrollToFormTop() {
    const box = document.getElementById('registrationRootBox');
    if (box) {
        const top = box.getBoundingClientRect().top + window.scrollY - 20;
        window.scrollTo({ top, behavior: 'smooth' });
    } else {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }
}

function updateStepButtons() {
    document.getElementById('prevBtn').style.display = currentStep > 1 ? 'inline-flex' : 'none';
    document.getElementById('nextBtn').style.display = currentStep < 5 ? 'inline-flex' : 'none';
    document.getElementById('submitBtn').style.display = (currentStep === 5 && registrationMode === "Offline") ? 'inline-flex' : 'none';
}

// VALIDATION
function validateCurrentStep() {
    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (!currentStepContent) return false;

    const requiredFields = currentStepContent.querySelectorAll('input[required], select[required]');
    let isValid = true;

    requiredFields.forEach(field => {
        if (!validateField({ target: field })) isValid = false;
    });

    if (currentStep === 2) {
        if (registrationMode === "Offline") {
            const branchCode = document.getElementById('branchCode').value;
            const yearCode = document.getElementById('yearCode').value;
            if (!branchCode || !yearCode) {
                showAlert('Please select both branch and academic year.', 'danger');
                isValid = false;
            }
        } else if (registrationMode === "Online" && !pinValidated) {
            showAlert('Please validate your PIN code first.', 'danger');
            isValid = false;
        }
    }

    if (currentStep === 3) {
        const completeSelections = selectedSubjects.filter(s => s.teacherCode);
        if (completeSelections.length === 0) {
            showAlert('Please select at least one subject and a teacher.', 'danger');
            isValid = false;
        }
    }

    if (currentStep === 4 && registrationMode === "Offline") {
        const missingSchedules = selectedSubjects.filter(s => s.teacherCode && !s.scheduleCode);
        if (missingSchedules.length > 0) {
            showAlert('Please select schedules for all chosen subjects.', 'danger');
            isValid = false;
        }
    }

    if (currentStep === 4 && registrationMode === "Online") {
        const username = document.getElementById('username')?.value?.trim();
        const password = document.getElementById('password')?.value;
        if (!username || !password) {
            showAlert('Username and password are required.', 'danger');
            isValid = false;
        }
    }

    return isValid;
}

function validateField(event) {
    const field = event.target;
    const value = field.value.trim();
    let isValid = true;
    let message = '';

    field.classList.remove('is-invalid');
    const feedback = field.parentNode.querySelector('.invalid-feedback');
    if (feedback) feedback.textContent = '';

    if (field.hasAttribute('required') && !value) {
        isValid = false;
        message = 'This field is required.';
    }

    if (value && field.type === 'tel') {
        if (!/^[\+]?[0-9\s\-\(\)]{8,}$/.test(value)) {
            isValid = false;
            message = 'Enter a valid phone number.';
        }
    }

    if (value && field.type === 'email') {
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) {
            isValid = false;
            message = 'Enter a valid email address.';
        }
    }

    if (!isValid) {
        field.classList.add('is-invalid');
        if (feedback) feedback.textContent = message;
    }

    return isValid;
}

function clearValidation(event) {
    const field = event.target;
    field.classList.remove('is-invalid');
    const feedback = field.parentNode.querySelector('.invalid-feedback');
    if (feedback) feedback.textContent = '';
}

// SUBJECT / TEACHER / SCHEDULE LOADING
async function loadAvailableSubjects() {
    let branchCode = '';
    let yearCode = '';

    if (registrationMode === "Offline") {
        branchCode = document.getElementById('branchCode')?.value || '';
        yearCode = document.getElementById('yearCode')?.value || '';
        if (!branchCode || !yearCode) {
            resetSubjectsAndTeachers();
            return;
        }
    } else if (registrationMode === "Online") {
        yearCode = document.getElementById('onlineYearCode')?.value || '';
        branchCode = '0';
        if (!yearCode) {
            resetSubjectsAndTeachers();
            return;
        }
    }

    showLoading();
    try {
        const url = `/Student/GetAvailableSubjects?branchCode=${branchCode}&yearCode=${yearCode}`;
        const response = await fetch(url);
        const result = await response.json();

        if (result.error) {
            showAlert(result.error, 'danger');
            availableSubjects = [];
        } else {
            availableSubjects = result;
        }

        renderSubjects();
    } catch (error) {
        showAlert('Failed to load subjects.', 'danger');
        availableSubjects = [];
    } finally {
        hideLoading();
    }
}

async function loadTeachersForSubjects() {
    let branchCode = '';
    let yearCode = '';

    if (registrationMode === "Offline") {
        branchCode = document.getElementById('branchCode')?.value || '';
        yearCode = document.getElementById('yearCode')?.value || '';
    } else if (registrationMode === "Online") {
        yearCode = document.getElementById('onlineYearCode')?.value || '';
        branchCode = '0';
    }

    const subjectCodes = availableSubjects.map(s => s.subjectCode).join(',');
    if (!subjectCodes) {
        availableTeachers = [];
        return;
    }

    try {
        const url = `/Student/GetTeachersForSubjects?subjectCodes=${subjectCodes}&branchCode=${branchCode}&yearCode=${yearCode}`;
        const response = await fetch(url);
        const result = await response.json();

        if (result.error) {
            console.warn(result.error);
            availableTeachers = [];
        } else {
            availableTeachers = result;
        }
    } catch (error) {
        console.error('Failed to load teachers:', error);
        availableTeachers = [];
    }
}

async function loadScheduleForSubjectTeacher(subjectCode, teacherCode) {
    let branchCode = '';
    let yearCode = '';

    if (registrationMode === "Offline") {
        branchCode = document.getElementById('branchCode')?.value || '';
        yearCode = document.getElementById('yearCode')?.value || '';
    }

    try {
        const url = `/Student/GetSchedulesForSubjectTeacher?subjectCode=${subjectCode}&teacherCode=${teacherCode}&branchCode=${branchCode}&yearCode=${yearCode}`;
        const response = await fetch(url);
        const result = await response.json();
        if (result.error) {
            console.error(result.error);
            return [];
        }
        return result;
    } catch (error) {
        console.error('Failed to load schedules:', error);
        return [];
    }
}

async function loadAvailableSchedules() {
    const container = document.getElementById('scheduleSelection');
    if (!container) return;

    const subjectsWithTeachers = selectedSubjects.filter(s => s.teacherCode);

    if (subjectsWithTeachers.length === 0) {
        container.innerHTML = `
            <div class="empty-block">
                <i class="fas fa-calendar-times"></i>
                <p>No subjects with teachers selected.</p>
            </div>`;
        return;
    }

    container.innerHTML = '<div class="loading-inline"><i class="fas fa-spinner fa-spin"></i> Loading schedules...</div>';

    let scheduleHtml = '';
    for (const subject of subjectsWithTeachers) {
        const schedules = await loadScheduleForSubjectTeacher(subject.subjectCode, subject.teacherCode);
        scheduleHtml += `
        <div class="schedule-subject-block">
            <h5><i class="fas fa-book me-2"></i>${subject.subjectName} - ${subject.teacherName}</h5>
            <div class="schedule-options">`;

        if (schedules.length > 0) {
            schedules.forEach(sch => {
                const isSelected = subject.scheduleCode === sch.scheduleCode;
                scheduleHtml += `
                <label class="schedule-option ${isSelected ? 'selected' : ''}">
                    <input type="radio"
                           name="schedule_${subject.subjectCode}_${subject.teacherCode}"
                           value="${sch.ScheduleCode}"
                           ${isSelected ? 'checked' : ''}
                           onchange="selectSchedule(${subject.subjectCode}, ${subject.teacherCode}, ${sch.scheduleCode}, '${sch.scheduleName}', '${sch.dayOfWeek}', '${sch.startTime}', '${sch.endTime}')">
                    <div class="so-meta">
                        <strong>${sch.scheduleName}</strong>
                        <small>${sch.dayOfWeek} ${sch.startTime}-${sch.endTime} | ${sch.hallName}</small>
                    </div>
                </label>`;
            });
        } else {
            scheduleHtml += '<p class="text-muted small mb-0">No schedules available.</p>';
        }

        scheduleHtml += `</div></div>`;
    }

    container.innerHTML = scheduleHtml;
}

// RENDER SUBJECTS
function renderSubjects() {
    const container = document.getElementById('availableSubjects');
    if (!container) return;

    if (availableSubjects.length === 0) {
        container.innerHTML = `
            <div class="empty-block">
                <i class="fas fa-book"></i>
                <p>No subjects available for the selected criteria.</p>
            </div>`;
        return;
    }

    loadTeachersForSubjects().then(() => {
        let html = '';
        availableSubjects.forEach(subject => {
            const isSelected = selectedSubjects.some(s => s.subjectCode === subject.subjectCode);
            const subjectTeachers = availableTeachers.filter(t => t.subjectCode === subject.subjectCode);

            html += `
            <div class="subject-selection ${isSelected ? 'selected' : ''}" data-subject-code="${subject.subjectCode}">
                <div class="subject-header">
                    <h5 class="subject-title"><i class="fas fa-book me-2"></i>${subject.subjectName}</h5>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox"
                               id="subject_${subject.subjectCode}"
                               ${isSelected ? 'checked' : ''}
                               onchange="toggleSubject(${subject.subjectCode}, '${subject.subjectName}')">
                    </div>
                </div>
                <div class="teachers-list ${isSelected ? '' : 'd-none'}" id="teachers_${subject.subjectCode}">
                    <label class="form-label small mb-2">${'Select Teacher'}</label>`;

            if (subjectTeachers.length > 0) {
                subjectTeachers.forEach(teacher => {
                    const selectedSubject = selectedSubjects.find(s => s.subjectCode === subject.subjectCode);
                    const isTeacherSelected = selectedSubject && selectedSubject.teacherCode === teacher.teacherCode;

                    html += `
                    <div class="teacher-option ${isTeacherSelected ? 'selected' : ''}"
                         onclick="selectTeacher(${subject.subjectCode}, '${subject.subjectName}', ${teacher.teacherCode}, '${teacher.teacherName}', ${teacher.yearCode}, ${teacher.eduYearCode})">
                        <div class="form-check">
                            <input class="form-check-input" type="radio"
                                   name="teacher_${subject.subjectCode}"
                                   value="${teacher.teacherCode}"
                                   id="teacher_${subject.subjectCode}_${teacher.teacherCode}"
                                   ${isTeacherSelected ? 'checked' : ''}>
                            <label class="form-check-label">
                                <strong>${teacher.teacherName}</strong><br>
                                <small class="text-muted">${teacher.teacherPhone || 'N/A'}</small>
                            </label>
                        </div>
                    </div>`;
                });
            } else {
                html += '<p class="text-muted small mb-0">No teachers available.</p>';
            }

            html += `</div></div>`;
        });

        container.innerHTML = html;
    });
}

// SUBJECT / TEACHER / SCHEDULE SELECTION HANDLERS
window.toggleSubject = function (subjectCode, subjectName) {
    const checkbox = document.getElementById(`subject_${subjectCode}`);
    const subjectCard = document.querySelector(`[data-subject-code="${subjectCode}"]`);
    const teachersDiv = document.getElementById(`teachers_${subjectCode}`);

    if (checkbox.checked) {
        if (!selectedSubjects.some(s => s.subjectCode === subjectCode)) {
            selectedSubjects.push({
                subjectCode,
                subjectName,
                teacherCode: null,
                teacherName: null,
                yearCode: null,
                eduYearCode: null,
                scheduleCode: null,
                scheduleName: null
            });
        }
        subjectCard.classList.add('selected');
        if (teachersDiv) teachersDiv.classList.remove('d-none');
    } else {
        selectedSubjects = selectedSubjects.filter(s => s.subjectCode !== subjectCode);
        subjectCard.classList.remove('selected');
        if (teachersDiv) teachersDiv.classList.add('d-none');
        document.querySelectorAll(`input[name="teacher_${subjectCode}"]`).forEach(r => r.checked = false);
    }

    updateSelectedSubjectsSummary();
};

window.selectTeacher = function (subjectCode, subjectName, teacherCode, teacherName, yearCode, eduYearCode) {
    const radio = document.getElementById(`teacher_${subjectCode}_${teacherCode}`);
    if (radio) radio.checked = true;

    const idx = selectedSubjects.findIndex(s => s.subjectCode === subjectCode);
    if (idx !== -1) {
        selectedSubjects[idx] = {
            ...selectedSubjects[idx],
            teacherCode,
            teacherName,
            yearCode,
            eduYearCode,
            scheduleCode: null,
            scheduleName: null
        };
    }

    document.querySelectorAll(`#teachers_${subjectCode} .teacher-option`).forEach(o => o.classList.remove('selected'));
    const clicked = document.querySelector(`#teacher_${subjectCode}_${teacherCode}`)?.closest('.teacher-option');
    if (clicked) clicked.classList.add('selected');

    updateSelectedSubjectsSummary();
};

window.selectSchedule = function (subjectCode, teacherCode, scheduleCode, scheduleName, dayOfWeek, startTime, endTime) {
    const idx = selectedSubjects.findIndex(s => s.subjectCode === subjectCode && s.teacherCode === teacherCode);
    if (idx !== -1) {
        selectedSubjects[idx].scheduleCode = scheduleCode;
        selectedSubjects[idx].scheduleName = `${dayOfWeek} ${startTime}-${endTime}`;
    }
    updateSelectedSubjectsSummary();
};

window.removeSubject = function (subjectCode) {
    const checkbox = document.getElementById(`subject_${subjectCode}`);
    if (checkbox) {
        checkbox.checked = false;
        window.toggleSubject(subjectCode, '');
    }
};

function updateSelectedSubjectsSummary() {
    const container = document.getElementById('selectedSubjectsSummary');
    const list = document.getElementById('selectedSubjectsList');

    if (selectedSubjects.length === 0) {
        container.style.display = 'none';
        return;
    }

    list.innerHTML = selectedSubjects.map(s => `
        <div class="summary-item">
            <div class="si-info">
                <strong>${s.subjectName}</strong>
                ${s.teacherName ? `<br><small class="text-muted">${s.teacherName}</small>` : ''}
                ${s.scheduleName ? `<br><small class="text-info">${s.scheduleName}</small>` : ''}
            </div>
            <button type="button" class="btn-remove" onclick="removeSubject(${s.subjectCode})">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `).join('');

    container.style.display = 'block';
}

// PIN VALIDATION
async function validatePin() {
    const pin = document.getElementById('pinCode').value.trim();
    if (!pin) {
        setPinError("PIN code is required.");
        return;
    }

    setPinError("");
    showLoading();

    try {
        const res = await fetch('/Student/ValidatePin', {
            method: "POST",
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ pin })
        });
        const data = await res.json();

        if (data.valid) {
            document.getElementById('pinError').textContent = "";
            pinValidated = true;
            document.getElementById('onlineYearSection').style.display = "";

            const onlineYearSelect = document.getElementById('onlineYearCode');
            if (onlineYearSelect && !onlineYearSelect.dataset.bound) {
                onlineYearSelect.addEventListener('change', function () {
                    resetSubjectsAndTeachers();
                });
                onlineYearSelect.dataset.bound = 'true';
            }

            showAlert("PIN validated successfully. Select academic year.", "success");
        } else {
            setPinError(data.error || "Invalid PIN.");
            pinValidated = false;
        }
    } catch {
        setPinError("Network error. Please try again.");
        pinValidated = false;
    }

    hideLoading();
}
function setPinError(msg) {
    document.getElementById('pinError').textContent = msg;
}

// USERNAME CHECK
async function checkUsername() {
    const username = document.getElementById('username').value.trim();
    if (!username) return;

    showLoading();
    try {
        const res = await fetch(`/Student/CheckUsername?username=${encodeURIComponent(username)}`);
        const data = await res.json();

        if (!data.available) {
            document.getElementById('userError').textContent = "Username is already taken.";
        } else {
            document.getElementById('userError').textContent = "";
        }
    } catch {
        document.getElementById('userError').textContent = "Network error checking username.";
    }
    hideLoading();
}

// REGISTRATION SUBMISSION
async function submitOnlineRegistration() {
    const formData = getFormData();
    formData.Mode = "Online";
    formData.PinCode = document.getElementById('pinCode').value.trim();
    formData.Username = document.getElementById('username').value.trim();
    formData.Password = document.getElementById('password').value;

    if (!formData.Username || !formData.Password) {
        document.getElementById('userError').textContent = "Username and password are required.";
        return;
    }

    showLoading();
    try {
        const root_code = getRootCodeFromUrl();
        const response = await fetch(`/Register/${root_code}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        });
        const result = await response.json();

        if (result.success) {
            Swal.fire({
                icon: 'success',
                title: 'Registration Successful!',
                text: "Your online registration is complete."
            }).then(() => {
                window.location.href = '/';
            });
        } else {
            showAlert(result.error || "Registration failed.", "danger");
        }
    } catch (ex) {
        showAlert("Network error. Please try again.", "danger");
    }
    hideLoading();
}

async function submitRegistration() {
    if (registrationMode === "Online") return;

    if (!document.getElementById('termsAccepted').checked) {
        showAlert('You must accept the terms and conditions.', 'danger');
        return;
    }

    const formData = getFormData();
    formData.Mode = registrationMode;

    showLoading();
    try {
        const root_code = getRootCodeFromUrl();
        const response = await fetch(`/Register/${root_code}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        });

        const text = await response.text();
        if (!response.ok) {
            showAlert(`Server error: ${response.status}`, 'danger');
            return;
        }

        let result;
        try {
            result = JSON.parse(text);
        } catch {
            showAlert('Invalid response from server', 'danger');
            return;
        }

        if (result.success) {
            Swal.fire({
                icon: 'success',
                title: 'Registration Successful!',
                text: result.message || "Welcome!"
            }).then(() => {
                window.location.href = result.redirectUrl || `/Register/${root_code}/Success`;
            });
        } else {
            showAlert(result.error || 'Registration failed.', 'danger');
        }
    } catch (error) {
        showAlert(`Network error: ${error.message}`, 'danger');
    } finally {
        hideLoading();
    }
}

// SUMMARY
function showRegistrationSummary() {
    const summary = document.getElementById('registrationSummary');
    const formData = getFormData();

    summary.innerHTML = `
        <div class="summary-grid">
            <div>
                <h5 class="summary-heading"><i class="fas fa-user me-2"></i>Personal Information</h5>
                <ul class="summary-list">
                    <li><strong>Name:</strong> ${formData.StudentName}</li>
                    <li><strong>Birthdate:</strong> ${formData.BirthDate}</li>
                    <li><strong>Student Phone:</strong> ${formData.StudentPhone}</li>
                    <li><strong>Father Phone:</strong> ${formData.StudentFatherPhone}</li>
                    <li><strong>Mother Phone:</strong> ${formData.StudentMotherPhone}</li>
                    <li><strong>Father Job:</strong> ${formData.StudentFatherJob}</li>
                    <li><strong>Mother Job:</strong> ${formData.StudentMotherJob}</li>
                    <li><strong>Gender:</strong> ${formData.Gender === true ? 'Male' : formData.Gender === false ? 'Female' : 'Not Specified'}</li>
                </ul>
            </div>
            <div>
                <h5 class="summary-heading"><i class="fas fa-school me-2"></i>Academic Information</h5>
                <ul class="summary-list">
                    <li><strong>Branch:</strong> ${document.getElementById('branchCode')?.selectedOptions[0]?.text || '—'}</li>
                    <li><strong>Academic Year:</strong> ${document.getElementById('yearCode')?.selectedOptions[0]?.text || '—'}</li>
                    <li><strong>Education Year:</strong> ${document.getElementById('eduYearCodeDisplay')?.value || '—'}</li>
                </ul>
            </div>
        </div>
        <h5 class="summary-heading mt-3"><i class="fas fa-books me-2"></i>Selected Subjects (${selectedSubjects.filter(s => s.teacherCode).length})</h5>
        <div class="subject-summary-list">
            ${selectedSubjects.filter(s => s.teacherCode).map(s => `
                <div class="subject-chip">
                    <div>
                        <strong>${s.subjectName}</strong><br>
                        <small>${s.teacherName}</small>
                        ${s.scheduleName ? `<br><small class="text-info">${s.scheduleName}</small>` : ''}
                    </div>
                </div>`).join('')}
        </div>
    `;
}

// FORM DATA
function getFormData() {
    const genderRadio = document.querySelector('input[name="Gender"]:checked');
    let genderValue = null;
    if (genderRadio) genderValue = genderRadio.value === 'true';

    let branchCodeValue, yearCodeValue, eduYearCodeValue;
    if (registrationMode === "Offline") {
        branchCodeValue = document.getElementById('branchCode')?.value;
        yearCodeValue = document.getElementById('yearCode')?.value;
        eduYearCodeValue = document.getElementById('eduYearCode')?.value;
    } else {
        branchCodeValue = null;
        yearCodeValue = document.getElementById('onlineYearCode')?.value;
        eduYearCodeValue = document.getElementById('onlineEduYearCode')?.value;
    }

    const subjectsArray = [];
    const teachersArray = [];
    const schedulesArray = [];

    selectedSubjects.forEach(s => {
        if (s.teacherCode && s.yearCode && s.eduYearCode) {
            subjectsArray.push(s.subjectCode);
            teachersArray.push(s.teacherCode);
            if (registrationMode === "Offline") {
                schedulesArray.push(s.scheduleCode ? s.scheduleCode : 0);
            }
        }
    });

    const rootCode = getRootCodeFromUrl();

    return {
        RootCode: rootCode ? parseInt(rootCode) : 0,
        StudentName: document.getElementById('studentName').value.trim(),
        StudentPhone: document.getElementById('studentPhone').value.trim(),
        StudentFatherPhone: document.getElementById('parentPhone').value.trim(),
        StudentMotherPhone: document.getElementById('motherPhone').value.trim(),
        StudentFatherJob: document.getElementById('fatherJob').value.trim(),
        StudentMotherJob: document.getElementById('motherJob').value.trim(),
        BirthDate: document.getElementById('birthDate').value,
        Gender: genderValue,
        Mode: registrationMode,
        BranchCode: branchCodeValue ? parseInt(branchCodeValue) : null,
        YearCode: yearCodeValue ? parseInt(yearCodeValue) : null,
        EduYearCode: eduYearCodeValue ? parseInt(eduYearCodeValue) : null,
        SelectedSubjects: subjectsArray,
        SelectedTeachers: teachersArray,
        SelectedSchedules: registrationMode === "Online" ? [] : schedulesArray,
        PinCode: document.getElementById('pinCode')?.value?.trim() || null,
        Username: document.getElementById('username')?.value?.trim() || null,
        Password: document.getElementById('password')?.value || null
    };
}

function getRootCodeFromUrl() {
    const match = window.location.pathname.match(/\/Register\/(\d+)/i);
    return match ? match[1] : null;
}

// UTILITIES
function showLoading() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) overlay.style.display = 'flex';
}

function hideLoading() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) overlay.style.display = 'none';
}

function showAlert(message, type = 'info') {
    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (!currentStepContent) return;

    currentStepContent.querySelectorAll('.alert.temp').forEach(a => a.remove());

    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} temp`;
    alertDiv.innerHTML = `
        <i class="fas ${type === 'success' ? 'fa-check-circle' : type === 'danger' ? 'fa-exclamation-triangle' : 'fa-info-circle'} me-2"></i>
        ${message}`;
    currentStepContent.prepend(alertDiv);

    currentStepContent.scrollIntoView({ behavior: 'smooth', block: 'start' });

    if (type !== 'danger') {
        setTimeout(() => alertDiv.remove(), 5000);
    }
}