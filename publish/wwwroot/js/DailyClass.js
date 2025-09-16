// LOCALIZED CALENDAR DAY VIEW JAVASCRIPT with SweetAlert2 integration
// Fixes:
// - Avoids collision with global "L" (e.g., Leaflet) by renaming the localization helper to LC()
// - Guards locale usage in toLocaleDateString
// - Adds safe modal hide

let currentDate = new Date();
let dailyClasses = [];
let dailyReservations = [];
let isEditMode = false;
let editingClassId = null;
let selectedDateStr = ""; // Always reflects the selected date as "yyyy-MM-dd"

const userContext = {
    currentUserRootCode: null,
    userRootName: "",
    isCenter: false,
    hasError: false,
    groupBranchCode: null,
    teacherCode: null // for teacher user scenario
};

// --- SweetAlert2 Alert Helpers ---
function showSuccessAlert(message) {
    return Swal.fire({
        icon: 'success',
        title: 'Success',
        text: message,
        confirmButtonColor: '#3085d6'
    });
}
function showErrorAlert(message) {
    return Swal.fire({
        icon: 'error',
        title: 'Error',
        text: message,
        confirmButtonColor: '#d33'
    });
}
function showInfoAlert(message) {
    return Swal.fire({
        icon: 'info',
        title: 'Info',
        text: message
    });
}
function showConfirmAlert(message) {
    return Swal.fire({
        icon: 'warning',
        title: 'Are you sure?',
        text: message,
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Yes',
        cancelButtonText: 'No'
    }).then((result) => result.isConfirmed);
}

// --- Localization ---
// Use LC instead of L to avoid conflicts with other libs (e.g., Leaflet uses window.L)
function LC(key, fallback = "") {
    return (window.DailyClassResx && window.DailyClassResx[key]) ? window.DailyClassResx[key] : fallback;
}
function getPreferredLocale() {
    return LC("Locale") || document.documentElement.lang || navigator.language || 'en-US';
}

function parseLocalDateFromInput(input) {
    const [year, month, day] = input.split('-').map(Number);
    return new Date(year, month - 1, day);
}

document.addEventListener('DOMContentLoaded', function () {
    const datePickerElement = document.getElementById('datePicker');
    if (datePickerElement && datePickerElement.value) {
        currentDate = parseLocalDateFromInput(datePickerElement.value);
        selectedDateStr = datePickerElement.value;
    } else {
        currentDate = new Date();
        selectedDateStr = getDateStringFromDate(currentDate);
    }
    initializeUserContext();
    setupEventListeners();
    updateDateDisplay();

    if (!userContext.hasError) {
        loadDayContent();
        resetModalForCreate();
    }
});

