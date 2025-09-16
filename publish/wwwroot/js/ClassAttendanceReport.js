// AjaxClassAttendanceReport.js - Full AJAX Implementation with Localization

// Localized string helper
function getJsString(key) {
    var el = document.getElementById('js-localization');
    return el ? (el.getAttribute('data-' + key) || '') : '';
}

let currentFilters = {
    teacherCode: '',
    subjectCode: '',
    classDate: '',
    page: 1,
    pageSize: 10
};

let selectedStudents = new Set();
let currentData = null;

$(document).ready(function () {
    initializeAjaxAttendanceReport();
    localizeStaticLabels();
});

function localizeStaticLabels() {
    $('#attendance-title').text(getJsString('title'));
    $('#attendance-subtitle').text(getJsString('subtitle'));
    $('#teacher-label').text(getJsString('teacher'));
    $('#subject-label').text(getJsString('subject'));
    $('#date-label').text(getJsString('date'));
    $('#apply-label').text(getJsString('apply'));
    $('#clear-label').text(getJsString('clear'));
    $('#loading-text').text(getJsString('loading'));
    $('#no-classes-title').text(getJsString('no-classes'));
    $('#no-classes-text').text(getJsString('no-classes-text'));
    $('#reset-label').text(getJsString('reset'));
}

/**
 * Initialize the AJAX attendance report
 */
function initializeAjaxAttendanceReport() {
    setupEventHandlers();
    loadAttendanceData();
}

/**
 * Setup all event handlers
 */
function setupEventHandlers() {
    // Filter form submission
    $('#filterForm').on('submit', function (e) {
        e.preventDefault();
        updateFiltersFromForm();
        loadAttendanceData();
    });

    // Clear filters
    $('#clearFilters, #clearFiltersBtn').on('click', function () {
        clearAllFilters();
    });

    // Export buttons
    $('#exportPdf').on('click', function () {
        exportData('pdf');
    });

    $('#exportExcel').on('click', function () {
        exportData('excel');
    });

    $('#printReport').on('click', function () {
        printReport();
    });

    // Global bulk actions
    $('#markSelectedPresent').on('click', function () {
        bulkMarkAttendance(true);
    });

    $('#markSelectedAbsent').on('click', function () {
        bulkMarkAttendance(false);
    });

    $('#smsAllSelected').on('click', function () {
        showSmsModal();
    });

    $('#clearAllSelections').on('click', function () {
        clearAllSelections();
    });

    // SMS Modal
    $('#sendSmsBtn').on('click', function () {
        sendSmsToSelected();
    });

    // Auto-refresh every 30 seconds (optional)
    // setInterval(loadAttendanceData, 30000);
}

/**
 * Update filters from form
 */
function updateFiltersFromForm() {
    currentFilters = {
        teacherCode: $('#teacherCode').val() || '',
        subjectCode: $('#subjectCode').val() || '',
        classDate: $('#classDate').val() || '',
        page: 1,
        pageSize: currentFilters.pageSize
    };
}

/**
 * Load attendance data via AJAX
 */
function loadAttendanceData() {
    showLoading();
    hideAllSections();

    $.ajax({
        url: '/Reports/GetClassAttendanceData',
        type: 'POST',
        data: currentFilters,
        success: function (data) {

            hideLoading();

            if (data.error) {
                showError(getJsString('loading-error-text') + ' ' + data.error);
                return;
            }

            currentData = data;
            renderAttendanceData(data);
            updatePagination(data);
            showAllSections();

            // Add fade-in animation
            setTimeout(() => {
                $('.fade-in').addClass('show');
            }, 100);
        },
        error: function (xhr, status, error) {
            console.error("AJAX Error:", xhr.responseText);
            hideLoading();
            showError(getJsString('loading-error-text') + ' ' + error);
        }
    });
}
/**
 * Render attendance data
 */
function renderAttendanceData(data) {
    const container = $('#attendanceContainer');

    container.empty();

    if (!data.attendanceDetails || data.attendanceDetails.length === 0) {
        $('#noDataMessage').show();
        return;
    }

    $('#noDataMessage').hide();

    data.attendanceDetails.forEach((classDetail, index) => {

        const classCard = createClassCard(classDetail, index);

        container.append(classCard);
    });

    setupDynamicEventHandlers();
}
/**
 * Create class card HTML
 */
