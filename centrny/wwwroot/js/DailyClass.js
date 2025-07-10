// CALENDAR DAY VIEW JAVASCRIPT WITH TIME GROUPING

let currentDate = new Date('2025-07-09');
let dailyClasses = [];
let isEditMode = false;
let editingClassId = null;
let weeklyGenerationStatus = null;

// OPTIMIZATION: Cache management
let classesCache = new Map();
let dropdownCache = null;
let cacheTimeout = 5 * 60 * 1000; // 5 minutes

// Store user context from server (will be updated from view)
const userContext = {
    currentUserRootCode: null,
    userRootName: "hossamadel7",
    isCenter: false,
    hasError: false
};

console.log('User Context:', userContext);

// Initialize user context from page elements
function initializeUserContext() {
    try {
        const rootCodeElement = document.getElementById('rootCode');
        const isCenterElement = document.getElementById('isCenter');

        if (rootCodeElement) {
            userContext.currentUserRootCode = rootCodeElement.value;
        }

        if (isCenterElement) {
            userContext.isCenter = isCenterElement.value === 'True';
        }

        // Check for error banner
        const errorBanner = document.querySelector('.error-banner');
        userContext.hasError = !!errorBanner;

        console.log('Updated User Context:', userContext);
    } catch (error) {
        console.warn('Could not initialize user context:', error);
    }
}

// OPTIMIZATION: Parallel loading instead of sequential
document.addEventListener('DOMContentLoaded', function () {
    // Initialize user context first
    initializeUserContext();

    // Set current date from page if available
    const datePickerValue = document.getElementById('datePicker')?.value;
    if (datePickerValue) {
        currentDate = new Date(datePickerValue + 'T00:00:00');
    }

    if (!userContext.hasError) {
        setupEventListeners();

        // Load critical data in parallel
        Promise.all([
            loadDailyClassesOptimized(),
            loadDropdownDataOptimized()
        ]).then(() => {
            hideInitialLoader();
            showWelcomeMessage();

            // Load non-critical features after main content
            setTimeout(() => {
                checkWeeklyGenerationStatusOptimized();
                conditionalAutoGenerate();
            }, 500);
        }).catch(error => {
            console.error('Error during initial load:', error);
            hideInitialLoader();
            showErrorToast('Error loading page data');
        });
    } else {
        hideInitialLoader();
    }
});

function setupEventListeners() {
    // Date navigation
    document.getElementById('prevDayBtn')?.addEventListener('click', () => navigateDate(-1));
    document.getElementById('nextDayBtn')?.addEventListener('click', () => navigateDate(1));
    document.getElementById('datePicker')?.addEventListener('change', onDatePickerChange);

    // Modal events
    document.getElementById('addClassModal')?.addEventListener('hidden.bs.modal', resetModalForCreate);
    document.getElementById('saveClassBtn')?.addEventListener('click', saveClass);
}

// OPTIMIZATION: Debounced navigation with caching
function navigateDate(days) {
    currentDate.setDate(currentDate.getDate() + days);
    updateDateDisplay();

    // Debounce the class loading
    clearTimeout(window.navigationTimeout);
    window.navigationTimeout = setTimeout(() => {
        loadDailyClassesOptimized();
    }, 150);
}

function onDatePickerChange() {
    const selectedDate = document.getElementById('datePicker').value;
    currentDate = new Date(selectedDate + 'T00:00:00');
    updateDateDisplay();
    loadDailyClassesOptimized();
}

function goToToday() {
    currentDate = new Date();
    updateDateDisplay();
    loadDailyClassesOptimized();
}

function updateDateDisplay() {
    const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
    const formattedDate = currentDate.toLocaleDateString('en-US', options);
    const dayOfWeek = currentDate.toLocaleDateString('en-US', { weekday: 'long' });
    const isoDate = currentDate.toISOString().split('T')[0];

    const displayDateElement = document.getElementById('displayDate');
    const displayDayOfWeekElement = document.getElementById('displayDayOfWeek');
    const datePickerElement = document.getElementById('datePicker');
    const calendarDateElement = document.getElementById('calendarDate');

    if (displayDateElement) displayDateElement.textContent = formattedDate;
    if (displayDayOfWeekElement) displayDayOfWeekElement.textContent = dayOfWeek;
    if (datePickerElement) datePickerElement.value = isoDate;
    if (calendarDateElement) calendarDateElement.textContent = formattedDate;

    // Update form date info
    const formDateElement = document.getElementById('formSelectedDate');
    if (formDateElement) {
        formDateElement.textContent = formattedDate;
    }

    const classDateInput = document.getElementById('classDate');
    if (classDateInput) {
        classDateInput.value = isoDate;
    }
}

