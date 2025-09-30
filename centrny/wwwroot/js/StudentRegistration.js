// Student Registration Form - Online username check & normalization fixes
// Preserves Offline flow exactly. Adds:
// - normalized username checks (trim + lowercase) everywhere
// - final availability re-check before submit to avoid "username already taken" surprises
// - when a root has account enabled, Offline mode will also show PIN + username/password creation
// NEW: If all 4 root flags are true (isOffline, isOnline, hasProfile, hasAccount) and user is in Offline flow:
//      - Hide PIN input and PIN validation button
//      - Require only Username/Password (no PIN), save credentials inactive
//      - Backend should activate after Item linking
// NEW (Online progressive flow):
//      - Show only PIN first
//      - After validating PIN -> show Year
//      - After picking Year -> show Subjects & Teachers
//      - After selecting at least one subject+teacher -> show Account Creation
//      - Hide Schedules (header + section) entirely in Online (bulletproof - never visible)
// Toggle debug: window.__regDebug = true/false

window.__regDebug = true;
function dlog(...args) { if (window.__regDebug) console.debug('[Reg]', ...args); }
function derror(...args) { if (window.__regDebug) console.error('[Reg]', ...args); }

let currentStep = 1;
let selectedSubjects = [];
let availableSubjects = [];
let availableTeachers = [];
let registrationMode = "Offline";
let pinValidated = false;
let rootFlags = window.rootFlags || null;

let scheduleLoadTimer = null;
const SCHEDULE_LOAD_DEBOUNCE_MS = 150;

let scheduleObserver = null;

function getMaxStep() {
    const steps = document.querySelectorAll('.form-step');
    return steps ? steps.length : 2;
}

/* Ensure setupEventListeners exists in the global scope before any usage */
function setupEventListeners() {
    dlog('setupEventListeners');

    const branchSelect = document.getElementById('branchCode');
    if (branchSelect) {
        branchSelect.addEventListener('change', function () {
            resetSubjectsAndTeachers();
            if (this.value && document.getElementById('yearCode')?.value) loadAvailableSubjects();
        });
    }

    const yearSelect = document.getElementById('yearCode');
    if (yearSelect) {
        yearSelect.addEventListener('change', function () {
            resetSubjectsAndTeachers();
            if (this.value && document.getElementById('branchCode')?.value) loadAvailableSubjects();
        });
    }

    const onlineYearSelect = document.getElementById('onlineYearCode');
    if (onlineYearSelect) {
        onlineYearSelect.addEventListener('change', function () {
            resetSubjectsAndTeachers();
            if (registrationMode === 'Online' && this.value && pinValidated) loadAvailableSubjects();
            updateOnlineFlowVisibility();
        });
    }

    document.querySelectorAll('input[required], select[required]').forEach(input => {
        input.addEventListener('blur', validateField);
        input.addEventListener('input', clearValidation);
    });

    const prevBtn = document.getElementById('prevBtn');
    if (prevBtn) prevBtn.addEventListener('click', () => changeStep(-1));

    const nextBtn = document.getElementById('nextBtn');
    if (nextBtn) nextBtn.addEventListener('click', () => changeStep(1));

    const submitBtn = document.getElementById('submitBtn');
    if (submitBtn) submitBtn.addEventListener('click', () => submitRegistration());

    const validatePinBtn = document.getElementById('validatePinBtn');
    if (validatePinBtn) validatePinBtn.addEventListener('click', validatePin);

    const validatePinAndUsernameBtn = document.getElementById('validatePinAndUsernameBtn');
    if (validatePinAndUsernameBtn) validatePinAndUsernameBtn.addEventListener('click', validatePinAndUsername);

    const usernameInput = document.getElementById('username');
    if (usernameInput) {
        usernameInput.addEventListener('blur', async () => {
            const raw = usernameInput.value || '';
            const normalized = normalizeUsername(raw);
            if (normalized !== raw) usernameInput.value = normalized;
            const check = await isUsernameAvailable(normalized);
            const ue = document.getElementById('userError');
            if (!check.available) {
                if (ue) ue.textContent = check.error || 'Username is not available.';
            } else {
                if (ue) ue.textContent = '';
            }
        });
    }

    const completeOnlineBtn = document.getElementById('completeOnlineBtn');
    if (completeOnlineBtn) completeOnlineBtn.addEventListener('click', submitOnlineRegistration);
}
window.setupEventListeners = setupEventListeners; // make globally accessible just in case

document.addEventListener('DOMContentLoaded', function () {
    dlog('DOMContentLoaded - init');
    rootFlags = window.rootFlags || rootFlags || null;

    updateStepButtons();
    setupEventListeners();
    initModeToggle();
    applyRootFlags();

    // Ensure CSS hook present ASAP
    toggleRootModeClass();
    modeChangeHandler();
    ensureScrollEnabled();

    // Global guards
    window.addEventListener('error', function (evt) {
        derror('Uncaught error:', evt.message, 'at', evt.filename + ':' + evt.lineno + ':' + evt.colno);
    });
    window.addEventListener('unhandledrejection', function (evt) {
        derror('Unhandled rejection:', evt.reason);
    });

    // Prime lookups then enforce visibility
    loadInitialLookups().then(() => {
        dlog('Initial lookups done');
        updateScheduleVisibility();
        updateOnlineFlowVisibility(); // ensure initial visibility is correct if Online
    });

    // Final first-paint enforcement and MutationObserver
    setTimeout(() => {
        toggleRootModeClass();
        if (registrationMode === 'Online') hideSchedulesHard(false); // don't destroy, just hide
        setupScheduleMutationObserver();
        updateOnlineFlowVisibility();
    }, 0);
});

// ------------------ Helpers & Lookups ------------------
async function loadInitialLookups() {
    try {
        dlog('loadInitialLookups start, rootCode=', window.registrationRootCode);

        // Branches
        try {
            const res = await fetch('/Student/GetAvailableBranches?rootCode=' + (window.registrationRootCode || '0'));
            const branches = await res.json();
            dlog('GetAvailableBranches returned', branches);
            const select = document.getElementById('branchCode');
            if (select && Array.isArray(branches)) {
                select.innerHTML = `<option value="">اختر الفرع</option>`;
                branches.forEach(branch => {
                    const option = document.createElement('option');
                    option.value = branch.Value ?? branch.value;
                    option.textContent = branch.Text ?? branch.text;
                    select.appendChild(option);
                });
            }
        } catch (ex) {
            console.warn('Failed to load branches', ex);
        }

        // Years (offline + online)
        try {
            const res2 = await fetch('/Student/GetYearsForEduYear');
            const years = await res2.json();
            dlog('GetYearsForEduYear returned', years);
            const selectY = document.getElementById('yearCode');
            const selectOnlineY = document.getElementById('onlineYearCode');
            if (selectY && Array.isArray(years)) {
                selectY.innerHTML = `<option value="">اختر السنة</option>`;
                years.forEach(year => {
                    const option = document.createElement('option');
                    option.value = year.Value ?? year.value;
                    option.textContent = year.Text ?? year.text;
                    selectY.appendChild(option);
                });
            }
            if (selectOnlineY && Array.isArray(years)) {
                selectOnlineY.innerHTML = `<option value="">اختر السنة</option>`;
                years.forEach(year => {
                    const option = document.createElement('option');
                    option.value = year.Value ?? year.value;
                    option.textContent = year.Text ?? year.text;
                    selectOnlineY.appendChild(option);
                });
            }
        } catch (ex) {
            console.warn('Failed to load years', ex);
        }

        // Edu Years
        try {
            const res3 = await fetch('/Student/GetAvailableEduYears?rootCode=' + (window.registrationRootCode || '0'));
            const eduyears = await res3.json();
            dlog('GetAvailableEduYears returned', eduyears);
            if (Array.isArray(eduyears) && eduyears.length > 0) {
                const display = document.getElementById('eduYearCodeDisplay');
                const hidden = document.getElementById('eduYearCode');
                if (display) display.value = eduyears[0].Text ?? eduyears[0].text ?? '';
                if (hidden) hidden.value = eduyears[0].Value ?? eduyears[0].value ?? '';
                const onlineDisplay = document.getElementById('onlineEduYearCodeDisplay');
                const onlineHidden = document.getElementById('onlineEduYearCode');
                if (onlineDisplay) onlineDisplay.value = eduyears[0].Text ?? eduyears[0].text ?? '';
                if (onlineHidden) onlineHidden.value = eduyears[0].Value ?? eduyears[0].value ?? '';
            }
        } catch (ex) {
            console.warn('Failed to load edu years', ex);
        }
    } catch (err) {
        console.error('Initial lookups failed', err);
    }
}