function createClassCard(classDetail, index) {
    // Determine attendance rate class
    const attendanceRateClass = classDetail.attendanceRate >= 80 ? 'rate-excellent' :
        classDetail.attendanceRate >= 60 ? 'rate-good' : 'rate-poor';

    return `
        <div class="enhanced-class-card fade-in" data-class-code="${classDetail.classCode}" onclick="viewClassStudents(${classDetail.classCode})">
            <!-- Enhanced Class Header -->
            <div class="class-card-header">
                <div class="class-title-section">
                    <div class="class-main-info">
                        <h3 class="class-name">
                            <i class="fas fa-chalkboard-teacher me-2"></i>
                            ${classDetail.className}
                        </h3>
                        
                        <div class="class-meta-badges">
                            <span class="class-badge">
                                <i class="fas fa-calendar me-1"></i>
                                ${formatDate(classDetail.classDate)}
                            </span>
                            <span class="class-badge">
                                <i class="fas fa-clock me-1"></i>
                                ${classDetail.classStartTime} - ${classDetail.classEndTime}
                            </span>
                            <span class="class-badge">
                                <i class="fas fa-user me-1"></i>
                                ${classDetail.teacherName}
                            </span>
                            <span class="class-badge">
                                <i class="fas fa-book me-1"></i>
                                ${classDetail.subjectName}
                            </span>
                        </div>
                    </div>
                    
                    <div class="class-stats-section">
                        <div class="class-stats-grid">
                            <div class="stat-item">
                                <div class="stat-number">${classDetail.enrolledCount}</div>
                                <div class="stat-label">${getJsString('enrolled')}</div>
                            </div>
                            <div class="stat-item">
                                <div class="stat-number">${classDetail.presentCount}</div>
                                <div class="stat-label">${getJsString('present')}</div>
                            </div>
                            <div class="stat-item">
                                <div class="stat-number">${classDetail.absentCount}</div>
                                <div class="stat-label">${getJsString('absent')}</div>
                            </div>
                            <div class="stat-item">
                                <div class="stat-number">${classDetail.attendanceRate}%</div>
                                <div class="stat-label">${getJsString('rate')}</div>
                            </div>
                        </div>
                        
                        <div class="attendance-rate-badge ${attendanceRateClass}">
                            <i class="fas fa-chart-line me-1"></i>
                            ${classDetail.attendanceRate}% ${getJsString('attendance-rate')}
                        </div>
                    </div>
                </div>
            </div>

            <!-- Enhanced Class Body -->
            <div class="class-card-body">
                <div class="class-details-grid">
                    <div class="detail-item">
                        <div class="detail-icon">
                            <i class="fas fa-building"></i>
                        </div>
                        <div class="detail-content">
                            <div class="detail-label">${getJsString('branch')}</div>
                            <div class="detail-value">${classDetail.branchName}</div>
                        </div>
                    </div>
                    
                    <div class="detail-item">
                        <div class="detail-icon">
                            <i class="fas fa-door-open"></i>
                        </div>
                        <div class="detail-content">
                            <div class="detail-label">${getJsString('hall')}</div>
                            <div class="detail-value">${classDetail.hallName}</div>
                        </div>
                    </div>
                    
                    <div class="detail-item">
                        <div class="detail-icon">
                            <i class="fas fa-users"></i>
                        </div>
                        <div class="detail-content">
                            <div class="detail-label">${getJsString('students')}</div>
                            <div class="detail-value">${classDetail.enrolledCount} ${getJsString('enrolled')}</div>
                        </div>
                    </div>
                    
                    <div class="detail-item">
                        <div class="detail-icon">
                            <i class="fas fa-percentage"></i>
                        </div>
                        <div class="detail-content">
                            <div class="detail-label">${getJsString('attendance')}</div>
                            <div class="detail-value">${classDetail.presentCount}/${classDetail.enrolledCount}</div>
                        </div>
                    </div>
                </div>
                
                <div class="class-actions">
                    <button class="enhanced-btn enhanced-btn-primary" onclick="event.stopPropagation(); viewClassStudents(${classDetail.classCode})">
                        <i class="fas fa-users me-2"></i>${getJsString('manage-attendance')}
                    </button>
                   
                   
                </div>
            </div>
        </div>
    `;
}

