// Enhanced Daily Classes JavaScript
// Additional features and improvements for the daily classes system

// ==================== ENHANCED FEATURES ====================

// Auto-refresh classes every 5 minutes to stay synchronized
let autoRefreshInterval;

function startAutoRefresh() {
    autoRefreshInterval = setInterval(() => {
        if (!userContext.hasError) {
            loadDailyClasses();
            console.log('Auto-refreshed classes at', new Date().toLocaleTimeString());
        }
    }, 5 * 60 * 1000); // 5 minutes
}

function stopAutoRefresh() {
    if (autoRefreshInterval) {
        clearInterval(autoRefreshInterval);
        autoRefreshInterval = null;
    }
}

// ==================== KEYBOARD SHORTCUTS ====================

function setupKeyboardShortcuts() {
    document.addEventListener('keydown', function (e) {
        // Ctrl/Cmd + N = Add new class
        if ((e.ctrlKey || e.metaKey) && e.key === 'n') {
            e.preventDefault();
            if (!userContext.hasError) {
                new bootstrap.Modal(document.getElementById('addClassModal')).show();
            }
        }

        // Arrow Left = Previous day
        if (e.key === 'ArrowLeft' && !isModalOpen()) {
            e.preventDefault();
            navigateDate(-1);
        }

        // Arrow Right = Next day
        if (e.key === 'ArrowRight' && !isModalOpen()) {
            e.preventDefault();
            navigateDate(1);
        }

        // T = Go to today
        if (e.key === 't' && !isModalOpen()) {
            e.preventDefault();
            goToToday();
        }

        // R = Refresh
        if (e.key === 'r' && !isModalOpen()) {
            e.preventDefault();
            refreshClasses();
        }

        // Escape = Close modals
        if (e.key === 'Escape') {
            closeAllModals();
        }
    });
}

function isModalOpen() {
    return document.querySelector('.modal.show') !== null;
}

function closeAllModals() {
    const modals = document.querySelectorAll('.modal.show');
    modals.forEach(modal => {
        const modalInstance = bootstrap.Modal.getInstance(modal);
        if (modalInstance) {
            modalInstance.hide();
        }
    });
}

// ==================== DRAG AND DROP (Future Enhancement) ====================

function setupDragAndDrop() {
    // This would allow dragging classes between time slots
    // Implementation would depend on specific requirements
    console.log('Drag and drop setup (placeholder for future enhancement)');
}

// ==================== CLASS CONFLICT DETECTION ====================

function checkForConflicts(startTime, endTime, hallCode, teacherCode, excludeClassId = null) {
    const conflicts = [];

    dailyClasses.forEach(cls => {
        if (excludeClassId && cls.classCode === excludeClassId) {
            return; // Skip the class we're editing
        }

        const clsStart = timeToMinutes(cls.startTime);
        const clsEnd = timeToMinutes(cls.endTime);
        const newStart = timeToMinutes(startTime);
        const newEnd = timeToMinutes(endTime);

        // Check for time overlap
        if (newStart < clsEnd && newEnd > clsStart) {
            // Check for hall conflict
            if (hallCode && cls.hallCode === parseInt(hallCode)) {
                conflicts.push({
                    type: 'hall',
                    class: cls,
                    message: `Hall conflict with "${cls.title}" (${cls.startTime12} - ${cls.endTime12})`
                });
            }

            // Check for teacher conflict
            if (teacherCode && cls.teacherCode === parseInt(teacherCode)) {
                conflicts.push({
                    type: 'teacher',
                    class: cls,
                    message: `Teacher conflict with "${cls.title}" (${cls.startTime12} - ${cls.endTime12})`
                });
            }
        }
    });

    return conflicts;
}

function timeToMinutes(timeStr) {
    const [hours, minutes] = timeStr.split(':').map(Number);
    return hours * 60 + minutes;
}

function validateClassTiming() {
    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;
    const hallCode = document.getElementById('hallCode').value;
    const teacherCode = document.getElementById('teacherCode').value;

    if (!startTime || !endTime) return;

    const conflicts = checkForConflicts(startTime, endTime, hallCode, teacherCode, editingClassId);

    const conflictWarning = document.getElementById('conflictWarning');
    if (conflicts.length > 0) {
        if (!conflictWarning) {
            const warningHtml = `
                <div class="alert alert-warning mt-3" id="conflictWarning">
                    <h6><i class="fas fa-exclamation-triangle me-2"></i>Potential Conflicts Detected:</h6>
                    <ul id="conflictList" class="mb-0"></ul>
                </div>
            `;
            document.getElementById('classForm').insertAdjacentHTML('afterend', warningHtml);
        }

        const conflictList = document.getElementById('conflictList');
        conflictList.innerHTML = conflicts.map(c => `<li>${c.message}</li>`).join('');
    } else if (conflictWarning) {
        conflictWarning.remove();
    }
}