function getDateStringFromDate(dateObj) {
    // Returns "yyyy-MM-dd"
    const year = dateObj.getFullYear();
    const month = String(dateObj.getMonth() + 1).padStart(2, '0');
    const day = String(dateObj.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

function initializeUserContext() {
    const rootCodeElement = document.getElementById('rootCode');
    const isCenterElement = document.getElementById('isCenter');
    if (rootCodeElement) userContext.currentUserRootCode = rootCodeElement.value;
    if (isCenterElement) {
        userContext.isCenter = String(isCenterElement.value).toLowerCase() === 'true';
    } else {
        userContext.isCenter = false;
        console.warn('isCenterElement not found in DOM!');
    }
    userContext.userRootName = document.getElementById('formSelectedDate')?.dataset?.username || '';
    userContext.hasError = !!document.querySelector('.error-banner');
}

function setupEventListeners() {
    document.getElementById('prevDayBtn')?.addEventListener('click', () => {
        navigateDate(-1);
    });
    document.getElementById('nextDayBtn')?.addEventListener('click', () => {
        navigateDate(1);
    });
    document.getElementById('datePicker')?.addEventListener('change', onDatePickerChange);

    document.getElementById('addClassModal')?.addEventListener('hidden.bs.modal', resetModalForCreate);
    document.getElementById('saveClassBtn')?.addEventListener('click', saveClass);

    // Lesson dropdown: reload on teacher/year/subject change
    document.getElementById('teacherCode')?.addEventListener('change', loadLessonsForDropdown);
    document.getElementById('yearCode')?.addEventListener('change', loadLessonsForDropdown);
    document.getElementById('subjectCode')?.addEventListener('change', loadLessonsForDropdown);

    // --- CENTER USER FLOW (custom cycle as requested) ---
    document.getElementById('teacherCode')?.addEventListener('change', function () {
        if (!userContext.isCenter) return;
        disable('hallCode');
        populateSelectAsync('hallCode', []);
        disable('subjectCode');
        populateSelectAsync('subjectCode', []);
        populateSelectAsync('yearCode', []);
        const teacherId = this.value;
        const eduYearCode = document.getElementById('eduYearCode').value;
        if (teacherId && userContext.groupBranchCode && eduYearCode) {
            enable('hallCode');
            loadHallsForBranch(userContext.groupBranchCode);
            enable('subjectCode');
            fetchSubjectsForTeacherEduYearBranch(teacherId, eduYearCode, userContext.groupBranchCode, true);
        }
    });

    document.getElementById('subjectCode')?.addEventListener('change', function () {
        if (userContext.isCenter) {
            populateSelectAsync('yearCode', []);
            const subjectId = this.value;
            const teacherId = document.getElementById('teacherCode').value;
            const eduYearCode = document.getElementById('eduYearCode').value;
            const branchCode = userContext.groupBranchCode;
            if (subjectId && teacherId && branchCode && eduYearCode) {
                loadYearsForTeach(branchCode, teacherId, subjectId, eduYearCode, true);
            } else {
                populateSelectAsync('yearCode', []);
            }
        } else {
            const eduYearCode = document.getElementById('eduYearCode').value;
            loadYearsByEduYear(eduYearCode);
        }
    });

    document.getElementById('centerCode')?.addEventListener('change', function () {
        let centerId = this.value;
        if (centerId) {
            enable('branchCode');
            loadBranchesByCenter(centerId);
            populateSelectAsync('yearCode', []);
        } else {
            populateSelectAsync('branchCode', []);
            disable('branchCode');
            populateSelectAsync('yearCode', []);
        }
        disable('subjectCode');
        populateSelectAsync('subjectCode', []);
    });

    document.getElementById('branchCode')?.addEventListener('change', function () {
        disable('subjectCode');
        populateSelectAsync('subjectCode', []);
        populateSelectAsync('yearCode', []);
        let branchId = this.value;
        let centerId = document.getElementById('centerCode').value;
        let eduYearCode = document.getElementById('eduYearCode').value;
        if (branchId && centerId && eduYearCode) {
            enable('subjectCode');
            fetchSubjectsForTeacherEduYearBranch(userContext.teacherCode, eduYearCode, branchId);
        }
        if (!userContext.isCenter) {
            const eduYearCode2 = document.getElementById('eduYearCode').value;
            loadYearsByEduYear(eduYearCode2);
        }
    });

    document.getElementById('eduYearCode')?.addEventListener('change', function () {
        populateSelectAsync('teacherCode', []);
        disable('hallCode');
        populateSelectAsync('hallCode', []);
        disable('subjectCode');
        populateSelectAsync('subjectCode', []);
        populateSelectAsync('yearCode', []);
        if (userContext.isCenter) {
            if (userContext.groupBranchCode) {
                loadStaffTeachersForBranch(userContext.groupBranchCode);
            }
        } else {
            let eduYearCode = this.value;
            let branchId = document.getElementById('branchCode').value;
            let centerId = document.getElementById('centerCode').value;
            if (eduYearCode && branchId && centerId) {
                enable('subjectCode');
                fetchSubjectsForTeacherEduYearBranch(userContext.teacherCode, eduYearCode, branchId);
            }
            loadYearsByEduYear(eduYearCode);
        }
    });

    document.getElementById('branchCode')?.addEventListener('change', function () {
        if (!userContext.isCenter) {
            loadHallsForBranch(this.value);
            loadTeachersForBranch(this.value);
        }
    });

    document.getElementById('editClassBtn')?.addEventListener('click', function () {
        if (editingClassId) {
            showEditClassModal(editingClassId);
        }
    });
    document.getElementById('deleteClassBtn')?.addEventListener('click', function () {
        if (editingClassId) {
            deleteClass(editingClassId);
        }
    });
}

// --- Dropdown Enable/Disable ---
function enable(id) { document.getElementById(id).disabled = false; }
function disable(id) {
    if (id !== 'yearCode') {
        const el = document.getElementById(id);
        if (el) el.disabled = true;
    }
}
// --- AJAX helper functions and dropdown population ---
async function loadStaffTeachersForBranch(branchCode) {
    try {
        const res = await fetch(`/DailyClass/GetTeachersForBranch?branchCode=${branchCode}`);
        const data = await res.json();
        let teachers = Array.isArray(data.teachers) ? data.teachers : [];
        populateSelectAsync('teacherCode', teachers);
        enable('teacherCode');
    } catch {
        populateSelectAsync('teacherCode', []);
        enable('teacherCode');
    }
}

async function fetchSubjectsForTeacherEduYearBranch(teacherCode, eduYearCode, branchCode, isCenter = false) {
    if (!teacherCode || !eduYearCode || !branchCode) {
        populateSelectAsync('subjectCode', []);
        disable('subjectCode');
        return;
    }
    try {
        const res = await fetch(`/DailyClass/GetSubjectsForTeacherRootEduYearBranch?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}&branchCode=${branchCode}`);
        const data = await res.json();
        if (Array.isArray(data) && data.length > 0) {
            populateSelectAsync('subjectCode', data);
            enable('subjectCode');
        } else {
            populateSelectAsync('subjectCode', []);
            disable('subjectCode');
            await showErrorAlert("No subjects available for the selected options");
        }
    } catch {
        populateSelectAsync('subjectCode', []);
        disable('subjectCode');
        await showErrorAlert("No subjects available for the selected options");
    }
}

async function loadYearsForTeach(branchCode, teacherCode, subjectCode, eduYearCode = null, isCenter = false) {
    if (!branchCode || !teacherCode || !subjectCode) {
        populateSelectAsync('yearCode', []);
        return;
    }
    let url = `/DailyClass/GetYearsForTeach?branchCode=${branchCode}&teacherCode=${teacherCode}&subjectCode=${subjectCode}`;
    if (eduYearCode) url += `&eduYearCode=${eduYearCode}`;
    try {
        const res = await fetch(url);
        const data = await res.json();
        let years = Array.isArray(data.years) ? data.years : [];
        populateSelectAsync('yearCode', years);
        enable('yearCode');
    } catch {
        populateSelectAsync('yearCode', []);
    }
}

async function loadEduYearsForRoot() {
    try {
        const res = await fetch('/DailyClass/GetEduYearsForRoot');
        const data = await res.json();
        const eduYears = data.eduYears || [];
        const select = document.getElementById('eduYearCode');
        populateSelectAsync('eduYearCode', eduYears);
        if (eduYears.length === 1) {
            select.value = eduYears[0].value;
            select.disabled = true;
            select.dispatchEvent(new Event('change'));
        } else {
            select.disabled = false;
            if (eduYears.length > 0) {
                select.value = eduYears[0].value;
                select.dispatchEvent(new Event('change'));
            }
        }
    } catch {
        populateSelectAsync('eduYearCode', []);
        document.getElementById('eduYearCode').disabled = true;
    }
}
function loadYearsByEduYear(eduYearCode) {
    if (!eduYearCode) {
        populateSelectAsync('yearCode', []);
        return;
    }
    fetch(`/DailyClass/GetYearsForEduYear`)
        .then(res => res.json())
        .then(data => {
            populateSelectAsync('yearCode', Array.isArray(data.years) ? data.years : []);
            enable('yearCode');
        })
        .catch(() => {
            populateSelectAsync('yearCode', []);
        });
}

function refreshClasses() {
    loadDayContent();
}

function goToToday() {
    currentDate = new Date();
    selectedDateStr = getDateStringFromDate(currentDate);
    updateDateDisplay();
    loadDayContent();
}

function generateWeeklyClasses() {
    fetch('/DailyClass/GenerateWeeklyClasses', { method: 'POST' })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                showSuccessAlert(data.message || "Weekly classes generated!").then(() => location.reload());
            } else {
                showErrorAlert(data.error || "Failed to generate weekly classes.");
            }
        })
        .catch(e => showErrorAlert("Network error: " + e.message));
}

