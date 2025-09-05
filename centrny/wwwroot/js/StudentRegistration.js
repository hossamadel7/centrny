// student-registration.js
// Handles all client-side logic for the multi-step student registration form, including online/offline flow, PIN, username, and new parent/job fields

// ========== GLOBAL VARIABLES ==========
let currentStep = 1;
let selectedSubjects = [];
let availableSubjects = [];
let availableTeaches = []; // Now stores Teach records, not just unique teachers
let availableSchedules = [];
let registrationMode = "Offline"; // Default
let pinValidated = false;

// ========== INITIALIZE ==========
document.addEventListener('DOMContentLoaded', function () {
    updateStepButtons();
    setupEventListeners();
    modeChangeHandler();
});

// ========== EVENT LISTENERS ==========
function setupEventListeners() {
    // Mode selection (online/offline)
    document.querySelectorAll('input[name="Mode"]').forEach(radio => {
        radio.addEventListener('change', modeChangeHandler);
    });

    // Branch/year change - reload subjects
    const branchSelect = document.getElementById('branchCode');
    if (branchSelect) {
        branchSelect.addEventListener('change', function () {
            loadAvailableSubjects();
        });
    }
    const yearSelect = document.getElementById('yearCode');
    if (yearSelect) {
        yearSelect.addEventListener('change', function () {
            loadAvailableSubjects();
        });
    }
    const yearCodeOnline = document.getElementById('yearCodeOnline');
    if (yearCodeOnline) {
        yearCodeOnline.addEventListener('change', function () {
            if (this.value) {
                loadAvailableSubjects();
            }
        });
    }

    // Form validation on input
    document.querySelectorAll('input[required], select[required]').forEach(input => {
        input.addEventListener('blur', validateField);
        input.addEventListener('input', clearValidation);
    });

    // Step navigation buttons
    document.getElementById('prevBtn').addEventListener('click', function () {
        changeStep(-1);
    });
    document.getElementById('nextBtn').addEventListener('click', function () {
        changeStep(1);
    });
    document.getElementById('submitBtn').addEventListener('click', function () {
        submitRegistration();
    });

    // PIN validate (online: step 4)
    const validatePinBtn = document.getElementById('validatePinBtn');
    if (validatePinBtn) {
        validatePinBtn.addEventListener('click', validatePin);
    }

    // Username uniqueness check (online: step 5)
    const usernameInput = document.getElementById('username');
    if (usernameInput) {
        usernameInput.addEventListener('blur', checkUsername);
    }

    // Complete online registration
    const completeOnlineBtn = document.getElementById('completeOnlineBtn');
    if (completeOnlineBtn) {
        completeOnlineBtn.addEventListener('click', submitOnlineRegistration);
    }
}

// ========== MODE HANDLING ==========
function modeChangeHandler() {
    const mode = document.querySelector('input[name="Mode"]:checked').value;
    registrationMode = mode;

    if (mode === "Online") {
        document.getElementById('branchRow').style.display = "none";
        document.getElementById('yearOnlyRow').style.display = "";
        document.getElementById('yearCodeOnline').required = true;
        document.getElementById('yearCode').required = false;
    } else {
        document.getElementById('branchRow').style.display = "";
        document.getElementById('yearOnlyRow').style.display = "none";
        document.getElementById('yearCode').required = true;
        document.getElementById('yearCodeOnline').required = false;
    }
    resetSubjectsAndSchedules();
}

function resetSubjectsAndSchedules() {
    selectedSubjects = [];
    availableSubjects = [];
    availableTeaches = [];
    availableSchedules = [];
    const subjectsDiv = document.getElementById('availableSubjects');
    if (subjectsDiv) subjectsDiv.innerHTML = '';
    const schedulesDiv = document.getElementById('scheduleSelection');
    if (schedulesDiv) schedulesDiv.innerHTML = '';
    document.getElementById('selectedSubjectsSummary').style.display = 'none';
}