// ==================== BULK OPERATIONS ====================

function setupBulkOperations() {
    // Add bulk operation buttons
    const bulkControlsHtml = `
        <div class="bulk-operations mt-3" style="display: none;">
            <div class="d-flex gap-2 align-items-center">
                <span class="text-muted">Bulk Actions:</span>
                <button type="button" class="btn btn-sm btn-outline-danger" onclick="bulkDeleteSelected()">
                    <i class="fas fa-trash me-1"></i>Delete Selected
                </button>
                <button type="button" class="btn btn-sm btn-outline-info" onclick="bulkExportSelected()">
                    <i class="fas fa-download me-1"></i>Export Selected
                </button>
                <button type="button" class="btn btn-sm btn-outline-secondary" onclick="clearSelection()">
                    <i class="fas fa-times me-1"></i>Clear Selection
                </button>
            </div>
        </div>
    `;

    document.querySelector('.controls-section').insertAdjacentHTML('afterend', bulkControlsHtml);
}

let selectedClasses = new Set();

function toggleClassSelection(classCode, element) {
    if (selectedClasses.has(classCode)) {
        selectedClasses.delete(classCode);
        element.classList.remove('selected');
    } else {
        selectedClasses.add(classCode);
        element.classList.add('selected');
    }

    updateBulkOperationsVisibility();
}

function updateBulkOperationsVisibility() {
    const bulkOps = document.querySelector('.bulk-operations');
    if (bulkOps) {
        bulkOps.style.display = selectedClasses.size > 0 ? 'block' : 'none';
    }
}

function clearSelection() {
    selectedClasses.clear();
    document.querySelectorAll('.class-item.selected').forEach(el => {
        el.classList.remove('selected');
    });
    updateBulkOperationsVisibility();
}

// ==================== STATISTICS AND ANALYTICS ====================

function calculateDayStatistics() {
    const stats = {
        totalClasses: dailyClasses.length,
        centerClasses: dailyClasses.filter(c => c.isCenter).length,
        teacherClasses: dailyClasses.filter(c => !c.isCenter).length,
        totalStudents: dailyClasses.reduce((sum, c) => sum + (c.noOfStudents || 0), 0),
        totalRevenue: dailyClasses.reduce((sum, c) => sum + (parseFloat(c.totalAmount) || 0), 0),
        avgClassDuration: 0,
        hallUtilization: {}
    };

    // Calculate average class duration
    if (dailyClasses.length > 0) {
        const totalMinutes = dailyClasses.reduce((sum, c) => {
            const duration = timeToMinutes(c.endTime) - timeToMinutes(c.startTime);
            return sum + duration;
        }, 0);
        stats.avgClassDuration = Math.round(totalMinutes / dailyClasses.length);
    }

    // Calculate hall utilization
    dailyClasses.forEach(c => {
        const hallName = c.hallName || 'Unknown Hall';
        stats.hallUtilization[hallName] = (stats.hallUtilization[hallName] || 0) + 1;
    });

    return stats;
}