function navigateDate(days) {
    currentDate.setDate(currentDate.getDate() + days);
    selectedDateStr = getDateStringFromDate(currentDate);
    document.getElementById('datePicker').value = selectedDateStr;
    updateDateDisplay();
    loadDayContent();
}

function onDatePickerChange() {
    const datePickerValue = document.getElementById('datePicker').value;
    currentDate = parseLocalDateFromInput(datePickerValue);
    selectedDateStr = datePickerValue;
    updateDateDisplay();
    loadDayContent();
}

function updateDateDisplay() {
    const locale = getPreferredLocale() || 'en-US';
    let formattedDate;
    try {
        formattedDate = currentDate.toLocaleDateString(locale, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
    } catch {
        formattedDate = currentDate.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
    }
    const displayDateEl = document.getElementById('displayDate');
    const calendarDateEl = document.getElementById('calendarDate');
    const selectedDateEl = document.getElementById('formSelectedDate');

    if (displayDateEl) displayDateEl.textContent = formattedDate;
    if (calendarDateEl) calendarDateEl.textContent = formattedDate;
    if (selectedDateEl) selectedDateEl.textContent = formattedDate;

    const classDateHidden = document.getElementById('classDate');
    if (classDateHidden) classDateHidden.value = selectedDateStr;
}
function hideInitialLoader() {
    const loader = document.getElementById('initialLoader');
    if (loader) {
        loader.style.opacity = '0';
        setTimeout(() => {
            if (loader.parentNode) {
                loader.remove();
            }
        }, 300);
    }
}

function loadDayContent() {
    const selectedDate = selectedDateStr;
    const container = document.getElementById('dayContent');
    container.innerHTML = `<div class="text-center py-3"><div class="spinner"></div> ${LC("Loading") || "Loading..."}</div>`;

    Promise.all([
        fetch(`/DailyClass/GetDailyClasses?date=${selectedDate}`).then(r => r.ok ? r.json() : []),
        fetch(`/DailyClass/GetDayReservations?date=${selectedDate}`).then(r => r.ok ? r.json() : [])
    ])
        .then(([classes, reservations]) => {
            dailyClasses = classes;
            dailyReservations = reservations;
            renderDayContent(classes, reservations);
            hideInitialLoader();
            enableScreen();
        })
        .catch((e) => {
            container.innerHTML = `<div class="alert alert-danger">${LC("LoadError") || "Error loading data"}: ${e.message}</div>`;
            hideInitialLoader();
            enableScreen();
        });
}

function renderDayContent(classes, reservations) {
    const container = document.getElementById('dayContent');
    container.innerHTML = '';
    const selectedDate = selectedDateStr;
    function extractDate(str) { return str?.slice(0, 10) || ''; }
    classes = Array.isArray(classes) ? classes.filter(cls => extractDate(cls.classDate) === selectedDate) : [];
    reservations = Array.isArray(reservations) ? reservations.filter(r => extractDate(r.rTime) === selectedDate) : [];
    let html = `<div class="classes-grid">`;
    if (classes.length === 0 && reservations.length === 0) {
        html += `<div class="empty-day-state"><i class="fas fa-calendar-times"></i><h4>${LC("NoClassesScheduled")}</h4><p>${LC("NoClassesSubText")}</p></div>`;
    } else {
        classes.forEach(cls => html += renderClassCard(cls));
        reservations.forEach(r => html += renderReservationCard(r));
    }
    html += `</div>`;
    container.innerHTML = html;
    setTimeout(() => {
        document.querySelectorAll('.class-card').forEach(card => {
            card.addEventListener('click', () => {
                const classId = card.dataset.classId || card.getAttribute('data-class-id');
                if (classId) showClassDetailsModal(classId);
            });
        });
    }, 100);
}

function renderClassCard(cls) {
    let typeLabel = cls.classType === 'schedule' ? LC("RecurringLabel") : (cls.classType === 'reservation' ? LC("ReservationLabel") : LC("DirectLabel"));
    return `
        <div class="class-card position-relative" data-class-id="${cls.classCode}">
            <div class="class-type-badge badge-${cls.classType}">${typeLabel}</div>
            <div class="class-card-header">
                <div class="class-title">
                    <i class="fas fa-chalkboard-teacher"></i> ${cls.title || cls.ClassName} - ${cls.classDate ? formatDate(cls.classDate) : ''}
                </div>
                <div class="class-time">
                    <i class="fas fa-clock"></i>
                    ${to12Hr(cls.startTime)} - ${to12Hr(cls.endTime)}
                </div>
            </div>
            <div class="class-card-body">
                ${cls.hallName ? `<div class="class-detail-item"><i class="fas fa-map-marker-alt class-detail-icon"></i> <span class="class-detail-label">${LC("HallLabel")}:</span> <span class="class-detail-value">${cls.hallName}</span></div>` : ''}
                ${cls.subjectName ? `<div class="class-detail-item"><i class="fas fa-book class-detail-icon"></i> <span class="class-detail-label">${LC("SubjectLabel")}:</span> <span class="class-detail-value">${cls.subjectName}</span></div>` : ''}
                <div class="class-detail-item"><i class="fas fa-users class-detail-icon"></i> <span class="class-detail-label">${LC("StudentsLabel")}:</span> <span class="class-detail-value">${cls.noOfStudents || '0'}</span></div>
              ${cls.classPrice ? `<div class="class-detail-item"><i class="fas fa-dollar-sign class-detail-icon"></i> <span class="class-detail-label">${LC("ClassPriceLabel") || "Class Price"}:</span> <span class="class-detail-value">${cls.classPrice}</span></div>` : ''}
            </div>
        </div>
    `;
}
function renderReservationCard(r) {
    return `
        <div class="class-card reservation-card position-relative">
            <div class="reservation-type-badge">${LC("ReservationLabel")}</div>
            <div class="class-card-header">
                <div class="class-title" style="color: #f7b731;">
                    <i class="fas fa-bookmark me-2"></i>${r.description || ''} - ${r.rTime || ''}
                </div>
                <div class="class-time" style="color: #6c5ce7;">
                    <i class="fas fa-clock"></i>
                    ${to12Hr(r.reservationStartTime)} - ${to12Hr(r.reservationEndTime)}
                </div>
            </div>
            <div class="class-card-body">
                <div class="class-detail-item"><i class="fas fa-user-tie class-detail-icon"></i><span class="class-detail-label">${LC("TeacherLabel")}:</span><span class="class-detail-value">${r.teacherName || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-map-marker-alt class-detail-icon"></i><span class="class-detail-label">${LC("HallLabel")}:</span><span class="class-detail-value">${r.hallName || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-users class-detail-icon"></i><span class="class-detail-label">${LC("CapacityLabel")}:</span><span class="class-detail-value">${r.capacity || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-dollar-sign class-detail-icon"></i><span class="class-detail-label">${LC("CostLabel")}:</span><span class="class-detail-value">${r.cost || ''}</span></div>
            </div>
        </div>
    `;
}

// Helper: safely hide a Bootstrap modal without throwing
function safeHideModalById(modalId) {
    try {
        const el = document.getElementById(modalId);
        if (!el) return;
        if (window.bootstrap && typeof window.bootstrap.Modal === 'function' && typeof window.bootstrap.Modal.getInstance === 'function') {
            const instance = window.bootstrap.Modal.getInstance(el);
            if (instance) { instance.hide(); return; }
        }
        if (typeof window.$ !== 'undefined' && typeof window.$('#' + modalId).modal === 'function') {
            window.$('#' + modalId).modal('hide');
            return;
        }
        const closeBtn = el.querySelector('.btn-close');
        if (closeBtn) closeBtn.click();
    } catch (e) {
        if (window.console && console.debug) console.debug('safeHideModalById error:', e);
    }
}

// --- Modal and Dropdown Management ---
function resetModalForCreate() {
    isEditMode = false;
    editingClassId = null;
    setupModalFields();
    resetFormFields();
    if (!userContext.isCenter) {
        fetch('/DailyClass/GetCurrentTeacherCode')
            .then(res => res.json())
            .then(data => {
                userContext.teacherCode = data.teacherCode;
            })
            .catch(() => {
                userContext.teacherCode = null;
            });
    }
    document.querySelector('#addClassModal .modal-title').innerHTML =
        `<i class="fas fa-plus-circle me-2"></i>${LC("AddClassBtn")}` +
        (userContext.userRootName ? `<small class="text-muted">${LC("ForUser") || "for"} ${userContext.userRootName}</small>` : '');
    if (userContext.isCenter) {
        loadUserBranch().then(() => {
            if (userContext.groupBranchCode) {
                loadEduYearsForRoot();
            }
        });
    } else {
        loadEduYearsForRoot();
        loadCentersForUserRoot();
        disable('branchCode');
        disable('subjectCode');
        disable('yearCode');
    }
}
function resetFormFields() {
    const fieldIds = ['className', 'startTime', 'endTime', 'subjectCode', 'branchCode', 'hallCode', 'eduYearCode', 'yearCode', 'classPrice'];
    if (userContext.isCenter) {
        fieldIds.push('teacherCode');
        document.getElementById('noOfStudents').value = '0';
    } else {
        fieldIds.push('centerCode');
        document.getElementById('noOfStudents').value = '0';
    }
    fieldIds.forEach(id => {
        const element = document.getElementById(id);
        if (element) {
            if (element.type === 'select-one') element.selectedIndex = 0;
            else element.value = '';
        }
    });
}
function setupModalFields() {
    if (userContext.isCenter) {
        document.getElementById('teacherField').style.display = '';
        document.getElementById('centerField').style.display = 'none';
        document.getElementById('branchNameReadonly').style.display = '';
        document.getElementById('branchCode').style.display = 'none';
        document.getElementById('hallField').style.display = '';
    } else {
        document.getElementById('teacherField').style.display = 'none';
        document.getElementById('centerField').style.display = '';
        document.getElementById('branchNameReadonly').style.display = 'none';
        document.getElementById('branchCode').style.display = '';
        document.getElementById('hallField').style.display = 'none';
    }
}
async function loadUserBranch() {
    try {
        const res = await fetch('/DailyClass/GetUserBranch');
        const branch = await res.json();
        if (branch.error) throw new Error(branch.error);
        document.getElementById('branchNameReadonly').value = branch.text;
        userContext.groupBranchCode = branch.value;
    } catch (e) {
        document.getElementById('branchNameReadonly').value = LC("ErrorLoadingBranch");
        userContext.groupBranchCode = null;
    }
}
async function loadCentersForUserRoot() {
    try {
        const res = await fetch('/DailyClass/GetCentersForUserRoot');
        const data = await res.json();
        populateSelectAsync('centerCode', data.centers || []);
        if (data.centers && data.centers.length === 1) {
            document.getElementById('centerCode').value = data.centers[0].value;
            loadBranchesByCenter(data.centers[0].value);
        }
    } catch {
        populateSelectAsync('centerCode', []);
    }
}
async function loadBranchesByCenter(centerId) {
    if (!centerId) { populateSelectAsync('branchCode', []); return; }
    try {
        const res = await fetch(`/DailyClass/GetBranchesByCenter?centerCode=${centerId}`);
        const data = await res.json();
        populateSelectAsync('branchCode', Array.isArray(data) ? data : []);
    } catch {
        populateSelectAsync('branchCode', []);
    }
}
async function loadHallsForBranch(branchCode) {
    if (!branchCode) {
        populateSelectAsync('hallCode', []);
        return;
    }
    try {
        const res = await fetch(`/DailyClass/GetHallsForBranch?branchCode=${branchCode}`);
        const data = await res.json();
        populateSelectAsync('hallCode', Array.isArray(data.halls) ? data.halls : []);
    } catch {
        populateSelectAsync('hallCode', []);
    }
}
async function loadTeachersForBranch(branchCode) {
    if (!branchCode) {
        populateSelectAsync('teacherCode', []);
        return;
    }
    try {
        const res = await fetch(`/DailyClass/GetTeachersForBranch?branchCode=${branchCode}`);
        const data = await res.json();
        populateSelectAsync('teacherCode', data.teachers || []);
        enable('teacherCode');
    } catch {
        populateSelectAsync('teacherCode', []);
        enable('teacherCode');
    }
}
async function loadSubjectsForTeacherAndBranch(teacherCode, branchCode) {
    if (!teacherCode || !branchCode) {
        populateSelectAsync('subjectCode', []);
        return;
    }
    try {
        const res = await fetch(`/DailyClass/GetSubjectsForTeacherAndBranch?teacherCode=${teacherCode}&branchCode=${branchCode}`);
        const data = await res.json();
        populateSelectAsync('subjectCode', Array.isArray(data) ? data : []);
    } catch {
        populateSelectAsync('subjectCode', []);
    }
}
function populateSelectAsync(selectId, options) {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">${LC("SelectOption") || "Select"}</option>`;
    if (!Array.isArray(options)) return;
    options.forEach(option => {
        if (option) {
            const optionElement = document.createElement('option');
            optionElement.value = option.value;
            optionElement.textContent = option.text;
            select.appendChild(optionElement);
        }
    });
}

async function loadLessonsForDropdown() {
    const teacherCode = userContext.isCenter ? document.getElementById('teacherCode').value : userContext.teacherCode;
    const yearCode = document.getElementById('yearCode').value;
    const subjectCode = document.getElementById('subjectCode').value;
    const lessonSelect = document.getElementById('lessonCode');
    lessonSelect.innerHTML = `<option value="">${LC("SelectLesson", "Select Lesson")}</option>`;

    if (!teacherCode || !yearCode || !subjectCode) return;

    try {
        const res = await fetch(`/DailyClass/GetLessonsForDropdown?teacherCode=${teacherCode}&yearCode=${yearCode}&subjectCode=${subjectCode}`);
        const chapters = await res.json();
        chapters.forEach(chapter => {
            if (!chapter.lessons || chapter.lessons.length === 0) return;
            const optgroup = document.createElement('optgroup');
            optgroup.label = chapter.chapterName;
            chapter.lessons.forEach(lesson => {
                const option = document.createElement('option');
                option.value = lesson.lessonCode;
                option.textContent = lesson.lessonName;
                optgroup.appendChild(option);
            });
            lessonSelect.appendChild(optgroup);
        });
    } catch {
        lessonSelect.innerHTML = `<option value="">No lessons found</option>`;
    }
}

// --- Save Logic (with SweetAlert2) ---
async function saveClass() {
    if (userContext.hasError) {
        await showErrorAlert(LC("SaveError"));
        return;
    }
    const submitBtn = document.getElementById('saveClassBtn');
    const originalText = submitBtn.innerHTML;
    submitBtn.innerHTML = `<div class="spinner-border spinner-border-sm me-2"></div>${isEditMode ? LC("Updating") : LC("Saving")}`;
    submitBtn.disabled = true;

    disableScreen();

    const requiredFields = [
        { id: 'className', name: LC("FormClassName") },
        { id: 'startTime', name: LC("FormStartTime") },
        { id: 'endTime', name: LC("FormEndTime") },
        { id: 'subjectCode', name: LC("FormSubject") },
        { id: 'eduYearCode', name: LC("FormEduYear") }
    ];
    if (userContext.isCenter) {
        requiredFields.push({ id: 'teacherCode', name: LC("FormTeacher") }, { id: 'hallCode', name: LC("FormHall") });
    } else {
        requiredFields.push({ id: 'centerCode', name: LC("FormCenter") }, { id: 'branchCode', name: LC("FormBranch") });
    }
    for (const field of requiredFields) {
        const element = document.getElementById(field.id);
        if (!element || !element.value || element.value.trim() === '') {
            await showErrorAlert(`${field.name} ${LC("IsRequired")}`);
            if (element) element.focus();
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
            enableScreen();
            return;
        }
    }
    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;
    if (startTime >= endTime) {
        await showErrorAlert(LC("EndTimeError"));
        submitBtn.innerHTML = originalText;
        submitBtn.disabled = false;
        enableScreen();
        return;
    }

    const formData = {
        className: document.getElementById('className').value,
        startTime: startTime,
        endTime: endTime,
        subjectCode: parseInt(document.getElementById('subjectCode').value),
        eduYearCode: parseInt(document.getElementById('eduYearCode').value),
        yearCode: parseInt(document.getElementById('yearCode').value) || null,
        classPrice: parseFloat(document.getElementById('classPrice').value) || null,
        rootCode: userContext.currentUserRootCode,
        classDate: selectedDateStr
    };
    if (userContext.isCenter) {
        formData.teacherCode = parseInt(document.getElementById('teacherCode').value);
        formData.branchCode = userContext.groupBranchCode;
        formData.hallCode = parseInt(document.getElementById('hallCode').value);
    } else {
        formData.centerCode = parseInt(document.getElementById('centerCode').value);
        formData.branchCode = parseInt(document.getElementById('branchCode').value);
        formData.teacherCode = userContext.teacherCode;
    }

    // Add lessonCode only if selected (nullable!)
    const lessonCode = document.getElementById('lessonCode')?.value;
    if (lessonCode) {
        formData.lessonCode = parseInt(lessonCode);
    }
    // --------- PATCH: Always send classCode when editing ---------
    if (isEditMode && editingClassId) {
        formData.classCode = editingClassId;
    }

    try {
        const checkResponse = await fetch('/DailyClass/CheckClassConflicts', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        });
        const check = await checkResponse.json();

        if (!check.success) {
            await showErrorAlert(check.error || "Error checking conflicts");
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
            enableScreen();
            return;
        }

        if (check.hallConflict) {
            await showErrorAlert("This hall is already occupied during this time. Please select another hall or time.");
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
            enableScreen();
            if (userContext.isCenter) return;
        }

        if (check.teacherConflict) {
            await showErrorAlert("This teacher already has a class during this time. Please select another teacher or time.");
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
            enableScreen();
            if (userContext.isCenter) return;
        }

        if (check.sameYearConflict) {
            let msg = "A class for this year already exists in this branch at this time.";
            if (check.conflictingTeacherName)
                msg += `\nConflicting teacher(s): ${check.conflictingTeacherName}`;
            msg += "\nDo you want to proceed?";
            if (!(await showConfirmAlert(msg))) {
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
                enableScreen();
                return;
            }
        }

        if (!formData.classPrice || formData.classPrice === 0) {
            if (!(await showConfirmAlert("Are you sure you want to make this class for free?"))) {
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
                enableScreen();
                return;
            }
        }

        const url = isEditMode ? `/DailyClass/EditClass/${editingClassId}` : '/DailyClass/CreateClass';
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        });
        const data = await response.json();
        if (data.success) {
            await showSuccessAlert(isEditMode ? LC("UpdateSuccess") : LC("SaveSuccess"));
            // Safe close instead of bootstrap.Modal.getInstance(...).hide()
            safeHideModalById('addClassModal');
            resetFormFields();
            resetModalForCreate();
            setTimeout(() => location.reload(), 700); // reload after 0.7s
        } else {
            await showErrorAlert((isEditMode ? LC("UpdateError") : LC("CreateError")) + (data.error || LC("UnknownError")));
            enableScreen();
        }
    } catch (error) {
        await showErrorAlert((LC("NetworkError") || "Network error: ") + error.message);
        enableScreen();
    } finally {
        submitBtn.innerHTML = originalText;
        submitBtn.disabled = false;
    }
}

