// student-registration.js
// Complete implementation for multi-step student registration form
// Flow: Personal Info → Mode/Branch → Subjects/Teachers → Schedules → Confirmation

// ========== GLOBAL VARIABLES ==========
let currentStep = 1;
let selectedSubjects = []; // Array of subject-teacher-schedule combinations
let availableSubjects = [];
let availableTeachers = [];
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

    // Branch change - reload subjects when branch changes
    const branchSelect = document.getElementById('branchCode');
    if (branchSelect) {
        branchSelect.addEventListener('change', function () {
            resetSubjectsAndTeachers();
            if (this.value && document.getElementById('yearCode').value) {
                loadAvailableSubjects();
            }
        });
    }

    // Year change - reload subjects when year changes
    const yearSelect = document.getElementById('yearCode');
    if (yearSelect) {
        yearSelect.addEventListener('change', function () {
            resetSubjectsAndTeachers();
            if (this.value && document.getElementById('branchCode').value) {
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

    // PIN validate (online)
    const validatePinBtn = document.getElementById('validatePinBtn');
    if (validatePinBtn) {
        validatePinBtn.addEventListener('click', validatePin);
    }

    // Username uniqueness check (online)
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

    const branchSection = document.getElementById('branchSection');
    const onlinePinSection = document.getElementById('onlinePinSection');

    if (mode === "Online") {
        if (branchSection) branchSection.style.display = "none";
        if (onlinePinSection) onlinePinSection.style.display = "";

        // Hide year selection until PIN is validated
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

    // Step 2: Mode-specific validation
    if (currentStep === 2) {
        if (registrationMode === "Offline") {
            const branchCode = document.getElementById('branchCode').value;
            const yearCode = document.getElementById('yearCode').value;
            if (!branchCode || !yearCode) {
                showAlert('Please select both branch and academic year for offline registration.', 'danger');
                isValid = false;
            }
        } else if (registrationMode === "Online" && !pinValidated) {
            showAlert('Please validate your PIN code for online registration.', 'danger');
            isValid = false;
        }
    }

    // Step 3: Must select at least one subject with teacher
    if (currentStep === 3) {
        const completeSelections = selectedSubjects.filter(s => s.teacherCode);
        if (completeSelections.length === 0) {
            showAlert('Please select at least one subject with a teacher.', 'danger');
            isValid = false;
        }
    }

    // Step 4: For offline, must select schedules for all subjects
    if (currentStep === 4 && registrationMode === "Offline") {
        const missingSchedules = selectedSubjects.filter(s => s.teacherCode && !s.scheduleCode);
        if (missingSchedules.length > 0) {
            showAlert('Please select schedules for all subjects.', 'danger');
            isValid = false;
        }
    }

    // Step 4: For online, must have username and password
    if (currentStep === 4 && registrationMode === "Online") {
        const username = document.getElementById('username')?.value?.trim();
        const password = document.getElementById('password')?.value;
        if (!username || !password) {
            showAlert('Please enter both username and password.', 'danger');
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

// ========== SUBJECT/TEACHER/SCHEDULE LOADING ==========
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
        // For online mode, use the online year selection
        yearCode = document.getElementById('onlineYearCode')?.value || '';
        branchCode = '0'; // Use 0 or empty for online mode

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
        branchCode = '0'; // Use 0 for online mode
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
            <div class="text-center py-4 text-muted">
                <i class="fas fa-calendar-times fa-3x mb-3"></i>
                <p>No subjects with teachers selected.</p>
            </div>
        `;
        return;
    }

    container.innerHTML = '<div class="text-center py-2"><i class="fas fa-spinner fa-spin"></i> Loading schedules...</div>';

    let scheduleHtml = '';

    for (const subject of subjectsWithTeachers) {
        const schedules = await loadScheduleForSubjectTeacher(subject.subjectCode, subject.teacherCode);

        scheduleHtml += `
            <div class="mb-4">
                <h5><i class="fas fa-book me-2"></i>${subject.subjectName} - ${subject.teacherName}</h5>
                <div class="schedule-options">
        `;

        if (schedules.length > 0) {
            schedules.forEach(schedule => {
                const isSelected = subject.scheduleCode === schedule.scheduleCode;
                scheduleHtml += `
                    <div class="form-check mb-2">
                        <input class="form-check-input" type="radio" 
                               name="schedule_${subject.subjectCode}_${subject.teacherCode}"
                               value="${schedule.ScheduleCode}"
                               id="schedule_${subject.subjectCode}_${subject.teacherCode}_${schedule.ScheduleCode}"
                               ${isSelected ? 'checked' : ''}
                               onchange="selectSchedule(${subject.subjectCode}, ${subject.teacherCode}, ${schedule.scheduleCode}, '${schedule.scheduleName}', '${schedule.dayOfWeek}', '${schedule.startTime}', '${schedule.endTime}')"
                        <label class="form-check-label" for="schedule_${subject.subjectCode}_${subject.teacherCode}_${schedule.ScheduleCode}">
                            <strong>${schedule.scheduleName}</strong><br>
<small class="text-muted">${schedule.dayOfWeek} ${schedule.startTime}-${schedule.endTime} | Hall: ${schedule.hallName}</small>
                        </label>
                    </div>
                `;
            });
        } else {
            scheduleHtml += '<p class="text-muted">No schedules available for this teacher.</p>';
        }

        scheduleHtml += '</div></div>';
    }

    container.innerHTML = scheduleHtml;
}

// ========== SUBJECT/TEACHER SELECTION UI ==========

function renderSubjects() {
    const container = document.getElementById('availableSubjects');
    if (!container) return;

    if (availableSubjects.length === 0) {
        container.innerHTML = `
            <div class="text-center py-4 text-muted">
                <i class="fas fa-book fa-3x mb-3"></i>
                <p>No subjects available for the selected criteria.</p>
            </div>
        `;
        return;
    }

    // Load teachers when subjects are rendered
    loadTeachersForSubjects().then(() => {
        let subjectsHtml = '';

        availableSubjects.forEach(subject => {
            const isSelected = selectedSubjects.some(s => s.subjectCode === subject.subjectCode);
            const subjectTeachers = availableTeachers.filter(t => t.subjectCode === subject.subjectCode);

            subjectsHtml += `
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
                                onchange="toggleSubject(${subject.subjectCode}, '${subject.subjectName}')">
                        </div>
                    </div>
                    
                    <div class="teachers-list ${isSelected ? '' : 'd-none'}" id="teachers_${subject.subjectCode}">
                        <label class="form-label small">Select Teacher</label>
            `;

            if (subjectTeachers.length > 0) {
                subjectTeachers.forEach(teacher => {
                    const selectedSubject = selectedSubjects.find(s => s.subjectCode === subject.subjectCode);
                    const isTeacherSelected = selectedSubject && selectedSubject.teacherCode === teacher.teacherCode;

                    subjectsHtml += `
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
                                    <small class="text-muted">${teacher.teacherPhone || 'No phone listed'}</small>
                                </label>
                            </div>
                        </div>
                    `;
                });
            } else {
                subjectsHtml += '<p class="text-muted small">No teachers available for this subject.</p>';
            }

            subjectsHtml += '</div></div>';
        });

        container.innerHTML = subjectsHtml;
    });
}
    
// ========== SELECTION HANDLERS ==========

window.toggleSubject = function (subjectCode, subjectName) {
    const checkbox = document.getElementById(`subject_${subjectCode}`);
    const subjectCard = document.querySelector(`[data-subject-code="${subjectCode}"]`);
    const teachersDiv = document.getElementById(`teachers_${subjectCode}`);

    if (checkbox.checked) {
        // Add subject (without teacher initially)
        if (!selectedSubjects.some(s => s.subjectCode === subjectCode)) {
            selectedSubjects.push({
                subjectCode: subjectCode,
                subjectName: subjectName,
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
        // Remove subject
        selectedSubjects = selectedSubjects.filter(s => s.subjectCode !== subjectCode);
        subjectCard.classList.remove('selected');
        if (teachersDiv) teachersDiv.classList.add('d-none');

        // Clear teacher selections
        const teacherRadios = document.querySelectorAll(`input[name="teacher_${subjectCode}"]`);
        teacherRadios.forEach(radio => radio.checked = false);
    }

    updateSelectedSubjectsSummary();
};

window.selectTeacher = function (subjectCode, subjectName, teacherCode, teacherName, yearCode, eduYearCode) {
    const radio = document.getElementById(`teacher_${subjectCode}_${teacherCode}`);
    if (radio) radio.checked = true;

    // Find and update the subject in selectedSubjects
    const subjectIndex = selectedSubjects.findIndex(s => s.subjectCode === subjectCode);
    if (subjectIndex !== -1) {
        selectedSubjects[subjectIndex] = {
            ...selectedSubjects[subjectIndex],
            teacherCode: teacherCode,
            teacherName: teacherName,
            yearCode: yearCode,
            eduYearCode: eduYearCode,
            scheduleCode: null, // Reset schedule when teacher changes
            scheduleName: null
        };
    }

    // Update teacher option visual state
    document.querySelectorAll(`#teachers_${subjectCode} .teacher-option`).forEach(option => {
        option.classList.remove('selected');
    });

    // Find the clicked teacher option and select it
    const clickedOption = document.querySelector(`#teacher_${subjectCode}_${teacherCode}`).closest('.teacher-option');
    if (clickedOption) {
        clickedOption.classList.add('selected');
    }

    updateSelectedSubjectsSummary();
};

window.selectSchedule = function (subjectCode, teacherCode, scheduleCode, scheduleName, dayOfWeek, startTime, endTime) {
    const subjectIndex = selectedSubjects.findIndex(s => s.subjectCode === subjectCode && s.teacherCode === teacherCode);
    if (subjectIndex !== -1) {
        selectedSubjects[subjectIndex].scheduleCode = scheduleCode;
        selectedSubjects[subjectIndex].scheduleName = `${dayOfWeek} ${startTime}-${endTime}`;
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

    list.innerHTML = selectedSubjects.map(subject => `
        <div class="d-flex justify-content-between align-items-center p-2 border rounded mb-2">
            <div>
                <strong>${subject.subjectName}</strong>
                ${subject.teacherName ? `<br><small class="text-muted">Teacher: ${subject.teacherName}</small>` : ''}
                ${subject.scheduleName ? `<br><small class="text-info">Schedule: ${subject.scheduleName}</small>` : ''}
            </div>
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeSubject(${subject.subjectCode})">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `).join('');

    container.style.display = 'block';
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
            pinValidated = true;

            // Show year selection section for online registration
            document.getElementById('onlineYearSection').style.display = "";

            // Add event listener for year change in online mode
            const onlineYearSelect = document.getElementById('onlineYearCode');
            if (onlineYearSelect) {
                onlineYearSelect.addEventListener('change', function () {
                    resetSubjectsAndTeachers();
                });
            }

            showAlert("PIN validated successfully! Please select your academic year.", "success");
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
        document.getElementById('userError').textContent = "Network error checking username.";
    }
    hideLoading();
}


// ========== REGISTRATION SUBMISSION ==========

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
                text: "Your online registration is complete. You can now log in with your credentials."
            }).then(() => {
                // Redirect to login or home page
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

    console.log('Form data being submitted:', formData);

    const root_code = getRootCodeFromUrl();
    console.log('Root code:', root_code);

    showLoading();
    try {
        const response = await fetch(`/Register/${root_code}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        });

        console.log('Response status:', response.status);

        const responseText = await response.text();
        console.log('Raw response:', responseText);

        if (!response.ok) {
            showAlert(`Server error: ${response.status} - ${responseText}`, 'danger');
            return;
        }

        let result;
        try {
            result = JSON.parse(responseText);
        } catch (parseError) {
            console.error('JSON parse error:', parseError);
            showAlert('Invalid response from server', 'danger');
            return;
        }

        console.log('Parsed result:', result);

        if (result.success) {
            Swal.fire({
                icon: 'success',
                title: 'Registration Successful!',
                text: result.message || "Welcome! Your registration has been completed successfully."
            }).then(() => {
                window.location.href = result.redirectUrl || `/Register/${root_code}/Success`;
            });
        } else {
            // Show the specific error from the server
            const errorMessage = result.error || result.details || 'Registration failed. Please try again.';
            console.error('Server error:', errorMessage);
            showAlert(errorMessage, 'danger');
        }
    } catch (error) {
        console.error('Network/Fetch error:', error);
        showAlert(`Network error: ${error.message}`, 'danger');
    } finally {
        hideLoading();
    }
}
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
                    <li><strong>Branch:</strong> ${document.getElementById('branchCode')?.selectedOptions[0]?.text || 'Not Selected'}</li>
                    <li><strong>Academic Year:</strong> ${document.getElementById('yearCode')?.selectedOptions[0]?.text || 'Not Selected'}</li>
                    <li><strong>Education Year:</strong> ${document.getElementById('eduYearCodeDisplay')?.value || 'Not Selected'}</li>
                </ul>
            </div>
        </div>
        <h5 class="mt-4"><i class="fas fa-books me-2"></i>Selected Subjects (${selectedSubjects.filter(s => s.teacherCode).length})</h5>
        <div class="row">
            ${selectedSubjects.filter(s => s.teacherCode).map(subject => `
                <div class="col-md-12 mb-2">
                    <div class="border rounded p-2">
                        <strong>${subject.subjectName}</strong>
                        <br><small class="text-muted">Teacher: ${subject.teacherName}</small>
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
        genderValue = genderRadio.value === 'true' ? true : false;
    }

    let branchCodeValue, yearCodeValue, eduYearCodeValue;

    if (registrationMode === "Offline") {
        branchCodeValue = document.getElementById('branchCode')?.value;
        yearCodeValue = document.getElementById('yearCode')?.value;
        eduYearCodeValue = document.getElementById('eduYearCode')?.value;
    } else {
        // For online mode, use online year selection
        branchCodeValue = null; // No branch for online
        yearCodeValue = document.getElementById('onlineYearCode')?.value;
        eduYearCodeValue = document.getElementById('onlineEduYearCode')?.value;
    }

    // Extract arrays for the PublicRegistrationRequest format
    const subjectsArray = [];
    const teachersArray = [];
    const schedulesArray = [];

    selectedSubjects.forEach(s => {
        if (s.teacherCode && s.yearCode && s.eduYearCode) {
            subjectsArray.push(s.subjectCode);
            teachersArray.push(s.teacherCode);

            // ✅ Fix: Only add schedules for offline mode
            if (registrationMode === "Offline" && s.scheduleCode) {
                schedulesArray.push(s.scheduleCode);
            } else if (registrationMode === "Offline") {
                schedulesArray.push(0); // Fallback for offline mode
            }
            // For online mode, don't add anything to schedulesArray
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
        SelectedSchedules: registrationMode === "Online" ? [] : schedulesArray, // ✅ Empty array for online
        PinCode: document.getElementById('pinCode')?.value?.trim() || null,
        Username: document.getElementById('username')?.value?.trim() || null,
        Password: document.getElementById('password')?.value || null
    };
}

function getRootCodeFromUrl() {
    const match = window.location.pathname.match(/\/Register\/(\d+)/i);
    return match ? match[1] : null;
}

// ========== UTILITY FUNCTIONS ==========

function showLoading() {
    document.getElementById('loadingOverlay').style.display = 'flex';
}

function hideLoading() {
    document.getElementById('loadingOverlay').style.display = 'none';
}

function showAlert(message, type = 'info') {
    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (!currentStepContent) return;

    // Remove existing alerts
    const existingAlerts = currentStepContent.querySelectorAll('.alert');
    existingAlerts.forEach(alert => {
        if (!alert.classList.contains('alert-success') && !alert.classList.contains('alert-info')) {
            alert.remove();
        }
    });

    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
    alertDiv.innerHTML = `
        <i class="fas ${type === 'success' ? 'fa-check-circle' : type === 'danger' ? 'fa-exclamation-triangle' : 'fa-info-circle'} me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;

    currentStepContent.insertBefore(alertDiv, currentStepContent.firstChild);

    if (type === 'success') {
        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.remove();
            }
        }, 5000);
    }

    currentStepContent.scrollIntoView({ behavior: 'smooth', block: 'start' });
}