function contactAbsentParents(classCode) {
    // Find the class data
    const classData = currentData.attendanceDetails.find(c => c.classCode === classCode);
    if (!classData) return;

    // Get absent students with parent phone numbers
    const absentStudents = classData.students.filter(s => !s.isPresent && s.studentParentPhone);

    if (absentStudents.length === 0) {
        showNotification(getJsString('no-absent-parent-phones'), 'info');
        return;
    }

    // Show SMS modal for absent students
    $('#smsRecipientCount').text(absentStudents.length);
    $('#smsMessage').val(getJsString('absent-sms-default').replace('{class}', classData.className));
    $('#smsModal').modal('show');

    // Store the student codes for SMS sending
    window.currentAbsentStudents = absentStudents.map(s => s.studentCode);
}

/**
 * Setup event handlers for dynamically created elements
 */
function setupDynamicEventHandlers() {
    // Student checkbox change
    $('.student-checkbox').off('change').on('change', function () {
        const studentCode = $(this).data('student');
        const classCode = $(this).data('class');

        if ($(this).is(':checked')) {
            selectedStudents.add(`${classCode}-${studentCode}`);
            $(this).closest('tr').addClass('table-selected');
        } else {
            selectedStudents.delete(`${classCode}-${studentCode}`);
            $(this).closest('tr').removeClass('table-selected');
        }

        updateBulkActions(classCode);
        updateGlobalBulkActions();
    });

    // Row hover effects
    $('.attendance-table tbody tr').hover(
        function () {
            $(this).addClass('table-hover-active');
        },
        function () {
            $(this).removeClass('table-hover-active');
        }
    );
}

/**
 * Toggle student attendance
 */
function toggleStudentAttendance(classCode, studentCode, isPresent) {
    showLoading();

    $.ajax({
        url: '/Reports/MarkAttendance',
        type: 'POST',
        data: {
            classCode: classCode,
            studentCode: studentCode,
            isPresent: isPresent
        },
        success: function (data) {
            hideLoading();

            if (data.success) {
                // Refresh the specific class or entire data
                loadAttendanceData();
                showNotification(data.message, 'success');
            } else {
                showNotification(getJsString('attendance-update-error') + ' ' + data.error, 'error');
            }
        },
        error: function (xhr, status, error) {
            hideLoading();
            showNotification(getJsString('attendance-update-failed') + ' ' + error, 'error');
        }
    });
}

function viewClassStudents(classCode) {
    // Create URL for class students page
    const studentsUrl = `/Reports/ClassStudents/${classCode}`;

    // Open in new tab
    window.open(studentsUrl, '_blank');

    // Alternative: You could also show a modal instead
    // showClassStudentsModal(classCode);
}

/**
 * Toggle all students in a class
 */
function toggleAllStudents(classCode, checked) {
    const classTable = $(`.attendance-table[data-class-code="${classCode}"]`);
    const checkboxes = classTable.find('.student-checkbox');

    checkboxes.prop('checked', checked);
    checkboxes.each(function () {
        const studentCode = $(this).data('student');
        const key = `${classCode}-${studentCode}`;

        if (checked) {
            selectedStudents.add(key);
            $(this).closest('tr').addClass('table-selected');
        } else {
            selectedStudents.delete(key);
            $(this).closest('tr').removeClass('table-selected');
        }
    });

    updateBulkActions(classCode);
    updateGlobalBulkActions();
}

/**
 * Mark all students in a class as present
 */
function markAllPresent(classCode) {
    const classData = currentData.AttendanceDetails.find(c => c.ClassCode === classCode);
    if (!classData) return;

    const studentCodes = classData.Students.map(s => s.StudentCode);

    bulkMarkAttendanceForClass(classCode, studentCodes, true);
}

/**
 * Bulk mark attendance for specific students in a class
 */