function showLoading() { const overlay = document.getElementById('loadingOverlay'); if (overlay) overlay.style.display = 'flex'; }
function hideLoading() { const overlay = document.getElementById('loadingOverlay'); if (overlay) overlay.style.display = 'none'; }

function showAlert(message, type = 'info') {
    try {
        const stepContent = document.getElementById(`step${currentStep}Content`) || document.querySelector('.form-box-wrapper') || document.body;
        (stepContent).querySelectorAll && stepContent.querySelectorAll('.alert.temp').forEach(a => a.remove());
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} temp`;
        alertDiv.style.marginBottom = '12px';
        alertDiv.innerHTML = `<i class="fas ${type === 'success' ? 'fa-check-circle' : type === 'danger' ? 'fa-exclamation-triangle' : 'fa-info-circle'} me-2"></i>${message}`;
        if (stepContent && stepContent.prepend) {
            stepContent.prepend(alertDiv);
            if (type !== 'danger') setTimeout(() => alertDiv.remove(), 5000);
        } else { alert(message); }
    } catch (ex) { console.warn('showAlert failed', ex); try { alert(message); } catch { } }
}

function ensureScrollEnabled() { document.documentElement.style.overflowY = 'auto'; document.body.style.overflowY = 'auto'; }

function escapeHtml(s) { if (s == null) return ''; return String(s).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", "&#39;"); }
function escapeHtmlAttr(s) { return escapeHtml(String(s ?? '')).replaceAll(' ', '&#32;'); }
function escapeJs(s) { if (s == null) return ''; return String(s).replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/"/g, '\\"'); }

function normalizeUsername(u) {
    if (!u) return '';
    return String(u).trim().toLowerCase();
}

async function isUsernameAvailable(usernameRaw) {
    const username = normalizeUsername(usernameRaw);
    if (!username) return { available: false, error: 'Username is required.' };
    try {
        dlog('Checking username availability for:', username);
        const res = await fetch(`/Student/CheckUsername?username=${encodeURIComponent(username)}`);
        const data = await res.json();
        dlog('CheckUsername response', data);
        if (!data || typeof data.available === 'undefined') {
            return { available: false, error: 'Unable to validate username availability.' };
        }
        return { available: !!data.available, error: data.error || null };
    } catch (ex) {
        derror('isUsernameAvailable error', ex);
        return { available: false, error: 'Network error checking username.' };
    }
}

function isAllFourTrue() {
    const f = rootFlags || {};
    return !!(f.isOffline && f.isOnline && f.hasProfile && f.hasAccount);
}

function getHeaderByLocalizeKey(key) {
    const span = document.querySelector(`span[data-localize="${key}"]`);
    return span ? span.closest('h4') : null;
}

function getHeaderBeforeSection(sectionEl) {
    if (!sectionEl) return null;
    let node = sectionEl.previousElementSibling;
    while (node) {
        if (node.tagName && node.tagName.toLowerCase() === 'h4') return node;
        node = node.previousElementSibling;
    }
    node = sectionEl.parentElement;
    while (node) {
        const h4 = node.querySelector('h4');
        if (h4) return h4;
        node = node.parentElement;
    }
    return null;
}

// Bulletproof: hard-hide schedules when in Online (no destroy by default)
function hideSchedulesHard(destroy = false) {
    let header = document.getElementById('scheduleHeader') || document.querySelector('span[data-localize="step4_title"]')?.closest('h4');
    let section = document.getElementById('scheduleSection');
    const selection = document.getElementById('scheduleSelection');

    if (header) {
        header.style.display = 'none';
        header.setAttribute('aria-hidden', 'true');
        if (destroy && header.parentNode) header.parentNode.removeChild(header);
    }
    if (section) {
        section.style.display = 'none';
        section.setAttribute('aria-hidden', 'true');
        if (selection) selection.innerHTML = '';
        if (destroy && section.parentNode) section.parentNode.removeChild(section);
    }
}

// Recreate schedule DOM if it was removed
function ensureScheduleDom() {
    let header = document.getElementById('scheduleHeader');
    let section = document.getElementById('scheduleSection');
    let selection = document.getElementById('scheduleSelection');
    const accountSection = document.getElementById('accountCreationSection');
    const container = accountSection ? accountSection.parentElement : document.getElementById('step2Content');

    // Insert before account section if possible
    let insertBeforeEl = accountSection || null;

    if (!header) {
        header = document.createElement('h4');
        header.id = 'scheduleHeader';
        header.className = 'mb-4 text-purple-1 fw-600 d-flex align-items-center gap-2';
        header.style.fontSize = '18px';
        header.innerHTML = '<i class="fas fa-calendar"></i> <span data-localize="step4_title">اختيار الجداول</span>';
        if (container) {
            if (insertBeforeEl) container.insertBefore(header, insertBeforeEl);
            else container.appendChild(header);
        }
    }
    if (!section) {
        section = document.createElement('div');
        section.id = 'scheduleSection';
        if (insertBeforeEl && insertBeforeEl.parentElement === container) {
            container.insertBefore(section, insertBeforeEl);
        } else if (container) {
            container.appendChild(section);
        }
    }
    if (section && !selection) {
        selection = document.createElement('div');
        selection.id = 'scheduleSelection';
        selection.className = 'schedule-list';
        section.appendChild(selection);
    }
    return { header, section, selection };
}

// MutationObserver: re-hide if anything tries to show schedules in Online mode
function setupScheduleMutationObserver() {
    try {
        if (scheduleObserver) scheduleObserver.disconnect();
        if (registrationMode !== 'Online') return; // only observe in Online
        scheduleObserver = new MutationObserver(() => {
            if (registrationMode === 'Online') hideSchedulesHard(false);
        });
        scheduleObserver.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['style', 'class']
        });
        dlog('Schedule MutationObserver attached');
    } catch (e) {
        console.warn('Failed to attach MutationObserver', e);
    }
}

function updateOnlineFlowVisibility() {
    const onlinePinSection = document.getElementById('onlinePinSection');
    const onlineYearSection = document.getElementById('onlineYearSection');
    const scheduleSection = document.getElementById('scheduleSection');
    const subjectsContainer = document.getElementById('availableSubjects');
    const subjectsSummary = document.getElementById('selectedSubjectsSummary');
    const accountSection = document.getElementById('accountCreationSection');

    const subjectsHeaderByKey = getHeaderByLocalizeKey('step3_title');
    const scheduleHeaderByKey = getHeaderByLocalizeKey('step4_title');
    const subjectsHeaderByPrev = getHeaderBeforeSection(subjectsContainer);
    const scheduleHeaderByPrev = getHeaderBeforeSection(scheduleSection);

    const explicitScheduleHeader = document.getElementById('scheduleHeader');
    const subjectsHeader = subjectsHeaderByKey || subjectsHeaderByPrev;
    const scheduleHeader = explicitScheduleHeader || scheduleHeaderByKey || scheduleHeaderByPrev;

    const onlineYearSelected = (document.getElementById('onlineYearCode')?.value || '').trim() !== '';
    const hasSubjectTeacherSelection = selectedSubjects.some(s => s.teacherCode);

    if (registrationMode !== 'Online') {
        // Ensure DOM exists (in case it was removed in a previous version)
        const ensured = ensureScheduleDom();
        if (subjectsHeader) subjectsHeader.style.display = '';
        if (subjectsContainer) subjectsContainer.style.display = '';
        if (subjectsSummary) subjectsSummary.style.display = selectedSubjects.length ? '' : 'none';
        if (ensured.header) ensured.header.style.display = '';
        if (ensured.section) ensured.section.style.display = '';
        return;
    }

    if (onlinePinSection) onlinePinSection.style.display = '';
    if (onlineYearSection) onlineYearSection.style.display = pinValidated ? '' : 'none';

    const showSubjects = pinValidated && onlineYearSelected;
    if (subjectsHeader) subjectsHeader.style.display = showSubjects ? '' : 'none';
    if (subjectsContainer) subjectsContainer.style.display = showSubjects ? '' : 'none';
    if (subjectsSummary) subjectsSummary.style.display = showSubjects && selectedSubjects.length ? '' : 'none';

    // Force hide schedules in Online (and keep them empty)
    hideSchedulesHard(false);

    if (accountSection) {
        accountSection.style.display = (showSubjects && hasSubjectTeacherSelection) ? '' : 'none';
    }
}

// ---------------- Root flags & mode ----------------
function getSingleTeacher(teachers) {
    if (!Array.isArray(teachers) || teachers.length === 0) return null;
    const code = teachers[0].teacherCode ?? teachers[0].TeacherCode;
    return teachers.every(t => (t.teacherCode ?? t.TeacherCode) === code)
        ? (teachers[0].teacherCode ? teachers[0] : { teacherCode: teachers[0].TeacherCode, teacherName: teachers[0].TeacherName })
        : null;
}

function applyRootFlags() {
    try {
        rootFlags = window.rootFlags || rootFlags || null;
        const group = document.getElementById('modeToggleGroup');
        const modeInput = document.getElementById('modeInput');
        if (!rootFlags) {
            if (group) group.classList.remove('mode-hidden');
            registrationMode = modeInput?.value || registrationMode;
            return;
        }
        const isOffline = !!rootFlags.isOffline;
        const isOnline = !!rootFlags.isOnline;
        if (isOffline && !isOnline) {
            if (group) group.classList.add('mode-hidden');
            registrationMode = 'Offline';
            if (modeInput) modeInput.value = 'Offline';
        } else if (isOnline && !isOffline) {
            if (group) group.classList.add('mode-hidden');
            registrationMode = 'Online';
            if (modeInput) modeInput.value = 'Online';
        } else {
            if (group) group.classList.remove('mode-hidden');
            registrationMode = modeInput?.value || 'Offline';
            if (modeInput) modeInput.value = registrationMode;
        }
        toggleAccountCreationSectionIfNeeded();
        dlog('applyRootFlags ->', registrationMode, rootFlags);
    } catch (ex) { console.warn('applyRootFlags error', ex); }
}

function toggleAccountCreationSectionIfNeeded() {
    const accountSection = document.getElementById('accountCreationSection');
    if (!accountSection) return;
    const hasAccount = !!(rootFlags && rootFlags.hasAccount);
    const offlineNoPin = registrationMode === 'Offline' && isAllFourTrue();

    if (registrationMode === 'Online') {
        accountSection.style.display = 'none';
        const pinAccountInput = document.getElementById('pinCodeAccount');
        if (pinAccountInput) {
            const wrapper = pinAccountInput.closest('.mb-3') || pinAccountInput.parentNode;
            if (wrapper) wrapper.style.display = 'none';
            pinAccountInput.value = '';
            const pinErrAcc = document.getElementById('pinErrorAccount');
            if (pinErrAcc) pinErrAcc.textContent = '';
            const pinOkAcc = document.getElementById('pinOkAccount');
            if (pinOkAcc) pinOkAcc.style.display = 'none';
        }
        const validateBtn = document.getElementById('validatePinAndUsernameBtn');
        if (validateBtn) validateBtn.style.display = '';
    } else {
        accountSection.style.display = hasAccount ? '' : 'none';

        const pinAccountInput = document.getElementById('pinCodeAccount');
        if (pinAccountInput) {
            const wrapper = pinAccountInput.closest('.mb-3') || pinAccountInput.parentNode;
            if (wrapper) wrapper.style.display = offlineNoPin ? 'none' : '';
            if (offlineNoPin) {
                pinAccountInput.value = '';
                const pinErrAcc = document.getElementById('pinErrorAccount');
                if (pinErrAcc) pinErrAcc.textContent = '';
                const pinOkAcc = document.getElementById('pinOkAccount');
                if (pinOkAcc) pinOkAcc.style.display = 'none';
            }
        }

        const validateBtn = document.getElementById('validatePinAndUsernameBtn');
        if (validateBtn) validateBtn.style.display = offlineNoPin ? 'none' : '';
    }

  
}

function initModeToggle() {
    const group = document.getElementById('modeToggleGroup');
    const modeInput = document.getElementById('modeInput');
    if (!group || !modeInput) {
        document.querySelectorAll('input[name="Mode"]').forEach(r => {
            r.removeEventListener('change', modeChangeHandler);
            r.addEventListener('change', modeChangeHandler);
        });
        return;
    }
    const btns = group.querySelectorAll('.mode-toggle-btn');
    btns.forEach(btn => {
        btn.addEventListener('click', function () {
            btns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            const mode = btn.getAttribute('data-mode') || 'Offline';
            modeInput.value = mode;
            registrationMode = mode;
            toggleRootModeClass();
            toggleAccountCreationSectionIfNeeded();
            modeChangeHandler();
        });
        btn.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); btn.click(); }
        });
    });
    const active = group.querySelector('.mode-toggle-btn.active');
    if (active) { const initial = active.getAttribute('data-mode') || 'Offline'; modeInput.value = initial; registrationMode = initial; }
}

function toggleRootModeClass() {
    const root = document.getElementById('htmlRoot') || document.documentElement;
    if (!root) return;
    if (registrationMode === 'Online') root.classList.add('mode-online');
    else root.classList.remove('mode-online');
}

function modeChangeHandler() {
    const modeInput = document.getElementById('modeInput');
    const checkedRadio = document.querySelector('input[name="Mode"]:checked');
    registrationMode = modeInput ? (modeInput.value || registrationMode) : (checkedRadio ? checkedRadio.value : registrationMode);

    const branchSection = document.getElementById('branchSection');
    const onlinePinSection = document.getElementById('onlinePinSection');
    const onlineYearSection = document.getElementById('onlineYearSection');
    const onlineUserSection = document.getElementById('onlineUserSection');
    const branchInput = document.getElementById('branchCode');
    const yearInput = document.getElementById('yearCode');

    function show(el) { if (!el) return; el.classList.remove('hidden-by-js'); el.classList.add('visible-by-js'); el.setAttribute('aria-hidden', 'false'); el.style.display = ''; }
    function hide(el) { if (!el) return; el.classList.add('hidden-by-js'); el.classList.remove('visible-by-js'); el.setAttribute('aria-hidden', 'true'); el.style.display = 'none'; }

    if (registrationMode === "Online") {
        hide(branchSection);
        show(onlinePinSection);
        if (pinValidated) show(onlineYearSection); else hide(onlineYearSection);
        hide(onlineUserSection);
        if (branchInput) branchInput.required = false; if (yearInput) yearInput.required = false;
    } else {
        show(branchSection);
        hide(onlinePinSection);
        hide(onlineYearSection);
        hide(onlineUserSection);
        if (branchInput) branchInput.required = true; if (yearInput) yearInput.required = true;
    }

    // Reset UI selections
    resetSubjectsAndTeachers();

    // Toggle CSS hook and observer every time mode changes
    toggleRootModeClass();
    setupScheduleMutationObserver();

    // If back to Offline and branch/year already chosen, reload subjects immediately
    if (registrationMode === 'Offline') {
        ensureScheduleDom(); // make sure the schedule DOM exists
        const branchVal = document.getElementById('branchCode')?.value;
        const yearVal = document.getElementById('yearCode')?.value;
        if (branchVal && yearVal) {
            loadAvailableSubjects();
        }
    }

    updateStepButtons();
    toggleAccountCreationSectionIfNeeded();
    updateScheduleVisibility();
    updateOnlineFlowVisibility();

    // Extra hard hide for Online (do not destroy to allow returning to Offline safely)
    if (registrationMode === 'Online') hideSchedulesHard(false);
}

// ---------------- Steps & Validation ----------------
function changeStep(direction) {
    const maxStep = getMaxStep();
    if (direction === 1 && !validateCurrentStep()) return;

    const currentStepContent = document.getElementById(`step${currentStep}Content`); if (currentStepContent) currentStepContent.classList.remove('active');
    const currentStepIndicator = document.getElementById(`step${currentStep}`); if (currentStepIndicator) currentStepIndicator.classList.remove('active');

    currentStep += direction;
    if (currentStep < 1) currentStep = 1;
    if (currentStep > maxStep) currentStep = maxStep;

    const newStepContent = document.getElementById(`step${currentStep}Content`); if (newStepContent) newStepContent.classList.add('active');
    const newStepIndicator = document.getElementById(`step${currentStep}`); if (newStepIndicator) newStepIndicator.classList.add('active');

    if (currentStep === 2) {
        const branchVal = document.getElementById('branchCode')?.value;
        const yearVal = document.getElementById('yearCode')?.value || document.getElementById('onlineYearCode')?.value;
        if ((registrationMode === 'Offline' && branchVal && yearVal) || (registrationMode === 'Online' && pinValidated && document.getElementById('onlineYearCode')?.value)) {
            loadAvailableSubjects();
        } else if (registrationMode === 'Online') {
            dlog('Online mode step2: waiting for PIN validation and year selection before loading subjects');
        }
        updateScheduleVisibility();
        updateOnlineFlowVisibility();

        // Ensure hide in Online without destroying DOM
        if (registrationMode === 'Online') hideSchedulesHard(false);
    }

    updateStepButtons();
    scrollToFormTop();
    dlog('changeStep ->', currentStep);
}

function scrollToFormTop() { const box = document.getElementById('registrationRootBox'); if (box) { const top = box.getBoundingClientRect().top + window.scrollY - 20; window.scrollTo({ top, behavior: 'smooth' }); } else window.scrollTo({ top: 0, behavior: 'smooth' }); }
function updateStepButtons() { const maxStep = getMaxStep(); const prevBtn = document.getElementById('prevBtn'); const nextBtn = document.getElementById('nextBtn'); const submitBtn = document.getElementById('submitBtn'); if (prevBtn) prevBtn.style.display = currentStep > 1 ? 'inline-flex' : 'none'; if (nextBtn) nextBtn.style.display = currentStep < maxStep ? 'inline-flex' : 'none'; if (submitBtn) submitBtn.style.display = (currentStep === maxStep) ? 'inline-flex' : 'none'; }

function validateCurrentStep() {
    const currentStepContent = document.getElementById(`step${currentStep}Content`);
    if (!currentStepContent) return false;
    const requiredFields = currentStepContent.querySelectorAll('input[required], select[required]');
    let isValid = true;
    requiredFields.forEach(field => { if (!validateField({ target: field })) isValid = false; });

    if (currentStep === 1) return isValid;

    if (currentStep === 2) {
        if (registrationMode === "Offline") {
            const branchCode = document.getElementById('branchCode')?.value;
            const yearCode = document.getElementById('yearCode')?.value;
            if (!branchCode || !yearCode) { showAlert('Please select both branch and academic year.', 'danger'); isValid = false; }
        } else {
            if (!pinValidated) { showAlert('Please validate your PIN code first.', 'danger'); isValid = false; }
            const onlineYearVal = document.getElementById('onlineYearCode')?.value;
            if (!onlineYearVal) { showAlert('Please select academic year for Online registration.', 'danger'); isValid = false; }
        }

        const completeSelections = selectedSubjects.filter(s => s.teacherCode);
        if (completeSelections.length === 0) { showAlert('Please select at least one subject and a teacher.', 'danger'); isValid = false; }

        if (registrationMode === "Offline") {
            const missingSchedules = selectedSubjects.filter(s => s.teacherCode && !s.scheduleCode);
            if (missingSchedules.length > 0) { showAlert('Please select schedules for all chosen subjects.', 'danger'); updateScheduleVisibility(); isValid = false; }
        }

        const accountVisible = document.getElementById('accountCreationSection') && document.getElementById('accountCreationSection').style.display !== 'none';
        const allFour = isAllFourTrue();
        if (registrationMode === "Online" || (rootFlags && rootFlags.hasAccount && accountVisible)) {
            const usernameVal = normalizeUsername(document.getElementById('username')?.value || '');
            const password = document.getElementById('password')?.value;
            const confirm = document.getElementById('passwordConfirm')?.value;
            if (!usernameVal || !password) { showAlert('Username and password are required.', 'danger'); isValid = false; }
            else if (password !== confirm) { showAlert('Passwords do not match.', 'danger'); isValid = false; }
            else if (rootFlags && rootFlags.hasAccount && registrationMode === "Offline" && !allFour && !pinValidated) {
                showAlert('Please validate the PIN and username using the provided button.', 'danger');
                isValid = false;
            }
        }
    }
    return isValid;
}

function validateField(event) {
    const field = event.target;
    const value = (field.value || '').toString().trim();
    let isValid = true;
    let message = '';
    field.classList.remove('is-invalid');
    const feedback = field.parentNode ? field.parentNode.querySelector('.invalid-feedback') : null;
    if (field.hasAttribute('required') && !value) { isValid = false; message = 'This field is required.'; }
    if (value && field.type === 'tel' && !/^[\+]?[0-9\s\-\(\)]{8,}$/.test(value)) { isValid = false; message = 'Enter a valid phone number.'; }
    if (value && field.type === 'email' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) { isValid = false; message = 'Enter a valid email address.'; }
    if (!isValid) { field.classList.add('is-invalid'); if (feedback) feedback.textContent = message; }
    return isValid;
}
function clearValidation(event) { const field = event.target; field.classList.remove('is-invalid'); const feedback = field.parentNode ? field.parentNode.querySelector('.invalid-feedback') : null; if (feedback) feedback.textContent = ''; }

// ---------------- Subjects / Teachers / Schedules ----------------
function resetSubjectsAndTeachers() {
    selectedSubjects = []; availableSubjects = []; availableTeachers = [];
    const subjectsDiv = document.getElementById('availableSubjects'); if (subjectsDiv) subjectsDiv.innerHTML = '';
    const schedulesDiv = document.getElementById('scheduleSelection'); if (schedulesDiv) schedulesDiv.innerHTML = '';
    const summaryDiv = document.getElementById('selectedSubjectsSummary'); if (summaryDiv) summaryDiv.style.display = 'none';
    updateOnlineFlowVisibility();
}

async function loadAvailableSubjects() {
    if (registrationMode === "Online" && !pinValidated) {
        dlog('loadAvailableSubjects aborted - Online PIN not validated');
        return;
    }

    let branchCode = '', yearCode = '';
    if (registrationMode === "Offline") {
        branchCode = document.getElementById('branchCode')?.value || '';
        yearCode = document.getElementById('yearCode')?.value || '';
        if (!branchCode || !yearCode) { resetSubjectsAndTeachers(); updateScheduleVisibility(); dlog('loadAvailableSubjects aborted - branch/year missing', branchCode, yearCode); return; }
    } else if (registrationMode === "Online") {
        yearCode = document.getElementById('onlineYearCode')?.value || '';
        branchCode = '0';
        if (!yearCode) { resetSubjectsAndTeachers(); updateScheduleVisibility(); dlog('loadAvailableSubjects aborted - onlineYear missing'); return; }
    }

    dlog('loadAvailableSubjects -> branchCode, yearCode =', branchCode, yearCode);
    showLoading();
    try {
        const url = `/Student/GetAvailableSubjects?branchCode=${encodeURIComponent(branchCode)}&yearCode=${encodeURIComponent(yearCode)}`;
        dlog('fetch', url);
        const response = await fetch(url);
        const result = await response.json();
        dlog('GetAvailableSubjects response', result);
        if (result && result.error) { showAlert(result.error, 'danger'); availableSubjects = []; }
        else availableSubjects = Array.isArray(result) ? result.map(s => ({ subjectCode: s.SubjectCode ?? s.subjectCode, subjectName: s.SubjectName ?? s.subjectName })) : [];
        renderSubjects();
    } catch (err) {
        showAlert('Failed to load subjects.', 'danger'); console.error('loadAvailableSubjects error', err); availableSubjects = [];
    } finally { hideLoading(); updateScheduleVisibility(); updateOnlineFlowVisibility(); }
}

async function loadTeachersForSubjects() {
    let branchCode = '', yearCode = '';
    if (registrationMode === "Offline") { branchCode = document.getElementById('branchCode')?.value || ''; yearCode = document.getElementById('yearCode')?.value || ''; }
    else { yearCode = document.getElementById('onlineYearCode')?.value || ''; branchCode = '0'; }

    const subjectCodes = availableSubjects.map(s => s.subjectCode).join(',');
    dlog('loadTeachersForSubjects -> subjectCodes, branchCode, yearCode =', subjectCodes, branchCode, yearCode);
    if (!subjectCodes) { availableTeachers = []; return; }

    try {
        const url = `/Student/GetTeachersForSubjects?subjectCodes=${encodeURIComponent(subjectCodes)}&branchCode=${encodeURIComponent(branchCode)}&yearCode=${encodeURIComponent(yearCode)}`;
        dlog('fetch', url);
        const response = await fetch(url);
        const result = await response.json();
        dlog('GetTeachersForSubjects response', result);
        if (result && result.error) { console.warn(result.error); availableTeachers = []; }
        else availableTeachers = Array.isArray(result) ? result.map(t => ({
            teacherCode: t.TeacherCode ?? t.teacherCode,
            teacherName: t.TeacherName ?? t.teacherName,
            teacherPhone: t.TeacherPhone ?? t.teacherPhone,
            subjectCode: t.SubjectCode ?? t.subjectCode,
            yearCode: t.YearCode ?? t.yearCode,
            eduYearCode: t.EduYearCode ?? t.eduYearCode
        })) : [];
    } catch (err) { console.error('loadTeachersForSubjects error', err); availableTeachers = []; }
    window.rootSingleTeacher = availableTeachers.length === 1 ? availableTeachers[0] : null;
}

async function loadScheduleForSubjectTeacher(subjectCode, teacherCode) {
    let branchCode = '', yearCode = '';
    if (registrationMode === "Offline") { branchCode = document.getElementById('branchCode')?.value || ''; yearCode = document.getElementById('yearCode')?.value || ''; }
    const url = `/Student/GetSchedulesForSubjectTeacher?subjectCode=${encodeURIComponent(subjectCode)}&teacherCode=${encodeURIComponent(teacherCode)}&branchCode=${encodeURIComponent(branchCode)}&yearCode=${encodeURIComponent(yearCode)}`;
    dlog('loadScheduleForSubjectTeacher fetch', url);
    try {
        const res = await fetch(url);
        if (!res.ok) { derror('schedules fetch http error', res.status, await res.text()); return []; }
        const result = await res.json();
        dlog('GetSchedulesForSubjectTeacher response', { subjectCode, teacherCode, result });
        if (result && result.error) { derror(result.error); return []; }
        return Array.isArray(result) ? result.map(sch => ({
            scheduleCode: sch.ScheduleCode ?? sch.scheduleCode,
            scheduleName: sch.ScheduleName ?? sch.scheduleName,
            dayOfWeek: sch.DayOfWeek ?? sch.dayOfWeek,
            startTime: sch.StartTime ?? sch.startTime,
            endTime: sch.EndTime ?? sch.endTime,
            hallName: sch.HallName ?? sch.hallName
        })) : [];
    } catch (err) { derror('Failed to load schedules:', err); return []; }
}

async function loadAvailableSchedules() {
    // Block entirely in Online mode
    if (registrationMode === 'Online') {
        hideSchedulesHard(false);
        dlog('loadAvailableSchedules skipped in Online');
        return;
    }

    dlog('loadAvailableSchedules start');
    // Ensure DOM exists (in case it was hidden/recreated)
    ensureScheduleDom();

    const container = document.getElementById('scheduleSelection');
    if (!container) { dlog('loadAvailableSchedules: no container found'); return; }

    const scheduleSection = document.getElementById('scheduleSection');
    if (scheduleSection && registrationMode === "Offline") scheduleSection.style.display = "";

    const subjectsWithTeachers = selectedSubjects.filter(s => s.teacherCode);

    if (subjectsWithTeachers.length === 0) {
        container.innerHTML = `<div class="empty-block"><i class="fas fa-calendar-times"></i><p>No subjects with teachers selected.</p></div>`;
        dlog('loadAvailableSchedules: no selected subjects with teachers');
        return;
    }

    container.innerHTML = '<div class="loading-inline"><i class="fas fa-spinner fa-spin"></i> Loading schedules...</div>';
    let scheduleHtml = '';

    for (const subject of subjectsWithTeachers) {
        const schedules = await loadScheduleForSubjectTeacher(subject.subjectCode, subject.teacherCode);
        scheduleHtml += `<div class="schedule-subject-block"><h5><i class="fas fa-book me-2"></i>${escapeHtml(subject.subjectName)} - ${escapeHtml(subject.teacherName)}</h5><div class="schedule-options">`;
        if (Array.isArray(schedules) && schedules.length > 0) {
            schedules.forEach(sch => {
                const isSelected = subject.scheduleCode == sch.scheduleCode;
                scheduleHtml += `
          <label class="schedule-option ${isSelected ? 'selected' : ''}">
            <input type="radio"
                  name="schedule_${escapeHtmlAttr(subject.subjectCode)}_${escapeHtmlAttr(subject.teacherCode)}"
                  value="${escapeHtmlAttr(sch.scheduleCode)}"
                  ${isSelected ? 'checked' : ''}
                  data-subject="${escapeHtmlAttr(subject.subjectCode)}"
                  data-teacher="${escapeHtmlAttr(subject.teacherCode)}"
                  data-schedule="${escapeHtmlAttr(sch.scheduleCode)}"
                  data-scheduletitle="${escapeHtmlAttr(sch.scheduleName)}">
            <div class="so-meta"><strong>${escapeHtml(sch.scheduleName)}</strong>
              <small>${escapeHtml(`${sch.dayOfWeek} ${sch.startTime}-${sch.endTime} | ${sch.hallName}`)}</small>
            </div>
          </label>`;
            });
        } else {
            scheduleHtml += '<p class="text-muted small mb-0">No schedules available.</p>';
        }
        scheduleHtml += `</div></div>`;
    }

    container.innerHTML = scheduleHtml;

    container.querySelectorAll('.schedule-option input[type="radio"]').forEach(inp => {
        inp.addEventListener('change', function () {
            const subj = this.getAttribute('data-subject');
            const teach = this.getAttribute('data-teacher');
            const sched = this.getAttribute('data-schedule');
            const title = this.getAttribute('data-scheduletitle');
            selectSchedule(subj, teach, sched, title, '', '', '');
            const parent = this.closest('.schedule-option'); parent && parent.classList.add('selected');
            parent && parent.parentNode && Array.from(parent.parentNode.querySelectorAll('.schedule-option')).forEach(sib => { if (sib !== parent) sib.classList.remove('selected'); });
        });
    });

    dlog('loadAvailableSchedules finished, rendered schedules for', subjectsWithTeachers.length, 'subjects');
}

// ---------------- Render Subjects ----------------
function renderSubjects() {
    const container = document.getElementById('availableSubjects');
    if (!container) { dlog('renderSubjects: no container'); return; }

    if (!availableSubjects || availableSubjects.length === 0) {
        container.innerHTML = `<div class="empty-block"><i class="fas fa-book"></i><p>No subjects available for the selected criteria.</p></div>`;
        updateScheduleVisibility();
        updateOnlineFlowVisibility();
        return;
    }

    loadTeachersForSubjects().then(() => {
        dlog('renderSubjects -> availableSubjects:', availableSubjects.length, 'availableTeachers:', availableTeachers.length);
        let html = '';

        availableSubjects.forEach(subject => {
            const isSelected = selectedSubjects.some(s => String(s.subjectCode) === String(subject.subjectCode));
            const subjectTeachers = availableTeachers.filter(t => String(t.subjectCode) === String(subject.subjectCode));
            const singleTeacher = getSingleTeacher(subjectTeachers);

            html += `<div class="subject-selection ${isSelected ? 'selected' : ''}" data-subject-code="${escapeHtmlAttr(subject.subjectCode)}">`;
            html += `<div class="subject-header"><h5 class="subject-title"><i class="fas fa-book me-2"></i>${escapeHtml(subject.subjectName)}</h5>`;
            html += `<div class="form-check"><input class="form-check-input subject-checkbox" type="checkbox" data-subject="${escapeHtmlAttr(subject.subjectCode)}" id="subject_${escapeHtmlAttr(subject.subjectCode)}" ${isSelected ? 'checked' : ''}></div></div>`;
            html += `<div class="teachers-list ${isSelected ? '' : 'd-none'}" id="teachers_${escapeHtmlAttr(subject.subjectCode)}">`;

            if (singleTeacher) {
                html += `<span class="text-muted small">Assigned teacher: <strong>${escapeHtml(singleTeacher.teacherName)}</strong></span>`;
            } else {
                html += `<label class="form-label small mb-2">Select Teacher</label>`;
                if (subjectTeachers.length > 0) {
                    subjectTeachers.forEach(teacher => {
                        const selectedSubject = selectedSubjects.find(s => String(s.subjectCode) === String(subject.subjectCode));
                        const isTeacherSelected = selectedSubject && String(selectedSubject.teacherCode) === String(teacher.teacherCode);
                        html += `<div class="teacher-option ${isTeacherSelected ? 'selected' : ''}" data-subject="${escapeHtmlAttr(subject.subjectCode)}" data-subjectname="${escapeHtmlAttr(subject.subjectName)}" data-teacher="${escapeHtmlAttr(teacher.teacherCode)}" data-teachername="${escapeHtmlAttr(teacher.teacherName)}" data-year="${escapeHtmlAttr(teacher.yearCode)}" data-eduyear="${escapeHtmlAttr(teacher.eduYearCode)}">`;
                        html += `<div class="form-check"><input class="form-check-input teacher-radio" type="radio" name="teacher_${escapeHtmlAttr(subject.subjectCode)}" value="${escapeHtmlAttr(teacher.teacherCode)}" id="teacher_${escapeHtmlAttr(subject.subjectCode)}_${escapeHtmlAttr(teacher.teacherCode)}" ${isTeacherSelected ? 'checked' : ''}>`;
                        html += `<label class="form-check-label"><strong>${escapeHtml(teacher.teacherName)}</strong><br><small class="text-muted">${escapeHtml(teacher.teacherPhone || 'N/A')}</small></label></div></div>`;
                    });
                } else {
                    html += '<p class="text-muted small mb-0">No teachers available.</p>';
                }
            }

            html += `</div></div>`;
        });

        container.innerHTML = html;

        container.querySelectorAll('.subject-checkbox').forEach(cb => {
            cb.removeEventListener('change', _subjectCheckboxHandler);
            cb.addEventListener('change', _subjectCheckboxHandler);
        });

        container.querySelectorAll('.teacher-option').forEach(el => {
            el.removeEventListener('click', _teacherClickHandler);
            el.addEventListener('click', _teacherClickHandler);
        });

        container.querySelectorAll('.teacher-radio').forEach(r => {
            r.removeEventListener('change', _radioChangeHandler);
            r.addEventListener('change', _radioChangeHandler);
        });

        updateScheduleVisibility();
        updateOnlineFlowVisibility();
    }).catch(err => { console.error('renderSubjects error', err); updateScheduleVisibility(); updateOnlineFlowVisibility(); });

    function _subjectCheckboxHandler(evt) {
        const subj = evt.currentTarget.getAttribute('data-subject');
        const subjName = availableSubjects.find(s => String(s.subjectCode) === String(subj))?.subjectName || '';
        toggleSubject(subj, subjName);
    }
    function _teacherClickHandler(evt) {
        try {
            const el = evt.currentTarget;
            const subj = el.getAttribute('data-subject');
            const subjName = el.getAttribute('data-subjectname');
            const teacher = el.getAttribute('data-teacher');
            const teacherName = el.getAttribute('data-teachername');
            const year = el.getAttribute('data-year');
            const eduYear = el.getAttribute('data-eduyear');
            const radio = el.querySelector('input[type="radio"]');
            if (radio) radio.checked = true;
            selectTeacher(subj, subjName, teacher, teacherName, year, eduYear);
        } catch (e) { console.error('teacher click handler error', e); }
    }
    function _radioChangeHandler(evt) {
        try {
            const r = evt.currentTarget;
            const parent = r.closest('.teacher-option');
            if (!parent) return;
            const subj = parent.getAttribute('data-subject');
            const subjName = parent.getAttribute('data-subjectname');
            const teacher = parent.getAttribute('data-teacher');
            const teacherName = parent.getAttribute('data-teachername');
            const year = parent.getAttribute('data-year');
            const eduYear = parent.getAttribute('data-eduyear');
            selectTeacher(subj, subjName, teacher, teacherName, year, eduYear);
        } catch (e) { console.error('radio change handler error', e); }
    }
}

// ---------------- Selection Handlers ----------------
window.toggleSubject = function (subjectCode, subjectName) {
    const checkbox = document.getElementById(`subject_${subjectCode}`);
    const subjectCard = document.querySelector(`[data-subject-code="${subjectCode}"]`);
    const teachersDiv = document.getElementById(`teachers_${subjectCode}`);

    const subjKey = String(subjectCode);
    if (checkbox && checkbox.checked) {
        if (!selectedSubjects.some(s => String(s.subjectCode) === subjKey)) {
            selectedSubjects.push({
                subjectCode: subjectCode,
                subjectName: subjectName || '',
                teacherCode: null,
                teacherName: null,
                yearCode: null,
                eduYearCode: null,
                scheduleCode: null,
                scheduleName: null
            });
        }
        subjectCard && subjectCard.classList.add('selected');
        teachersDiv && teachersDiv.classList.remove('d-none');

        const subjectTeachers = availableTeachers.filter(t => String(t.subjectCode) === subjKey);
        const singleTeacher = getSingleTeacher(subjectTeachers);
        if (singleTeacher) {
            selectTeacher(subjectCode, subjectName, singleTeacher.teacherCode, singleTeacher.teacherName, singleTeacher.yearCode, singleTeacher.eduYearCode);
            return;
        }
    } else {
        selectedSubjects = selectedSubjects.filter(s => String(s.subjectCode) !== subjKey);
        subjectCard && subjectCard.classList.remove('selected');
        teachersDiv && teachersDiv.classList.add('d-none');
        document.querySelectorAll(`input[name="teacher_${escapeHtmlAttr(subjectCode)}"]`).forEach(r => r.checked = false);
    }

    updateSelectedSubjectsSummary();
    updateScheduleVisibility();
    updateOnlineFlowVisibility();
};

window.selectTeacher = function (subjectCode, subjectName, teacherCode, teacherName, yearCode, eduYearCode) {
    try {
        dlog('selectTeacher called', { subjectCode, teacherCode, teacherName, yearCode, eduYearCode });

        const subj = (subjectCode !== null && subjectCode !== undefined && String(subjectCode).trim() !== '') ? (isFinite(subjectCode) ? Number(subjectCode) : String(subjectCode)) : subjectCode;
        const teach = (teacherCode !== null && teacherCode !== undefined && String(teacherCode).trim() !== '') ? (isFinite(teacherCode) ? Number(teacherCode) : String(teacherCode)) : teacherCode;

        let finalYear = yearCode ?? (registrationMode === 'Online' ? (document.getElementById('onlineYearCode')?.value || null) : (document.getElementById('yearCode')?.value || null));
        let finalEduYear = eduYearCode ?? (registrationMode === 'Online' ? (document.getElementById('onlineEduYearCode')?.value || null) : (document.getElementById('eduYearCode')?.value || null));

        const idx = selectedSubjects.findIndex(s => String(s.subjectCode) === String(subj));
        const newEntry = {
            subjectCode: subj,
            subjectName: subjectName || '',
            teacherCode: teach,
            teacherName: teacherName || '',
            yearCode: finalYear,
            eduYearCode: finalEduYear,
            scheduleCode: null,
            scheduleName: null
        };
        if (idx !== -1) selectedSubjects[idx] = { ...selectedSubjects[idx], ...newEntry };
        else selectedSubjects.push(newEntry);

        try {
            const radioId = `teacher_${subj}_${teach}`;
            const radio = document.getElementById(radioId);
            if (radio) radio.checked = true;
            else dlog('selectTeacher: radio element not found for id=', radioId);

            document.querySelectorAll(`#teachers_${escapeHtmlAttr(String(subj))} .teacher-option`).forEach(o => o.classList.remove('selected'));
            const clicked = document.getElementById(`teacher_${escapeHtmlAttr(String(subj))}_${escapeHtmlAttr(String(teach))}`)?.closest('.teacher-option');
            if (clicked) clicked.classList.add('selected');
        } catch (e) { }

        updateSelectedSubjectsSummary();

        if (scheduleLoadTimer) clearTimeout(scheduleLoadTimer);
        scheduleLoadTimer = setTimeout(() => {
            try {
                updateScheduleVisibility();
                updateOnlineFlowVisibility();
            } catch (e) {
                derror('updateScheduleVisibility failed inside selectTeacher', e);
                if (typeof loadAvailableSchedules === 'function') loadAvailableSchedules();
            }
        }, SCHEDULE_LOAD_DEBOUNCE_MS);
    } catch (ex) {
        derror('selectTeacher exception', ex);
    }
};

