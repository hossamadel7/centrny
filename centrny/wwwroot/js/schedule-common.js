/**
 * Schedule Management - Common JavaScript
 * Shared functionality for Index and Calendar views
 */

class ScheduleManager {
    constructor() {
        this.isEditMode = false;
        this.editingScheduleId = null;
        this.scheduleToDelete = null;
        this.allSchedules = [];
        this.userContext = {};

        // Initialize when DOM is ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.initialize());
        } else {
            this.initialize();
        }
    }

    // ==================== INITIALIZATION ====================

    initialize() {
        console.log('🔍 ScheduleManager: Initializing...');

        // Get user context from global variables (set by views)
        this.userContext = window.userContext || {};
        console.log('User Context:', this.userContext);

        if (!this.userContext.hasError) {
            this.setupEventHandlers();
            this.setupModalHandlers();
        }
    }

    setupEventHandlers() {
        // Anti-forgery token setup
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) {
            // Set up AJAX defaults
            this.setupAjaxDefaults(token);
        }

        // Save button handler
        const saveBtn = document.getElementById('saveScheduleBtn');
        if (saveBtn) {
            saveBtn.addEventListener('click', () => this.saveSchedule());
        }

        // Delete confirmation handler
        const confirmDeleteBtn = document.getElementById('confirmDeleteBtn');
        if (confirmDeleteBtn) {
            confirmDeleteBtn.addEventListener('click', () => {
                if (this.scheduleToDelete) {
                    this.performDelete(this.scheduleToDelete);
                }
            });
        }

        // Event details modal handlers
        const editEventBtn = document.getElementById('editEventBtn');
        const deleteEventBtn = document.getElementById('deleteEventBtn');

        if (editEventBtn) {
            editEventBtn.addEventListener('click', () => {
                if (this.scheduleToDelete) {
                    this.editSchedule(this.scheduleToDelete);
                }
            });
        }

        if (deleteEventBtn) {
            deleteEventBtn.addEventListener('click', () => {
                if (this.scheduleToDelete) {
                    this.deleteSchedule(this.scheduleToDelete);
                }
            });
        }
    }

    setupModalHandlers() {
        const scheduleModal = document.getElementById('scheduleModal');
        if (!scheduleModal) return;

        // Reset modal when closed
        scheduleModal.addEventListener('hidden.bs.modal', () => {
            this.resetModalForCreate();
        });

        // Load data when modal opens
        scheduleModal.addEventListener('shown.bs.modal', () => {
            console.log('🔍 MODAL OPENED - Starting AJAX loading...');

            if (this.userContext.isCenter && !this.isEditMode) {
                console.log('🔍 Center user detected - loading branches and teachers via AJAX...');
                this.loadBranchesForCenterUser();
                this.loadTeachersForCenterUser();
            }
        });
    }

    setupAjaxDefaults(token) {
        // Set default headers for all AJAX requests
        const originalFetch = window.fetch;
        window.fetch = function (url, options = {}) {
            options.headers = options.headers || {};
            options.headers['RequestVerificationToken'] = token;
            options.headers['Content-Type'] = options.headers['Content-Type'] || 'application/json';
            return originalFetch(url, options);
        };
    }

    // ==================== AJAX OPERATIONS ====================

    async loadBranchesForCenterUser() {
        console.log('🔍 === LOADING BRANCHES VIA AJAX ===');

        if (!this.userContext.isCenter) {
            console.log('❌ Not a center user, skipping branch loading');
            return;
        }

        const branchSelect = document.getElementById('branchCode');
        if (!branchSelect) {
            console.log('❌ Branch dropdown not found');
            return;
        }

        try {
            branchSelect.innerHTML = '<option value="">Loading branches...</option>';
            branchSelect.disabled = true;

            const response = await fetch('/Schedule/GetBranchesForCenterUser');
            const data = await response.json();

            console.log('🔍 Branch API Response:', data);

            if (data.success && data.branches) {
                branchSelect.innerHTML = '<option value="">Select Branch (Optional)</option>';
                data.branches.forEach(branch => {
                    const option = document.createElement('option');
                    option.value = branch.value;
                    option.textContent = branch.text;
                    branchSelect.appendChild(option);
                });

                // Store center code if provided
                if (data.centerCode) {
                    const centerCodeInput = document.getElementById('centerCode');
                    if (centerCodeInput) {
                        centerCodeInput.value = data.centerCode;
                    }
                }

                console.log('✅ Branches loaded successfully');
            } else {
                branchSelect.innerHTML = '<option value="">No branches available</option>';
                console.log('❌ Failed to load branches:', data.error);
            }
        } catch (error) {
            console.error('❌ Error loading branches:', error);
            branchSelect.innerHTML = '<option value="">Error loading branches</option>';
        } finally {
            branchSelect.disabled = false;
        }
    }

    async loadTeachersForCenterUser() {
        console.log('🔍 === LOADING TEACHERS VIA AJAX ===');

        if (!this.userContext.isCenter) {
            console.log('❌ Not a center user, skipping teacher loading');
            return;
        }

        const teacherSelect = document.getElementById('teacherCode');
        if (!teacherSelect) {
            console.log('❌ Teacher dropdown not found');
            return;
        }

        try {
            teacherSelect.innerHTML = '<option value="">Loading teachers...</option>';
            teacherSelect.disabled = true;

            const response = await fetch('/Schedule/GetTeachersForCenterUser');
            const data = await response.json();

            console.log('🔍 Teacher API Response:', data);

            if (data.success && data.teachers) {
                teacherSelect.innerHTML = '<option value="">Select Teacher</option>';
                data.teachers.forEach(teacher => {
                    const option = document.createElement('option');
                    option.value = teacher.value;
                    option.textContent = teacher.text;
                    teacherSelect.appendChild(option);
                });
                console.log('✅ Teachers loaded successfully');
            } else {
                teacherSelect.innerHTML = '<option value="">No teachers available</option>';
                console.log('❌ Failed to load teachers:', data.error);
            }
        } catch (error) {
            console.error('❌ Error loading teachers:', error);
            teacherSelect.innerHTML = '<option value="">Error loading teachers</option>';
        } finally {
            teacherSelect.disabled = false;
        }
    }

    async loadBranchesForCenter() {
        const centerSelect = document.getElementById('centerCode');
        const branchSelect = document.getElementById('branchCode');

        if (!centerSelect || !branchSelect) return;

        const centerCode = centerSelect.value;

        if (!centerCode) {
            branchSelect.innerHTML = '<option value="">Select Center First</option>';
            branchSelect.disabled = true;
            return;
        }

        try {
            branchSelect.innerHTML = '<option value="">Loading branches...</option>';
            branchSelect.disabled = true;

            const response = await fetch(`/Schedule/GetBranchesForCenter?centerCode=${centerCode}`);
            const data = await response.json();

            if (data.success && data.branches) {
                branchSelect.innerHTML = '<option value="">Select Branch (Optional)</option>';
                data.branches.forEach(branch => {
                    const option = document.createElement('option');
                    option.value = branch.value;
                    option.textContent = branch.text;
                    branchSelect.appendChild(option);
                });
            } else {
                branchSelect.innerHTML = '<option value="">No branches available</option>';
                this.showToast('Warning', data.error || 'Failed to load branches', 'warning');
            }
        } catch (error) {
            console.error('Error loading branches:', error);
            branchSelect.innerHTML = '<option value="">Error loading branches</option>';
            this.showToast('Error', 'Failed to load branches', 'error');
        } finally {
            branchSelect.disabled = false;
        }
    }

    async loadHallsForBranch() {
        const branchSelect = document.getElementById('branchCode');
        const hallSelect = document.getElementById('hallCode');

        if (!branchSelect || !hallSelect) return;

        const branchCode = branchSelect.value;

        if (!branchCode) {
            hallSelect.innerHTML = '<option value="">Select Branch First</option>';
            hallSelect.disabled = true;
            return;
        }

        try {
            hallSelect.innerHTML = '<option value="">Loading halls...</option>';
            hallSelect.disabled = true;

            const response = await fetch(`/Schedule/GetHallsForBranch?branchCode=${branchCode}`);
            const data = await response.json();

            if (data.success && data.halls) {
                hallSelect.innerHTML = '<option value="">Select Hall (Optional)</option>';
                data.halls.forEach(hall => {
                    const option = document.createElement('option');
                    option.value = hall.value;
                    option.textContent = `${hall.text}${hall.capacity ? ` (${hall.capacity})` : ''}`;
                    hallSelect.appendChild(option);
                });
            } else {
                hallSelect.innerHTML = '<option value="">No halls available</option>';
            }
        } catch (error) {
            console.error('Error loading halls:', error);
            hallSelect.innerHTML = '<option value="">Error loading halls</option>';
        } finally {
            hallSelect.disabled = false;
        }
    }

    async loadSubjectsForTeacher() {
        const teacherSelect = document.getElementById('teacherCode');
        const yearSelect = document.getElementById('yearCode');
        const subjectSelect = document.getElementById('subjectCode');

        if (!teacherSelect || !subjectSelect) return;

        const teacherCode = teacherSelect.value;
        const yearCode = yearSelect?.value;

        if (!teacherCode) {
            subjectSelect.innerHTML = '<option value="">Select Teacher First</option>';
            return;
        }

        try {
            subjectSelect.innerHTML = '<option value="">Loading subjects...</option>';

            let url = `/Schedule/GetSubjectsForTeacher?teacherCode=${teacherCode}`;
            if (yearCode) {
                url += `&yearCode=${yearCode}`;
            }

            const response = await fetch(url);
            const data = await response.json();

            if (data.success && data.subjects) {
                subjectSelect.innerHTML = '<option value="">Select Subject (Optional)</option>';
                data.subjects.forEach(subject => {
                    const option = document.createElement('option');
                    option.value = subject.value;
                    option.textContent = subject.text;
                    subjectSelect.appendChild(option);
                });
            } else {
                subjectSelect.innerHTML = '<option value="">No subjects available</option>';
            }
        } catch (error) {
            console.error('Error loading subjects:', error);
            subjectSelect.innerHTML = '<option value="">Error loading subjects</option>';
        }
    }

    // ==================== SCHEDULE OPERATIONS ====================

    async saveSchedule() {
        console.log('🔍 SAVE SCHEDULE - Starting validation...');

        if (!this.validateScheduleForm()) {
            return;
        }

        const scheduleData = this.collectFormData();
        console.log('🔍 Collected form data:', scheduleData);

        try {
            const url = this.isEditMode
                ? `/Schedule/EditScheduleEvent/${this.editingScheduleId}`
                : '/Schedule/CreateScheduleEvent';

            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(scheduleData)
            });

            const result = await response.json();
            console.log('🔍 Save API Response:', result);

            if (result.success) {
                this.showToast('Success',
                    this.isEditMode ? 'Schedule updated successfully!' : 'Schedule created successfully!',
                    'success');

                // Close modal
                const modal = bootstrap.Modal.getInstance(document.getElementById('scheduleModal'));
                if (modal) modal.hide();

                // Trigger refresh for specific view
                if (typeof this.onScheduleSaved === 'function') {
                    this.onScheduleSaved();
                }
            } else {
                this.showToast('Error', result.error || 'Failed to save schedule', 'error');
            }
        } catch (error) {
            console.error('❌ Error saving schedule:', error);
            this.showToast('Error', 'Failed to save schedule', 'error');
        }
    }

    validateScheduleForm() {
        const requiredFields = [
            { id: 'scheduleName', name: 'Schedule Name' },
            { id: 'dayOfWeek', name: 'Day of Week' },
            { id: 'startTime', name: 'Start Time' },
            { id: 'endTime', name: 'End Time' },
            { id: 'yearCode', name: 'Year' }
        ];

        // Add teacher requirement for center users
        if (this.userContext.isCenter) {
            requiredFields.push({ id: 'teacherCode', name: 'Teacher' });
        }

        for (const field of requiredFields) {
            const element = document.getElementById(field.id);
            if (!element || !element.value.trim()) {
                this.showToast('Validation Error', `${field.name} is required`, 'warning');
                element?.focus();
                return false;
            }
        }

        // Time validation
        const startTime = document.getElementById('startTime').value;
        const endTime = document.getElementById('endTime').value;

        if (startTime && endTime && startTime >= endTime) {
            this.showToast('Validation Error', 'End time must be after start time', 'warning');
            return false;
        }

        return true;
    }

    collectFormData() {
        const data = {
            title: document.getElementById('scheduleName')?.value?.trim(),
            dayOfWeek: document.getElementById('dayOfWeek')?.value,
            startTime: document.getElementById('startTime')?.value,
            endTime: document.getElementById('endTime')?.value,
            yearCode: parseInt(document.getElementById('yearCode')?.value) || null,
            hallCode: parseInt(document.getElementById('hallCode')?.value) || null,
            eduYearCode: parseInt(document.getElementById('eduYearCode')?.value) || null,
            centerCode: parseInt(document.getElementById('centerCode')?.value) || null,
            branchCode: parseInt(document.getElementById('branchCode')?.value) || null,
            teacherCode: parseInt(document.getElementById('teacherCode')?.value) || null,
            subjectCode: parseInt(document.getElementById('subjectCode')?.value) || null,
            scheduleAmount: parseFloat(document.getElementById('scheduleAmount')?.value) || null
        };

        return data;
    }

    async showScheduleDetails(scheduleId) {
        console.log('🔍 Showing details for schedule:', scheduleId);

        // Find schedule in loaded data or fetch it
        let schedule = this.allSchedules.find(s => s.scheduleCode == scheduleId);

        if (!schedule) {
            this.showToast('Error', 'Schedule not found', 'error');
            return;
        }

        // Store for edit/delete operations
        this.scheduleToDelete = scheduleId;

        // Build details HTML
        const detailsHtml = this.buildScheduleDetailsHtml(schedule);

        // Show modal
        const detailsContent = document.getElementById('eventDetailsContent');
        if (detailsContent) {
            detailsContent.innerHTML = detailsHtml;
        }

        const modal = new bootstrap.Modal(document.getElementById('eventDetailsModal'));
        modal.show();
    }

    buildScheduleDetailsHtml(schedule) {
        return `
            <div class="event-details-header">
                <div class="event-icon">
                    <i class="fas fa-calendar-check"></i>
                </div>
                <h4>${schedule.scheduleName || 'Untitled Schedule'}</h4>
            </div>
            <div class="event-details-body">
                <div class="detail-item">
                    <span class="detail-label">Day</span>
                    <span class="detail-value">${schedule.dayOfWeek || 'Not set'}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-label">Time</span>
                    <span class="detail-value">
                        ${schedule.startTime ? new Date('1970-01-01T' + schedule.startTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : 'Not set'} - 
                        ${schedule.endTime ? new Date('1970-01-01T' + schedule.endTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : 'Not set'}
                    </span>
                </div>
                <div class="detail-item">
                    <span class="detail-label">Type</span>
                    <span class="detail-value">
                        <span class="badge ${schedule.isCenter ? 'badge-center' : 'badge-teacher'}">
                            ${schedule.isCenter ? 'Center' : 'Teacher'}
                        </span>
                    </span>
                </div>
                ${schedule.hallName ? `
                <div class="detail-item">
                    <span class="detail-label">Hall</span>
                    <span class="detail-value">${schedule.hallName}</span>
                </div>` : ''}
                ${schedule.teacherName ? `
                <div class="detail-item">
                    <span class="detail-label">Teacher</span>
                    <span class="detail-value">${schedule.teacherName}</span>
                </div>` : ''}
                ${schedule.subjectName ? `
                <div class="detail-item">
                    <span class="detail-label">Subject</span>
                    <span class="detail-value">${schedule.subjectName}</span>
                </div>` : ''}
                ${schedule.centerName ? `
                <div class="detail-item">
                    <span class="detail-label">Center</span>
                    <span class="detail-value">${schedule.centerName}</span>
                </div>` : ''}
                ${schedule.branchName ? `
                <div class="detail-item">
                    <span class="detail-label">Branch</span>
                    <span class="detail-value">${schedule.branchName}</span>
                </div>` : ''}
                ${schedule.scheduleAmount ? `
                <div class="detail-item">
                    <span class="detail-label">Amount</span>
                    <span class="detail-value text-success fw-bold">$${parseFloat(schedule.scheduleAmount).toFixed(2)}</span>
                </div>` : ''}
            </div>
        `;
    }

    editSchedule(scheduleId) {
        console.log('🔍 Edit schedule:', scheduleId);

        const schedule = this.allSchedules.find(s => s.scheduleCode == scheduleId);
        if (!schedule) {
            this.showToast('Error', 'Schedule not found', 'error');
            return;
        }

        // Switch to edit mode
        this.isEditMode = true;
        this.editingScheduleId = scheduleId;

        // Close details modal
        const detailsModal = bootstrap.Modal.getInstance(document.getElementById('eventDetailsModal'));
        if (detailsModal) detailsModal.hide();

        // Populate form with schedule data
        this.populateFormForEdit(schedule);

        // Show schedule modal
        const modal = new bootstrap.Modal(document.getElementById('scheduleModal'));
        modal.show();
    }

    populateFormForEdit(schedule) {
        // Update modal title
        const modalTitle = document.getElementById('scheduleModalTitle');
        const modalHeader = document.getElementById('scheduleModalHeader');

        if (modalTitle) {
            modalTitle.innerHTML = '<i class="fas fa-edit me-2"></i>Edit Schedule';
        }
        if (modalHeader) {
            modalHeader.className = 'modal-header bg-warning';
        }

        // Populate form fields
        const fields = {
            'scheduleName': schedule.scheduleName,
            'dayOfWeek': schedule.dayOfWeek,
            'startTime': schedule.startTime?.substring(0, 5), // Extract HH:MM
            'endTime': schedule.endTime?.substring(0, 5),
            'yearCode': schedule.yearCode,
            'hallCode': schedule.hallCode,
            'eduYearCode': schedule.eduYearCode,
            'centerCode': schedule.centerCode,
            'branchCode': schedule.branchCode,
            'teacherCode': schedule.teacherCode,
            'subjectCode': schedule.subjectCode,
            'scheduleAmount': schedule.scheduleAmount
        };

        Object.entries(fields).forEach(([fieldId, value]) => {
            const element = document.getElementById(fieldId);
            if (element && value != null) {
                element.value = value;
            }
        });
    }

    deleteSchedule(scheduleId) {
        console.log('🔍 Delete schedule:', scheduleId);

        const schedule = this.allSchedules.find(s => s.scheduleCode == scheduleId);
        if (!schedule) {
            this.showToast('Error', 'Schedule not found', 'error');
            return;
        }

        this.scheduleToDelete = scheduleId;

        // Close details modal
        const detailsModal = bootstrap.Modal.getInstance(document.getElementById('eventDetailsModal'));
        if (detailsModal) detailsModal.hide();

        // Show confirmation modal
        const summaryElement = document.getElementById('deleteScheduleSummary');
        if (summaryElement) {
            summaryElement.innerHTML = `
                <h6><i class="fas fa-calendar-alt me-2"></i>${schedule.scheduleName}</h6>
                <p class="mb-1"><strong>Day:</strong> ${schedule.dayOfWeek}</p>
                <p class="mb-1"><strong>Time:</strong> ${schedule.startTime} - ${schedule.endTime}</p>
                ${schedule.teacherName ? `<p class="mb-1"><strong>Teacher:</strong> ${schedule.teacherName}</p>` : ''}
                ${schedule.hallName ? `<p class="mb-0"><strong>Hall:</strong> ${schedule.hallName}</p>` : ''}
            `;
        }

        const modal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
        modal.show();
    }

    async performDelete(scheduleId) {
        try {
            const response = await fetch(`/Schedule/DeleteScheduleEvent/${scheduleId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                }
            });

            const result = await response.json();

            if (result.success) {
                this.showToast('Success', 'Schedule deleted successfully!', 'success');

                // Close modal
                const modal = bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal'));
                if (modal) modal.hide();

                // Trigger refresh for specific view
                if (typeof this.onScheduleDeleted === 'function') {
                    this.onScheduleDeleted();
                }
            } else {
                this.showToast('Error', result.error || 'Failed to delete schedule', 'error');
            }
        } catch (error) {
            console.error('❌ Error deleting schedule:', error);
            this.showToast('Error', 'Failed to delete schedule', 'error');
        }
    }

    // ==================== MODAL MANAGEMENT ====================

    resetModalForCreate() {
        console.log('🔍 Resetting modal for create mode');

        this.isEditMode = false;
        this.editingScheduleId = null;

        // Reset modal title and header
        const modalTitle = document.getElementById('scheduleModalTitle');
        const modalHeader = document.getElementById('scheduleModalHeader');

        if (modalTitle) {
            modalTitle.innerHTML = '<i class="fas fa-calendar-plus me-2"></i>Add New Schedule';
        }
        if (modalHeader) {
            modalHeader.className = 'modal-header';
        }

        // Reset form
        const form = document.getElementById('scheduleForm');
        if (form) {
            const inputs = form.querySelectorAll('input, select, textarea');
            inputs.forEach(input => {
                if (input.type === 'hidden' && input.id === 'rootCode') {
                    // Keep root code
                    return;
                }
                if (input.id === 'centerCode' && this.userContext.isCenter) {
                    // Keep center code for center users
                    return;
                }
                input.value = '';
            });
        }

        // Reset dropdowns that depend on selections
        this.resetDependentDropdowns();
    }

    resetDependentDropdowns() {
        const dropdownsToReset = [
            { id: 'branchCode', defaultText: this.userContext.isCenter ? 'Select Branch (Optional)' : 'Select Center First' },
            { id: 'hallCode', defaultText: 'Select Branch First' },
            { id: 'subjectCode', defaultText: 'Select Subject (Optional)' }
        ];

        dropdownsToReset.forEach(dropdown => {
            const element = document.getElementById(dropdown.id);
            if (element) {
                element.innerHTML = `<option value="">${dropdown.defaultText}</option>`;
            }
        });
    }

    // ==================== UTILITY METHODS ====================

    showToast(title, message, type = 'info') {
        // Create toast container if it doesn't exist
        let container = document.querySelector('.toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container position-fixed top-0 end-0 p-3';
            document.body.appendChild(container);
        }

        // Create toast element
        const toastId = 'toast-' + Date.now();
        const bgColor = {
            'success': 'bg-success',
            'error': 'bg-danger',
            'warning': 'bg-warning',
            'info': 'bg-info'
        }[type] || 'bg-info';

        const toastHtml = `
            <div id="${toastId}" class="toast ${bgColor} text-white" role="alert">
                <div class="toast-header ${bgColor} text-white border-0">
                    <i class="fas fa-${this.getToastIcon(type)} me-2"></i>
                    <strong class="me-auto">${title}</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;

        container.insertAdjacentHTML('beforeend', toastHtml);

        // Show toast
        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement, { delay: 4000 });
        toast.show();

        // Remove from DOM after hiding
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    }

    getToastIcon(type) {
        const icons = {
            'success': 'check-circle',
            'error': 'exclamation-triangle',
            'warning': 'exclamation-triangle',
            'info': 'info-circle'
        };
        return icons[type] || 'info-circle';
    }

    // ==================== PUBLIC API ====================

    // These methods can be overridden by specific views
    onScheduleSaved() {
        console.log('Schedule saved - override this method in specific view');
    }

    onScheduleDeleted() {
        console.log('Schedule deleted - override this method in specific view');
    }

    // Global functions for backward compatibility
    static createGlobalFunctions(manager) {
        window.loadBranchesForCenterUser = () => manager.loadBranchesForCenterUser();
        window.loadBranchesForCenter = () => manager.loadBranchesForCenter();
        window.loadHallsForBranch = () => manager.loadHallsForBranch();
        window.loadSubjectsForTeacher = () => manager.loadSubjectsForTeacher();
        window.showScheduleDetails = (id) => manager.showScheduleDetails(id);
        window.editSchedule = (id) => manager.editSchedule(id);
        window.deleteSchedule = (id) => manager.deleteSchedule(id);
        window.addScheduleForDay = (day) => manager.addScheduleForDay(day);
    }

    addScheduleForDay(dayOfWeek) {
        // Set the day in the form and open modal
        this.resetModalForCreate();

        setTimeout(() => {
            const daySelect = document.getElementById('dayOfWeek');
            if (daySelect) {
                daySelect.value = dayOfWeek;
            }
        }, 100);

        const modal = new bootstrap.Modal(document.getElementById('scheduleModal'));
        modal.show();
    }
}