// OPTIMIZATION: Cached loading with timeout
async function loadDailyClassesOptimized() {
    if (userContext.hasError) {
        console.log('User has no root assignment - skipping classes load');
        return;
    }

    const dateStr = currentDate.toISOString().split('T')[0];
    const cacheKey = `classes_${dateStr}_${userContext.currentUserRootCode}`;

    // Check cache first
    const cached = classesCache.get(cacheKey);
    if (cached && (Date.now() - cached.timestamp) < cacheTimeout) {
        console.log('Using cached classes data');
        dailyClasses = cached.data;
        renderCalendarDayView();
        return;
    }

    showLoadingOverlay();

    try {
        const response = await fetch(`/DailyClass/GetDailyClasses?date=${dateStr}`, {
            signal: AbortSignal.timeout(10000) // 10 second timeout
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.error) {
            throw new Error(data.error);
        }

        dailyClasses = Array.isArray(data) ? data : [];

        // Cache the result
        classesCache.set(cacheKey, {
            data: dailyClasses,
            timestamp: Date.now()
        });

        // Clean old cache entries
        cleanCache();

        console.log(`Loaded ${dailyClasses.length} classes for date ${dateStr}`);
        renderCalendarDayView();

    } catch (error) {
        console.error('Error fetching daily classes:', error);
        showErrorToast('Failed to load classes: ' + error.message);
    } finally {
        hideLoadingOverlay();
    }
}

// OPTIMIZATION: Async dropdown loading with caching
async function loadDropdownDataOptimized() {
    if (dropdownCache && (Date.now() - dropdownCache.timestamp) < cacheTimeout) {
        console.log('Using cached dropdown data');
        populateDropdownsFromCache(dropdownCache.data);
        return;
    }

    try {
        const response = await fetch('/DailyClass/GetDropdownData');
        if (!response.ok) throw new Error('Failed to load dropdown data');

        const data = await response.json();

        if (data.error) {
            console.warn('Dropdown data error:', data.error);
            return;
        }

        // Cache the data
        dropdownCache = {
            data: data,
            timestamp: Date.now()
        };

        populateDropdownsFromCache(data);

    } catch (error) {
        console.error('Error loading dropdown data:', error);
    }
}

function populateDropdownsFromCache(data) {
    // Populate dropdowns efficiently using document fragments
    populateSelectAsync('subjectCode', data.subjects);
    populateSelectAsync('yearCode', data.years);
    populateSelectAsync('hallCode', data.halls);
    populateSelectAsync('eduYearCode', data.eduYears);

    if (userContext.isCenter) {
        populateSelectAsync('teacherCode', data.teachers);
        populateSelectAsync('branchCode', data.branches);
    } else {
        populateSelectAsync('centerCode', data.centers);
        populateSelectAsync('branchCode', data.branches);
    }
}

function populateSelectAsync(selectId, options) {
    const select = document.getElementById(selectId);
    if (!select || !options) return;

    // Clear existing options except first
    select.innerHTML = '<option value="">Select...</option>';

    // Add options efficiently using document fragment
    const fragment = document.createDocumentFragment();
    options.forEach(option => {
        const optionElement = document.createElement('option');
        optionElement.value = option.value;
        optionElement.textContent = option.text;
        fragment.appendChild(optionElement);
    });

    select.appendChild(fragment);
}

// CALENDAR DAY VIEW RENDERING WITH TIME GROUPING
function renderCalendarDayView() {
    const classesContent = document.getElementById('classesContent');

    if (!classesContent) return;

    if (dailyClasses.length === 0) {
        classesContent.innerHTML = `
            <div class="empty-day-state">
                <i class="fas fa-calendar-times"></i>
                <h4>No classes scheduled</h4>
                <p>Click "Add New Class" to schedule your first class for this day.</p>
            </div>
        `;
        return;
    }

    // Sort classes by start time
    const sortedClasses = [...dailyClasses].sort((a, b) => {
        return a.startTime.localeCompare(b.startTime);
    });

    // Group classes by start time
    const groupedClasses = {};
    sortedClasses.forEach(cls => {
        const timeKey = `${cls.startTime}-${cls.endTime}`;
        if (!groupedClasses[timeKey]) {
            groupedClasses[timeKey] = {
                startTime: cls.startTime,
                endTime: cls.endTime,
                startTime12: cls.startTime12,
                endTime12: cls.endTime12,
                classes: []
            };
        }
        groupedClasses[timeKey].classes.push(cls);
    });

    // Render time groups
    let html = '';
    Object.values(groupedClasses).forEach(group => {
        html += renderTimeGroup(group);
    });

    classesContent.innerHTML = html;
}

function renderTimeGroup(group) {
    const timeDisplay = `${group.startTime12} - ${group.endTime12}`;
    const timePeriod = getTimePeriod(group.startTime);

    let html = `
        <div class="time-group">
            <div class="time-group-header">
                <h5><i class="fas fa-clock me-2"></i>${timeDisplay}</h5>
                <div class="time-period">${timePeriod}</div>
            </div>
            <div class="classes-grid">
    `;

    group.classes.forEach(cls => {
        html += renderClassCard(cls);
    });

    html += `
            </div>
        </div>
    `;

    return html;
}

function renderClassCard(cls) {
    // Determine what information to show based on user type
    let organizationInfo = '';
    let teacherInfo = '';

    if (userContext.isCenter) {
        // Center users see teacher information
        teacherInfo = cls.teacherName || '';
    } else {
        // Teacher users see center and branch information
        const centerPart = cls.centerName || '';
        const branchPart = cls.branchName || '';
        organizationInfo = [centerPart, branchPart].filter(part => part).join(' • ');
    }

    // Determine class type badge
    const classType = cls.classType || 'direct';
    const badgeClass = `badge-${classType}`;
    const badgeText = classType === 'schedule' ? 'Recurring' :
        classType === 'reservation' ? 'Reserved' : 'Direct';

    return `
        <div class="class-card" onclick="showClassDetails(${cls.classCode})" data-class-id="${cls.classCode}">
            <div class="class-type-badge ${badgeClass}">${badgeText}</div>
            
            <div class="class-card-header">
                <div class="class-title">
                    <i class="fas fa-chalkboard-teacher"></i>
                    ${cls.title}
                </div>
                <div class="class-time">
                    <i class="fas fa-clock"></i>
                    ${cls.startTime12} - ${cls.endTime12}
                </div>
            </div>

            <div class="class-card-body">
                ${teacherInfo ? `
                    <div class="class-detail-item">
                        <i class="fas fa-user-tie class-detail-icon"></i>
                        <span class="class-detail-label">Teacher:</span>
                        <span class="class-detail-value">${teacherInfo}</span>
                    </div>
                ` : ''}
                
                ${organizationInfo ? `
                    <div class="class-detail-item">
                        <i class="fas fa-building class-detail-icon"></i>
                        <span class="class-detail-label">Location:</span>
                        <span class="class-detail-value">${organizationInfo}</span>
                    </div>
                ` : ''}
                
                ${cls.hallName ? `
                    <div class="class-detail-item">
                        <i class="fas fa-map-marker-alt class-detail-icon"></i>
                        <span class="class-detail-label">Hall:</span>
                        <span class="class-detail-value">${cls.hallName}</span>
                    </div>
                ` : ''}
                
                ${cls.subjectName ? `
                    <div class="class-detail-item">
                        <i class="fas fa-book class-detail-icon"></i>
                        <span class="class-detail-label">Subject:</span>
                        <span class="class-detail-value">${cls.subjectName}</span>
                    </div>
                ` : ''}
                
                ${cls.noOfStudents !== undefined ? `
                    <div class="class-detail-item">
                        <i class="fas fa-users class-detail-icon"></i>
                        <span class="class-detail-label">Students:</span>
                        <span class="class-detail-value">${cls.noOfStudents}</span>
                    </div>
                ` : ''}
                
                ${cls.totalAmount ? `
                    <div class="class-detail-item">
                        <i class="fas fa-dollar-sign class-detail-icon"></i>
                        <span class="class-detail-label">Amount:</span>
                        <span class="class-detail-value">${cls.totalAmount}</span>
                    </div>
                ` : ''}
            </div>
        </div>
    `;
}

function getTimePeriod(startTime) {
    const hour = parseInt(startTime.split(':')[0]);

    if (hour >= 6 && hour < 12) {
        return 'Morning';
    } else if (hour >= 12 && hour < 18) {
        return 'Afternoon';
    } else {
        return 'Evening';
    }
}

// ==================== WEEKLY GENERATION FUNCTIONS ====================

async function conditionalAutoGenerate() {
    const today = new Date();
    const isSaturday = today.getDay() === 6;

    if (isSaturday || shouldCheckGeneration()) {
        await autoGenerateIfNeeded();
    }
}

function shouldCheckGeneration() {
    try {
        const lastCheck = localStorage.getItem('lastGenerationCheck');
        if (!lastCheck) return true;

        const daysSinceCheck = (Date.now() - parseInt(lastCheck)) / (1000 * 60 * 60 * 24);
        return daysSinceCheck >= 1;
    } catch (e) {
        return true;
    }
}

async function autoGenerateIfNeeded() {
    try {
        const response = await fetch('/DailyClass/CheckAndGenerateWeeklyClasses', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) throw new Error('Failed to check auto-generation');

        const result = await response.json();

        if (result.success && result.autoGenerated) {
            showSuccessToast(result.message);
            loadDailyClassesOptimized();
            checkWeeklyGenerationStatusOptimized();
        }

        try {
            localStorage.setItem('lastGenerationCheck', Date.now().toString());
        } catch (e) {
            console.warn('Could not save to localStorage');
        }
    } catch (error) {
        console.error('Error in auto-generation check:', error);
    }
}

let statusCheckTimeout;
async function checkWeeklyGenerationStatusOptimized() {
    clearTimeout(statusCheckTimeout);

    statusCheckTimeout = setTimeout(async () => {
        try {
            const response = await fetch('/DailyClass/GetWeeklyGenerationStatus');
            if (!response.ok) throw new Error('Failed to get status');

            weeklyGenerationStatus = await response.json();
            updateWeeklyGenerationBanner();
        } catch (error) {
            console.error('Error checking weekly generation status:', error);
        }
    }, 300);
}

function updateWeeklyGenerationBanner() {
    const banner = document.getElementById('weeklyGenerationBanner');
    const statusText = document.getElementById('generationStatusText');
    const statusDetails = document.getElementById('generationStatusDetails');
    const generateBtn = document.getElementById('generateWeeklyBtn');

    if (!banner || !weeklyGenerationStatus || weeklyGenerationStatus.error) {
        if (banner) banner.style.display = 'none';
        return;
    }

    const { activeSchedulesCount, existingClassesCount, needsGeneration, canGenerate, weekStartFormatted, weekEndFormatted } = weeklyGenerationStatus;

    banner.style.display = 'block';
    banner.className = 'weekly-generation-banner';

    if (!canGenerate) {
        banner.classList.add('info');
        if (statusText) statusText.textContent = 'No Active Schedules';
        if (statusDetails) statusDetails.textContent = 'No weekly schedules found. Create schedules first to generate classes.';
        if (generateBtn) generateBtn.style.display = 'none';
    } else if (needsGeneration) {
        banner.classList.add('warning');
        if (statusText) statusText.textContent = 'Classes Need Generation';
        if (statusDetails) statusDetails.textContent = `Week ${weekStartFormatted} - ${weekEndFormatted}: ${existingClassesCount}/${activeSchedulesCount} classes generated. Click to generate missing classes.`;
        if (generateBtn) generateBtn.style.display = 'inline-block';
    } else {
        banner.classList.add('success');
        if (statusText) statusText.textContent = 'Classes Generated';
        if (statusDetails) statusDetails.textContent = `Week ${weekStartFormatted} - ${weekEndFormatted}: All ${existingClassesCount} classes generated from schedules.`;
        if (generateBtn) generateBtn.style.display = 'none';
    }
}

async function generateWeeklyClasses() {
    const btn = event.target.closest('button');
    const originalText = btn.innerHTML;

    try {
        btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Generating...';
        btn.disabled = true;

        const response = await fetch('/DailyClass/GenerateWeeklyClasses', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) throw new Error('Failed to generate classes');

        const result = await response.json();

        if (result.success) {
            showSuccessToast(result.message);
            loadDailyClassesOptimized();
            checkWeeklyGenerationStatusOptimized();

            const modal = bootstrap.Modal.getInstance(document.getElementById('weeklyGenerationModal'));
            if (modal) modal.hide();
        } else {
            throw new Error(result.error || 'Unknown error occurred');
        }
    } catch (error) {
        console.error('Error generating weekly classes:', error);
        showErrorToast('Error generating classes: ' + error.message);
    } finally {
        btn.innerHTML = originalText;
        btn.disabled = false;
    }
}

async function showWeeklyGenerationStatus() {
    const modal = new bootstrap.Modal(document.getElementById('weeklyGenerationModal'));
    const content = document.getElementById('weeklyGenerationContent');

    modal.show();

    content.innerHTML = `
        <div class="text-center py-3">
            <div class="spinner"></div>
            <p class="mt-2">Loading generation status...</p>
        </div>
    `;

    try {
        await checkWeeklyGenerationStatusOptimized();

        if (weeklyGenerationStatus && !weeklyGenerationStatus.error) {
            content.innerHTML = renderWeeklyGenerationStatus();

            const modalBtn = document.getElementById('modalGenerateBtn');
            if (modalBtn) modalBtn.style.display = weeklyGenerationStatus.canGenerate ? 'inline-block' : 'none';
        } else {
            content.innerHTML = '<div class="alert alert-danger">Error loading status</div>';
        }
    } catch (error) {
        content.innerHTML = '<div class="alert alert-danger">Failed to load status</div>';
    }
}

function renderWeeklyGenerationStatus() {
    const {
        activeSchedulesCount,
        existingClassesCount,
        schedulesByDay,
        classesByDay,
        weekStartFormatted,
        weekEndFormatted,
        canGenerate,
        needsGeneration
    } = weeklyGenerationStatus;

    const days = ['Saturday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

    return `
        <div class="generation-status-card ${needsGeneration ? 'warning' : 'success'}">
            <h6>Week ${weekStartFormatted} - ${weekEndFormatted}</h6>
            <p class="mb-0">
                ${canGenerate ?
            `${existingClassesCount} of ${activeSchedulesCount} possible classes generated from schedules.` :
            'No active schedules found for class generation.'
        }
            </p>
        </div>

        <div class="generation-summary">
            <div class="summary-item">
                <span class="summary-value">${activeSchedulesCount}</span>
                <span class="summary-label">Active Schedules</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${existingClassesCount}</span>
                <span class="summary-label">Generated Classes</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${Math.max(0, activeSchedulesCount - existingClassesCount)}</span>
                <span class="summary-label">Missing Classes</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${activeSchedulesCount > 0 ? Math.round((existingClassesCount / activeSchedulesCount) * 100) : 0}%</span>
                <span class="summary-label">Completion</span>
            </div>
        </div>

        <h6>Daily Breakdown</h6>
        <div class="day-breakdown">
            ${days.map(day => {
            const scheduleCount = schedulesByDay[day] || 0;
            const classCount = classesByDay[day] || 0;
            const hasSchedules = scheduleCount > 0;
            const hasClasses = classCount > 0;
            const needsGen = hasSchedules && classCount < scheduleCount;

            let className = 'day-item';
            if (needsGen) className += ' needs-generation';
            else if (hasClasses) className += ' has-classes';
            else if (hasSchedules) className += ' has-schedules';

            return `
                    <div class="${className}">
                        <div class="day-name">${day.substring(0, 3)}</div>
                        <div class="day-counts">
                            ${classCount}/${scheduleCount}
                        </div>
                    </div>
                `;
        }).join('')}
        </div>

        <div class="mt-3">
            <small class="text-muted">
                <i class="fas fa-info-circle me-1"></i>
                Classes are generated from your weekly schedule templates.
                Each generated class can be individually customized with student counts, costs, and other details.
            </small>
        </div>
    `;
}

function generateWeeklyClassesFromModal() {
    generateWeeklyClasses();
}

// ==================== CENTER/BRANCH FILTERING ====================

async function loadBranchesByCenter() {
    const centerCode = document.getElementById('centerCode').value;
    const branchSelect = document.getElementById('branchCode');

    branchSelect.innerHTML = '<option value="">Select Branch</option>';

    if (!centerCode) {
        return;
    }

    try {
        const response = await fetch(`/DailyClass/GetBranchesByCenter?centerCode=${centerCode}`);
        if (!response.ok) throw new Error('Failed to load branches');

        const branches = await response.json();

        const fragment = document.createDocumentFragment();
        branches.forEach(branch => {
            const option = document.createElement('option');
            option.value = branch.value;
            option.textContent = branch.text;
            fragment.appendChild(option);
        });

        branchSelect.appendChild(fragment);
    } catch (error) {
        console.error('Error loading branches:', error);
        showErrorToast('Failed to load branches for selected center');
    }
}

// ==================== DAILY CLASS FUNCTIONS ====================

function addClassAtTime(time) {
    if (userContext.hasError) {
        showErrorToast('Unable to add class. Please contact administrator.');
        return;
    }

    resetModalForCreate();
    if (time) {
        document.getElementById('startTime').value = time;

        const [hours, minutes] = time.split(':');
        const endHour = (parseInt(hours) + 1) % 24;
        document.getElementById('endTime').value = `${endHour.toString().padStart(2, '0')}:${minutes}`;
    }

    new bootstrap.Modal(document.getElementById('addClassModal')).show();
}

function saveClass() {
    console.log('Save class function called');

    if (userContext.hasError) {
        showErrorToast('Unable to save class. Please contact administrator.');
        return;
    }

    const submitBtn = document.getElementById('saveClassBtn');
    const originalText = submitBtn.innerHTML;

    // Validate required fields based on user type
    const requiredFields = [
        { id: 'className', name: 'Class Name' },
        { id: 'startTime', name: 'Start Time' },
        { id: 'endTime', name: 'End Time' },
        { id: 'subjectCode', name: 'Subject' },
        { id: 'branchCode', name: 'Branch' },
        { id: 'hallCode', name: 'Hall' },
        { id: 'eduYearCode', name: 'Education Year' }
    ];

    // Add user-type specific required fields
    if (userContext.isCenter) {
        requiredFields.push({ id: 'teacherCode', name: 'Teacher' });
    } else {
        requiredFields.push({ id: 'centerCode', name: 'Center' });
    }

    for (const field of requiredFields) {
        const element = document.getElementById(field.id);
        if (!element || !element.value || element.value.trim() === '') {
            showErrorToast(`${field.name} is required`);
            if (element) element.focus();
            return;
        }
    }

    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;

    if (startTime >= endTime) {
        showErrorToast('End time must be after start time');
        return;
    }

    // Show loading state
    const loadingText = isEditMode ? 'Updating...' : 'Saving...';
    submitBtn.innerHTML = `<div class="spinner-border spinner-border-sm me-2"></div>${loadingText}`;
    submitBtn.disabled = true;

    const formData = {
        className: document.getElementById('className').value,
        startTime: startTime,
        endTime: endTime,
        subjectCode: parseInt(document.getElementById('subjectCode').value),
        branchCode: parseInt(document.getElementById('branchCode').value),
        hallCode: parseInt(document.getElementById('hallCode').value),
        eduYearCode: parseInt(document.getElementById('eduYearCode').value),
        yearCode: parseInt(document.getElementById('yearCode').value) || null,
        totalAmount: parseFloat(document.getElementById('totalAmount').value) || null,
        teacherAmount: parseFloat(document.getElementById('teacherAmount').value) || null,
        centerAmount: parseFloat(document.getElementById('centerAmount').value) || null,
        rootCode: userContext.currentUserRootCode,
        classDate: currentDate
    };

    // Add user-type specific fields
    if (userContext.isCenter) {
        formData.teacherCode = parseInt(document.getElementById('teacherCode').value);
    } else {
        formData.centerCode = parseInt(document.getElementById('centerCode').value);
    }

    console.log('Form data prepared:', formData);

    const url = isEditMode ? `/DailyClass/EditClass/${editingClassId}` : '/DailyClass/CreateClass';

    fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(formData)
    })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                const successMessage = isEditMode ? 'Class updated successfully!' : 'Class created successfully!';
                showSuccessToast(successMessage);
                bootstrap.Modal.getInstance(document.getElementById('addClassModal')).hide();
                resetFormFields();
                resetModalForCreate();
                loadDailyClassesOptimized();

                cleanCache();
            } else {
                const errorMessage = isEditMode ? 'Error updating class: ' : 'Error creating class: ';
                showErrorToast(errorMessage + (data.error || 'Unknown error'));
            }
        })
        .catch(error => {
            console.error('Fetch error:', error);
            showErrorToast('Network error occurred: ' + error.message);
        })
        .finally(() => {
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
        });
}