window.selectSchedule = function (subjectCode, teacherCode, scheduleCode, scheduleName, dayOfWeek, startTime, endTime) {
    const idx = selectedSubjects.findIndex(s => String(s.subjectCode) === String(subjectCode) && String(s.teacherCode) === String(teacherCode));
    if (idx !== -1) {
        selectedSubjects[idx].scheduleCode = scheduleCode;
        selectedSubjects[idx].scheduleName = scheduleName || `${dayOfWeek} ${startTime}-${endTime}`;
    }
    updateSelectedSubjectsSummary();
};

window.removeSubject = function (subjectCode) {
    const checkbox = document.getElementById(`subject_${subjectCode}`);
    if (checkbox) { checkbox.checked = false; toggleSubject(subjectCode, ''); }
};



// ---------------- Summary UI ----------------
function updateSelectedSubjectsSummary() {
    const container = document.getElementById('selectedSubjectsSummary');
    const list = document.getElementById('selectedSubjectsList');
    if (!container || !list) return;
    if (selectedSubjects.length === 0) { container.style.display = 'none'; list.innerHTML = ''; return; }

    list.innerHTML = selectedSubjects.map(s => `
    <div class="summary-item">
      <div class="si-info">
        <strong>${escapeHtml(s.subjectName)}</strong>
        ${s.teacherName ? `<br><small class="text-muted">${escapeHtml(s.teacherName)}</small>` : ''}
        ${s.scheduleName ? `<br><small class="text-info">${escapeHtml(s.scheduleName)}</small>` : ''}
      </div>
      <button type="button" class="btn-remove" onclick="removeSubject('${escapeJs(s.subjectCode)}')"><i class="fas fa-times"></i></button>
    </div>`).join('');
    container.style.display = 'block';
}

