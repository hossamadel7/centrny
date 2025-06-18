/**
 * Student Profile JavaScript
 * Handles AJAX data loading, filtering, sorting, and interactive features
 */

// Global variables
let studentProfileData = {
    itemKey: '',
    subjects: [],
    plans: [],
    attendance: [],
    exams: [],
    stats: {},
    currentAttendancePage: 1,
    attendancePageSize: 10
};

// Constants
const API_ENDPOINTS = {
    STATS: '/Student/GetStudentStats/',
    SUBJECTS: '/Student/GetStudentSubjects/',
    PLANS: '/Student/GetStudentPlans/',
    ATTENDANCE: '/Student/GetStudentAttendance/',
    EXAMS: '/Student/GetStudentExams/'
};

// Initialize the student profile functionality
function initializeStudentProfile(itemKey) {
    studentProfileData.itemKey = itemKey;

    // Set up event listeners
    setupEventListeners();

    // Load all data
    loadAllStudentData();
}

// Setup event listeners for interactive features
function setupEventListeners() {
    // Search and filter functionality
    const subjectsSearch = document.getElementById('subjectsSearch');
    const subjectsFilter = document.getElementById('subjectsFilter');

    if (subjectsSearch) {
        subjectsSearch.addEventListener('input', debounce(filterSubjects, 300));
    }

    if (subjectsFilter) {
        subjectsFilter.addEventListener('change', filterSubjects);
    }

    // Add click handlers for interactive elements
    document.addEventListener('click', handleDocumentClick);

    // Add keyboard shortcuts
    document.addEventListener('keydown', handleKeyboardShortcuts);
}

// Load all student data
async function loadAllStudentData() {
    try {
        // Show loading states
        showLoadingState();

        // Load data in parallel for better performance
        const promises = [
            loadStudentStats(),
            loadStudentSubjects(),
            loadStudentPlans(),
            loadStudentAttendance(1),
            loadStudentExams()
        ];

        await Promise.allSettled(promises);

        // Hide loading states
        hideLoadingState();

        console.log('All student data loaded successfully');

    } catch (error) {
        console.error('Error loading student data:', error);
        showGlobalError('Failed to load student data. Please refresh the page.');
    }
}

// Load student statistics
async function loadStudentStats() {
    try {
        const response = await fetch(`${API_ENDPOINTS.STATS}${studentProfileData.itemKey}`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.error) {
            throw new Error(data.error);
        }

        studentProfileData.stats = data;
        renderStudentStats(data);

    } catch (error) {
        console.error('Error loading student stats:', error);
        showStatsError();
    }
}

// Render student statistics
function renderStudentStats(stats) {
    const elements = {
        subjectsCount: document.getElementById('subjectsCount'),
        attendanceCount: document.getElementById('attendanceCount'),
        examsCount: document.getElementById('examsCount'),
        averageGrade: document.getElementById('averageGrade')
    };

    // Animate the numbers
    if (elements.subjectsCount) animateNumber(elements.subjectsCount, stats.subjectsCount);
    if (elements.attendanceCount) animateNumber(elements.attendanceCount, stats.totalAttendance);
    if (elements.examsCount) animateNumber(elements.examsCount, stats.examsCount);
    if (elements.averageGrade) animateNumber(elements.averageGrade, stats.averageGrade, '%');
}

// Load student subjects with enhanced error handling
async function loadStudentSubjects() {
    try {
        const response = await fetch(`${API_ENDPOINTS.SUBJECTS}${studentProfileData.itemKey}`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.error) {
            throw new Error(data.error);
        }

        studentProfileData.subjects = data;
        renderSubjects(data);

        // Enable filtering after data is loaded
        enableSubjectFiltering();

    } catch (error) {
        console.error('Error loading subjects:', error);
        showError('subjectsGrid', 'Failed to load subjects: ' + error.message);
    }
}