// ========== STEP NAVIGATION ==========
function changeStep(direction) {
    if (direction === 1 && !validateCurrentStep()) return;

    // Hide current step
    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (currentStepContent) currentStepContent.classList.remove('active');
    const currentStepIndicator = document.getElementById(`step${currentStep}`);
    if (currentStepIndicator) currentStepIndicator.classList.remove('active');

    // Update step number
    currentStep += direction;
    if (currentStep < 1) currentStep = 1;
    if (currentStep > 5) currentStep = 5;

    // Show new step
    const newStepContent = document.getElementById(`step${currentStep}Content`);
    if (newStepContent) newStepContent.classList.add('active');
    const newStepIndicator = document.getElementById(`step${currentStep}`);
    if (newStepIndicator) newStepIndicator.classList.add('active');

    // Step-specific logic
    if (currentStep === 3) {
        loadAvailableSubjects();
    } else if (currentStep === 4) {
        if (registrationMode === "Offline") {
            document.getElementById('offlineScheduleSection').style.display = "";
            document.getElementById('onlinePinSection').style.display = "none";
            loadAvailableSchedules();
        } else {
            document.getElementById('offlineScheduleSection').style.display = "none";
            document.getElementById('onlinePinSection').style.display = "";
            document.getElementById('onlineUserSection').style.display = "none";
            pinValidated = false;
        }
    } else if (currentStep === 5) {
        if (registrationMode === "Offline") {
            document.getElementById('offlineSummarySection').style.display = "";
            document.getElementById('onlineUserSection').style.display = "none";
            showRegistrationSummary();
        } else {
            document.getElementById('offlineSummarySection').style.display = "none";
            document.getElementById('onlineUserSection').style.display = pinValidated ? "" : "none";
        }
    }
    updateStepButtons();
}

function updateStepButtons() {
    document.getElementById('prevBtn').style.display = currentStep > 1 ? 'block' : 'none';
    document.getElementById('nextBtn').style.display = (currentStep < 5 ? 'block' : 'none');
    document.getElementById('submitBtn').style.display = (currentStep === 5 && registrationMode === "Offline") ? 'block' : 'none';
}

// ========== VALIDATION ==========
function validateCurrentStep() {
    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (!currentStepContent) return false;
    const requiredFields = currentStepContent.querySelectorAll('input[required], select[required]');
    let isValid = true;
    requiredFields.forEach(field => {
        if (!validateField({ target: field })) isValid = false;
    });
    if (currentStep === 3 && selectedSubjects.length === 0) {
        showAlert('Please select at least one subject.', 'danger');
        isValid = false;
    }
    if (currentStep === 4 && registrationMode === "Offline") {
        const missingSchedules = selectedSubjects.filter(s => !s.scheduleCode);
        if (missingSchedules.length > 0) {
            showAlert('Please select schedules for all subjects.', 'danger');
            isValid = false;
        }
    }
    // Online, step 4: PIN must be validated before going to username/password
    if (currentStep === 4 && registrationMode === "Online" && !pinValidated) {
        const pinSection = document.getElementById('onlinePinSection');
        if (pinSection && pinSection.style.display !== "none") {
            showAlert('You must validate a PIN to continue.', 'danger');
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
        message = 'Required field.';
    }
    if (value && field.type === 'tel') {
        if (!/^[\+]?[0-9\s\-\(\)]{8,}$/.test(value)) {
            isValid = false;
            message = 'Enter a valid phone number.';
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

// ========== PIN VALIDATION ==========
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
            document.getElementById('onlinePinSection').style.display = "none";
            document.getElementById('onlineUserSection').style.display = "";
            pinValidated = true;
        } else {
            setPinError(data.error || "Invalid PIN.");
            pinValidated = false;
        }
    } catch {
        setPinError("Network error.");
        pinValidated = false;
    }
    hideLoading();
}
function setPinError(msg) {
    document.getElementById('pinError').textContent = msg;
}

// ========== USERNAME CHECK ==========
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
        document.getElementById('userError').textContent = "Network error.";
    }
    hideLoading();
}

