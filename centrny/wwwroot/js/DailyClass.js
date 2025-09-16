// LOCALIZED CALENDAR DAY VIEW JAVASCRIPT with SweetAlert2 integration
// Fixes:
// - Avoids collision with global "L" by using LC()
// - Guards locale usage in toLocaleDateString
// - Safe modal hide
// - Edit mode fetches fresh data (GetClassById)
// - Suppress cascading change/XHR during edit hydration
// - No invalid optional chaining assignments on LHS
// - Prevent "resetModalForCreate is not defined" by wrapping the listener

let currentDate = new Date();
let dailyClasses = [];
let dailyReservations = [];
let isEditMode = false;
let editingClassId = null;
let selectedDateStr = ""; // yyyy-MM-dd
let isHydrating = false;   // suppress change-driven XHR during edit fill

const userContext = {
    currentUserRootCode: null,
    userRootName: "",
    isCenter: false,
    hasError: false,
    groupBranchCode: null,
    teacherCode: null
};

// --- SweetAlert2 Helpers ---
function showSuccessAlert(message) {
    return Swal.fire({ icon: 'success', title: 'Success', text: message, confirmButtonColor: '#3085d6' });
}
function showErrorAlert(message) {
    return Swal.fire({ icon: 'error', title: 'Error', text: message, confirmButtonColor: '#d33' });
}
function showInfoAlert(message) {
    return Swal.fire({ icon: 'info', title: 'Info', text: message });
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
    }).then(r => r.isConfirmed);
}

// --- Localization ---
function LC(key, fallback = "") {
    return (window.DailyClassResx && window.DailyClassResx[key]) ? window.DailyClassResx[key] : fallback;
}
function getPreferredLocale() {
    return LC("Locale") || document.documentElement.lang || navigator.language || 'en-US';
}

// --- Date helpers ---
function parseLocalDateFromInput(input) {
    const [y, m, d] = input.split('-').map(Number);
    return new Date(y, m - 1, d);
}
function getDateStringFromDate(dateObj) {
    return [
        dateObj.getFullYear(),
        String(dateObj.getMonth() + 1).padStart(2, '0'),
        String(dateObj.getDate()).padStart(2, '0')
    ].join('-');
}

// Small utilities (used in multiple places)
function setVal(id, val) {
    const el = document.getElementById(id);
    if (el != null) el.value = (val ?? '').toString();
}
function toHHmm(timeVal) {
    if (!timeVal) return '';
    if (typeof timeVal === 'string') {
        // Expect "HH:mm" or "HH:mm:ss"
        return timeVal.length >= 5 ? timeVal.substring(0, 5) : timeVal;
    }
    if (typeof timeVal === 'object') {
        // Handle possible object serialization { hours, minutes } or Pascal-case
        const h = timeVal.hours ?? timeVal.Hours;
        const m = timeVal.minutes ?? timeVal.Minutes;
        if (h != null && m != null) return String(h).padStart(2, '0') + ':' + String(m).padStart(2, '0');
    }
    return '';
}

// Listener wrapper to avoid ReferenceError if bundlers reorder things
function onAddModalHidden() {
    try {
        if (typeof resetModalForCreate === 'function') {
            resetModalForCreate();
        }
    } catch (_) {
        // no-op
    }
}

