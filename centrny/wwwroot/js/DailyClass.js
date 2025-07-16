// LOCALIZED CALENDAR DAY VIEW JAVASCRIPT

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

// Use localized strings from window.DailyClassResx
function L(key, fallback = "") {
    return (window.DailyClassResx && window.DailyClassResx[key]) ? window.DailyClassResx[key] : fallback;
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

    // Teacher scenario: center and branch selection drives subject/year dropdowns
    document.getElementById('centerCode')?.addEventListener('change', function () {
        let centerId = this.value;
        if (centerId) {
            enable('branchCode');
            loadBranchesByCenter(centerId);
        } else {
            populateSelectAsync('branchCode', []);
            disable('branchCode');
        }
        disable('subjectCode');
        disable('yearCode');
        populateSelectAsync('subjectCode', []);
        populateSelectAsync('yearCode', []);
    });

    document.getElementById('branchCode')?.addEventListener('change', function () {
        disable('subjectCode');
        disable('yearCode');
        populateSelectAsync('subjectCode', []);
        populateSelectAsync('yearCode', []);

        let branchId = this.value;
        let centerId = document.getElementById('centerCode').value;
        let eduYearCode = document.getElementById('eduYearCode').value;
        if (branchId && centerId && eduYearCode) {
            enable('subjectCode');
            fetchSubjectsForTeacherEduYearBranch(userContext.teacherCode, eduYearCode, branchId);
        }
    });

    document.getElementById('eduYearCode')?.addEventListener('change', function () {
        disable('subjectCode');
        disable('yearCode');
        populateSelectAsync('subjectCode', []);
        populateSelectAsync('yearCode', []);
        let eduYearCode = this.value;
        let branchId = document.getElementById('branchCode').value;
        let centerId = document.getElementById('centerCode').value;
        if (eduYearCode && branchId && centerId) {
            enable('subjectCode');
            fetchSubjectsForTeacherEduYearBranch(userContext.teacherCode, eduYearCode, branchId);
        }
        // Always reload years for this eduYear in year dropdown if needed
        loadYearsByEduYear(eduYearCode);
    });

    document.getElementById('subjectCode')?.addEventListener('change', function () {
        let subjectId = this.value;
        let branchId = document.getElementById('branchCode').value;
        let teacherCode = userContext.teacherCode;
        if (subjectId && branchId && teacherCode) {
            enable('yearCode');
            loadYearsForTeach(branchId, teacherCode, subjectId);
        } else {
            disable('yearCode');
            populateSelectAsync('yearCode', []);
        }
    });

    // Center scenario: teacher dropdown changes subject and year
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

    // Center scenario: branch change
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

    document.getElementById('hallCode')?.addEventListener('change', function () { /* optional */ });
    document.getElementById('yearCode')?.addEventListener('change', function () { /* optional */ });

    // Class details modal edit/delete
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
function disable(id) { document.getElementById(id).disabled = true; }

// --- Dropdown Population ---
function loadYearsByEduYear(eduYearCode) {
    if (!eduYearCode) {
        populateSelectAsync('yearCode', []);
        return;
    }
    fetch(`/DailyClass/GetYearsForEduYear?eduYearCode=${eduYearCode}`)
        .then(res => res.json())
        .then(data => {
            populateSelectAsync('yearCode', Array.isArray(data.years) ? data.years : []);
        })
        .catch(() => populateSelectAsync('yearCode', []));
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
                showSuccessToast(data.message || "Weekly classes generated!");
                loadDayContent();
            } else {
                showErrorToast(data.error || "Failed to generate weekly classes.");
            }
        })
        .catch(e => showErrorToast("Network error: " + e.message));
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
    const formattedDate = currentDate.toLocaleDateString(L("Locale") || 'en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
    document.getElementById('displayDate').textContent = formattedDate;
    document.getElementById('calendarDate').textContent = formattedDate;
    document.getElementById('formSelectedDate').textContent = formattedDate;
    document.getElementById('classDate').value = selectedDateStr;
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
    // Use selectedDateStr for all API calls!
    const selectedDate = selectedDateStr;
    const container = document.getElementById('dayContent');
    container.innerHTML = `<div class="text-center py-3"><div class="spinner"></div> ${L("Loading") || "Loading..."}</div>`;

    Promise.all([
        fetch(`/DailyClass/GetDailyClasses?date=${selectedDate}`).then(r => r.ok ? r.json() : []),
        fetch(`/DailyClass/GetDayReservations?date=${selectedDate}`).then(r => r.ok ? r.json() : [])
    ])
        .then(([classes, reservations]) => {
            dailyClasses = classes;
            dailyReservations = reservations;
            renderDayContent(classes, reservations);
            hideInitialLoader();
            enableScreen(); // <-- Re-enable the UI after loading content
        })
        .catch((e) => {
            container.innerHTML = `<div class="alert alert-danger">${L("LoadError") || "Error loading data"}: ${e.message}</div>`;
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
        html += `<div class="empty-day-state"><i class="fas fa-calendar-times"></i><h4>${L("NoClassesScheduled")}</h4><p>${L("NoClassesSubText")}</p></div>`;
    } else {
        classes.forEach(cls => html += renderClassCard(cls));
        reservations.forEach(r => html += renderReservationCard(r));
    }
    html += `</div>`;
    container.innerHTML = html;

    // Attach details click handler
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
    let typeLabel = cls.classType === 'schedule' ? L("RecurringLabel") : (cls.classType === 'reservation' ? L("ReservationLabel") : L("DirectLabel"));
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

    // For teacher user, fetch own teacher code (from claims/session) via API if needed
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
    } else {
        loadCentersForUserRoot();
        disable('branchCode');
        disable('subjectCode');
        disable('yearCode');
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

async function fetchSubjectsForTeacherEduYearBranch(teacherCode, eduYearCode, branchCode) {
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
            showErrorToast("No subjects available for the selected options");
        }
    } catch {
        populateSelectAsync('subjectCode', []);
        disable('subjectCode');
        showErrorToast("No subjects available for the selected options");
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

    // Disable the UI while saving
    disableScreen();

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
            enableScreen();
            return;
        }
    }
    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;
    if (startTime >= endTime) {
        showErrorToast(L("EndTimeError"));
        submitBtn.innerHTML = originalText;
        submitBtn.disabled = false;
        enableScreen();
        return;
    }

    // Always use selectedDateStr for classDate!
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
        classDate: selectedDateStr // FIXED: always send the selected date string!
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
                enableScreen();
            }
        })
        .catch(error => {
            showErrorToast(L("NetworkError") + error.message);
            enableScreen();
        })
        .finally(() => {
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
        });
}