async function submitOnlineRegistration() {
    const formData = getFormData();
    formData.Mode = "Online";
    formData.PinCode = document.getElementById('pinCode').value.trim();
    formData.Username = document.getElementById('username').value.trim();
    formData.Password = document.getElementById('password').value;
    formData.EduYearCode = document.getElementById('eduYearCode').value ? parseInt(document.getElementById('eduYearCode').value) : null;
    if (!formData.Username || !formData.Password) {
        document.getElementById('userError').textContent = "Username and password required.";
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
                title: 'Congratulations!',
                text: "Your registration was successful. Welcome! You can now log in with your chosen username and password."
            });
            // Optionally clear form or stay on page
        } else {
            showAlert(result.error || "Registration failed.", "danger");
        }
    } catch (ex) {
        showAlert("Network error.", "danger");
    }
    hideLoading();
}

// ========== SUBJECT/SCHEDULE LOADING ==========
async function loadAvailableSubjects() {
    let url;
    let yearCode;
    if (registrationMode === "Offline") {
        const branchCode = document.getElementById('branchCode').value;
        yearCode = document.getElementById('yearCode').value;
        // ALWAYS send the request, even if branchCode is empty!
        url = `/Student/GetAvailableSubjects?branchCode=${branchCode}&yearCode=${yearCode || ''}`;
    } else {
        yearCode = document.getElementById('yearCodeOnline').value;
        url = `/Student/GetAvailableSubjects?branchCode=&yearCode=${yearCode || ''}`;
    }
    showLoading();
    try {
        const response = await fetch(url);
        const subjects = await response.json();
        if (subjects.error) {
            showAlert(subjects.error, 'danger');
            return;
        }
        availableSubjects = subjects;
        await loadAvailableTeaches();
        renderSubjects();
    } catch {
        showAlert('Failed to load subjects.', 'danger');
    } finally {
        hideLoading();
    }
}

// Updated: load all Teach records for the selected subject(s), year, root, and eduyear
async function loadAvailableTeaches() {
    let branchCode = "";
    if (registrationMode === "Offline") {
        branchCode = document.getElementById('branchCode').value;
    }
    const yearCode = (registrationMode === "Online")
        ? document.getElementById('yearCodeOnline').value
        : document.getElementById('yearCode').value;
    const eduYearCode = document.getElementById('eduYearCode')?.value;
    const subjectCodes = availableSubjects.map(s => s.subjectCode).join(',');
    if (!subjectCodes) return;
    try {
        const url = `/Student/GetAvailableTeachers?subjectCodes=${subjectCodes}&branchCode=${branchCode}&yearCode=${yearCode || ''}&eduYearCode=${eduYearCode || ''}`;
        const response = await fetch(url);
        const teaches = await response.json();
        if (teaches.error) {
            showAlert(teaches.error, 'danger');
            availableTeaches = [];
            return;
        }
        availableTeaches = teaches;
    } catch {
        showAlert('Failed to load teachers.', 'danger');
        availableTeaches = [];
    }
}

async function loadAvailableSchedules() {
    if (registrationMode !== "Offline") return;
    const branchCode = document.getElementById('branchCode').value;
    const yearCode = document.getElementById('yearCode').value;
    if (selectedSubjects.length === 0) {
        document.getElementById('scheduleSelection').innerHTML = `
            <div class="text-center py-4 text-muted">
                <i class="fas fa-calendar-times fa-3x mb-3"></i>
                <p>No subjects selected.</p>
            </div>
        `;
        return;
    }
    showLoading();
    try {
        const subjectCodes = selectedSubjects.map(s => s.subjectCode).join(',');
        const teacherCodes = selectedSubjects.map(s => s.teacherCode).join(',');
        const url = `/Student/GetAvailableSchedules?subjectCodes=${subjectCodes}&teacherCodes=${teacherCodes}&branchCode=${branchCode}&yearCode=${yearCode || ''}`;
        const response = await fetch(url);
        const schedules = await response.json();

        if (!Array.isArray(schedules)) {
            showAlert('Failed to load schedules.', 'danger');
            return;
        }
        availableSchedules = schedules;
        renderScheduleSelection();
    } catch (e) {
        showAlert(e.error, 'danger');
        console.error(e);
    } finally {
        hideLoading();
    }
}