function bulkMarkAttendanceForClass(classCode, studentCodes, isPresent) {
    showLoading();

    $.ajax({
        url: '/Reports/BulkMarkAttendance',
        type: 'POST',
        data: {
            classCode: classCode,
            studentCodes: studentCodes,
            isPresent: isPresent
        },
        success: function (data) {
            hideLoading();

            if (data.success) {
                loadAttendanceData();
                showNotification(data.message, 'success');
            } else {
                showNotification(getJsString('attendance-update-error') + ' ' + data.error, 'error');
            }
        },
        error: function (xhr, status, error) {
            hideLoading();
            showNotification(getJsString('attendance-update-failed') + ' ' + error, 'error');
        }
    });
}

/**
 * Mark selected students as present/absent
 */
function markSelectedPresent(classCode) {
    const selectedInClass = getSelectedStudentsInClass(classCode);
    if (selectedInClass.length === 0) {
        showNotification(getJsString('no-selected-in-class'), 'warning');
        return;
    }

    bulkMarkAttendanceForClass(classCode, selectedInClass, true);
}

function markSelectedAbsent(classCode) {
    const selectedInClass = getSelectedStudentsInClass(classCode);
    if (selectedInClass.length === 0) {
        showNotification(getJsString('no-selected-in-class'), 'warning');
        return;
    }

    bulkMarkAttendanceForClass(classCode, selectedInClass, false);
}

/**
 * Global bulk mark attendance
 */
function bulkMarkAttendance(isPresent) {
    if (selectedStudents.size === 0) {
        showNotification(getJsString('no-students-selected'), 'warning');
        return;
    }

    const promises = [];
    const classBatches = {};

    // Group selected students by class
    selectedStudents.forEach(key => {
        const [classCode, studentCode] = key.split('-');
        if (!classBatches[classCode]) {
            classBatches[classCode] = [];
        }
        classBatches[classCode].push(parseInt(studentCode));
    });

    // Send bulk requests for each class
    Object.keys(classBatches).forEach(classCode => {
        const promise = $.ajax({
            url: '/Reports/BulkMarkAttendance',
            type: 'POST',
            data: {
                classCode: parseInt(classCode),
                studentCodes: classBatches[classCode],
                isPresent: isPresent
            }
        });
        promises.push(promise);
    });

    showLoading();

    Promise.all(promises)
        .then(() => {
            hideLoading();
            clearAllSelections();
            loadAttendanceData();
            showNotification(getJsString('attendance-updated-for').replace('{count}', selectedStudents.size), 'success');
        })
        .catch((error) => {
            hideLoading();
            showNotification(getJsString('attendance-update-error') + ' ' + error, 'error');
        });
}

/**
 * Update bulk actions for a specific class
 */
function updateBulkActions(classCode) {
    const selectedInClass = getSelectedStudentsInClass(classCode);
    const bulkActionsBar = $(`#bulkActions-${classCode}`);

    if (selectedInClass.length > 0) {
        bulkActionsBar.find('.selected-count').text(selectedInClass.length);
        bulkActionsBar.show();
    } else {
        bulkActionsBar.hide();
    }
}

/**
 * Update global bulk actions
 */
function updateGlobalBulkActions() {
    const globalCount = selectedStudents.size;

    if (globalCount > 0) {
        $('#globalSelectedCount').text(globalCount);
        $('#globalBulkActions').show();
    } else {
        $('#globalBulkActions').hide();
    }
}

/**
 * Get selected students in a specific class
 */
function getSelectedStudentsInClass(classCode) {
    const selected = [];
    selectedStudents.forEach(key => {
        const [cCode, studentCode] = key.split('-');
        if (cCode === classCode.toString()) {
            selected.push(parseInt(studentCode));
        }
    });
    return selected;
}

/**
 * Clear all selections
 */
function clearAllSelections() {
    selectedStudents.clear();
    $('.student-checkbox, .select-all-students').prop('checked', false);
    $('.table-selected').removeClass('table-selected');
    $('.bulk-actions-bar').hide();
    $('#globalBulkActions').hide();
}

/**
 * Clear selection for specific class
 */
