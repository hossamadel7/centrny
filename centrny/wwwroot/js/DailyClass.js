// LOCALIZED CALENDAR DAY VIEW JAVASCRIPT

let currentDate = new Date();
let dailyClasses = [];
let isEditMode = false;
let editingClassId = null;

const userContext = {
    currentUserRootCode: null,
    userRootName: "",
    isCenter: false,
    hasError: false,
    groupBranchCode: null
};

// Use localized strings from window.DailyClassResx
function L(key, fallback = "") {
    return (window.DailyClassResx && window.DailyClassResx[key]) ? window.DailyClassResx[key] : fallback;
}

// --- FIX: Parse date input as LOCAL time
function parseLocalDateFromInput(input) {
    const [year, month, day] = input.split('-').map(Number);
    return new Date(year, month - 1, day);
}

document.addEventListener('DOMContentLoaded', function () {
    const datePickerElement = document.getElementById('datePicker');
    currentDate = datePickerElement && datePickerElement.value
        ? parseLocalDateFromInput(datePickerElement.value)
        : new Date();
    initializeUserContext();
    setupEventListeners();
    updateDateDisplay();

    if (!userContext.hasError) {
        loadDayContent();
        resetModalForCreate();
    }
});

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
    document.getElementById('prevDayBtn')?.addEventListener('click', () => navigateDate(-1));
    document.getElementById('nextDayBtn')?.addEventListener('click', () => navigateDate(1));
    document.getElementById('datePicker')?.addEventListener('change', onDatePickerChange);

    document.getElementById('addClassModal')?.addEventListener('hidden.bs.modal', resetModalForCreate);
    document.getElementById('saveClassBtn')?.addEventListener('click', saveClass);

    document.getElementById('teacherCode')?.addEventListener('change', function () {
        const teacherId = this.value;
        let branchId = userContext.isCenter ? userContext.groupBranchCode : document.getElementById('branchCode')?.value;
        if (teacherId && branchId) {
            loadSubjectsForTeacherAndBranch(teacherId, branchId);
            const subjectId = document.getElementById('subjectCode')?.value;
            if (subjectId) {
                loadYearsForTeach(branchId, teacherId, subjectId);
            }
        } else {
            populateSelectAsync('subjectCode', []);
            populateSelectAsync('yearCode', []);
        }
    });

    document.getElementById('branchCode')?.addEventListener('change', function () {
        const branchId = this.value;
        loadHallsForBranch(branchId);
        if (!userContext.isCenter) {
            loadTeachersForBranch(branchId);
        }
        const teacherId = document.getElementById('teacherCode')?.value;
        if (teacherId && branchId) {
            loadSubjectsForTeacherAndBranch(teacherId, branchId);
            const subjectId = document.getElementById('subjectCode')?.value;
            if (subjectId) {
                loadYearsForTeach(branchId, teacherId, subjectId);
            }
        } else {
            populateSelectAsync('subjectCode', []);
            populateSelectAsync('yearCode', []);
        }
    });

    document.getElementById('subjectCode')?.addEventListener('change', function () {
        const subjectId = this.value;
        let branchId = userContext.isCenter ? userContext.groupBranchCode : document.getElementById('branchCode')?.value;
        const teacherId = document.getElementById('teacherCode')?.value;
        if (branchId && teacherId && subjectId) {
            loadYearsForTeach(branchId, teacherId, subjectId);
        } else {
            populateSelectAsync('yearCode', []);
        }
    });

    document.getElementById('centerCode')?.addEventListener('change', function () {
        const centerId = this.value;
        if (centerId) loadBranchesByCenter(centerId);
    });

    document.getElementById('eduYearCode')?.addEventListener('change', function () {
        loadYearsByEduYear(this.value);
    });
}

function navigateDate(days) {
    currentDate.setDate(currentDate.getDate() + days);
    const year = currentDate.getFullYear();
    const month = String(currentDate.getMonth() + 1).padStart(2, '0');
    const day = String(currentDate.getDate()).padStart(2, '0');
    const dateStr = `${year}-${month}-${day}`;
    document.getElementById('datePicker').value = dateStr;
    updateDateDisplay();
    loadDayContent();
}

function onDatePickerChange() {
    const datePickerValue = document.getElementById('datePicker').value;
    currentDate = parseLocalDateFromInput(datePickerValue);
    updateDateDisplay();
    loadDayContent();
}