// --- Enable/Disable Screen ---
function disableScreen() {
    document.body.classList.add('screen-disabled');
    document.querySelectorAll('button, input, select')
        .forEach(el => {
            if (!el.closest('.toast-container')) {
                el.disabled = true;
            }
        });
}
function enableScreen() {
    document.body.classList.remove('screen-disabled');
    document.querySelectorAll('button, input, select')
        .forEach(el => {
            if (!el.closest('.toast-container')) {
                el.disabled = false;
            }
        });
}
// --- Edit Logic ---
async function showEditClassModal(classId) {
    isEditMode = true;
    editingClassId = classId;
    const cls = dailyClasses.find(c => String(c.classCode) === String(classId));
    if (!cls) {
        showErrorAlert(LC("ClassNotFound"));
        return;
    }
    setupModalFields();
    document.getElementById('className').value = cls.className || '';
    document.getElementById('startTime').value = cls.startTime || '';
    document.getElementById('endTime').value = cls.endTime || '';
    document.getElementById('subjectCode').value = cls.subjectCode || '';
    document.getElementById('eduYearCode').value = cls.eduYearCode || '';
    document.getElementById('yearCode').value = cls.yearCode || '';
    document.getElementById('classPrice').value = cls.classPrice || '';
    document.getElementById('noOfStudents').value = cls.noOfStudents || '0';
    if (userContext.isCenter) {
        document.getElementById('teacherCode').value = cls.teacherCode || '';
        document.getElementById('hallCode').value = cls.hallCode || '';
    } else {
        document.getElementById('centerCode').value = cls.centerCode || '';
        document.getElementById('branchCode').value = cls.branchCode || '';
    }
    // Load lessons and select the correct one (if any)
    await loadLessonsForDropdown();
    document.getElementById('lessonCode').value = cls.lessonCode || '';
    const modal = new bootstrap.Modal(document.getElementById('addClassModal'));
    modal.show();
}