// --- Enable/Disable Screen ---
function disableScreen() {
    // Optionally add a semi-transparent overlay & disable all buttons/inputs in main container
    document.body.classList.add('screen-disabled');
    document.querySelectorAll('button, input, select').forEach(el => el.disabled = true);
}
function enableScreen() {
    document.body.classList.remove('screen-disabled');
    document.querySelectorAll('button, input, select').forEach(el => el.disabled = false);
}

// --- Edit Logic ---
function showEditClassModal(classId) {
    isEditMode = true;
    editingClassId = classId;
    const cls = dailyClasses.find(c => String(c.classCode) === String(classId));
    if (!cls) {
        showErrorToast(L("ClassNotFound"));
        return;
    }
    setupModalFields();
    // Populate fields with class data
    document.getElementById('className').value = cls.className || '';
    document.getElementById('startTime').value = cls.startTime || '';
    document.getElementById('endTime').value = cls.endTime || '';
    document.getElementById('subjectCode').value = cls.subjectCode || '';
    document.getElementById('eduYearCode').value = cls.eduYearCode || '';
    document.getElementById('yearCode').value = cls.yearCode || '';
    document.getElementById('totalAmount').value = cls.totalAmount || '';
    document.getElementById('teacherAmount').value = cls.teacherAmount || '';
    document.getElementById('centerAmount').value = cls.centerAmount || '';
    document.getElementById('noOfStudents').value = cls.noOfStudents || '0';
    if (userContext.isCenter) {
        document.getElementById('teacherCode').value = cls.teacherCode || '';
        document.getElementById('hallCode').value = cls.hallCode || '';
    } else {
        document.getElementById('centerCode').value = cls.centerCode || '';
        document.getElementById('branchCode').value = cls.branchCode || '';
    }
    // Open modal
    const modal = new bootstrap.Modal(document.getElementById('addClassModal'));
    modal.show();
}

// --- Delete Logic ---
function deleteClass(classId) {
    if (!confirm(L("DeleteConfirm") || "Are you sure you want to delete this class?")) return;
    fetch(`/DailyClass/DeleteClass?id=${classId}`, {
        method: 'POST'
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                showSuccessToast(L("DeleteSuccess"));
                loadDayContent();
                const modal = bootstrap.Modal.getInstance(document.getElementById('classDetailsModal'));
                if (modal) modal.hide();
            } else {
                showErrorToast(data.error || L("DeleteError"));
                enableScreen();
            }
        }).catch(err => {
            showErrorToast(L("NetworkError") + err.message);
            enableScreen();
        });
}

// --- Class Details Modal ---
function showClassDetailsModal(classId) {
    editingClassId = classId;
    const cls = dailyClasses.find(c => String(c.classCode) === String(classId));
    if (!cls) {
        showErrorToast(L("ClassNotFound"));
        return;
    }
    let html = `<div class="class-details-list">`;
    html += `<div><strong>${L("FormClassName")}</strong>: ${cls.className}</div>`;
    html += `<div><strong>${L("FormStartTime")}</strong>: ${to12Hr(cls.startTime)}</div>`;
    html += `<div><strong>${L("FormEndTime")}</strong>: ${to12Hr(cls.endTime)}</div>`;
    html += `<div><strong>${L("FormSubject")}</strong>: ${cls.subjectName}</div>`;
    html += `<div><strong>${L("FormEduYear")}</strong>: ${cls.eduYearName}</div>`;
    html += `<div><strong>${L("FormYear")}</strong>: ${cls.yearName}</div>`;
    html += `<div><strong>${L("FormTeacher")}</strong>: ${cls.teacherName || ''}</div>`;
    html += `<div><strong>${L("FormHall")}</strong>: ${cls.hallName || ''}</div>`;
    html += `<div><strong>${L("FormCenter")}</strong>: ${cls.centerName || ''}</div>`;
    html += `<div><strong>${L("FormBranch")}</strong>: ${cls.branchName || ''}</div>`;
    html += `<div><strong>${L("FormNoOfStudents")}</strong>: ${cls.noOfStudents}</div>`;
    html += `<div><strong>${L("FormTotalAmount")}</strong>: ${cls.totalAmount}</div>`;
    html += `<div><strong>${L("FormTeacherAmount")}</strong>: ${cls.teacherAmount}</div>`;
    html += `<div><strong>${L("FormCenterAmount")}</strong>: ${cls.centerAmount}</div>`;
    html += `</div>`;

    document.getElementById('classDetailsContent').innerHTML = html;
    const modal = new bootstrap.Modal(document.getElementById('classDetailsModal'));
    modal.show();
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