function showClassDetails(classCode) {
    const cls = dailyClasses.find(c => c.classCode === classCode);
    if (!cls) return;

    // Build display content based on user type
    let organizationInfo = '';
    let teacherInfo = '';

    if (userContext.isCenter) {
        teacherInfo = cls.teacherName ? `<div class="detail-item mb-2"><strong>Teacher:</strong> ${cls.teacherName}</div>` : '';
    } else {
        organizationInfo = `
            ${cls.centerName ? `<div class="detail-item mb-2"><strong>Center:</strong> ${cls.centerName}</div>` : ''}
            ${cls.branchName ? `<div class="detail-item mb-2"><strong>Branch:</strong> ${cls.branchName}</div>` : ''}
        `;
    }

    const content = `
        <div class="class-details-header text-center mb-3">
            <div class="mb-2">
                <i class="fas fa-chalkboard-teacher fa-2x text-primary"></i>
            </div>
            <h4>${cls.title}</h4>
            <div class="d-flex justify-content-center gap-2 flex-wrap">
                <span class="badge bg-primary">Class</span>
                <span class="badge ${cls.classType === 'schedule' ? 'bg-info' : cls.classType === 'reservation' ? 'bg-warning' : 'bg-success'}">${cls.classType === 'schedule' ? 'Recurring' :
            cls.classType === 'reservation' ? 'Reserved' :
                'Direct'
        }</span>
                <span class="badge bg-secondary">${userContext.userRootName}</span>
                ${cls.classDate ? `<span class="badge bg-dark">${cls.classDate}</span>` : ''}
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="detail-item mb-2">
                    <strong>Time:</strong> ${cls.startTime12} - ${cls.endTime12}
                </div>
                ${teacherInfo}
                ${organizationInfo}
                ${cls.subjectName ? `<div class="detail-item mb-2"><strong>Subject:</strong> ${cls.subjectName}</div>` : ''}
            </div>
            <div class="col-md-6">
                ${cls.hallName ? `<div class="detail-item mb-2"><strong>Hall:</strong> ${cls.hallName}</div>` : ''}
                ${cls.eduYearName ? `<div class="detail-item mb-2"><strong>Education Year:</strong> ${cls.eduYearName}</div>` : ''}
                ${cls.yearName ? `<div class="detail-item mb-2"><strong>Year:</strong> ${cls.yearName}</div>` : ''}
                ${cls.noOfStudents !== undefined ? `<div class="detail-item mb-2"><strong>Students:</strong> ${cls.noOfStudents} <small class="text-muted">(from attendance)</small></div>` : ''}
            </div>
        </div>

        ${cls.totalAmount || cls.teacherAmount || cls.centerAmount ? `
            <hr>
            <div class="row">
                ${cls.totalAmount ? `<div class="col-md-4"><strong>Total:</strong> ${cls.totalAmount}</div>` : ''}
                ${cls.teacherAmount ? `<div class="col-md-4"><strong>Teacher:</strong> ${cls.teacherAmount}</div>` : ''}
                ${cls.centerAmount ? `<div class="col-md-4"><strong>Center:</strong> ${cls.centerAmount}</div>` : ''}
            </div>
        ` : ''}
    `;

    document.getElementById('classDetailsContent').innerHTML = content;

    // Setup modal buttons
    document.getElementById('editClassBtn').onclick = () => {
        editClassInModal(classCode);
    };

    document.getElementById('deleteClassBtn').onclick = () => {
        deleteClass(classCode);
    };

    new bootstrap.Modal(document.getElementById('classDetailsModal')).show();
}