// ========== SUBJECT/TUTOR SELECTION UI ==========
function renderSubjects() {
    const container = document.getElementById('availableSubjects');
    if (!container) return;
    if (availableSubjects.length === 0) {
        container.innerHTML = `
            <div class="text-center py-4 text-muted">
                <i class="fas fa-book fa-3x mb-3"></i>
                <p>No subjects available.</p>
            </div>
        `;
        return;
    }
    container.innerHTML = availableSubjects.map(subject => {
        // Display all Teach records for this subject, year, root, eduyear
        const teachesForSubject = availableTeaches.filter(t => t.subjectCode === subject.subjectCode);
        const isSelected = selectedSubjects.some(s => s.subjectCode === subject.subjectCode);
        return `
            <div class="subject-selection ${isSelected ? 'selected' : ''}" data-subject-code="${subject.subjectCode}">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h5 class="mb-0">
                        <i class="fas fa-book me-2"></i>
                        ${subject.subjectName}
                    </h5>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox"
                            id="subject_${subject.subjectCode}"
                            ${isSelected ? 'checked' : ''}
                            onclick="toggleSubject(${subject.subjectCode})">
                    </div>
                </div>
              ${teachesForSubject.length > 0 ? `
 <div class="teachers-list ${isSelected ? '' : 'd-none'}" id="teachers_${subject.subjectCode}">
     <label class="form-label small">Select Teacher/Year/EduYear</label>
     ${teachesForSubject.map(teach => `
         <div class="teacher-option" onclick="selectTeach('${subject.subjectCode}', '${teach.teacherCode}', '${teach.yearCode}', '${teach.eduYearCode}')">
             <div class="form-check">
                 <input class="form-check-input" type="radio"
                     name="teach_${subject.subjectCode}"
                     value="${teach.teacherCode}_${teach.yearCode}_${teach.eduYearCode}"
                     id="teach_${subject.subjectCode}_${teach.teacherCode}_${teach.yearCode}_${teach.eduYearCode}">
                 <label class="form-check-label">
                     <strong>${teach.teacherName}</strong>
                    
                 </label>
             </div>
         </div>
     `).join('')}
 </div>
` : `
    <div class="text-muted small">
        <i class="fas fa-info-circle me-1"></i>
        No teachers available.
    </div>
`}
            </div>
        `;
    }).join('');
}

window.toggleSubject = function (subjectCode) {
    const checkbox = document.getElementById(`subject_${subjectCode}`);
    const subjectCard = document.querySelector(`[data-subject-code="${subjectCode}"]`);
    const teachersDiv = document.getElementById(`teachers_${subjectCode}`);
    if (checkbox.checked) {
        const subject = availableSubjects.find(s => s.subjectCode === subjectCode);
        const selection = {
            subjectCode: subjectCode,
            subjectName: subject.subjectName,
            teacherCode: null,
            teacherName: null,
            yearCode: null,
            eduYearCode: null,
            scheduleCode: null,
            scheduleName: null,
            isOnline: (registrationMode === "Online"),
            studentFee: null
        };
        selectedSubjects.push(selection);
        subjectCard.classList.add('selected');
        if (teachersDiv) teachersDiv.classList.remove('d-none');
    } else {
        selectedSubjects = selectedSubjects.filter(s => s.subjectCode !== subjectCode);
        subjectCard.classList.remove('selected');
        if (teachersDiv) teachersDiv.classList.add('d-none');
        const teachRadios = document.querySelectorAll(`input[name="teach_${subjectCode}"]`);
        teachRadios.forEach(radio => radio.checked = false);
    }
    updateSelectedSubjectsSummary();
};

// When user picks a Teach record (teacher+year+eduyear) for a subject
window.selectTeach = function (subjectCode, teacherCode, yearCode, eduYearCode) {
    const radio = document.getElementById(`teach_${subjectCode}_${teacherCode}_${yearCode}_${eduYearCode}`);
    if (radio) radio.checked = true;
    const selection = selectedSubjects.find(s => s.subjectCode == subjectCode);
    if (selection) {
        const teach = availableTeaches.find(t =>
            t.SubjectCode == subjectCode &&
            t.TeacherCode == teacherCode &&
            t.YearCode == yearCode &&
            t.EduYearCode == eduYearCode
        );
        selection.teacherCode = teacherCode;
        selection.teacherName = teach?.TeacherName || '';
        selection.yearCode = yearCode;
        selection.eduYearCode = eduYearCode;
    }
    updateSelectedSubjectsSummary();
};