function displayStatistics() {
    const stats = calculateDayStatistics();

    const statsHtml = `
        <div class="statistics-panel mt-3 p-3 bg-light rounded">
            <h6><i class="fas fa-chart-bar me-2"></i>Daily Statistics</h6>
            <div class="row">
                <div class="col-md-3">
                    <div class="stat-item">
                        <span class="stat-value">${stats.totalClasses}</span>
                        <span class="stat-label">Total Classes</span>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="stat-item">
                        <span class="stat-value">${stats.totalStudents}</span>
                        <span class="stat-label">Total Students</span>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="stat-item">
                        <span class="stat-value">$${stats.totalRevenue.toFixed(2)}</span>
                        <span class="stat-label">Total Revenue</span>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="stat-item">
                        <span class="stat-value">${stats.avgClassDuration}min</span>
                        <span class="stat-label">Avg Duration</span>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Remove existing stats and add new ones
    const existingStats = document.querySelector('.statistics-panel');
    if (existingStats) {
        existingStats.remove();
    }

    document.querySelector('.timeline-container').insertAdjacentHTML('afterend', statsHtml);
}

// ==================== EXPORT FUNCTIONALITY ====================

function exportDailySchedule() {
    const stats = calculateDayStatistics();
    const dateStr = currentDate.toISOString().split('T')[0];

    const exportData = {
        date: dateStr,
        dayOfWeek: currentDate.toLocaleDateString('en-US', { weekday: 'long' }),
        rootName: userContext.userRootName,
        statistics: stats,
        classes: dailyClasses.map(c => ({
            name: c.title,
            startTime: c.startTime12,
            endTime: c.endTime12,
            teacher: c.teacherName,
            subject: c.subjectName,
            hall: c.hallName,
            students: c.noOfStudents,
            amount: c.totalAmount
        }))
    };

    // Create and download JSON file
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `daily-classes-${dateStr}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    showSuccessToast('Daily schedule exported successfully!');
}

// ==================== SEARCH AND FILTER ====================

function setupSearchAndFilter() {
    const searchFilterHtml = `
        <div class="search-filter-panel mt-3 p-3 bg-light rounded">
            <div class="row">
                <div class="col-md-4">
                    <label class="form-label">Search Classes</label>
                    <input type="text" class="form-control" id="classSearch" placeholder="Search by name, teacher, subject...">
                </div>
                <div class="col-md-3">
                    <label class="form-label">Filter by Teacher</label>
                    <select class="form-select" id="teacherFilter">
                        <option value="">All Teachers</option>
                    </select>
                </div>
                <div class="col-md-3">
                    <label class="form-label">Filter by Subject</label>
                    <select class="form-select" id="subjectFilter">
                        <option value="">All Subjects</option>
                    </select>
                </div>
                <div class="col-md-2">
                    <label class="form-label">&nbsp;</label>
                    <button type="button" class="btn btn-outline-secondary w-100" onclick="clearFilters()">
                        <i class="fas fa-times me-1"></i>Clear
                    </button>
                </div>
            </div>
        </div>
    `;

    document.querySelector('.legend').insertAdjacentHTML('afterend', searchFilterHtml);

    // Setup search and filter event listeners
    document.getElementById('classSearch').addEventListener('input', applyFilters);
    document.getElementById('teacherFilter').addEventListener('change', applyFilters);
    document.getElementById('subjectFilter').addEventListener('change', applyFilters);
}

function populateFilterDropdowns() {
    const teachers = [...new Set(dailyClasses.map(c => c.teacherName).filter(Boolean))];
    const subjects = [...new Set(dailyClasses.map(c => c.subjectName).filter(Boolean))];

    const teacherFilter = document.getElementById('teacherFilter');
    const subjectFilter = document.getElementById('subjectFilter');

    if (teacherFilter) {
        teacherFilter.innerHTML = '<option value="">All Teachers</option>' +
            teachers.map(t => `<option value="${t}">${t}</option>`).join('');
    }

    if (subjectFilter) {
        subjectFilter.innerHTML = '<option value="">All Subjects</option>' +
            subjects.map(s => `<option value="${s}">${s}</option>`).join('');
    }
}

function applyFilters() {
    const searchTerm = document.getElementById('classSearch')?.value.toLowerCase() || '';
    const teacherFilter = document.getElementById('teacherFilter')?.value || '';
    const subjectFilter = document.getElementById('subjectFilter')?.value || '';

    const classItems = document.querySelectorAll('.class-item');

    classItems.forEach(item => {
        const classCode = parseInt(item.dataset.classId);
        const cls = dailyClasses.find(c => c.classCode === classCode);

        if (!cls) return;

        const matchesSearch = !searchTerm ||
            cls.title.toLowerCase().includes(searchTerm) ||
            (cls.teacherName && cls.teacherName.toLowerCase().includes(searchTerm)) ||
            (cls.subjectName && cls.subjectName.toLowerCase().includes(searchTerm));

        const matchesTeacher = !teacherFilter || cls.teacherName === teacherFilter;
        const matchesSubject = !subjectFilter || cls.subjectName === subjectFilter;

        if (matchesSearch && matchesTeacher && matchesSubject) {
            item.style.display = 'block';
        } else {
            item.style.display = 'none';
        }
    });
}