function editClassInModal(classCode) {
    const cls = dailyClasses.find(c => c.classCode === classCode);
    if (!cls) return;

    // Set edit mode
    isEditMode = true;
    editingClassId = classCode;

    // Update modal title
    document.querySelector('#addClassModal .modal-title').innerHTML =
        '<i class="fas fa-edit me-2"></i>Edit Class' +
        (userContext.userRootName ? `<small class="text-muted">for ${userContext.userRootName}</small>` : '');

    // Populate form with existing data
    document.getElementById('className').value = cls.title;
    document.getElementById('startTime').value = cls.startTime;
    document.getElementById('endTime').value = cls.endTime;

    if (userContext.isCenter) {
        document.getElementById('teacherCode').value = cls.teacherCode || '';
    } else {
        const centerField = document.getElementById('centerCode');
        if (centerField) {
            // You might need to implement logic to get center from class data
        }
    }

    document.getElementById('subjectCode').value = cls.subjectCode || '';
    document.getElementById('branchCode').value = cls.branchCode || '';
    document.getElementById('hallCode').value = cls.hallCode || '';
    document.getElementById('eduYearCode').value = cls.eduYearCode || '';
    document.getElementById('yearCode').value = cls.yearCode || '';
    document.getElementById('noOfStudents').value = cls.noOfStudents || '0';
    document.getElementById('totalAmount').value = cls.totalAmount ? parseFloat(cls.totalAmount) : '';
    document.getElementById('teacherAmount').value = cls.teacherAmount ? parseFloat(cls.teacherAmount) : '';
    document.getElementById('centerAmount').value = cls.centerAmount ? parseFloat(cls.centerAmount) : '';

    // Hide details modal and show edit modal
    bootstrap.Modal.getInstance(document.getElementById('classDetailsModal')).hide();
    new bootstrap.Modal(document.getElementById('addClassModal')).show();
}