// ---------------- Schedule visibility ----------------
function updateScheduleVisibility() {
    try {
        // Ensure DOM exists (especially after switching back from Online)
        const ensured = ensureScheduleDom();
        const scheduleSection = ensured.section || document.getElementById('scheduleSection');

        const scheduleHeaderByKey = getHeaderByLocalizeKey('step4_title');
        const scheduleHeaderByPrev = getHeaderBeforeSection(scheduleSection);
        const explicitScheduleHeader = document.getElementById('scheduleHeader');
        const scheduleHeader = explicitScheduleHeader || scheduleHeaderByKey || scheduleHeaderByPrev;

        if (!scheduleSection) { dlog('updateScheduleVisibility: scheduleSection not found'); return; }

        if (registrationMode === "Offline") {
            if (scheduleHeader) scheduleHeader.style.display = '';
            const hasSelection = Array.isArray(selectedSubjects) && selectedSubjects.some(s => s.teacherCode);
            if (hasSelection) {
                scheduleSection.style.display = "";
                if (scheduleLoadTimer) clearTimeout(scheduleLoadTimer);
                scheduleLoadTimer = setTimeout(() => {
                    loadAvailableSchedules();
                }, SCHEDULE_LOAD_DEBOUNCE_MS);
            } else {
                scheduleSection.style.display = "none";
                const container = document.getElementById('scheduleSelection');
                if (container) container.innerHTML = `<div class="empty-block"><i class="fas fa-calendar-times"></i><p>No subjects with teachers selected.</p></div>`;
            }
        } else {
            // Online: fully hide header + section and ensure they remain empty
            hideSchedulesHard(false);
        }
    } catch (ex) {
        console.warn('updateScheduleVisibility error', ex);
    }
}