// Render subjects with enhanced UI
function renderSubjects(subjects) {
    const container = document.getElementById('subjectsGrid');

    if (!container) return;

    if (subjects.length === 0) {
        container.innerHTML = createEmptyState('fa-book-open', 'No Subjects Found', 'This student is not enrolled in any subjects yet.');
        return;
    }

    // Sort subjects by name for consistent display
    const sortedSubjects = [...subjects].sort((a, b) => a.subjectName.localeCompare(b.subjectName));

    container.innerHTML = sortedSubjects.map(subject => createSubjectCard(subject)).join('');

    // Add entrance animations
    animateCards(container.querySelectorAll('.subject-card'));
}

// Create subject card HTML
function createSubjectCard(subject) {
    const hasSchedule = subject.schedules && subject.schedules.length > 0;
    const onlineClass = subject.isOnline;

    return `
        <div class="subject-card" data-subject="${subject.subjectName.toLowerCase()}" 
             data-teacher="${subject.teacherName.toLowerCase()}" 
             data-online="${onlineClass}">
            <div class="subject-header">
                <div class="subject-icon ${onlineClass ? 'online' : 'offline'}">
                    <i class="fas fa-${onlineClass ? 'laptop' : 'chalkboard-teacher'}"></i>
                </div>
                <div class="subject-info">
                    <h3 class="subject-name">${subject.subjectName}</h3>
                    <span class="subject-type ${onlineClass ? 'badge-info' : 'badge-success'}">
                        ${onlineClass ? 'Online' : 'In-Person'}
                    </span>
                </div>
            </div>
            
            <div class="subject-details">
                <div class="subject-detail">
                    <i class="fas fa-chalkboard-teacher"></i>
                    <span>${subject.teacherName}</span>
                </div>
                <div class="subject-detail">
                    <i class="fas fa-phone"></i>
                    <span>${subject.teacherPhone || 'N/A'}</span>
                </div>
                <div class="subject-detail">
                    <i class="fas fa-building"></i>
                    <span>${subject.branchName}</span>
                </div>
                <div class="subject-detail">
                    <i class="fas fa-graduation-cap"></i>
                    <span>${subject.eduYearName}</span>
                </div>
                ${subject.chapterName ? `
                    <div class="subject-detail">
                        <i class="fas fa-bookmark"></i>
                        <span>${subject.chapterName}</span>
                    </div>
                ` : ''}
            </div>
            
            ${hasSchedule ? createScheduleInfo(subject.schedules) : ''}
            
            ${subject.studentFee ? `
                <div class="subject-fee">
                    <i class="fas fa-dollar-sign"></i>
                    <span>Fee: $${subject.studentFee}</span>
                </div>
            ` : ''}
            
            <div class="subject-actions">
                <button class="btn-action" onclick="showSubjectDetails('${subject.subjectCode}')">
                    <i class="fas fa-info-circle"></i> Details
                </button>
                ${hasSchedule ? `
                    <button class="btn-action btn-secondary" onclick="showScheduleDetails('${subject.subjectCode}')">
                        <i class="fas fa-calendar"></i> Schedule
                    </button>
                ` : ''}
            </div>
        </div>
    `;
}

// Create schedule information HTML
function createScheduleInfo(schedules) {
    return `
        <div class="schedule-info">
            <div class="schedule-header">
                <i class="fas fa-clock"></i>
                <span>Class Schedule</span>
            </div>
            ${schedules.map(schedule => `
                <div class="schedule-item">
                    <div class="schedule-day">${schedule.dayOfWeek}</div>
                    <div class="schedule-time">${schedule.startTime12} - ${schedule.endTime12}</div>
                    ${schedule.hallName ? `<div class="schedule-location">${schedule.hallName}</div>` : ''}
                </div>
            `).join('')}
        </div>
    `;
}