window.selectSchedule = function (subjectCode, teacherCode, scheduleCode) {
    const radio = document.getElementById(`schedule_${subjectCode}_${teacherCode}_${scheduleCode}`);
    radio.checked = true;
    const subject = selectedSubjects.find(s => s.subjectCode == subjectCode && s.teacherCode == teacherCode);
    if (subject) {
        const schedule = availableSchedules.find(s => s.scheduleCode === scheduleCode);
        subject.scheduleCode = scheduleCode;
        subject.scheduleName = `${schedule.dayName} ${schedule.startTime}-${schedule.endTime}`;
    }
};

function updateSelectedSubjectsSummary() {
    const container = document.getElementById('selectedSubjectsSummary');
    const list = document.getElementById('selectedSubjectsList');
    if (selectedSubjects.length === 0) {
        container.style.display = 'none';
        return;
    }
    list.innerHTML = selectedSubjects.map(subject => `
        <div class="d-flex justify-content-between align-items-center p-2 border rounded mb-2">
            <div>
                <strong>${subject.subjectName}</strong>
                ${subject.teacherName ? `<br><small class="text-muted">Teacher: ${subject.teacherName}</small>` : ''}
               
            </div>
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeSubject(${subject.subjectCode})">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `).join('');
    container.style.display = 'block';
}

window.removeSubject = function (subjectCode) {
    const checkbox = document.getElementById(`subject_${subjectCode}`);
    checkbox.checked = false;
    window.toggleSubject(subjectCode);
};

// ========== SUMMARY ==========
function showRegistrationSummary() {
    const summary = document.getElementById('registrationSummary');
    const formData = getFormData();
    summary.innerHTML = `
        <div class="row">
            <div class="col-md-6">
                <h5><i class="fas fa-user me-2"></i>Personal Information</h5>
                <ul class="list-unstyled">
                    <li><strong>Full Name:</strong> ${formData.StudentName}</li>
                    <li><strong>Birthdate:</strong> ${formData.BirthDate}</li>
                    <li><strong>Student Phone:</strong> ${formData.StudentPhone}</li>
                    <li><strong>Father Phone:</strong> ${formData.StudentFatherPhone}</li>
                    <li><strong>Mother Phone:</strong> ${formData.StudentMotherPhone}</li>
                    <li><strong>Father Job:</strong> ${formData.StudentFatherJob}</li>
                    <li><strong>Mother Job:</strong> ${formData.StudentMotherJob}</li>
                    <li><strong>Gender:</strong> ${formData.Gender === true ? 'Male' : formData.Gender === false ? 'Female' : 'Not Specified'}</li>
                </ul>
            </div>
            <div class="col-md-6">
                <h5><i class="fas fa-school me-2"></i>Academic Information</h5>
                <ul class="list-unstyled">
                    <li><strong>Branch:</strong> ${document.getElementById('branchCode')?.selectedOptions[0]?.text || 'Online'}</li>
                    <li><strong>Academic Year:</strong> ${document.getElementById(registrationMode === "Online" ? 'yearCodeOnline' : 'yearCode')?.selectedOptions[0]?.text || 'Not Selected'}</li>
                    <li><strong>Edu Year:</strong> ${document.getElementById('eduYearCodeDisplay')?.value || 'Not Selected'}</li>
                </ul>
            </div>
        </div>
        <h5><i class="fas fa-books me-2"></i>Selected Subjects (${selectedSubjects.length})</h5>
        <div class="row">
            ${selectedSubjects.map(subject => `
                <div class="col-md-12 mb-2">
                    <div class="border rounded p-2">
                        <strong>${subject.subjectName}</strong>
                        ${subject.teacherName ? `<br><small class="text-muted">Teacher: ${subject.teacherName}</small>` : ''}
                        ${subject.yearCode ? `<br><small class="text-muted">Year: ${subject.yearCode}</small>` : ''}
                        ${subject.eduYearCode ? `<br><small class="text-muted">EduYear: ${subject.eduYearCode}</small>` : ''}
                        ${subject.scheduleName ? `<br><small class="text-info">Schedule: ${subject.scheduleName}</small>` : ''}
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}