// ---------------- PIN / username / submission ----------------
async function validatePin() {
    const pin = document.getElementById('pinCode')?.value?.trim() || '';
    if (!pin) { setPinError("PIN code is required."); return; }
    setPinError(''); showLoading();
    try {
        const res = await fetch('/Student/ValidatePin', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ pin }) });
        const data = await res.json();
        if (data && data.valid) {
            document.getElementById('pinError').textContent = '';
            pinValidated = true;
            const onlineYearSection = document.getElementById('onlineYearSection');
            if (onlineYearSection) onlineYearSection.style.display = '';
            const pinOk = document.getElementById('pinOk');
            if (pinOk) pinOk.style.display = '';
            showAlert('PIN validated successfully. Select academic year.', 'success');
            dlog('PIN validated, pinValidated=true');

            const onlineYear = document.getElementById('onlineYearCode')?.value;
            if (onlineYear) loadAvailableSubjects();

            toggleAccountCreationSectionIfNeeded();
            updateOnlineFlowVisibility();
            if (registrationMode === 'Online') hideSchedulesHard(false);
        } else {
            setPinError(data?.error || 'Invalid PIN.');
            pinValidated = false;
            updateOnlineFlowVisibility();
        }
    } catch (e) { setPinError('Network error. Please try again.'); pinValidated = false; derror('validatePin error', e); } finally { hideLoading(); }
}
function setPinError(msg) { const el = document.getElementById('pinError'); if (el) el.textContent = msg; }