// Filter subjects based on search and filter criteria
function filterSubjects() {
    const searchTerm = document.getElementById('subjectsSearch')?.value.toLowerCase() || '';
    const filterValue = document.getElementById('subjectsFilter')?.value || '';

    const filteredSubjects = studentProfileData.subjects.filter(subject => {
        // Search criteria
        const matchesSearch = searchTerm === '' ||
            subject.subjectName.toLowerCase().includes(searchTerm) ||
            subject.teacherName.toLowerCase().includes(searchTerm) ||
            subject.branchName.toLowerCase().includes(searchTerm);

        // Filter criteria
        const matchesFilter = filterValue === '' ||
            (filterValue === 'online' && subject.isOnline) ||
            (filterValue === 'offline' && !subject.isOnline);

        return matchesSearch && matchesFilter;
    });

    renderSubjects(filteredSubjects);

    // Update results count
    updateResultsCount(filteredSubjects.length, studentProfileData.subjects.length);
}

// Load student subscription plans
async function loadStudentPlans() {
    try {
        const response = await fetch(`${API_ENDPOINTS.PLANS}${studentProfileData.itemKey}`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.error) {
            throw new Error(data.error);
        }

        studentProfileData.plans = data;
        renderSubscriptionPlans(data);

    } catch (error) {
        console.error('Error loading subscription plans:', error);
        showError('plansContent', 'Failed to load subscription plans: ' + error.message);
    }
}

// Render subscription plans with enhanced UI
function renderSubscriptionPlans(plans) {
    const container = document.getElementById('plansContent');

    if (!container) return;

    if (plans.length === 0) {
        container.innerHTML = createEmptyState('fa-credit-card', 'No Subscription Plans', 'This student has no active subscription plans.');
        return;
    }

    // Sort plans by date (newest first)
    const sortedPlans = [...plans].sort((a, b) => new Date(b.subDate) - new Date(a.subDate));

    container.innerHTML = `
        <div class="plans-grid">
            ${sortedPlans.map(plan => createPlanCard(plan)).join('')}
        </div>
    `;

    // Add entrance animations
    animateCards(container.querySelectorAll('.plan-card'));
}

// Create subscription plan card
function createPlanCard(plan) {
    const isActive = !plan.isExpired;
    const daysRemaining = plan.daysRemaining || 0;
    const urgency = daysRemaining <= 7 && isActive ? 'urgent' : '';

    return `
        <div class="plan-card ${isActive ? 'active' : 'expired'} ${urgency}">
            <div class="plan-header">
                <div class="plan-icon ${isActive ? 'success' : 'danger'}">
                    <i class="fas fa-credit-card"></i>
                </div>
                <div class="plan-info">
                    <h3 class="plan-name">${plan.planName}</h3>
                    <span class="plan-status ${isActive ? 'badge-success' : 'badge-danger'}">
                        ${isActive ? 'Active' : 'Expired'}
                    </span>
                </div>
            </div>
            
            <div class="plan-description">
                ${plan.description}
            </div>
            
            <div class="plan-details">
                <div class="plan-detail">
                    <i class="fas fa-dollar-sign"></i>
                    <span>$${plan.price}</span>
                </div>
                <div class="plan-detail">
                    <i class="fas fa-graduation-cap"></i>
                    <span>${plan.eduYearName}</span>
                </div>
                <div class="plan-detail">
                    <i class="fas fa-hashtag"></i>
                    <span>${plan.totalCount} classes</span>
                </div>
                <div class="plan-detail">
                    <i class="fas fa-hourglass-half"></i>
                    <span>${plan.expiryMonths} months</span>
                </div>
            </div>
            
            <div class="plan-dates">
                <div class="plan-date">
                    <label>Subscribed:</label>
                    <span>${formatDate(plan.subDate)}</span>
                </div>
                <div class="plan-date">
                    <label>Expires:</label>
                    <span>${formatDate(plan.expiryDate)}</span>
                </div>
                ${isActive ? `
                    <div class="plan-date remaining ${urgency}">
                        <label>Remaining:</label>
                        <span>${daysRemaining} days</span>
                    </div>
                ` : ''}
            </div>
        </div>
    `;
}