function clearClassSelection(classCode) {
    // Remove selections for this class
    const toRemove = [];
    selectedStudents.forEach(key => {
        if (key.startsWith(classCode + '-')) {
            toRemove.push(key);
        }
    });

    toRemove.forEach(key => selectedStudents.delete(key));

    // Update UI
    const classTable = $(`.attendance-table[data-class-code="${classCode}"]`);
    classTable.find('.student-checkbox, .select-all-students').prop('checked', false);
    classTable.find('.table-selected').removeClass('table-selected');

    updateBulkActions(classCode);
    updateGlobalBulkActions();
}

/**
 * Show SMS modal
 */
function showSmsModal() {
    if (selectedStudents.size === 0) {
        showNotification(getJsString('no-students-selected'), 'warning');
        return;
    }

    $('#smsRecipientCount').text(selectedStudents.size);
    $('#smsMessage').val(getJsString('absent-sms-default-global'));
    $('#smsModal').modal('show');
}

/**
 * Send SMS to selected parents
 */
function sendSmsToSelected() {
    const message = $('#smsMessage').val().trim();
    if (!message) {
        showNotification(getJsString('please-enter-message'), 'warning');
        return;
    }

    const studentCodes = Array.from(selectedStudents).map(key => {
        return parseInt(key.split('-')[1]);
    });

    showLoading();

    $.ajax({
        url: '/Reports/SendSmsToParents',
        type: 'POST',
        data: {
            studentCodes: studentCodes,
            message: message
        },
        success: function (data) {
            hideLoading();
            $('#smsModal').modal('hide');

            if (data.success) {
                showNotification(data.message, 'success');
                clearAllSelections();
            } else {
                showNotification(getJsString('sms-error') + ' ' + data.error, 'error');
            }
        },
        error: function (xhr, status, error) {
            hideLoading();
            showNotification(getJsString('sms-failed') + ' ' + error, 'error');
        }
    });
}

/**
 * Individual contact actions
 */
function callParent(studentName, parentPhone) {
    if (!parentPhone) {
        showNotification(getJsString('no-parent-phone-for').replace('{name}', studentName), 'warning');
        return;
    }

    window.location.href = `tel:${parentPhone}`;
}

function sendSmsToParent(studentName, parentPhone) {
    if (!parentPhone) {
        showNotification(getJsString('no-parent-phone-for').replace('{name}', studentName), 'warning');
        return;
    }

    const message = encodeURIComponent(getJsString('absent-sms-individual').replace('{name}', studentName));
    window.location.href = `sms:${parentPhone}?body=${message}`;
}

function viewStudentDetails(studentCode) {
    window.location.href = `/Students/Details/${studentCode}`;
}

/**
 * Call all absent parents in a class
 */
function callAllAbsentParents(classCode) {
    const classData = currentData.AttendanceDetails.find(c => c.ClassCode === classCode);
    if (!classData) return;

    const absentStudents = classData.Students.filter(s => !s.IsPresent && s.StudentParentPhone);

    if (absentStudents.length === 0) {
        showNotification(getJsString('no-absent-parent-phones'), 'info');
        return;
    }

    showNotification(getJsString('would-call-absent-parents').replace('{count}', absentStudents.length), 'info');
}

/**
 * Update global statistics
 */
function updateGlobalStats(data) {
    console.log("Individual class statistics are now shown in each card");
}

/**
 * Update pagination
 */