// Initialize the manager and create global functions
const scheduleManager = new ScheduleManager();
ScheduleManager.createGlobalFunctions(scheduleManager);

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ScheduleManager;
}

// On modal open
function loadEduYears() {
    var rootCode = $('#rootCode').val();
    $.getJSON('/api/EduYear/GetByRootCode', { rootCode }, function (data) {
        fillDropdown('#eduYearCode', data, 'EduCode', 'EduName');
    });
}

// On EduYear change
$('#eduYearCode').on('change', function () {
    var eduYearCode = $(this).val();
    $.getJSON('/api/Year/GetByEduYearCode', { eduYearCode }, function (data) {
        fillDropdown('#yearCode', data, 'YearCode', 'YearName');
    });
});

// On Subject or Teacher change
$('#subjectCode, #teacherCode').on('change', function () {
    var teacherCode = $('#teacherCode').val();
    var subjectCode = $('#subjectCode').val();
    if (teacherCode && subjectCode) {
        $.getJSON('/api/Teach/GetYearsForSubjectTeacher', { teacherCode, subjectCode }, function (data) {
            fillDropdown('#yearNamesByTeach', data, 'YearCode', 'YearName'); // You can adapt for your UI
        });
    }
});

// Helper
function fillDropdown(selector, data, valueField, textField) {
    var $dropdown = $(selector);
    $dropdown.empty();
    $dropdown.append($('<option>').val('').text('Select...'));
    $.each(data, function (i, item) {
        $dropdown.append($('<option>').val(item[valueField]).text(item[textField]));
    });
}