// Load student attendance with pagination
async function loadStudentAttendance(page = 1) {
    try {
        const response = await fetch(`${API_ENDPOINTS.ATTENDANCE}${studentProfileData.itemKey}?page=${page}&pageSize=${studentProfileData.attendancePageSize}`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.error) {
            throw new Error(data.error);
        }

        studentProfileData.attendance = data.attendance;
        studentProfileData.currentAttendancePage = page;
        renderAttendanceHistory(data);

    } catch (error) {
        console.error('Error loading attendance history:', error);
        showError('attendanceContent', 'Failed to load attendance history: ' + error.message);
    }
}

// Render attendance history with enhanced table
function renderAttendanceHistory(data) {
    const container = document.getElementById('attendanceContent');

    if (!container) return;

    if (data.attendance.length === 0) {
        container.innerHTML = createEmptyState('fa-calendar-times', 'No Attendance Records', 'This student has no attendance history yet.');
        return;
    }

    container.innerHTML = `
        <div class="attendance-summary">
            <div class="summary-item">
                <span class="summary-value">${data.totalCount}</span>
                <span class="summary-label">Total Records</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${data.currentPage}</span>
                <span class="summary-label">Current Page</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${data.totalPages}</span>
                <span class="summary-label">Total Pages</span>
            </div>
        </div>
        
        <div class="table-responsive">
            <table class="data-table attendance-table">
                <thead>
                    <tr>
                        <th><i class="fas fa-calendar"></i> Date & Time</th>
                        <th><i class="fas fa-chalkboard-teacher"></i> Teacher</th>
                        <th><i class="fas fa-book"></i> Subject</th>
                        <th><i class="fas fa-door-open"></i> Class/Hall</th>
                        <th><i class="fas fa-dollar-sign"></i> Session Price</th>
                        <th><i class="fas fa-tag"></i> Type</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.attendance.map(record => createAttendanceRow(record)).join('')}
                </tbody>
            </table>
        </div>
        
        ${data.totalPages > 1 ? createPaginationControls(data) : ''}
    `;
}

// Create attendance table row
function createAttendanceRow(record) {
    const attendDate = new Date(record.attendDate);
    const isRecent = (new Date() - attendDate) <= (7 * 24 * 60 * 60 * 1000); // Within last 7 days

    return `
        <tr class="${isRecent ? 'recent-attendance' : ''}">
            <td>
                <div class="date-info">
                    <div class="date">${attendDate.toLocaleDateString()}</div>
                    <div class="time">${attendDate.toLocaleTimeString()}</div>
                </div>
            </td>
            <td>${record.teacherName}</td>
            <td>
                <span class="subject-badge">${record.subjectName}</span>
            </td>
            <td>
                <div class="location-info">
                    <div class="class-name">${record.className}</div>
                    <div class="hall-name">${record.hallName}</div>
                </div>
            </td>
            <td>
                <span class="price-value">$${record.sessionPrice}</span>
            </td>
            <td>
                <span class="type-badge badge-info">${record.type}</span>
            </td>
        </tr>
    `;
}

// Load student exam results
async function loadStudentExams() {
    try {
        const response = await fetch(`${API_ENDPOINTS.EXAMS}${studentProfileData.itemKey}`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.error) {
            throw new Error(data.error);
        }

        studentProfileData.exams = data;
        renderExamResults(data);

    } catch (error) {
        console.error('Error loading exam results:', error);
        showError('examsContent', 'Failed to load exam results: ' + error.message);
    }
}

// Render exam results with enhanced cards
function renderExamResults(exams) {
    const container = document.getElementById('examsContent');

    if (!container) return;

    if (exams.length === 0) {
        container.innerHTML = createEmptyState('fa-file-alt', 'No Exam Results', 'This student has not taken any exams yet.');
        return;
    }

    // Sort exams by date (newest first)
    const sortedExams = [...exams].sort((a, b) => new Date(b.examDate) - new Date(a.examDate));

    // Calculate summary statistics
    const totalExams = exams.length;
    const passedExams = exams.filter(e => e.passed).length;
    const averageScore = exams.reduce((sum, e) => sum + (e.studentPercentage || 0), 0) / totalExams;

    container.innerHTML = `
        <div class="exams-summary">
            <div class="summary-item">
                <span class="summary-value">${totalExams}</span>
                <span class="summary-label">Total Exams</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${passedExams}</span>
                <span class="summary-label">Passed</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${Math.round(averageScore)}%</span>
                <span class="summary-label">Average Score</span>
            </div>
            <div class="summary-item">
                <span class="summary-value">${Math.round((passedExams / totalExams) * 100)}%</span>
                <span class="summary-label">Pass Rate</span>
            </div>
        </div>
        
        <div class="exams-grid">
            ${sortedExams.map(exam => createExamCard(exam)).join('')}
        </div>
    `;

    // Add entrance animations
    animateCards(container.querySelectorAll('.exam-card'));
}