function updatePagination(data) {
    const startItem = ((data.currentPage - 1) * data.pageSize) + 1;  // lowercase
    const endItem = Math.min(data.currentPage * data.pageSize, data.totalCount);  // lowercase

    $('#paginationInfo').text(getJsString('pagination-info')
        .replace('{start}', startItem)
        .replace('{end}', endItem)
        .replace('{total}', data.totalCount));

    const paginationLinks = $('#paginationLinks');
    paginationLinks.empty();

    if (data.totalPages > 1) {  // lowercase
        // Previous button
        if (data.currentPage > 1) {  // lowercase
            paginationLinks.append(`
                <li class="page-item">
                    <a class="page-link" href="#" onclick="goToPage(${data.currentPage - 1})">${getJsString('previous')}</a>
                </li>
            `);
        }

        // Page numbers
        const startPage = Math.max(1, data.currentPage - 2);
        const endPage = Math.min(data.totalPages, data.currentPage + 2);

        for (let i = startPage; i <= endPage; i++) {
            paginationLinks.append(`
                <li class="page-item ${i === data.currentPage ? 'active' : ''}">
                    <a class="page-link" href="#" onclick="goToPage(${i})">${i}</a>
                </li>
            `);
        }

        // Next button
        if (data.currentPage < data.totalPages) {
            paginationLinks.append(`
                <li class="page-item">
                    <a class="page-link" href="#" onclick="goToPage(${data.currentPage + 1})">${getJsString('next')}</a>
                </li>
            `);
        }
    }
}
/**
 * Go to specific page
 */
function goToPage(page) {
    currentFilters.page = page;
    loadAttendanceData();
}

/**
 * Export data
 */
function exportData(format) {
    const params = new URLSearchParams({
        format: format,
        ...currentFilters
    });

    showNotification(getJsString('preparing-export').replace('{format}', format.toUpperCase()), 'info');
    window.location.href = `/Reports/ExportClassAttendance?${params.toString()}`;
}

/**
 * Export single class
 */
function exportSingleClass(classCode) {
    const params = new URLSearchParams({
        format: 'excel',
        classCode: classCode,
        ...currentFilters
    });

    showNotification(getJsString('preparing-class-export'), 'info');
    window.location.href = `/Reports/ExportClassAttendance?${params.toString()}`;
}

/**
 * Print report
 */
function printReport() {
    $('.filter-panel, .export-section, .pagination-modern, .bulk-actions-bar, #globalBulkActions').hide();
    $('body').addClass('print-mode');

    window.print();

    setTimeout(() => {
        $('.filter-panel, .export-section, .pagination-modern').show();
        $('body').removeClass('print-mode');
    }, 1000);
}

/**
 * Clear all filters
 */
function clearAllFilters() {
    $('#filterForm')[0].reset();
    currentFilters = {
        teacherCode: '',
        subjectCode: '',
        classDate: '',
        page: 1,
        pageSize: 10
    };
    loadAttendanceData();
}

/**
 * Utility functions
 */
function showLoading() {
    $('#loadingIndicator').show();
}

function hideLoading() {
    $('#loadingIndicator').hide();
}

function showAllSections() {
    $('#exportSection').show();
    $('#paginationContainer').show();
    $('#attendanceContainer').show();
}

function hideAllSections() {
    $('#exportSection').hide();
    $('#paginationContainer').hide();
    $('#attendanceContainer').hide();
    $('#noDataMessage').hide();
}

function showError(message) {
    showNotification(message, 'error');
    $('#noDataMessage').find('h4').text(getJsString('loading-error-title'));
    $('#noDataMessage').find('p').text(message);
    $('#noDataMessage').show();
}

function formatDate(dateString) {
    if (!dateString) return '';

    const date = new Date(dateString);
    const options = {
        weekday: 'short',
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    };

    return date.toLocaleDateString('en-US', options);
}

function showNotification(message, type = 'info') {
    // Remove existing notifications
    $('.notification-toast').remove();

    const typeClasses = {
        'success': 'alert-success',
        'error': 'alert-danger',
        'warning': 'alert-warning',
        'info': 'alert-info'
    };

    const icons = {
        'success': 'check-circle',
        'error': 'exclamation-circle',
        'warning': 'exclamation-triangle',
        'info': 'info-circle'
    };

    const notification = $(`
        <div class="alert ${typeClasses[type]} alert-dismissible fade show notification-toast position-fixed" 
             style="top: 20px; right: 20px; z-index: 9999; min-width: 350px; box-shadow: 0 4px 12px rgba(0,0,0,0.15);">
            <i class="fas fa-${icons[type]} me-2"></i>
            <strong>${message}</strong>
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `);

    $('body').append(notification);

    // Auto-remove after 5 seconds
    setTimeout(() => {
        notification.fadeOut(() => notification.remove());
    }, 5000);
}