// validatePinAndUsername used for centers' offline account creation flow.
async function validatePinAndUsername() {
    const pinField = registrationMode === 'Online' ? document.getElementById('pinCode') : document.getElementById('pinCodeAccount');
    const pin = pinField?.value?.trim();
    const usernameRaw = document.getElementById('username')?.value || '';
    const username = normalizeUsername(usernameRaw);
    const password = document.getElementById('password')?.value;
    const confirm = document.getElementById('passwordConfirm')?.value;

    if (!pin) { const pe = document.getElementById('pinErrorAccount'); if (pe) pe.textContent = "PIN is required."; return; }
    if (!username) { document.getElementById('userError') && (document.getElementById('userError').textContent = "Username is required."); return; }
    if (!password || !confirm) { document.getElementById('pwMismatch') && (document.getElementById('pwMismatch').style.display = ''); return; }
    if (password !== confirm) { document.getElementById('pwMismatch') && (document.getElementById('pwMismatch').style.display = ''); return; }
    document.getElementById('pwMismatch') && (document.getElementById('pwMismatch').style.display = 'none');

    const usernameInput = document.getElementById('username');
    if (usernameInput && usernameInput.value !== username) usernameInput.value = username;

    showLoading();
    try {
        const pinRes = await fetch('/Student/ValidatePin', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ pin }) });
        const pinData = await pinRes.json();
        if (!(pinData && pinData.valid)) { document.getElementById('pinErrorAccount') && (document.getElementById('pinErrorAccount').textContent = pinData?.error || "Invalid PIN"); document.getElementById('pinOkAccount') && (document.getElementById('pinOkAccount').style.display = 'none'); hideLoading(); return; }
        document.getElementById('pinErrorAccount') && (document.getElementById('pinErrorAccount').textContent = "");
        document.getElementById('pinOkAccount') && (document.getElementById('pinOkAccount').style.display = '');

        const avail = await isUsernameAvailable(username);
        if (!avail.available) {
            document.getElementById('userError') && (document.getElementById('userError').textContent = avail.error || "Username not available.");
            hideLoading();
            return;
        }

        document.getElementById('userError') && (document.getElementById('userError').textContent = "");
        pinValidated = true;
        showAlert('PIN and username validated. You can now submit the registration.', 'success');
    } catch (ex) { derror('validatePinAndUsername error', ex); showAlert('Network error during validation. Please try again.', 'danger'); } finally { hideLoading(); }
}