function deleteClass(classCode) {
    if (!confirm('Are you sure you want to delete this class? This action cannot be undone.')) {
        return;
    }

    fetch('/DailyClass/DeleteClass/' + classCode, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        }
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Failed to delete class');
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                showSuccessToast('Class deleted successfully!');
                bootstrap.Modal.getInstance(document.getElementById('classDetailsModal')).hide();
                loadDailyClassesOptimized();

                cleanCache();
            } else {
                throw new Error(data.error || 'Failed to delete class');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showErrorToast('Error deleting class: ' + error.message);
        });
}

function resetModalForCreate() {
    isEditMode = false;
    editingClassId = null;

    // Reset modal title
    document.querySelector('#addClassModal .modal-title').innerHTML =
        '<i class="fas fa-plus-circle me-2"></i>Add New Class' +
        (userContext.userRootName ? `<small class="text-muted">for ${userContext.userRootName}</small>` : '');

    // Clear form fields
    resetFormFields();
}

function resetFormFields() {
    const fieldIds = ['className', 'startTime', 'endTime', 'subjectCode', 'branchCode', 'hallCode', 'eduYearCode', 'yearCode', 'totalAmount', 'teacherAmount', 'centerAmount'];

    // Add user-type specific fields
    if (userContext.isCenter) {
        fieldIds.push('teacherCode');
    } else {
        fieldIds.push('centerCode');
    }

    fieldIds.forEach(id => {
        const element = document.getElementById(id);
        if (element) {
            if (element.type === 'select-one') {
                element.selectedIndex = 0;
            } else {
                element.value = '';
            }
        }
    });

    // Set student count to 0 for new classes (always readonly)
    document.getElementById('noOfStudents').value = '0';

    // Update date fields
    updateDateDisplay();
}