// Create exam result card
function createExamCard(exam) {
    const passed = exam.passed;
    const percentage = Math.round(exam.studentPercentage || 0);
    const isHighScore = percentage >= 90;

    return `
        <div class="exam-card ${passed ? 'passed' : 'failed'} ${isHighScore ? 'high-score' : ''}">
            <div class="exam-header">
                <div class="exam-icon ${passed ? 'success' : 'danger'}">
                    <i class="fas fa-${exam.examType === 'Exam' ? 'file-alt' : 'question-circle'}"></i>
                </div>
                <div class="exam-info">
                    <h3 class="exam-name">${exam.examName}</h3>
                    <div class="exam-badges">
                        <span class="badge-custom ${passed ? 'badge-success' : 'badge-danger'}">
                            ${passed ? 'Passed' : 'Failed'}
                        </span>
                        <span class="badge-custom badge-info">${exam.examType}</span>
                        ${exam.isOnline ? '<span class="badge-custom badge-warning">Online</span>' : ''}
                    </div>
                </div>
            </div>
            
            <div class="exam-details">
                <div class="exam-detail">
                    <i class="fas fa-book"></i>
                    <span>${exam.subjectName}</span>
                </div>
                <div class="exam-detail">
                    <i class="fas fa-chalkboard-teacher"></i>
                    <span>${exam.teacherName}</span>
                </div>
                <div class="exam-detail">
                    <i class="fas fa-calendar"></i>
                    <span>${formatDate(exam.examDate)}</span>
                </div>
                <div class="exam-detail">
                    <i class="fas fa-clock"></i>
                    <span>${exam.examTime}</span>
                </div>
                ${exam.lessonName !== 'N/A' ? `
                    <div class="exam-detail">
                        <i class="fas fa-bookmark"></i>
                        <span>${exam.lessonName}</span>
                    </div>
                ` : ''}
            </div>
            
            <div class="exam-scores">
                <div class="score-item">
                    <div class="score-value">${exam.studentResult || 0}</div>
                    <div class="score-label">Score</div>
                </div>
                <div class="score-item">
                    <div class="score-value">${percentage}%</div>
                    <div class="score-label">Percentage</div>
                </div>
                <div class="score-item">
                    <div class="score-value">${exam.grade}</div>
                    <div class="score-label">Grade</div>
                </div>
            </div>
            
            <div class="exam-progress">
                <div class="progress-bar">
                    <div class="progress-fill ${passed ? 'success' : 'danger'}" 
                         style="width: ${percentage}%"></div>
                </div>
                <div class="progress-text">${percentage}% of 100%</div>
            </div>
        </div>
    `;
}

// Utility Functions

// Create empty state HTML
function createEmptyState(icon, title, message) {
    return `
        <div class="empty-state">
            <i class="fas ${icon}"></i>
            <h4>${title}</h4>
            <p>${message}</p>
        </div>
    `;
}

// Show error in specific container
function showError(containerId, message) {
    const container = document.getElementById(containerId);
    if (container) {
        container.innerHTML = `
            <div class="error-state">
                <i class="fas fa-exclamation-triangle"></i>
                <h4>Error</h4>
                <p>${message}</p>
                <button class="btn-retry" onclick="loadAllStudentData()">
                    <i class="fas fa-redo"></i> Retry
                </button>
            </div>
        `;
    }
}