async function checkUsername() {
    const usernameRaw = document.getElementById('username')?.value || '';
    const username = normalizeUsername(usernameRaw);
    const ue = document.getElementById('userError');

    const usernameInput = document.getElementById('username');
    if (usernameInput && usernameInput.value !== username) usernameInput.value = username;

    if (!username) { if (ue) ue.textContent = ''; return; }
    showLoading();
    try {
        const check = await isUsernameAvailable(username);
        if (!check.available) {
            if (ue) ue.textContent = check.error || 'Username is not available.';
        } else {
            if (ue) ue.textContent = '';
        }
    } catch (ex) { if (ue) ue.textContent = 'Network error checking username.'; derror('checkUsername error', ex); } finally { hideLoading(); }
}

async function submitOnlineRegistration() {
    const usernameInput = document.getElementById('username');
    const usernameRaw = usernameInput?.value || '';
    const username = normalizeUsername(usernameRaw);
    if (usernameInput && usernameInput.value !== username) usernameInput.value = username;

    if (!username) {
        await Swal.fire({ icon: 'error', title: 'Missing username', text: 'Username is required for online registration.' });
        usernameInput && usernameInput.focus();
        return;
    }

    const avail = await isUsernameAvailable(username);
    if (!avail.available) {
        await Swal.fire({ icon: 'error', title: 'Username not available', text: avail.error || 'This username is already taken.' });
        usernameInput && usernameInput.focus();
        return;
    } else {
        document.getElementById('userError') && (document.getElementById('userError').textContent = '');
    }

    const formData = getFormData(); formData.Mode = "Online";
    formData.Username = username;
    formData.PinCode = document.getElementById('pinCode')?.value?.trim();
    formData.Password = document.getElementById('password')?.value;

    if (!formData.Username || !formData.Password) {
        await Swal.fire({ icon: 'error', title: 'Missing credentials', text: 'Username and password are required.' });
        return;
    }

    const completeSelections = selectedSubjects.filter(s => s.teacherCode);
    if (completeSelections.length === 0) { await Swal.fire({ icon: 'error', title: 'Selection required', text: 'Please select at least one subject and a teacher.' }); return; }

    dlog('Submitting Online registration with Username:', formData.Username);
    showLoading();
    try {
        const root_code = getRootCodeFromUrl();
        const response = await fetch(`/Register/${root_code}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formData) });
        const result = await response.json();
        dlog('Register response', result);
        if (result && result.success) {
            await Swal.fire({ icon: 'success', title: 'Registration Successful!', text: result.message || "Your online registration is complete." });
            window.location.href = result.redirectUrl || '/';
        } else {
            const errMsg = result?.error || 'Registration failed.';
            dlog('Server rejected registration:', result, 'usernameSent=', formData.Username);
            await Swal.fire({ icon: 'error', title: 'Registration Failed', text: errMsg });
            const ue = document.getElementById('userError');
            if (ue) ue.textContent = result?.error || '';
        }
    } catch (ex) {
        derror('submitOnlineRegistration error', ex);
        await Swal.fire({ icon: 'error', title: 'Network Error', text: 'Network error. Please try again.' });
    } finally { hideLoading(); }
}

async function submitRegistration() {
    if (registrationMode === "Online") { await submitOnlineRegistration(); return; }
    const terms = document.getElementById('termsAccepted'); if (terms && !terms.checked) { await Swal.fire({ icon: 'error', title: 'Terms required', text: 'You must accept the terms and conditions.' }); return; }

    const prevStep = currentStep; currentStep = 2; if (!validateCurrentStep()) { currentStep = prevStep; return; } currentStep = prevStep;

    const requiresAccount = !!(rootFlags && rootFlags.hasAccount);
    const allFour = isAllFourTrue();

    if (requiresAccount) {
        const usernameInput = document.getElementById('username');
        const usernameRaw = usernameInput?.value || '';
        const usernameNorm = normalizeUsername(usernameRaw);
        if (usernameInput && usernameInput.value !== usernameNorm) usernameInput.value = usernameNorm;

        const password = document.getElementById('password')?.value;
        const confirm = document.getElementById('passwordConfirm')?.value;

        if (!usernameNorm || !password || !confirm) {
            await Swal.fire({ icon: 'error', title: 'Missing data', text: 'Account credentials are required for this center.' });
            return;
        }
        if (password !== confirm) {
            await Swal.fire({ icon: 'error', title: 'Passwords mismatch', text: 'Passwords do not match.' });
            return;
        }

        showLoading();
        try {
            const avail = await isUsernameAvailable(usernameNorm);
            if (!avail.available) {
                hideLoading();
                await Swal.fire({ icon: 'error', title: 'Username not available', text: avail.error || 'This username is already taken.' });
                usernameInput && usernameInput.focus();
                return;
            } else {
                document.getElementById('userError') && (document.getElementById('userError').textContent = '');
            }

            if (!allFour) {
                if (!pinValidated) {
                    hideLoading();
                    await Swal.fire({
                        icon: 'warning',
                        title: '',
                        text: 'يجب ان تتحقق من اسم المستخدم و الرمز اولا'
                    });
                    return;
                }
            }
        } catch (ex) {
            hideLoading();
            await Swal.fire({ icon: 'error', title: 'Network Error', text: 'Network error validating username or PIN.' });
            return;
        } finally {
            hideLoading();
        }
    }

    const formData = getFormData(); formData.Mode = registrationMode;
    showLoading();
    try {
        const root_code = getRootCodeFromUrl();
        const response = await fetch(`/Register/${root_code}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(formData) });
        const text = await response.text();
        if (!response.ok) { await Swal.fire({ icon: 'error', title: 'Server Error', text: `Server error: ${response.status}` }); console.error('submitRegistration http error', response.status, text); return; }
        let result;
        try { result = JSON.parse(text); } catch (e) { await Swal.fire({ icon: 'error', title: 'Server Error', text: 'Invalid response from server' }); console.error('submitRegistration parse error', e, text); return; }
        if (result && result.success) { await Swal.fire({ icon: 'success', title: 'Registration Successful!', text: result.message || "Welcome!" }); window.location.href = result.redirectUrl || `/Register/${root_code}/Success`; }
        else {
            const errMsg = result?.error || 'Registration failed.';
            await Swal.fire({ icon: 'error', title: 'Registration Failed', text: errMsg });
        }
    } catch (error) { await Swal.fire({ icon: 'error', title: 'Network Error', text: `Network error: ${error.message}` }); console.error('submitRegistration error', error); } finally { hideLoading(); }
}