function getFormData() {
    const genderRadio = document.querySelector('input[name="Gender"]:checked');
    let genderValue = null;
    if (genderRadio) {
        if (genderRadio.value === 'true') genderValue = true;
        else if (genderRadio.value === 'false') genderValue = false;
    }
    let branchCodeValue = document.getElementById('branchCode')?.value;
    let yearCodeValue = (registrationMode === "Online") ? document.getElementById('yearCodeOnline').value : document.getElementById('yearCode').value;
    let eduYearCodeValue = document.getElementById('eduYearCode')?.value;
    // For each subject, collect the selected teacher, year, and eduyear (from Teach)
    let selectedSubjectsData = selectedSubjects
        .filter(s => s.teacherCode && s.yearCode && s.eduYearCode && (registrationMode === "Online" || s.scheduleCode))
        .map(s => ({
            SubjectCode: s.subjectCode,
            TeacherCode: s.teacherCode,
            YearCode: s.yearCode,
            EduYearCode: s.eduYearCode,
            ScheduleCode: (registrationMode === "Online" ? null : s.scheduleCode)
        }));
    return {
        StudentName: document.getElementById('studentName').value.trim(),
        StudentPhone: document.getElementById('studentPhone').value.trim(),
        StudentFatherPhone: document.getElementById('parentPhone').value.trim(),
        StudentMotherPhone: document.getElementById('motherPhone').value.trim(),
        StudentFatherJob: document.getElementById('fatherJob').value.trim(),
        StudentMotherJob: document.getElementById('motherJob').value.trim(),
        BirthDate: document.getElementById('birthDate').value,
        Gender: genderValue,
        BranchCode: branchCodeValue ? parseInt(branchCodeValue) : null,
        YearCode: yearCodeValue ? parseInt(yearCodeValue) : null,
        EduYearCode: eduYearCodeValue ? parseInt(eduYearCodeValue) : null,
        SelectedSubjects: selectedSubjectsData
    };
}

function getRootCodeFromUrl() {
    const match = window.location.pathname.match(/\/Register\/(\d+)/i);
    return match ? match[1] : null;
}

async function submitRegistration() {
    if (registrationMode === "Online") return;
    if (!document.getElementById('termsAccepted').checked) {
        showAlert('You must accept the terms.', 'danger');
        return;
    }
    const formData = getFormData();
    formData.Mode = registrationMode;
    formData.SelectedSchedules = selectedSubjects.map(s => s.scheduleCode);
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
            showAlert(result.message, 'success');
            setTimeout(() => {
                window.location.href = result.redirectUrl;
            }, 2000);
        } else {
            showAlert(result.error || 'Registration failed.', 'danger');
        }
    } catch {
        showAlert('Failed to connect. Try again later.', 'danger');
    } finally {
        hideLoading();
    }
}

function showLoading() {
    document.getElementById('loadingOverlay').style.display = 'flex';
}
function hideLoading() {
    document.getElementById('loadingOverlay').style.display = 'none';
}
function showAlert(message, type = 'info') {
    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (!currentStepContent) return;
    const existingAlerts = currentStepContent.querySelectorAll('.alert');
    existingAlerts.forEach(alert => {
        if (!alert.classList.contains('alert-success') && !alert.classList.contains('alert-info')) {
            alert.remove();
        }
    });
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type}`;
    alertDiv.innerHTML = `
        <i class="fas ${type === 'success' ? 'fa-check-circle' : type === 'danger' ? 'fa-exclamation-triangle' : 'fa-info-circle'} me-2"></i>
        ${message}
    `;
    currentStepContent.insertBefore(alertDiv, currentStepContent.firstChild);
    if (type === 'success') {
        setTimeout(() => {
            alertDiv.remove();
        }, 5000);
    }
    currentStepContent.scrollIntoView({ behavior: 'smooth', block: 'start' });
}