// Create pagination controls
function createPaginationControls(data) {
    return `
        <div class="pagination-controls">
            <button class="pagination-btn" 
                    onclick="loadStudentAttendance(${data.currentPage - 1})" 
                    ${data.currentPage <= 1 ? 'disabled' : ''}>
                <i class="fas fa-chevron-left"></i> Previous
            </button>
            <span class="pagination-info">
                Page ${data.currentPage} of ${data.totalPages} (${data.totalCount} total records)
            </span>
            <button class="pagination-btn" 
                    onclick="loadStudentAttendance(${data.currentPage + 1})"
                    ${data.currentPage >= data.totalPages ? 'disabled' : ''}>
                Next <i class="fas fa-chevron-right"></i>
            </button>
        </div>
    `;
}

// Format date for display
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    });
}

// Animate number counting
function animateNumber(element, targetValue, suffix = '') {
    const startValue = 0;
    const duration = 1000;
    const startTime = performance.now();

    function updateNumber(currentTime) {
        const elapsed = currentTime - startTime;
        const progress = Math.min(elapsed / duration, 1);

        const currentValue = Math.round(startValue + (targetValue - startValue) * progress);
        element.textContent = currentValue + suffix;

        if (progress < 1) {
            requestAnimationFrame(updateNumber);
        }
    }

    requestAnimationFrame(updateNumber);
}

// Animate cards entrance
function animateCards(cards) {
    cards.forEach((card, index) => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';

        setTimeout(() => {
            card.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
            card.style.opacity = '1';
            card.style.transform = 'translateY(0)';
        }, index * 100);
    });
}

// Debounce function for search
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Show/hide loading states
function showLoadingState() {
    const loadingElements = document.querySelectorAll('.loading-spinner');
    loadingElements.forEach(el => el.style.display = 'flex');
}

function hideLoadingState() {
    const loadingElements = document.querySelectorAll('.loading-spinner');
    loadingElements.forEach(el => el.style.display = 'none');
}

// Handle document clicks for interactive elements
function handleDocumentClick(event) {
    // Handle collapsible elements, modals, etc.
    // Add specific click handlers as needed
}

// Handle keyboard shortcuts
function handleKeyboardShortcuts(event) {
    // Add keyboard shortcuts for better UX
    if (event.ctrlKey && event.key === 'f') {
        event.preventDefault();
        const searchInput = document.getElementById('subjectsSearch');
        if (searchInput) {
            searchInput.focus();
        }
    }
}

// Enable subject filtering after data is loaded
function enableSubjectFiltering() {
    const searchInput = document.getElementById('subjectsSearch');
    const filterSelect = document.getElementById('subjectsFilter');

    if (searchInput) searchInput.disabled = false;
    if (filterSelect) filterSelect.disabled = false;
}

// Update results count display
function updateResultsCount(filtered, total) {
    // Add results count display if needed
    console.log(`Showing ${filtered} of ${total} subjects`);
}

// Show statistics error
function showStatsError() {
    const statsElements = ['subjectsCount', 'attendanceCount', 'examsCount', 'averageGrade'];
    statsElements.forEach(id => {
        const element = document.getElementById(id);
        if (element) element.textContent = 'Error';
    });
}

// Show global error message
function showGlobalError(message) {
    // Implementation for global error display
    console.error('Global error:', message);
}

// Modal functions for detailed views
function showSubjectDetails(subjectCode) {
    const subject = studentProfileData.subjects.find(s => s.subjectCode == subjectCode);
    if (subject) {
        // Implementation for subject details modal
        console.log('Show subject details:', subject);
    }
}

function showScheduleDetails(subjectCode) {
    const subject = studentProfileData.subjects.find(s => s.subjectCode == subjectCode);
    if (subject && subject.schedules) {
        // Implementation for schedule details modal
        console.log('Show schedule details:', subject.schedules);
    }
}

// Export functions for global access
window.studentProfile = {
    init: initializeStudentProfile,
    loadAttendance: loadStudentAttendance,
    showSubjectDetails,
    showScheduleDetails
};