// --- Delete Logic ---
async function deleteClass(classId) {
    if (!(await showConfirmAlert(LC("DeleteConfirm") || "Are you sure you want to delete this class?"))) return;
    fetch(`/DailyClass/DeleteClass?id=${classId}`, {
        method: 'POST'
    })
        .then(res => res.json())
        .then(async data => {
            if (data.success) {
                await showSuccessAlert(LC("DeleteSuccess"));
                loadDayContent();
                // Safe close instead of bootstrap.Modal.getInstance(...).hide()
                safeHideModalById('classDetailsModal');
            } else {
                await showErrorAlert(data.error || LC("DeleteError"));
                enableScreen();
            }
        }).catch(async err => {
            await showErrorAlert((LC("NetworkError") || "Network error: ") + err.message);
            enableScreen();
        });
}

// --- Class Details Modal ---
function showClassDetailsModal(classId) {
    editingClassId = classId;
    const cls = dailyClasses.find(c => String(c.classCode) === String(classId));
    if (!cls) {
        showErrorAlert(LC("ClassNotFound"));
        return;
    }
    let html = `<div class="class-details-list">`;

    html += `<div><strong>${LC("FormStartTime")}</strong>: ${to12Hr(cls.startTime)}</div>`;
    html += `<div><strong>${LC("FormEndTime")}</strong>: ${to12Hr(cls.endTime)}</div>`;
    html += `<div><strong>${LC("FormSubject")}</strong>: ${cls.subjectName}</div>`;
    html += `<div><strong>${LC("FormEduYear")}</strong>: ${cls.eduYearName}</div>`;
    html += `<div><strong>${LC("FormYear")}</strong>: ${cls.yearName}</div>`;
    html += `<div><strong>${LC("FormTeacher")}</strong>: ${cls.teacherName || ''}</div>`;
    html += `<div><strong>${LC("FormHall")}</strong>: ${cls.hallName || ''}</div>`;
    html += `<div><strong>${LC("FormCenter")}</strong>: ${cls.centerName || ''}</div>`;
    html += `<div><strong>${LC("FormBranch")}</strong>: ${cls.branchName || ''}</div>`;
    html += `<div><strong>${LC("FormNoOfStudents")}</strong>: ${cls.noOfStudents}</div>`;
    html += `<div><strong>${LC("FormClassPrice")}</strong>: ${cls.classPrice}</div>`;
    html += `</div>`;
    document.getElementById('classDetailsContent').innerHTML = html;
    const modal = new bootstrap.Modal(document.getElementById('classDetailsModal'));
    modal.show();
}

// --- Utility ---
function to12Hr(timeStr) {
    if (!timeStr) return "";
    let [h, m] = timeStr.split(':');
    h = parseInt(h, 10);
    if (isNaN(h)) return ""; // defensive
    const ampm = h >= 12 ? (LC("PM") || "PM") : (LC("AM") || "AM");
    h = h % 12;
    if (h === 0) h = 12;
    return `${h}:${m} ${ampm}`;
}
function formatDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    let formatted;
    try {
        formatted = d.toLocaleDateString(getPreferredLocale() || 'en-US', { month: 'short', day: 'numeric' });
    } catch {
        formatted = d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }
    return formatted;
}