function clearFilters() {
    document.getElementById('classSearch').value = '';
    document.getElementById('teacherFilter').value = '';
    document.getElementById('subjectFilter').value = '';
    applyFilters();
}

// ==================== ENHANCED INITIALIZATION ====================

function initializeEnhancedFeatures() {
    setupKeyboardShortcuts();
    setupBulkOperations();
    setupSearchAndFilter();
    startAutoRefresh();

    // Add enhanced validation to the form
    const startTimeInput = document.getElementById('startTime');
    const endTimeInput = document.getElementById('endTime');
    const hallCodeSelect = document.getElementById('hallCode');
    const teacherCodeSelect = document.getElementById('teacherCode');

    if (startTimeInput) startTimeInput.addEventListener('change', validateClassTiming);
    if (endTimeInput) endTimeInput.addEventListener('change', validateClassTiming);
    if (hallCodeSelect) hallCodeSelect.addEventListener('change', validateClassTiming);
    if (teacherCodeSelect) teacherCodeSelect.addEventListener('change', validateClassTiming);

    // Add export button to controls
    const exportBtn = `
        <button type="button" class="btn-modern btn-info" onclick="exportDailySchedule()">
            <i class="fas fa-download"></i> Export
        </button>
    `;
    document.querySelector('.controls-section').insertAdjacentHTML('beforeend', exportBtn);

    // Add statistics toggle button
    const statsBtn = `
        <button type="button" class="btn-modern" onclick="toggleStatistics()">
            <i class="fas fa-chart-bar"></i> Statistics
        </button>
    `;
    document.querySelector('.controls-section').insertAdjacentHTML('beforeend', statsBtn);

    console.log('Enhanced features initialized');
}

function toggleStatistics() {
    const existingStats = document.querySelector('.statistics-panel');
    if (existingStats) {
        existingStats.remove();
    } else {
        displayStatistics();
    }
}

// ==================== ENHANCED RENDER FUNCTION ====================

function renderDailyTimelineEnhanced() {
    renderDailyTimeline(); // Call the original function
    populateFilterDropdowns();
    applyFilters();

    // Add click handlers for selection (Ctrl+Click to select multiple)
    document.querySelectorAll('.class-item').forEach(item => {
        item.addEventListener('click', function (e) {
            if (e.ctrlKey || e.metaKey) {
                e.stopPropagation();
                const classCode = parseInt(this.dataset.classId);
                toggleClassSelection(classCode, this);
            }
        });
    });
}

// ==================== CLEANUP ====================

function cleanup() {
    stopAutoRefresh();
    clearSelection();
}

// Override the original render function
const originalRenderDailyTimeline = renderDailyTimeline;
renderDailyTimeline = renderDailyTimelineEnhanced;

// Initialize enhanced features when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    if (!userContext.hasError) {
        setTimeout(() => {
            initializeEnhancedFeatures();
        }, 1500); // Initialize after the main system loads
    }
});

// Cleanup on page unload
window.addEventListener('beforeunload', cleanup);

// Add CSS for enhanced features
const enhancedStyles = `
<style>
.class-item.selected {
    border: 2px solid var(--warning-color) !important;
    box-shadow: 0 0 10px rgba(243, 156, 18, 0.3) !important;
}

.stat-item {
    text-align: center;
    padding: 0.5rem;
}

.stat-value {
    display: block;
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--primary-color);
}

.stat-label {
    display: block;
    font-size: 0.85rem;
    color: var(--text-muted);
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.search-filter-panel {
    border: 1px solid var(--border-color);
    transition: var(--transition);
}

.search-filter-panel:hover {
    box-shadow: var(--shadow);
}

.statistics-panel {
    border: 1px solid var(--border-color);
    transition: var(--transition);
}

.statistics-panel:hover {
    box-shadow: var(--shadow);
}

.bulk-operations {
    background: linear-gradient(135deg, rgba(243, 156, 18, 0.1) 0%, rgba(230, 126, 34, 0.1) 100%);
    border: 1px solid rgba(243, 156, 18, 0.2);
    border-radius: var(--border-radius);
    padding: 1rem;
}

#conflictWarning {
    animation: fadeIn 0.3s ease-in-out;
}

@keyframes fadeIn {
    from { opacity: 0; transform: translateY(-10px); }
    to { opacity: 1; transform: translateY(0); }
}
</style>
`;

document.head.insertAdjacentHTML('beforeend', enhancedStyles);