// --- Init ---
document.addEventListener('DOMContentLoaded', () => {
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

function initializeUserContext() {
    const rootCodeElement = document.getElementById('rootCode');
    const isCenterElement = document.getElementById('isCenter');
    if (rootCodeElement) userContext.currentUserRootCode = rootCodeElement.value;
    userContext.isCenter = !!(isCenterElement && String(isCenterElement.value).toLowerCase() === 'true');
    const spanUser = document.getElementById('formSelectedDate');
    userContext.userRootName = spanUser && spanUser.dataset ? (spanUser.dataset.username || '') : '';
    userContext.hasError = !!document.querySelector('.error-banner');
}

function setupEventListeners() {
    const prevBtn = document.getElementById('prevDayBtn');
    if (prevBtn) prevBtn.addEventListener('click', () => navigateDate(-1));
    const nextBtn = document.getElementById('nextDayBtn');
    if (nextBtn) nextBtn.addEventListener('click', () => navigateDate(1));
    const datePicker = document.getElementById('datePicker');
    if (datePicker) datePicker.addEventListener('change', onDatePickerChange);

    const addModal = document.getElementById('addClassModal');
    if (addModal) addModal.addEventListener('hidden.bs.modal', onAddModalHidden);

    const saveBtn = document.getElementById('saveClassBtn');
    if (saveBtn) saveBtn.addEventListener('click', saveClass);

    // Change handlers guarded by isHydrating to avoid XHR storms during edit fill
    const teacherSelect = document.getElementById('teacherCode');
    if (teacherSelect) teacherSelect.addEventListener('change', () => { if (!isHydrating) loadLessonsForDropdown(); });
    const yearSelect = document.getElementById('yearCode');
    if (yearSelect) yearSelect.addEventListener('change', () => { if (!isHydrating) loadLessonsForDropdown(); });
    const subjectSelect = document.getElementById('subjectCode');
    if (subjectSelect) subjectSelect.addEventListener('change', () => { if (!isHydrating) loadLessonsForDropdown(); });

    // Center-specific dependencies
    if (teacherSelect) {
        teacherSelect.addEventListener('change', function () {
            if (isHydrating || !userContext.isCenter) return;
            disable('hallCode'); populateSelectAsync('hallCode', []);
            disable('subjectCode'); populateSelectAsync('subjectCode', []);
            populateSelectAsync('yearCode', []);
            const teacherId = this.value;
            const eduYearCode = document.getElementById('eduYearCode')?.value;
            if (teacherId && userContext.groupBranchCode && eduYearCode) {
                enable('hallCode');
                loadHallsForBranch(userContext.groupBranchCode);
                enable('subjectCode');
                fetchSubjectsForTeacherEduYearBranch(teacherId, eduYearCode, userContext.groupBranchCode);
            }
        });
    }

    if (subjectSelect) {
        subjectSelect.addEventListener('change', function () {
            if (isHydrating) return;
            if (userContext.isCenter) {
                populateSelectAsync('yearCode', []);
                const subjectId = this.value;
                const teacherId = document.getElementById('teacherCode')?.value;
                const eduYearCode = document.getElementById('eduYearCode')?.value;
                const branchCode = userContext.groupBranchCode;
                if (subjectId && teacherId && branchCode && eduYearCode) {
                    loadYearsForTeach(branchCode, teacherId, subjectId, eduYearCode);
                }
            } else {
                const eduYearCode = document.getElementById('eduYearCode')?.value;
                loadYearsByEduYear(eduYearCode);
            }
        });
    }

    const centerSelect = document.getElementById('centerCode');
    if (centerSelect) {
        centerSelect.addEventListener('change', function () {
            if (isHydrating) return;
            const centerId = this.value;
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
    }

    const branchSelect = document.getElementById('branchCode');
    if (branchSelect) {
        branchSelect.addEventListener('change', function () {
            if (isHydrating) return;
            disable('subjectCode'); populateSelectAsync('subjectCode', []);
            populateSelectAsync('yearCode', []);
            const branchId = this.value;
            const centerId = document.getElementById('centerCode')?.value;
            const eduYearCode = document.getElementById('eduYearCode')?.value;
            if (branchId && centerId && eduYearCode) {
                enable('subjectCode');
                const teacherId = userContext.isCenter ? document.getElementById('teacherCode')?.value : userContext.teacherCode;
                fetchSubjectsForTeacherEduYearBranch(teacherId, eduYearCode, branchId);
            }
            if (!userContext.isCenter) {
                loadYearsByEduYear(eduYearCode);
            }
        });
    }

    const eduYearSelect = document.getElementById('eduYearCode');
    if (eduYearSelect) {
        eduYearSelect.addEventListener('change', function () {
            if (isHydrating) return;
            populateSelectAsync('teacherCode', []);
            disable('hallCode'); populateSelectAsync('hallCode', []);
            disable('subjectCode'); populateSelectAsync('subjectCode', []);
            populateSelectAsync('yearCode', []);
            if (userContext.isCenter) {
                if (userContext.groupBranchCode) loadStaffTeachersForBranch(userContext.groupBranchCode);
            } else {
                const eduYearCode = this.value;
                const branchId = document.getElementById('branchCode')?.value;
                const centerId = document.getElementById('centerCode')?.value;
                if (eduYearCode && branchId && centerId) {
                    enable('subjectCode');
                    const teacherId = userContext.teacherCode;
                    fetchSubjectsForTeacherEduYearBranch(teacherId, eduYearCode, branchId);
                }
                loadYearsByEduYear(eduYearCode);
            }
        });
    }

    const editBtn = document.getElementById('editClassBtn');
    if (editBtn) editBtn.addEventListener('click', () => { if (editingClassId) showEditClassModal(editingClassId); });

    const delBtn = document.getElementById('deleteClassBtn');
    if (delBtn) delBtn.addEventListener('click', () => { if (editingClassId) deleteClass(editingClassId); });
}

// Enable/Disable helpers
function enable(id) {
    const el = document.getElementById(id);
    if (el) el.disabled = false;
}
function disable(id) {
    if (id === 'yearCode') return; // yearCode exception
    const el = document.getElementById(id);
    if (el) el.disabled = true;
}

// --- AJAX helpers & dropdown population ---
async function loadStaffTeachersForBranch(branchCode) {
    try {
        const res = await fetch(`/DailyClass/GetTeachersForBranch?branchCode=${branchCode}`);
        const data = await res.json();
        const teachers = Array.isArray(data.teachers) ? data.teachers : [];
        populateSelectAsync('teacherCode', teachers);
        enable('teacherCode');
    } catch {
        populateSelectAsync('teacherCode', []); enable('teacherCode');
    }
}

async function fetchSubjectsForTeacherEduYearBranch(teacherCode, eduYearCode, branchCode) {
    if (!teacherCode || !eduYearCode || !branchCode) {
        populateSelectAsync('subjectCode', []); disable('subjectCode'); return;
    }
    try {
        const res = await fetch(`/DailyClass/GetSubjectsForTeacherRootEduYearBranch?teacherCode=${teacherCode}&eduYearCode=${eduYearCode}&branchCode=${branchCode}`);
        const data = await res.json();
        if (Array.isArray(data) && data.length > 0) {
            populateSelectAsync('subjectCode', data); enable('subjectCode');
        } else {
            populateSelectAsync('subjectCode', []); disable('subjectCode');
            await showErrorAlert("No subjects available for the selected options");
        }
    } catch {
        populateSelectAsync('subjectCode', []); disable('subjectCode');
        await showErrorAlert("No subjects available for the selected options");
    }
}

async function loadYearsForTeach(branchCode, teacherCode, subjectCode, eduYearCode = null) {
    if (!branchCode || !teacherCode || !subjectCode) {
        populateSelectAsync('yearCode', []); return;
    }
    let url = `/DailyClass/GetYearsForTeach?branchCode=${branchCode}&teacherCode=${teacherCode}&subjectCode=${subjectCode}`;
    if (eduYearCode) url += `&eduYearCode=${eduYearCode}`;
    try {
        const res = await fetch(url);
        const data = await res.json();
        const years = Array.isArray(data.years) ? data.years : [];
        populateSelectAsync('yearCode', years); enable('yearCode');
    } catch {
        populateSelectAsync('yearCode', []);
    }
}

async function loadEduYearsForRoot(autoDispatch = true) {
    try {
        const res = await fetch('/DailyClass/GetEduYearsForRoot');
        const data = await res.json();
        const eduYears = data.eduYears || [];
        const select = document.getElementById('eduYearCode');
        populateSelectAsync('eduYearCode', eduYears);
        if (eduYears.length === 1) {
            select.value = eduYears[0].value;
            select.disabled = true;
            if (autoDispatch) select.dispatchEvent(new Event('change'));
        } else {
            select.disabled = false;
            if (eduYears.length > 0) {
                select.value = eduYears[0].value;
                if (autoDispatch) select.dispatchEvent(new Event('change'));
            }
        }
    } catch {
        populateSelectAsync('eduYearCode', []);
        const sel = document.getElementById('eduYearCode');
        if (sel) sel.disabled = true;
    }
}

async function loadCentersForUserRoot(autoSelect = true) {
    try {
        const res = await fetch('/DailyClass/GetCentersForUserRoot');
        const data = await res.json();
        const centers = data.centers || [];
        populateSelectAsync('centerCode', centers);
        if (autoSelect && centers.length === 1) {
            const centerSelect = document.getElementById('centerCode');
            if (centerSelect) centerSelect.value = centers[0].value;
            await loadBranchesByCenter(centers[0].value);
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
    if (!branchCode) { populateSelectAsync('hallCode', []); return; }
    try {
        const res = await fetch(`/DailyClass/GetHallsForBranch?branchCode=${branchCode}`);
        const data = await res.json();
        populateSelectAsync('hallCode', Array.isArray(data.halls) ? data.halls : []);
    } catch {
        populateSelectAsync('hallCode', []);
    }
}

async function loadTeachersForBranch(branchCode) {
    if (!branchCode) { populateSelectAsync('teacherCode', []); return; }
    try {
        const res = await fetch(`/DailyClass/GetTeachersForBranch?branchCode=${branchCode}`);
        const data = await res.json();
        populateSelectAsync('teacherCode', data.teachers || []);
        enable('teacherCode');
    } catch {
        populateSelectAsync('teacherCode', []); enable('teacherCode');
    }
}

async function loadSubjectsForTeacherAndBranch(teacherCode, branchCode) {
    if (!teacherCode || !branchCode) { populateSelectAsync('subjectCode', []); return; }
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
    const placeholder = LC("SelectOption") || "Select";
    select.innerHTML = `<option value="">${placeholder}</option>`;
    if (!Array.isArray(options)) return;
    options.forEach(o => {
        if (!o) return;
        const opt = document.createElement('option');
        opt.value = o.value;
        opt.textContent = o.text;
        select.appendChild(opt);
    });
}

async function loadLessonsForDropdown() {
    const teacherCode = userContext.isCenter ? document.getElementById('teacherCode')?.value : userContext.teacherCode;
    const yearCode = document.getElementById('yearCode')?.value;
    const subjectCode = document.getElementById('subjectCode')?.value;
    const lessonSelect = document.getElementById('lessonCode');
    if (!lessonSelect) return;
    lessonSelect.innerHTML = `<option value="">${LC("SelectLesson", "Select Lesson")}</option>`;
    if (!teacherCode || !yearCode || !subjectCode) return;
    try {
        const res = await fetch(`/DailyClass/GetLessonsForDropdown?teacherCode=${teacherCode}&yearCode=${yearCode}&subjectCode=${subjectCode}`);
        const chapters = await res.json();
        chapters.forEach(ch => {
            if (!Array.isArray(ch.lessons) || ch.lessons.length === 0) return;
            const group = document.createElement('optgroup');
            group.label = ch.chapterName;
            ch.lessons.forEach(lesson => {
                const opt = document.createElement('option');
                opt.value = lesson.lessonCode;
                opt.textContent = lesson.lessonName;
                group.appendChild(opt);
            });
            lessonSelect.appendChild(group);
        });
    } catch {
        lessonSelect.innerHTML = `<option value="">No lessons found</option>`;
    }
}

// --- Toolbar actions ---
function refreshClasses() { loadDayContent(); }
function goToToday() {
    currentDate = new Date();
    selectedDateStr = getDateStringFromDate(currentDate);
    updateDateDisplay();
    loadDayContent();
}

function generateWeeklyClasses() {
    fetch('/DailyClass/GenerateWeeklyClasses', { method: 'POST' })
        .then(r => r.json())
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
    const datePicker = document.getElementById('datePicker');
    if (datePicker) datePicker.value = selectedDateStr;
    updateDateDisplay();
    loadDayContent();
}
function onDatePickerChange() {
    const val = document.getElementById('datePicker')?.value;
    if (!val) return;
    currentDate = parseLocalDateFromInput(val);
    selectedDateStr = val;
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
    if (displayDateEl) displayDateEl.textContent = formattedDate;
    const calendarDateEl = document.getElementById('calendarDate');
    if (calendarDateEl) calendarDateEl.textContent = formattedDate;
    const selectedDateEl = document.getElementById('formSelectedDate');
    if (selectedDateEl) selectedDateEl.textContent = formattedDate;
    const classDateHidden = document.getElementById('classDate');
    if (classDateHidden) classDateHidden.value = selectedDateStr;
}

function hideInitialLoader() {
    const loader = document.getElementById('initialLoader');
    if (loader) {
        loader.style.opacity = '0';
        setTimeout(() => { if (loader.parentNode) loader.remove(); }, 300);
    }
}

// --- Data load & render ---
function loadDayContent() {
    const date = selectedDateStr;
    const container = document.getElementById('dayContent');
    if (container) {
        container.innerHTML = `<div class="text-center py-3"><div class="spinner"></div> ${LC("Loading") || "Loading..."}</div>`;
    }
    Promise.all([
        fetch(`/DailyClass/GetDailyClasses?date=${date}`).then(r => r.ok ? r.json() : []),
        fetch(`/DailyClass/GetDayReservations?date=${date}`).then(r => r.ok ? r.json() : [])
    ])
        .then(([classes, reservations]) => {
            dailyClasses = classes;
            dailyReservations = reservations;
            renderDayContent(classes, reservations);
            hideInitialLoader();
            enableScreen();
        })
        .catch(e => {
            if (container) {
                container.innerHTML = `<div class="alert alert-danger">${LC("LoadError") || "Error loading data"}: ${e.message}</div>`;
            }
            hideInitialLoader();
            enableScreen();
        });
}

function renderDayContent(classes, reservations) {
    const container = document.getElementById('dayContent');
    if (!container) return;
    container.innerHTML = '';
    const selectedDate = selectedDateStr;
    const extractDate = str => (str ? str.slice(0, 10) : '');
    classes = Array.isArray(classes) ? classes.filter(c => extractDate(c.classDate) === selectedDate) : [];
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
        container.querySelectorAll('.class-card').forEach(card => {
            card.addEventListener('click', () => {
                const classId = card.getAttribute('data-class-id');
                if (classId) showClassDetailsModal(classId);
            });
        });
    }, 50);
}

function renderClassCard(cls) {
    const typeLabel = cls.classType === 'schedule' ? LC("RecurringLabel") : (cls.classType === 'reservation' ? LC("ReservationLabel") : LC("DirectLabel"));
    return `
        <div class="class-card position-relative" data-class-id="${cls.classCode}">
            <div class="class-type-badge badge-${cls.classType}">${typeLabel}</div>
            <div class="class-card-header">
                <div class="class-title"><i class="fas fa-chalkboard-teacher"></i> ${cls.title || cls.ClassName} - ${cls.classDate ? formatDate(cls.classDate) : ''}</div>
                <div class="class-time"><i class="fas fa-clock"></i> ${to12Hr(cls.startTime)} - ${to12Hr(cls.endTime)}</div>
            </div>
            <div class="class-card-body">
                ${cls.hallName ? `<div class="class-detail-item"><i class="fas fa-map-marker-alt class-detail-icon"></i><span class="class-detail-label">${LC("HallLabel")}:</span><span class="class-detail-value">${cls.hallName}</span></div>` : ''}
                ${cls.subjectName ? `<div class="class-detail-item"><i class="fas fa-book class-detail-icon"></i><span class="class-detail-label">${LC("SubjectLabel")}:</span><span class="class-detail-value">${cls.subjectName}</span></div>` : ''}
                <div class="class-detail-item"><i class="fas fa-users class-detail-icon"></i><span class="class-detail-label">${LC("StudentsLabel")}:</span><span class="class-detail-value">${cls.noOfStudents || '0'}</span></div>
                ${cls.classPrice ? `<div class="class-detail-item"><i class="fas fa-dollar-sign class-detail-icon"></i><span class="class-detail-label">${LC("ClassPriceLabel") || "Class Price"}:</span><span class="class-detail-value">${cls.classPrice}</span></div>` : ''}
            </div>
        </div>`;
}

function renderReservationCard(r) {
    return `
        <div class="class-card reservation-card position-relative">
            <div class="reservation-type-badge">${LC("ReservationLabel")}</div>
            <div class="class-card-header">
                <div class="class-title" style="color:#f7b731;"><i class="fas fa-bookmark me-2"></i>${r.description || ''} - ${r.rTime || ''}</div>
                <div class="class-time" style="color:#6c5ce7;"><i class="fas fa-clock"></i> ${to12Hr(r.reservationStartTime)} - ${to12Hr(r.reservationEndTime)}</div>
            </div>
            <div class="class-card-body">
                <div class="class-detail-item"><i class="fas fa-user-tie class-detail-icon"></i><span class="class-detail-label">${LC("TeacherLabel")}:</span><span class="class-detail-value">${r.teacherName || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-map-marker-alt class-detail-icon"></i><span class="class-detail-label">${LC("HallLabel")}:</span><span class="class-detail-value">${r.hallName || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-users class-detail-icon"></i><span class="class-detail-label">${LC("CapacityLabel")}:</span><span class="class-detail-value">${r.capacity || ''}</span></div>
                <div class="class-detail-item"><i class="fas fa-dollar-sign class-detail-icon"></i><span class="class-detail-label">${LC("CostLabel")}:</span><span class="class-detail-value">${r.cost || ''}</span></div>
            </div>
        </div>`;
}

// --- Save Logic ---
async function saveClass() {
    if (userContext.hasError) {
        await showErrorAlert(LC("SaveError"));
        return;
    }
    const btn = document.getElementById('saveClassBtn');
    const original = btn ? btn.innerHTML : '';
    if (btn) {
        btn.innerHTML = `<div class="spinner-border spinner-border-sm me-2"></div>${isEditMode ? LC("Updating") : LC("Saving")}`;
        btn.disabled = true;
    }
    disableScreen();

    const required = [
        { id: 'className', name: LC("FormClassName") },
        { id: 'startTime', name: LC("FormStartTime") },
        { id: 'endTime', name: LC("FormEndTime") },
        { id: 'subjectCode', name: LC("FormSubject") },
        { id: 'eduYearCode', name: LC("FormEduYear") }
    ];
    if (userContext.isCenter) {
        required.push({ id: 'teacherCode', name: LC("FormTeacher") }, { id: 'hallCode', name: LC("FormHall") });
    } else {
        required.push({ id: 'centerCode', name: LC("FormCenter") }, { id: 'branchCode', name: LC("FormBranch") });
    }
    for (const f of required) {
        const el = document.getElementById(f.id);
        if (!el || !el.value || el.value.trim() === '') {
            await showErrorAlert(`${f.name} ${LC("IsRequired")}`);
            if (el) el.focus();
            if (btn) { btn.innerHTML = original; btn.disabled = false; }
            enableScreen();
            return;
        }
    }

    const startTime = document.getElementById('startTime')?.value;
    const endTime = document.getElementById('endTime')?.value;
    if (startTime >= endTime) {
        await showErrorAlert(LC("EndTimeError"));
        if (btn) { btn.innerHTML = original; btn.disabled = false; }
        enableScreen(); return;
    }

    const formData = {
        className: document.getElementById('className')?.value,
        startTime,
        endTime,
        subjectCode: parseInt(document.getElementById('subjectCode')?.value),
        eduYearCode: parseInt(document.getElementById('eduYearCode')?.value),
        yearCode: parseInt(document.getElementById('yearCode')?.value) || null,
        classPrice: parseFloat(document.getElementById('classPrice')?.value) || null,
        rootCode: userContext.currentUserRootCode,
        classDate: selectedDateStr
    };
    if (userContext.isCenter) {
        formData.teacherCode = parseInt(document.getElementById('teacherCode')?.value);
        formData.branchCode = userContext.groupBranchCode;
        formData.hallCode = parseInt(document.getElementById('hallCode')?.value);
    } else {
        formData.centerCode = parseInt(document.getElementById('centerCode')?.value);
        formData.branchCode = parseInt(document.getElementById('branchCode')?.value);
        formData.teacherCode = userContext.teacherCode;
    }
    const lessonCode = document.getElementById('lessonCode')?.value;
    if (lessonCode) formData.lessonCode = parseInt(lessonCode);
    if (isEditMode && editingClassId) formData.classCode = editingClassId;

    try {
        const checkRes = await fetch('/DailyClass/CheckClassConflicts', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formData)
        });
        const check = await checkRes.json();
        if (!check.success) { await showErrorAlert(check.error || "Error checking conflicts"); throw new Error('conflict-check-failed'); }
        if (check.hallConflict) { await showErrorAlert("This hall is already occupied during this time. Please select another hall or time."); throw new Error('hall-conflict'); }
        if (check.teacherConflict) { await showErrorAlert("This teacher already has a class during this time. Please select another teacher or time."); throw new Error('teacher-conflict'); }
        if (check.sameYearConflict) {
            let msg = "A class for this year already exists in this branch at this time.";
            if (check.conflictingTeacherName) msg += `\nConflicting teacher(s): ${check.conflictingTeacherName}`;
            msg += "\nDo you want to proceed?";
            const proceed = await showConfirmAlert(msg);
            if (!proceed) throw new Error('same-year-abort');
        }
        if (!formData.classPrice || formData.classPrice === 0) {
            const confirmFree = await showConfirmAlert("Are you sure you want to make this class for free?");
            if (!confirmFree) throw new Error('free-abort');
        }

        const url = isEditMode ? `/DailyClass/EditClass/${editingClassId}` : '/DailyClass/CreateClass';
        const saveRes = await fetch(url, {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formData)
        });
        const saveData = await saveRes.json();
        if (saveData.success) {
            await showSuccessAlert(isEditMode ? LC("UpdateSuccess") : LC("SaveSuccess"));
            safeHideModalById('addClassModal');
            resetFormFields();
            resetModalForCreate();
            setTimeout(() => location.reload(), 700);
        } else {
            await showErrorAlert((isEditMode ? LC("UpdateError") : LC("CreateError")) + (saveData.error || LC("UnknownError")));
            enableScreen();
        }
    } catch (e) {
        if (!['conflict-check-failed', 'hall-conflict', 'teacher-conflict', 'same-year-abort', 'free-abort'].includes(e.message)) {
            await showErrorAlert((LC("NetworkError") || "Network error: ") + e.message);
        }
        enableScreen();
    } finally {
        if (btn) { btn.innerHTML = original; btn.disabled = false; }
    }
}

// --- Screen lock ---
function disableScreen() {
    document.body.classList.add('screen-disabled');
    document.querySelectorAll('button, input, select').forEach(el => {
        if (!el.closest('.toast-container')) el.disabled = true;
    });
}
function enableScreen() {
    document.body.classList.remove('screen-disabled');
    document.querySelectorAll('button, input, select').forEach(el => {
        if (!el.closest('.toast-container')) el.disabled = false;
    });
}
// Add this helper anywhere above showEditClassModal (e.g., near other helpers)
// Add this helper anywhere above showEditClassModal (updated)
async function ensureYearInDropdown(yearVal) {
    if (yearVal === null || yearVal === undefined || yearVal === '') return;
    const sel = document.getElementById('yearCode');
    if (!sel) return;

    // If option already exists, set and dispatch change
    const exists = Array.from(sel.options).some(o => String(o.value) === String(yearVal));
    if (exists) {
        sel.value = String(yearVal);
        sel.dispatchEvent(new Event('change'));
        return;
    }

    // Otherwise fetch all years and append the missing one, then set and dispatch change
    try {
        const res = await fetch(`/DailyClass/GetYearsForEduYear`);
        const data = await res.json();
        const years = Array.isArray(data.years) ? data.years : [];
        const match = years.find(y => String(y.value) === String(yearVal));
        if (match) {
            const opt = document.createElement('option');
            opt.value = String(match.value);
            opt.textContent = match.text;
            sel.appendChild(opt);
            sel.value = String(yearVal);
            sel.dispatchEvent(new Event('change'));
        } else {
            // Fallback: at least show something if needed
            const opt = document.createElement('option');
            opt.value = String(yearVal);
            opt.textContent = `Year ${yearVal}`;
            sel.appendChild(opt);
            sel.value = String(yearVal);
            sel.dispatchEvent(new Event('change'));
        }
    } catch {
        // silent fail
    }
}
// Replace your showEditClassModal with this version (only this function is changed)
// Replace the existing showEditClassModal with this parallelized version
async function showEditClassModal(classId) {
    isEditMode = true;
    editingClassId = classId;

    let cls;
    try {
        const res = await fetch(`/DailyClass/GetClassById?id=${classId}`);
        cls = await res.json();
        if (cls.error) throw new Error(cls.error);
    } catch (e) {
        showErrorAlert(LC("ClassNotFound") + ": " + (e.message || e));
        return;
    }

    const startTimeHHmm = toHHmm(cls.classStartTime ?? cls.startTime);
    const endTimeHHmm = toHHmm(cls.classEndTime ?? cls.endTime);
    const lessonCodeVal = cls.classLessonCode ?? cls.lessonCode ?? '';
    const eduYearVal = cls.eduYearCode ?? cls.EduYearCode ?? '';
    const yearVal = cls.yearCode ?? cls.YearCode ?? '';
    const subjectVal = cls.subjectCode ?? cls.SubjectCode ?? '';
    const branchVal = cls.branchCode ?? cls.BranchCode ?? '';
    const hallVal = cls.hallCode ?? cls.HallCode ?? '';
    const teacherVal = cls.teacherCode ?? cls.TeacherCode ?? '';
    const centerVal = cls.centerCode ?? cls.CenterCode ?? null;

    if (teacherVal) userContext.teacherCode = teacherVal;

    // Prepare the modal immediately (better perceived performance)
    setupModalFields();
    isHydrating = true;
    try {
        const modalEl = document.getElementById('addClassModal');
        if (modalEl) new bootstrap.Modal(modalEl).show();

        // Set the simple fields right away
        setVal('className', cls.className ?? cls.ClassName ?? '');
        setVal('startTime', startTimeHHmm);
        setVal('endTime', endTimeHHmm);
        setVal('classPrice', cls.classPrice ?? '');
        setVal('noOfStudents', cls.noOfStudents ?? '0');

        if (userContext.isCenter) {
            // Show branch name immediately
            if (branchVal) {
                userContext.groupBranchCode = branchVal;
                const bn = document.getElementById('branchNameReadonly');
                if (bn) bn.value = cls.branchName || '';
            }

            // Kick off all required loads in parallel
            const pEduYears = (async () => { await loadEduYearsForRoot(false); setVal('eduYearCode', eduYearVal); })();
            const pTeachers = (async () => { await loadStaffTeachersForBranch(userContext.groupBranchCode); setVal('teacherCode', teacherVal); })();
            const pSubjects = (async () => { await fetchSubjectsForTeacherEduYearBranch(teacherVal, eduYearVal, userContext.groupBranchCode); setVal('subjectCode', subjectVal); })();
            const pYears = (async () => { await loadYearsForTeach(userContext.groupBranchCode, teacherVal, subjectVal, eduYearVal); })();
            const pHalls = (async () => { await loadHallsForBranch(userContext.groupBranchCode); setVal('hallCode', hallVal); })();

            // Wait for all parallel loads to finish
            await Promise.all([pEduYears, pTeachers, pSubjects, pYears, pHalls]);

            // Ensure the class YearCode exists and is selected, then trigger change
            await ensureYearInDropdown(yearVal);
        } else {
            // Non-center: load what we can in parallel (center/branches/eduYears/subjects/years)
            const pCenters = (async () => { await loadCentersForUserRoot(false); if (centerVal) setVal('centerCode', centerVal); })();
            const pBranches = (async () => { if (centerVal) { await loadBranchesByCenter(centerVal); if (branchVal) setVal('branchCode', branchVal); } })();
            const pEduYears = (async () => { await loadEduYearsForRoot(false); setVal('eduYearCode', eduYearVal); })();
            const pSubjects = (async () => { await fetchSubjectsForTeacherEduYearBranch(teacherVal || userContext.teacherCode, eduYearVal, branchVal); setVal('subjectCode', subjectVal); })();
            const pYears = (async () => { await loadYearsByEduYear(eduYearVal); })();

            // Run all together
            await Promise.all([pCenters, pBranches, pEduYears, pSubjects, pYears]);

            // Ensure the class YearCode exists and is selected, then trigger change
            await ensureYearInDropdown(yearVal);
        }

        // Now that teacher/subject/year are in the DOM, load lessons and set selected
        await loadLessonsForDropdown();
        setVal('lessonCode', lessonCodeVal);
    } finally {
        isHydrating = false;
    }
}
function resetModalForCreate() {
    isEditMode = false;
    editingClassId = null;
    setupModalFields();
    resetFormFields();

    if (!userContext.isCenter) {
        fetch('/DailyClass/GetCurrentTeacherCode')
            .then(r => r.json())
            .then(d => userContext.teacherCode = d.teacherCode)
            .catch(() => userContext.teacherCode = null);
    }

    const modalTitleEl = document.querySelector('#addClassModal .modal-title');
    if (modalTitleEl) {
        modalTitleEl.innerHTML =
            `<i class="fas fa-plus-circle me-2"></i>${LC("AddClassBtn")}` +
            (userContext.userRootName ? ` <small class="text-muted">${LC("ForUser") || "for"} ${userContext.userRootName}</small>` : '');
    }

    if (userContext.isCenter) {
        loadUserBranch().then(() => {
            if (userContext.groupBranchCode) loadEduYearsForRoot();
        });
    } else {
        loadEduYearsForRoot();
        loadCentersForUserRoot();
        disable('branchCode'); disable('subjectCode'); disable('yearCode');
    }
}

function resetFormFields() {
    const ids = ['className', 'startTime', 'endTime', 'subjectCode', 'branchCode', 'hallCode', 'eduYearCode', 'yearCode', 'classPrice'];
    if (userContext.isCenter) ids.push('teacherCode'); else ids.push('centerCode');
    const noStd = document.getElementById('noOfStudents');
    if (noStd) noStd.value = '0';
    ids.forEach(id => {
        const el = document.getElementById(id);
        if (!el) return;
        if (el.tagName === 'SELECT') el.selectedIndex = 0;
        else el.value = '';
    });
}

function setupModalFields() {
    const teacherField = document.getElementById('teacherField');
    const centerField = document.getElementById('centerField');
    const branchReadonly = document.getElementById('branchNameReadonly');
    const branchSelect = document.getElementById('branchCode');
    const hallField = document.getElementById('hallField');

    if (userContext.isCenter) {
        if (teacherField) teacherField.style.display = '';
        if (centerField) centerField.style.display = 'none';
        if (branchReadonly) branchReadonly.style.display = '';
        if (branchSelect) branchSelect.style.display = 'none';
        if (hallField) hallField.style.display = '';
    } else {
        if (teacherField) teacherField.style.display = 'none';
        if (centerField) centerField.style.display = '';
        if (branchReadonly) branchReadonly.style.display = 'none';
        if (branchSelect) branchSelect.style.display = '';
        if (hallField) hallField.style.display = 'none';
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

async function loadUserBranch() {
    try {
        const res = await fetch('/DailyClass/GetUserBranch');
        const branch = await res.json();
        if (branch.error) throw new Error(branch.error);
        const readonly = document.getElementById('branchNameReadonly');
        if (readonly) readonly.value = branch.text;
        userContext.groupBranchCode = branch.value;
    } catch {
        const readonly = document.getElementById('branchNameReadonly');
        if (readonly) readonly.value = LC("ErrorLoadingBranch");
        userContext.groupBranchCode = null;
    }
}

// --- Delete ---
async function deleteClass(classId) {
    if (!(await showConfirmAlert(LC("DeleteConfirm") || "Are you sure you want to delete this class?"))) return;
    fetch(`/DailyClass/DeleteClass?id=${classId}`, { method: 'POST' })
        .then(r => r.json())
        .then(async data => {
            if (data.success) {
                await showSuccessAlert(LC("DeleteSuccess"));
                loadDayContent();
                safeHideModalById('classDetailsModal');
            } else {
                await showErrorAlert(data.error || LC("DeleteError"));
                enableScreen();
            }
        })
        .catch(async e => {
            await showErrorAlert((LC("NetworkError") || "Network error: ") + e.message);
            enableScreen();
        });
}

// --- Details Modal ---
function showClassDetailsModal(classId) {
    editingClassId = classId;
    const cls = dailyClasses.find(c => String(c.classCode) === String(classId));
    if (!cls) { showErrorAlert(LC("ClassNotFound")); return; }
    const html = `
        <div class="class-details-list">
            <div><strong>${LC("FormStartTime")}</strong>: ${to12Hr(cls.startTime)}</div>
            <div><strong>${LC("FormEndTime")}</strong>: ${to12Hr(cls.endTime)}</div>
            <div><strong>${LC("FormSubject")}</strong>: ${cls.subjectName || ''}</div>
            <div><strong>${LC("FormEduYear")}</strong>: ${cls.eduYearName || ''}</div>
            <div><strong>${LC("FormYear")}</strong>: ${cls.yearName || ''}</div>
            <div><strong>${LC("FormTeacher")}</strong>: ${cls.teacherName || ''}</div>
            <div><strong>${LC("FormHall")}</strong>: ${cls.hallName || ''}</div>
            <div><strong>${LC("FormCenter")}</strong>: ${cls.centerName || ''}</div>
            <div><strong>${LC("FormBranch")}</strong>: ${cls.branchName || ''}</div>
            <div><strong>${LC("FormNoOfStudents")}</strong>: ${cls.noOfStudents || 0}</div>
            <div><strong>${LC("FormClassPrice")}</strong>: ${cls.classPrice || ''}</div>
        </div>`;
    const target = document.getElementById('classDetailsContent');
    if (target) target.innerHTML = html;
    new bootstrap.Modal(document.getElementById('classDetailsModal')).show();
}

// --- Utilities ---
function to12Hr(timeStr) {
    if (!timeStr) return "";
    let [h, m] = timeStr.split(':');
    h = parseInt(h, 10);
    if (isNaN(h)) return "";
    const ampm = h >= 12 ? (LC("PM") || "PM") : (LC("AM") || "AM");
    h = h % 12; if (h === 0) h = 12;
    return `${h}:${m} ${ampm}`;
}
function formatDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    try {
        return d.toLocaleDateString(getPreferredLocale() || 'en-US', { month: 'short', day: 'numeric' });
    } catch {
        return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }
}

// --- Safe modal hide ---
function safeHideModalById(id) {
    try {
        const el = document.getElementById(id);
        if (!el) return;
        if (window.bootstrap?.Modal?.getInstance) {
            const inst = window.bootstrap.Modal.getInstance(el);
            if (inst) { inst.hide(); return; }
        }
        if (window.$ && typeof window.$(`#${id}`).modal === 'function') {
            window.$(`#${id}`).modal('hide'); return;
        }
        const closeBtn = el.querySelector('.btn-close');
        if (closeBtn) closeBtn.click();
    } catch (e) {
        console.debug('safeHideModalById error', e);
    }
}