function updateDateDisplay() {
    const formattedDate = currentDate.toLocaleDateString(L("Locale") || 'en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
    document.getElementById('displayDate').textContent = formattedDate;
    document.getElementById('calendarDate').textContent = formattedDate;
    document.getElementById('formSelectedDate').textContent = formattedDate;

    const year = currentDate.getFullYear();
    const month = String(currentDate.getMonth() + 1).padStart(2, '0');
    const day = String(currentDate.getDate()).padStart(2, '0');
    const dateStr = `${year}-${month}-${day}`;
    document.getElementById('classDate').value = dateStr;
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
    const year = currentDate.getFullYear();
    const month = String(currentDate.getMonth() + 1).padStart(2, '0');
    const day = String(currentDate.getDate()).padStart(2, '0');
    const selectedDate = `${year}-${month}-${day}`;
    const container = document.getElementById('dayContent');
    container.innerHTML = `<div class="text-center py-3"><div class="spinner"></div> ${L("Loading") || "Loading..."}</div>`;

    Promise.all([
        fetch(`/DailyClass/GetDailyClasses?date=${selectedDate}`).then(r => r.ok ? r.json() : []),
        fetch(`/DailyClass/GetDayReservations?date=${selectedDate}`).then(r => r.ok ? r.json() : [])
    ])
        .then(([classes, reservations]) => {
            renderDayContent(classes, reservations);
            hideInitialLoader();
        })
        .catch((e) => {
            container.innerHTML = `<div class="alert alert-danger">${L("LoadError") || "Error loading data"}: ${e.message}</div>`;
            hideInitialLoader();
        });
}

function renderDayContent(classes, reservations) {
    const container = document.getElementById('dayContent');
    container.innerHTML = '';
    const year = currentDate.getFullYear();
    const month = String(currentDate.getMonth() + 1).padStart(2, '0');
    const day = String(currentDate.getDate()).padStart(2, '0');
    const selectedDate = `${year}-${month}-${day}`;

    function extractDate(str) { return str?.slice(0, 10) || ''; }
    classes = Array.isArray(classes) ? classes.filter(cls => extractDate(cls.classDate) === selectedDate) : [];
    reservations = Array.isArray(reservations) ? reservations.filter(r => extractDate(r.rTime) === selectedDate) : [];

    let html = `<div class="classes-grid">`;
    if (classes.length === 0 && reservations.length === 0) {
        html += `<div class="empty-day-state"><i class="fas fa-calendar-times"></i><h4>${L("NoClassesScheduled")}</h4><p>${L("NoClassesSubText")}</p></div>`;
    } else {
        classes.forEach(cls => html += renderClassCard(cls));
        reservations.forEach(r => html += renderReservationCard(r));
    }
    html += `</div>`;
    container.innerHTML = html;
}

function renderClassCard(cls) {
    let typeLabel = cls.classType === 'schedule' ? L("RecurringLabel") : (cls.classType === 'reservation' ? L("ReservationLabel") : L("DirectLabel"));
    return `
        <div class="class-card position-relative">
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
                ${cls.hallName ? `<div class="class-detail-item"><i class="fas fa-map-marker-alt class-detail-icon"></i> <span class="class-detail-label">${L("HallLabel")}:</span> <span class="class-detail-value">${cls.hallName}</span></div>` : ''}
                ${cls.subjectName ? `<div class="class-detail-item"><i class="fas fa-book class-detail-icon"></i> <span class="class-detail-label">${L("SubjectLabel")}:</span> <span class="class-detail-value">${cls.subjectName}</span></div>` : ''}
                <div class="class-detail-item"><i class="fas fa-users class-detail-icon"></i> <span class="class-detail-label">${L("StudentsLabel")}:</span> <span class="class-detail-value">${cls.noOfStudents || '0'}</span></div>
                ${cls.totalAmount ? `<div class="class-detail-item"><i class="fas fa-dollar-sign class-detail-icon"></i> <span class="class-detail-label">${L("AmountLabel")}:</span> <span class="class-detail-value">${cls.totalAmount}</span></div>` : ''}
            </div>
        </div>
    `;
}

function renderReservationCard(r) {
    return `
        <div class="class-card reservation-card position-relative">
            <div class="reservation-type-badge">${L("ReservationLabel")}</div>
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
                <div class="class-detail-item"><i class="fas fa-user-tie class-detail-icon"></i><span class="class-detail-label">${L("TeacherLabel")}:</span><span class="class-detail-value">${r.teacherName || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-map-marker-alt class-detail-icon"></i><span class="class-detail-label">${L("HallLabel")}:</span><span class="class-detail-value">${r.hallName || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-users class-detail-icon"></i><span class="class-detail-label">${L("CapacityLabel")}:</span><span class="class-detail-value">${r.capacity || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-dollar-sign class-detail-icon"></i><span class="class-detail-label">${L("CostLabel")}:</span><span class="class-detail-value">${r.cost || ''}</span></div>
            </div>
        </div>
    `;
}

// --- Modal and Dropdown Management ---
function resetModalForCreate() {
    isEditMode = false;
    editingClassId = null;
    setupModalFields();
    resetFormFields();
    document.querySelector('#addClassModal .modal-title').innerHTML =
        `<i class="fas fa-plus-circle me-2"></i>${L("AddClassBtn")}` +
        (userContext.userRootName ? `<small class="text-muted">${L("ForUser") || "for"} ${userContext.userRootName}</small>` : '');

    loadEduYearsForRoot();
    if (userContext.isCenter) {
        loadUserBranch().then(() => {
            if (userContext.groupBranchCode) {
                loadHallsForBranch(userContext.groupBranchCode);
                loadTeachersForBranch(userContext.groupBranchCode);
            }
        });
    }
}
function resetFormFields() {
    const fieldIds = ['className', 'startTime', 'endTime', 'subjectCode', 'branchCode', 'hallCode', 'eduYearCode', 'yearCode', 'totalAmount', 'teacherAmount', 'centerAmount'];
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
        document.getElementById('branchNameReadonly').value = L("ErrorLoadingBranch");
        userContext.groupBranchCode = null;
    }
}