function refreshClasses() {
    // Clear cache and reload
    cleanCache();
    loadDailyClassesOptimized();
    showSuccessToast('Classes refreshed!');
}

function showWelcomeMessage() {
    let message = 'Welcome to Daily Classes Management!';

    if (userContext.userRootName) {
        const userType = userContext.isCenter ? 'Center' : 'Teacher';
        message = `Welcome ${userContext.userRootName}! Manage your daily classes here as a ${userType}.`;
    }

    showToast(message, 'bg-info');
}

// ==================== UTILITY FUNCTIONS ====================

function cleanCache() {
    const now = Date.now();

    // Clean classes cache
    for (const [key, value] of classesCache.entries()) {
        if (now - value.timestamp > cacheTimeout) {
            classesCache.delete(key);
        }
    }

    // Limit cache size
    if (classesCache.size > 50) {
        const entries = Array.from(classesCache.entries());
        entries.sort((a, b) => a[1].timestamp - b[1].timestamp);

        // Remove oldest 10 entries
        for (let i = 0; i < 10; i++) {
            classesCache.delete(entries[i][0]);
        }
    }

    // Clear dropdown cache if too old
    if (dropdownCache && (now - dropdownCache.timestamp) > cacheTimeout) {
        dropdownCache = null;
    }
}

function showLoadingOverlay() {
    let overlay = document.getElementById('loadingOverlay');
    if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = 'loadingOverlay';
        overlay.className = 'loading-overlay';
        overlay.innerHTML = '<div class="spinner"></div>';

        const container = document.querySelector('.calendar-day-container');
        if (container) {
            container.appendChild(overlay);
        }
    }
    overlay.style.display = 'flex';
}

function hideLoadingOverlay() {
    const overlay = document.getElementById('loadingOverlay');
    if (overlay) {
        overlay.style.display = 'none';
    }
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

function showSuccessToast(message) {
    showToast(message, 'bg-success');
}

function showErrorToast(message) {
    showToast(message, 'bg-danger');
}

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

    toastElement.addEventListener('hidden.bs.toast', () => {
        toastElement.remove();
    });
}