// ---------------- Form data ----------------
function getFormData() {
    const genderRadio = document.querySelector('input[name="Gender"]:checked');
    let genderValue = null; if (genderRadio) genderValue = genderRadio.value === 'true';

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

    const subjectsArray = [], teachersArray = [], schedulesArray = [];
    selectedSubjects.forEach(s => {
        if (s.teacherCode && (registrationMode === "Online" || (s.yearCode != null && s.eduYearCode != null))) {
            subjectsArray.push(s.subjectCode);
            teachersArray.push(s.teacherCode);
            if (registrationMode === "Offline") schedulesArray.push(s.scheduleCode ? s.scheduleCode : 0);
        }
    });

    const rootCode = getRootCodeFromUrl();
    const usernameInput = document.getElementById('username');
    const usernameNorm = usernameInput ? normalizeUsername(usernameInput.value || '') : null;
    if (usernameInput && usernameInput.value !== usernameNorm) usernameInput.value = usernameNorm;

    const allFour = isAllFourTrue();
    const pinForPayload = (registrationMode === "Offline" && allFour)
        ? null
        : ((document.getElementById('pinCodeAccount')?.value || document.getElementById('pinCode')?.value || '').trim() || null);

    return {
        RootCode: rootCode ? parseInt(rootCode) : 0,
        StudentName: (document.getElementById('studentName')?.value || '').trim(),
        StudentPhone: (document.getElementById('studentPhone')?.value || '').trim(),
        StudentFatherPhone: (document.getElementById('parentPhone')?.value || '').trim(),
        StudentMotherPhone: (document.getElementById('motherPhone')?.value || '').trim(),
        StudentFatherJob: (document.getElementById('fatherJob')?.value || '').trim(),
        StudentMotherJob: (document.getElementById('motherJob')?.value || '').trim(),
        BirthDate: document.getElementById('birthDate')?.value,
        Gender: genderValue,
        Mode: registrationMode,
        BranchCode: branchCodeValue ? parseInt(branchCodeValue) : null,
        YearCode: yearCodeValue ? parseInt(yearCodeValue) : null,
        EduYearCode: eduYearCodeValue ? parseInt(eduYearCodeValue) : null,
        SelectedSubjects: subjectsArray,
        SelectedTeachers: teachersArray,
        SelectedSchedules: registrationMode === "Online" ? [] : schedulesArray,
        PinCode: pinForPayload,
        Username: usernameNorm || null,
        Password: document.getElementById('password')?.value || null
    };
}

function getRootCodeFromUrl() { const match = window.location.pathname.match(/\/Register\/(\d+)/i); return match ? match[1] : null; }