async function loadTeachersForCenterUser() {
    try {
        const res = await fetch('/DailyClass/GetTeachersForCenterUser');
        const data = await res.json();
        populateSelectAsync('teacherCode', data.teachers || []);
    } catch {
        populateSelectAsync('teacherCode', []);
    }
}

async function loadCentersForUserRoot() {
    try {
        const res = await fetch('/DailyClass/GetCentersForUserRoot');
        const data = await res.json();
        populateSelectAsync('centerCode', data.centers || []);
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

async function loadSubjectsForTeacher(teacherId) {
    if (!teacherId) { populateSelectAsync('subjectCode', []); return; }
    try {
        const res = await fetch(`/DailyClass/GetSubjectsForTeacher?teacherCode=${teacherId}`);
        const data = await res.json();
        populateSelectAsync('subjectCode', Array.isArray(data) ? data : []);
    } catch {
        populateSelectAsync('subjectCode', []);
    }
}
async function loadEduYearsForRoot() {
    try {
        const res = await fetch('/DailyClass/GetEduYearsForRoot');
        const data = await res.json();
        const eduYears = data.eduYears || [];
        populateSelectAsync('eduYearCode', eduYears);
        if (eduYears.length > 0) {
            const select = document.getElementById('eduYearCode');
            select.value = eduYears[0].value;
            select.dispatchEvent(new Event('change'));
        }
    } catch {
        populateSelectAsync('eduYearCode', []);
    }
}
async function loadYearsForTeach(branchCode, teacherCode, subjectCode) {
    if (!branchCode || !teacherCode || !subjectCode) {
        populateSelectAsync('yearCode', []);
        return;
    }
    try {
        const res = await fetch(`/DailyClass/GetYearsForTeach?branchCode=${branchCode}&teacherCode=${teacherCode}&subjectCode=${subjectCode}`);
        const data = await res.json();
        populateSelectAsync('yearCode', Array.isArray(data.years) ? data.years : []);
    } catch {
        populateSelectAsync('yearCode', []);
    }
}

function populateSelectAsync(selectId, options) {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">${L("SelectOption")}</option>`;
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

// --- Save Logic ---
function saveClass() {
    if (userContext.hasError) {
        showErrorToast(L("SaveError"));
        return;
    }
    const submitBtn = document.getElementById('saveClassBtn');
    const originalText = submitBtn.innerHTML;
    submitBtn.innerHTML = `<div class="spinner-border spinner-border-sm me-2"></div>${isEditMode ? L("Updating") : L("Saving")}`;
    submitBtn.disabled = true;

    const requiredFields = [
        { id: 'className', name: L("FormClassName") },
        { id: 'startTime', name: L("FormStartTime") },
        { id: 'endTime', name: L("FormEndTime") },
        { id: 'subjectCode', name: L("FormSubject") },
        { id: 'eduYearCode', name: L("FormEduYear") }
    ];
    if (userContext.isCenter) {
        requiredFields.push({ id: 'teacherCode', name: L("FormTeacher") }, { id: 'hallCode', name: L("FormHall") });
    } else {
        requiredFields.push({ id: 'centerCode', name: L("FormCenter") }, { id: 'branchCode', name: L("FormBranch") });
    }
    for (const field of requiredFields) {
        const element = document.getElementById(field.id);
        if (!element || !element.value || element.value.trim() === '') {
            showErrorToast(`${field.name} ${L("IsRequired")}`);
            if (element) element.focus();
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
            return;
        }
    }
    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;
    if (startTime >= endTime) {
        showErrorToast(L("EndTimeError"));
        submitBtn.innerHTML = originalText;
        submitBtn.disabled = false;
        return;
    }

    const formData = {
        className: document.getElementById('className').value,
        startTime: startTime,
        endTime: endTime,
        subjectCode: parseInt(document.getElementById('subjectCode').value),
        eduYearCode: parseInt(document.getElementById('eduYearCode').value),
        yearCode: parseInt(document.getElementById('yearCode').value) || null,
        totalAmount: parseFloat(document.getElementById('totalAmount').value) || null,
        teacherAmount: parseFloat(document.getElementById('teacherAmount').value) || null,
        centerAmount: parseFloat(document.getElementById('centerAmount').value) || null,
        rootCode: userContext.currentUserRootCode,
        classDate: currentDate
    };
    if (userContext.isCenter) {
        formData.teacherCode = parseInt(document.getElementById('teacherCode').value);
        formData.branchCode = userContext.groupBranchCode;
        formData.hallCode = parseInt(document.getElementById('hallCode').value);
    } else {
        formData.centerCode = parseInt(document.getElementById('centerCode').value);
        formData.branchCode = parseInt(document.getElementById('branchCode').value);
    }

    const url = isEditMode ? `/DailyClass/EditClass/${editingClassId}` : '/DailyClass/CreateClass';
    fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(formData)
    })
        .then(response => {
            if (!response.ok) throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            return response.json();
        })
        .then(data => {
            if (data.success) {
                showSuccessToast(isEditMode ? L("UpdateSuccess") : L("SaveSuccess"));
                bootstrap.Modal.getInstance(document.getElementById('addClassModal')).hide();
                resetFormFields();
                resetModalForCreate();
                loadDayContent();
            } else {
                showErrorToast((isEditMode ? L("UpdateError") : L("CreateError")) + (data.error || L("UnknownError")));
            }
        })
        .catch(error => {
            showErrorToast(L("NetworkError") + error.message);
        })
        .finally(() => {
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
        });
}

// --- Toasts ---
function showSuccessToast(message) { showToast(message, 'bg-success'); }
function showErrorToast(message) { showToast(message, 'bg-danger'); }
function showToast(message, bgClass) {
    const toastContainer = document.querySelector('.toast-container');
    const toastId = 'toast-' + Date.now();
    const toastHtml = `
        <div class="toast ${bgClass} text-white" role="alert" id="${toastId}">
            <div class="toast-body d-flex align-items-center">
                <i class="fas ${bgClass === 'bg-success' ? 'fa-check-circle' : 'fa-exclamation-circle'} me-2"></i>
                <span class="flex-grow-1">${message}</span>
                <button type="button" class="btn-close btn-close-white ms-2" data-bs-dismiss="toast"></button>
            </div>
        </div>
    `;
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement);
    toast.show();
    toastElement.addEventListener('hidden.bs.toast', () => { toastElement.remove(); });
}

// --- Utility ---
function to12Hr(timeStr) {
    if (!timeStr) return "";
    let [h, m] = timeStr.split(':');
    h = parseInt(h, 10);
    const ampm = h >= 12 ? L("PM") : L("AM");
    h = h % 12;
    if (h === 0) h = 12;
    return `${h}:${m} ${ampm}`;
}
function formatDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return d.toLocaleDateString(L("Locale") || 'en-US', { month: 'short', day: 'numeric' });
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
    } catch {
        populateSelectAsync('teacherCode', []);
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