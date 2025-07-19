// schedule-common.js - Complete updated version with teacher/center display logic, now with full localization support
// Updated: 2025-07-10 23:57:23 UTC by Copilot

// Helper for localization: fetch strings from #js-localization
function getJsString(key) {
    const el = document.getElementById('js-localization');
    if (!el) return key;
    key = key.replace(/([A-Z])/g, '-$1').toLowerCase();
    return el.dataset[key] || key;
}

class ScheduleManager {
    constructor() {
        this.schedules = [];
        this.editMode = false;
        this.currentScheduleId = null;
        this.lastViewedScheduleCode = null;
        this.codeToDelete = null;
        this.dom = {};
        this.debounceTimeouts = {};
        document.addEventListener('DOMContentLoaded', () => this.init());
    }

    init() {
        this.cacheDom();
        this.loadSchedules();
        this.setupFormListeners();
        this.setupDropdownListeners();
        this.setupModalEditButton();
        this.setupModalDeleteButton();
        this.setupGlobalActions();
    }

    localizeStaticUi() {
        // Modal & button labels (if present)
        if (this.dom.modalTitle) this.dom.modalTitle.innerHTML = `<i class="fas fa-calendar-plus me-2"></i>${getJsString('createNewScheduleBtn')}`;
        if (this.dom.saveScheduleBtn) this.dom.saveScheduleBtn.querySelector('.btn-text').textContent = getJsString('saveScheduleBtn');
        // Schedule Modal form labels
        const labelMap = [
            ['scheduleNameLabel', 'scheduleNameLabel'],
            ['dayOfWeekLabel', 'dayOfWeekLabel'],
            ['startTimeLabel', 'startTimeLabel'],
            ['endTimeLabel', 'endTimeLabel'],
            ['branchLabel', 'branchLabel'],
            ['centerLabel', 'centerLabel'],
            ['hallLabel', 'hallLabel'],
            ['educationalYearLabel', 'educationalYearLabel'],
            ['yearLabel', 'yearLabel'],
            ['teacherLabel', 'teacherLabel'],
            ['subjectLabel', 'subjectLabel'],
            ['amountLabel', 'amountLabel']
        ];
        labelMap.forEach(([id, key]) => {
            const el = document.getElementById(id);
            if (el) el.textContent = getJsString(key);
        });
        // Modal footers
        const cancelBtns = document.querySelectorAll('button.btn-secondary[data-bs-dismiss="modal"]');
        cancelBtns.forEach(btn => btn.textContent = getJsString('cancelBtn'));
        // Table headers
        document.querySelectorAll('th').forEach(th => {
            const txt = th.textContent.trim();
            if (txt === "Schedule Name") th.textContent = getJsString('scheduleName');
            else if (txt === "Day") th.textContent = getJsString('day');
            else if (txt === "Time") th.textContent = getJsString('time');
            else if (txt === "Type") th.textContent = getJsString('type');
            else if (txt === "Hall") th.textContent = getJsString('hall');
            else if (txt === "Center / Branch") th.textContent = getJsString('centerBranch');
            else if (txt === "Teacher / Subject") th.textContent = getJsString('teacherSubject');
            else if (txt === "Amount") th.textContent = getJsString('amount');
            else if (txt === "Actions") th.textContent = getJsString('actions');
        });
        // Day headers in grid
        ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'].forEach(day => {
            const dayHeader = document.querySelector(`.day-header[data-day="${day}"]`);
            if (dayHeader) dayHeader.textContent = getJsString(day.toLowerCase());
        });
        // Placeholder/localization for selects
        if (this.dom.dayOfWeek) this.dom.dayOfWeek.querySelector('option[value=""]').textContent = getJsString('selectDayOption');
        if (this.dom.centerCode) this.dom.centerCode.querySelector('option[value=""]').textContent = getJsString('selectCenterOption');
        if (this.dom.branchCode) this.dom.branchCode.querySelector('option[value=""]').textContent = getJsString('selectBranchOption');
        if (this.dom.hallCode) this.dom.hallCode.querySelector('option[value=""]').textContent = getJsString('selectHallOption');
        if (this.dom.yearCode) this.dom.yearCode.querySelector('option[value=""]').textContent = getJsString('selectYearOption');
        if (this.dom.teacherCode) this.dom.teacherCode.querySelector('option[value=""]').textContent = getJsString('selectTeacherOption');
        if (this.dom.subjectCode) this.dom.subjectCode.querySelector('option[value=""]').textContent = getJsString('selectSubjectOption');
    }

    cacheDom() {
        this.dom = {
            loader: document.getElementById('initialLoader'),
            scheduleForm: document.getElementById('scheduleForm'),
            scheduleModal: document.getElementById('scheduleModal'),
            modalTitle: document.getElementById('scheduleModalTitle'),
            scheduleName: document.getElementById('scheduleName'),
            dayOfWeek: document.getElementById('dayOfWeek'),
            startTime: document.getElementById('startTime'),
            endTime: document.getElementById('endTime'),
            rootCode: document.getElementById('rootCode'),
            centerCode: document.getElementById('centerCode'),
            branchCode: document.getElementById('branchCode'),
            hallCode: document.getElementById('hallCode'),
            eduYearCode: document.getElementById('eduYearCode'),
            teacherCode: document.getElementById('teacherCode'),
            subjectCode: document.getElementById('subjectCode'),
            yearCode: document.getElementById('yearCode'),
            scheduleAmount: document.getElementById('scheduleAmount'),
            floatingAddBtn: document.querySelector('.floating-add-btn'),
            eventDetailsContent: document.getElementById('eventDetailsContent'),
            saveScheduleBtn: document.getElementById('saveScheduleBtn'),
            editEventBtn: document.getElementById('editEventBtn'),
            deleteEventBtn: document.getElementById('deleteEventBtn'),
            deleteConfirmBtn: document.getElementById('confirmDeleteBtn'),
            deleteConfirmModal: document.getElementById('deleteConfirmModal'),
            deleteScheduleSummary: document.getElementById('deleteScheduleSummary')
        };
    }

    // Main Schedules Loader
    async loadSchedules() {
        this.showLoader && this.showLoader();
        try {
            let url = '/Schedule/GetCalendarEvents?start=2000-01-01&end=2030-12-31';
            if (window.selectedBranchCode) {
                url += '&branchCode=' + encodeURIComponent(window.selectedBranchCode);
            }
            const res = await fetch(url);
            const data = await res.json();
            this.schedules = Array.isArray(data) ? data : [];
            this.renderWeeklyGrid();
        } catch (e) {
            this.showToast && this.showToast('Failed to load schedules', 'error');
        } finally {
            this.hideLoader && this.hideLoader();
        }
    }

    // Save (Create/Edit) Schedule
    async saveSchedule(formData) {
        this.showModalLoader(true);
        try {
            const url = this.editMode && this.currentScheduleId
                ? `/Schedule/EditScheduleEvent/${this.currentScheduleId}`
                : '/Schedule/CreateScheduleEvent';
            const res = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                },
                body: JSON.stringify(formData)
            });
            const result = await res.json();
            if (result.success) {
                this.closeModal('scheduleModal');
                this.resetForm();
                await this.loadSchedules();
                this.showToast(this.editMode ? 'Schedule updated!' : 'Schedule created!', 'success');
            } else {
                this.showToast(result.error, 'error');
            }
        } catch (e) {
            this.showToast('Error saving schedule', 'error');
        } finally {
            this.showModalLoader(false);
        }
    }

    // Delete Schedule (from modal confirm only)
    async deleteSchedule(id) {
        this.showModalDeleteSpinner(true);
        try {
            const res = await fetch(`/Schedule/DeleteScheduleEvent/${id}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                }
            });
            const result = await res.json();
            if (result.success) {
                await this.loadSchedules();
                this.showToast('Schedule deleted!', 'success');
                this.closeModal('deleteConfirmModal');
                this.closeModal('eventDetailsModal');
                this.codeToDelete = null;
            } else {
                this.showToast(result.error, 'error');
            }
        } catch (e) {
            this.showToast('Error deleting schedule', 'error');
        } finally {
            this.showModalDeleteSpinner(false);
        }
    }

    // Dropdowns AJAX
    async loadDropdown(url, selectId, placeholder = 'Select...', selectedValue = null) {
        console.log(`[DEBUG] Loading dropdown ${selectId} from ${url}`);
        const select = document.getElementById(selectId);
        if (!select) {
            console.log(`[ERROR] Select element ${selectId} not found`);
            return;
        }

        select.innerHTML = `<option value="">${placeholder}</option>`;

        try {
            console.log(`[DEBUG] Fetching: ${url}`);
            const res = await fetch(url);
            const data = await res.json();
            console.log(`[DEBUG] Response for ${selectId}:`, data);

            if (data.success) {
                const arr = data.centers || data.branches || data.teachers || data.subjects || data.years || data.halls || [];
                console.log(`[DEBUG] Found ${arr.length} items for ${selectId}`);

                arr.forEach(item => {
                    const option = document.createElement('option');
                    option.value = item.value;
                    option.textContent = item.text;
                    if (selectedValue && String(selectedValue) === String(item.value)) {
                        option.selected = true;
                    }
                    select.appendChild(option);
                });

                console.log(`[SUCCESS] Populated ${selectId} with ${arr.length} options`);
            } else {
                console.log(`[ERROR] API returned success=false for ${selectId}:`, data.error);
                this.showToast(data.error || `Error loading ${selectId}`, 'error');
            }
        } catch (e) {
            console.log(`[ERROR] Exception loading ${selectId}:`, e);
            this.showToast(`Error loading ${selectId}`, 'error');
        }
    }

    // ...inside ScheduleManager class...

    async preloadDropdownsForModal(schedule = null) {
        console.log('[DEBUG] preloadDropdownsForModal called with schedule:', schedule);
        console.log('[DEBUG] window.userContext:', window.userContext);

        // Check if user is center or teacher
        const isCenter = window.userContext?.isCenter === true;
        const isTeacher = window.userContext?.isTeacher === true;

        console.log('[DEBUG] User type - isCenter:', isCenter, 'isTeacher:', isTeacher);

        let promises = [];

        if (isCenter) {
            // For CENTER USERS: 
            // If branch selection is a dropdown (center admin, no groupBranchCode), load branches
            if (!window.userContext?.groupBranchCode) {
                promises.push(
                    this.loadDropdown('/Schedule/GetBranchesForCenterUser', 'branchCode', 'Select Branch (Optional)', schedule?.extendedProps?.branchCode)
                );
            }

            // Always load teachers for center users
            promises.push(
                this.loadDropdown('/Schedule/GetTeachersForCenterUser', 'teacherCode', 'Select Teacher', schedule?.extendedProps?.teacherCode)
            );
        } else {
            // For TEACHER USERS: Load centers first
            promises.push(
                this.loadDropdown('/Schedule/GetCentersForUserRoot', 'centerCode', 'Select Center', schedule?.extendedProps?.centerCode)
            );
        }

        // Load years based on user type
        if (isTeacher) {
            // For teachers, load years from their teaching assignments
            promises.push(
                this.loadDropdown('/Schedule/GetYearsForTeacherRoot', 'yearCode', 'Select Year', schedule?.extendedProps?.yearCode)
            );
        } else if (isCenter) {
            // For centers, load years by educational year if available
            const eduYearCode = schedule?.extendedProps?.eduYearCode;
            if (eduYearCode) {
                promises.push(
                    this.loadDropdown(`/Schedule/GetYearsByEduYear?eduYearCode=${eduYearCode}`, 'yearCode', 'Select Year', schedule?.extendedProps?.yearCode)
                );
            }
        }

        // Wait for initial loads
        console.log('[DEBUG] Waiting for initial dropdown loads...');
        await Promise.all(promises);
        console.log('[DEBUG] Initial dropdown loads completed');

        // Now load dependent dropdowns
        if (isCenter) {
            // For center users WITH groupBranchCode (branch is auto-selected as hidden input):
            if (window.userContext?.groupBranchCode) {
                const autoBranchCode = window.userContext.groupBranchCode || document.getElementById('branchCode')?.value;
                if (autoBranchCode) {
                    await this.loadDropdown(`/Schedule/GetHallsForBranch?branchCode=${autoBranchCode}`, 'hallCode', 'Select Hall', schedule?.extendedProps?.hallCode);
                } else {
                    if (this.dom.hallCode) {
                        this.dom.hallCode.innerHTML = '<option value="">Select Branch First</option>';
                    }
                }
            } else {
                // For center admin: branchCode is a dropdown, load halls if a branch is pre-selected (editing) 
                const branchVal = schedule?.extendedProps?.branchCode || this.dom.branchCode?.value;
                if (branchVal) {
                    await this.loadDropdown(`/Schedule/GetHallsForBranch?branchCode=${branchVal}`, 'hallCode', 'Select Hall', schedule?.extendedProps?.hallCode);
                } else {
                    if (this.dom.hallCode) {
                        this.dom.hallCode.innerHTML = '<option value="">Select Branch First</option>';
                    }
                }
            }
        } else {
            // For teacher users, if we have a center selected, load branches
            if (schedule?.extendedProps?.centerCode || this.dom.centerCode?.value) {
                const centerVal = schedule?.extendedProps?.centerCode || this.dom.centerCode?.value;
                await this.loadDropdown(`/Schedule/GetBranchesForCenter?centerCode=${centerVal}`, 'branchCode', 'Select Branch', schedule?.extendedProps?.branchCode);

                // Then load halls if branch is selected
                const branchVal = schedule?.extendedProps?.branchCode || this.dom.branchCode?.value;
                if (branchVal) {
                    await this.loadDropdown(`/Schedule/GetHallsForBranch?branchCode=${branchVal}`, 'hallCode', 'Select Hall', schedule?.extendedProps?.hallCode);
                } else {
                    if (this.dom.hallCode) this.dom.hallCode.innerHTML = '<option value="">Select Branch First</option>';
                }
            } else {
                if (this.dom.branchCode) this.dom.branchCode.innerHTML = '<option value="">Select Center First</option>';
                if (this.dom.hallCode) this.dom.hallCode.innerHTML = '<option value="">Select Branch First</option>';
            }
        }

        // Load subjects based on user type
        if (isCenter && (schedule?.extendedProps?.teacherCode || this.dom.teacherCode?.value) && (schedule?.extendedProps?.yearCode || this.dom.yearCode?.value)) {
            const teacherId = schedule?.extendedProps?.teacherCode || this.dom.teacherCode?.value;
            const yearId = schedule?.extendedProps?.yearCode || this.dom.yearCode?.value;
            let url = `/Schedule/GetSubjectsForTeacher?teacherCode=${teacherId}&yearCode=${yearId}`;
            await this.loadDropdown(url, 'subjectCode', 'Select Subject', schedule?.extendedProps?.subjectCode);
        } else if (isTeacher && (schedule?.extendedProps?.yearCode || this.dom.yearCode?.value)) {
            await this.loadSubjectsForTeacherByYearAndBranch(schedule?.extendedProps?.subjectCode);
        } else {
            if (this.dom.subjectCode) this.dom.subjectCode.innerHTML = '<option value="">Select Subject</option>';
        }

        console.log('[DEBUG] preloadDropdownsForModal completed');
    }
    setupDropdownListeners() {
        this.dom.centerCode?.addEventListener('change', async (e) => {
            const val = e.target.value;
            if (val) {
                await this.loadDropdown(`/Schedule/GetBranchesForCenter?centerCode=${val}`, 'branchCode', getJsString('selectBranchOption'));
                if (this.dom.hallCode) this.dom.hallCode.innerHTML = `<option value="">${getJsString('selectBranchOption')}</option>`;
            } else {
                if (this.dom.branchCode) this.dom.branchCode.innerHTML = `<option value="">${getJsString('selectCenterOption')}</option>`;
                if (this.dom.hallCode) this.dom.hallCode.innerHTML = `<option value="">${getJsString('selectBranchOption')}</option>`;
            }
        });
        this.dom.branchCode?.addEventListener('change', (e) => {
            const val = e.target.value;
            if (val) {
                this.loadDropdown(`/Schedule/GetHallsForBranch?branchCode=${val}`, 'hallCode', getJsString('selectHallOption'));
            } else {
                if (this.dom.hallCode) this.dom.hallCode.innerHTML = `<option value="">${getJsString('selectBranchOption')}</option>`;
            }
        });
        this.dom.teacherCode?.addEventListener('change', (e) => {
            const teacherId = e.target.value;
            const yearId = this.dom.yearCode?.value;
            if (teacherId && yearId) {
                let url = `/Schedule/GetSubjectsForTeacher?teacherCode=${teacherId}&yearCode=${yearId}`;
                this.loadDropdown(url, 'subjectCode', getJsString('selectSubjectOption'));
            } else {
                if (this.dom.subjectCode) this.dom.subjectCode.innerHTML = `<option value="">${getJsString('selectSubjectOption')}</option>`;
            }
        });
        this.dom.yearCode?.addEventListener('change', (e) => {
            if (window.userContext?.isTeacher) {
                this.loadSubjectsForTeacherByYearAndBranch();
            } else if (window.userContext?.isCenter) {
                const teacherId = this.dom.teacherCode?.value;
                const yearId = e.target.value;
                if (teacherId && yearId) {
                    let url = `/Schedule/GetSubjectsForTeacher?teacherCode=${teacherId}&yearCode=${yearId}`;
                    this.loadDropdown(url, 'subjectCode', getJsString('selectSubjectOption'));
                } else {
                    if (this.dom.subjectCode) this.dom.subjectCode.innerHTML = `<option value="">${getJsString('selectSubjectOption')}</option>`;
                }
            }
        });
        this.dom.branchCode?.addEventListener('change', (e) => {
            if (window.userContext?.isTeacher) {
                this.loadSubjectsForTeacherByYearAndBranch();
            }
        });
        this.dom.eduYearCode?.addEventListener('change', (e) => {
            const eduYearId = e.target.value;
            if (eduYearId) {
                this.loadDropdown(`/Schedule/GetYearsByEduYear?eduYearCode=${eduYearId}`, 'yearCode', getJsString('selectYearOption'));
            } else {
                if (this.dom.yearCode) this.dom.yearCode.innerHTML = `<option value="">${getJsString('selectYearOption')}</option>`;
            }
        });
    }

    async loadSubjectsForTeacherByYearAndBranch(selectedValue = null) {
        const yearCode = this.dom.yearCode?.value;
        const branchCode = this.dom.branchCode?.value;
        const subjectSelect = this.dom.subjectCode;
        if (!yearCode || !subjectSelect) return;
        try {
            const tRes = await fetch('/Schedule/GetYearsForTeacherRoot');
            const tData = await tRes.json();
            if (!tData.success || !tData.teacherCode) return;
            let url = `/Schedule/GetSubjectsForTeacherByYearAndBranch?teacherCode=${tData.teacherCode}&yearCode=${yearCode}`;
            if (branchCode) url += `&branchCode=${branchCode}`;
            await this.loadDropdown(url, 'subjectCode', 'Select Subject', selectedValue);
        } catch (e) {
            this.showToast('Failed to load teacher info', 'error');
        }
    }

    setupFormListeners() {
        if (this.dom.saveScheduleBtn) {
            this.dom.saveScheduleBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                this.dom.saveScheduleBtn.disabled = true;
                this.showModalLoader(true);
                const formData = this.collectFormData();
                await this.saveSchedule(formData);
                this.dom.saveScheduleBtn.disabled = false;
            });
        }

        if (this.dom.floatingAddBtn) {
            this.dom.floatingAddBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                console.log('[DEBUG] Floating add button clicked');
                this.editMode = false;
                this.currentScheduleId = null;
                this.resetForm();
                this.showModalLoader(true);
                this.openModal('scheduleModal');
                await this.preloadDropdownsForModal();
                this.populateFormWithSchedule();
                this.showModalLoader(false);
                if (this.dom.modalTitle) {
                    this.dom.modalTitle.innerHTML = `<i class="fas fa-calendar-plus me-2"></i>Add New Schedule`;
                }
            });
        }

        // ALSO LISTEN TO THE BUTTON IN THE MODAL HEADER
        const addScheduleBtn = document.querySelector('[data-bs-target="#scheduleModal"]');
        if (addScheduleBtn) {
            addScheduleBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                console.log('[DEBUG] Add schedule button clicked');
                this.editMode = false;
                this.currentScheduleId = null;
                this.resetForm();
                this.showModalLoader(true);
                setTimeout(async () => {
                    await this.preloadDropdownsForModal();
                    this.populateFormWithSchedule();
                    this.showModalLoader(false);
                }, 100);
                if (this.dom.modalTitle) {
                    this.dom.modalTitle.innerHTML = `<i class="fas fa-calendar-plus me-2"></i>Add New Schedule`;
                }
            });
        }
    }

    setupModalEditButton() {
        const editEventBtn = this.dom.editEventBtn || document.getElementById('editEventBtn');
        if (editEventBtn) {
            editEventBtn.addEventListener('click', async () => {
                if (this.lastViewedScheduleCode) {
                    this.showModalLoader(true);
                    this.editMode = true;
                    this.currentScheduleId = this.lastViewedScheduleCode;
                    const schedule = this.schedules.find(s => s.extendedProps?.scheduleCode === this.lastViewedScheduleCode);
                    this.resetForm();
                    this.openModal('scheduleModal');
                    await this.preloadDropdownsForModal(schedule);
                    this.populateFormWithSchedule(schedule);
                    this.showModalLoader(false);
                    if (this.dom.modalTitle) {
                        this.dom.modalTitle.innerHTML = `<i class="fas fa-edit me-2"></i>Edit Schedule: ${schedule.title}`;
                    }
                    this.closeModal('eventDetailsModal');
                }
            });
        }
    }

    setupModalDeleteButton() {
        const deleteEventBtn = this.dom.deleteEventBtn || document.getElementById('deleteEventBtn');
        if (deleteEventBtn) {
            deleteEventBtn.addEventListener('click', () => {
                if (this.lastViewedScheduleCode) {
                    this.codeToDelete = this.lastViewedScheduleCode;
                    if (this.dom.deleteScheduleSummary) {
                        const schedule = this.schedules.find(s => s.extendedProps?.scheduleCode === this.codeToDelete);
                        this.dom.deleteScheduleSummary.innerHTML = schedule ? this.generateScheduleDetailsHTML(schedule) : "";
                    }
                    this.openModal('deleteConfirmModal');
                }
            });
        }
        const confirmDeleteBtn = this.dom.deleteConfirmBtn || document.getElementById('confirmDeleteBtn');
        if (confirmDeleteBtn) {
            confirmDeleteBtn.addEventListener('click', async () => {
                if (this.codeToDelete) {
                    await this.deleteSchedule(this.codeToDelete);
                }
            });
        }
    }

    collectFormData() {
        return {
            title: this.dom.scheduleName?.value?.trim(),
            dayOfWeek: this.dom.dayOfWeek?.value,
            startTime: this.dom.startTime?.value,
            endTime: this.dom.endTime?.value,
            rootCode: parseInt(this.dom.rootCode?.value) || null,
            centerCode: parseInt(this.dom.centerCode?.value) || null,
            branchCode: parseInt(this.dom.branchCode?.value) || null,
            hallCode: parseInt(this.dom.hallCode?.value) || null,
            eduYearCode: parseInt(this.dom.eduYearCode?.value) || null,
            teacherCode: parseInt(this.dom.teacherCode?.value) || null,
            subjectCode: parseInt(this.dom.subjectCode?.value) || null,
            yearCode: parseInt(this.dom.yearCode?.value) || null,
            scheduleAmount: parseFloat(this.dom.scheduleAmount?.value) || null
        };
    }

    // UPDATED: Weekly grid rendering with teacher/center display logic
    renderWeeklyGrid() {
        const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
        for (const day of days) {
            const dayContainer = document.getElementById(`day-${day}`);
            if (!dayContainer) continue;
            const daySchedules = this.schedules.filter(s => s.extendedProps?.dayOfWeek === day);
            if (daySchedules.length === 0) {
                dayContainer.innerHTML = `
                    <div class="empty-day">No schedules</div>
                    <button class="add-schedule-btn" onclick="scheduleManager.addScheduleForDay('${day}')" title="Add schedule for ${day}">
                        <i class="fas fa-plus"></i>
                    </button>
                `;
            } else {
                let html = '';
                for (const schedule of daySchedules) {
                    const eventClass = schedule.extendedProps?.isCenter ? 'center-event' : 'teacher-event';
                    const s = schedule.extendedProps;

                    // Determine what to show based on user context
                    const isCurrentUserCenter = window.userContext?.isCenter === true;
                    const isCurrentUserTeacher = window.userContext?.isTeacher === true;

                    html += `
                        <div class="schedule-event ${eventClass}" onclick="scheduleManager.showScheduleDetails(${s?.scheduleCode || 0})">
                            <div class="event-title">
                                <i class="fas fa-${s?.isCenter ? 'building' : 'user'}"></i>
                                ${schedule.title || 'Untitled'}
                            </div>
                            <div class="event-time">${s?.startTime || ''} - ${s?.endTime || ''}</div>
                            ${s?.hallName ? `<div class="event-details">📍 ${s.hallName}</div>` : ''}
                    `;

                    // Show different information based on current user type
                    if (isCurrentUserTeacher) {
                        // For TEACHER users: Show Center + Branch instead of teacher name
                        if (s?.centerName && s?.branchName) {
                            html += `<div class="event-details">🏢 ${s.centerName} - ${s.branchName}</div>`;
                        } else if (s?.centerName) {
                            html += `<div class="event-details">🏢 ${s.centerName}</div>`;
                        } else if (s?.branchName) {
                            html += `<div class="event-details">🏢 ${s.branchName}</div>`;
                        }
                    } else if (isCurrentUserCenter) {
                        // For CENTER users: Show Teacher name as before
                        if (s?.teacherName) {
                            html += `<div class="event-details">👨‍🏫 ${s.teacherName}</div>`;
                        }
                    }

                    // Always show subject if available
                    if (s?.subjectName) {
                        html += `<div class="event-details">📚 ${s.subjectName}</div>`;
                    }

                    html += `
                                <div style="margin-top:6px">
                                    <button class="btn btn-sm btn-light" onclick="event.stopPropagation();scheduleManager.editSchedule(${s?.scheduleCode})" title="Edit"><i class="fas fa-edit"></i></button>
                                    <button class="btn btn-sm btn-danger" onclick="event.stopPropagation();scheduleManager.handleDeleteRequest(${s?.scheduleCode})" title="Delete"><i class="fas fa-trash"></i></button>
                                </div>
                            </div>
                        `;
                }

                html += `
                    <button class="add-schedule-btn" onclick="scheduleManager.addScheduleForDay('${day}')" title="Add schedule for ${day}">
                        <i class="fas fa-plus"></i>
                    </button>
                `;

                dayContainer.innerHTML = html;
            }
        }
    }

    addScheduleForDay(day) {
        console.log('[DEBUG] addScheduleForDay called for:', day);
        this.editMode = false;
        this.currentScheduleId = null;
        this.resetForm();
        this.showModalLoader(true);
        this.openModal('scheduleModal');
        if (this.dom.dayOfWeek) this.dom.dayOfWeek.value = day;
        this.preloadDropdownsForModal().then(() => {
            this.populateFormWithSchedule();
            this.showModalLoader(false);
        });
        if (this.dom.modalTitle) {
            this.dom.modalTitle.innerHTML = `<i class="fas fa-calendar-plus me-2"></i>Add New Schedule for ${day}`;
        }
    }

    showScheduleDetails(scheduleCode) {
        const schedule = this.schedules.find(s => s.extendedProps?.scheduleCode === scheduleCode);
        if (!schedule) {
            this.showToast('Schedule not found', 'error');
            return;
        }
        this.lastViewedScheduleCode = scheduleCode;
        if (this.dom.eventDetailsContent) {
            this.dom.eventDetailsContent.innerHTML = this.generateScheduleDetailsHTML(schedule);
        }
        this.openModal('eventDetailsModal');
    }

    // UPDATED: Schedule details with teacher/center display logic
    generateScheduleDetailsHTML(schedule) {
        const s = schedule.extendedProps;
        const isCurrentUserTeacher = window.userContext?.isTeacher === true;
        const isCurrentUserCenter = window.userContext?.isCenter === true;

        let html = `
            <div class="schedule-details">
                <h5><i class="fas fa-calendar me-2"></i>${schedule.title}</h5>
                <div class="detail-grid">
                    <div class="detail-item"><strong>Day:</strong> ${s.dayOfWeek}</div>
                    <div class="detail-item"><strong>Time:</strong> ${s.startTime} - ${s.endTime}</div>
                    ${s.hallName ? `<div class="detail-item"><strong>Hall:</strong> ${s.hallName}</div>` : ''}
        `;

        // Show different details based on user type
        if (isCurrentUserTeacher) {
            // For TEACHER users: Show center and branch details
            if (s.centerName) {
                html += `<div class="detail-item"><strong>Center:</strong> ${s.centerName}</div>`;
            }
            if (s.branchName) {
                html += `<div class="detail-item"><strong>Branch:</strong> ${s.branchName}</div>`;
            }
        } else if (isCurrentUserCenter) {
            // For CENTER users: Show teacher details
            if (s.teacherName) {
                html += `<div class="detail-item"><strong>Teacher:</strong> ${s.teacherName}</div>`;
            }
            if (s.branchName) {
                html += `<div class="detail-item"><strong>Branch:</strong> ${s.branchName}</div>`;
            }
        }

        // Always show subject and amount if available
        if (s.subjectName) {
            html += `<div class="detail-item"><strong>Subject:</strong> ${s.subjectName}</div>`;
        }
        if (s.scheduleAmount) {
            html += `<div class="detail-item"><strong>Amount:</strong> $${parseFloat(s.scheduleAmount).toFixed(2)}</div>`;
        }

        html += `
                </div>
            </div>
        `;

        return html;
    }

    handleDeleteRequest(scheduleCode) {
        const code = scheduleCode || this.lastViewedScheduleCode;
        if (code) {
            this.codeToDelete = code;
            if (this.dom.deleteScheduleSummary) {
                const schedule = this.schedules.find(s => s.extendedProps?.scheduleCode === code);
                this.dom.deleteScheduleSummary.innerHTML = schedule ? this.generateScheduleDetailsHTML(schedule) : "";
            }
            this.openModal('deleteConfirmModal');
        }
    }

    async editSchedule(scheduleCode) {
        const schedule = this.schedules.find(s => s.extendedProps?.scheduleCode === scheduleCode);
        if (!schedule) {
            this.showToast('Schedule not found', 'error');
            return;
        }
        this.editMode = true;
        this.currentScheduleId = scheduleCode;
        this.resetForm();
        this.showModalLoader(true);
        this.openModal('scheduleModal');
        await this.preloadDropdownsForModal(schedule);
        this.populateFormWithSchedule(schedule);
        this.showModalLoader(false);
        if (this.dom.modalTitle) {
            this.dom.modalTitle.innerHTML = `<i class="fas fa-edit me-2"></i>Edit Schedule: ${schedule.title}`;
        }
    }

    populateFormWithSchedule(schedule) {
        if (!schedule) return;
        const s = schedule.extendedProps;
        if (this.dom.scheduleName) this.dom.scheduleName.value = schedule.title || '';
        if (this.dom.dayOfWeek) this.dom.dayOfWeek.value = s.dayOfWeek || '';
        if (this.dom.startTime) this.dom.startTime.value = this.convertTo24Hour(s.startTime);
        if (this.dom.endTime) this.dom.endTime.value = this.convertTo24Hour(s.endTime);
        if (this.dom.scheduleAmount) this.dom.scheduleAmount.value = s.scheduleAmount || '';
    }

    resetForm() {
        if (this.dom.scheduleForm) {
            ['scheduleName', 'dayOfWeek', 'startTime', 'endTime', 'rootCode', 'centerCode', 'hallCode', 'eduYearCode', 'teacherCode', 'subjectCode', 'yearCode', 'scheduleAmount'].forEach(id => {
                if (this.dom[id]) {
                    if (this.dom[id].tagName === 'SELECT') this.dom[id].selectedIndex = 0;
                    else this.dom[id].value = '';
                }
            });
            // Only reset branchCode if not fixed by groupBranchCode
            if (!window.userContext?.groupBranchCode && this.dom.branchCode) {
                if (this.dom.branchCode.tagName === 'SELECT') this.dom.branchCode.selectedIndex = 0;
                else this.dom.branchCode.value = '';
            }
            // If groupBranchCode, set hidden input value to the correct branch code
            if (window.userContext?.groupBranchCode && this.dom.branchCode) {
                this.dom.branchCode.value = window.userContext.groupBranchCode;
            }
            if (window.userContext?.activeEduYearCode && this.dom.eduYearCode) {
                this.dom.eduYearCode.value = window.userContext.activeEduYearCode;
                this.dom.eduYearCode.dispatchEvent(new Event('change'));
            }
        }
        this.removeStuckBackdrop();
    }
    convertTo24Hour(time12h) {
        if (!time12h || typeof time12h !== 'string') return '';
        try {
            const [time, modifier] = time12h.split(' ');
            let [hours, minutes] = time.split(':');
            if (hours === '12') hours = '00';
            if (modifier && modifier.toUpperCase() === 'PM') {
                hours = parseInt(hours, 10) + 12;
            }
            return `${hours.toString().padStart(2, '0')}:${minutes}`;
        } catch (error) {
            return '';
        }
    }

    // Loader, Modal, Toast

    showLoader() {
        if (this.dom.loader) this.dom.loader.style.display = 'block';
    }
    hideLoader() {
        if (this.dom.loader) this.dom.loader.style.display = 'none';
    }

    showModalLoader(isLoading) {
        const modalBody = this.dom.scheduleModal?.querySelector('.modal-body');
        if (!modalBody) return;
        let spinner = modalBody.querySelector('.modal-spinner-loader');
        if (isLoading) {
            if (!spinner) {
                spinner = document.createElement('div');
                spinner.className = 'modal-spinner-loader d-flex justify-content-center align-items-center';
                spinner.innerHTML = `<div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div>`;
                spinner.style.position = 'absolute';
                spinner.style.top = '0'; spinner.style.left = '0'; spinner.style.width = '100%'; spinner.style.height = '100%';
                spinner.style.background = 'rgba(255,255,255,0.65)';
                spinner.style.zIndex = '1050';
                modalBody.appendChild(spinner);
            }
        } else if (spinner) {
            spinner.remove();
        }
    }

    showModalDeleteSpinner(isLoading) {
        const modalFooter = this.dom.deleteConfirmModal?.querySelector('.modal-footer');
        const confirmBtn = this.dom.deleteConfirmBtn;
        if (!modalFooter || !confirmBtn) return;
        let spinner = confirmBtn.querySelector('.spinner-border');
        if (isLoading) {
            confirmBtn.disabled = true;
            if (!spinner) {
                spinner = document.createElement('span');
                spinner.className = 'spinner-border spinner-border-sm ms-2';
                spinner.role = 'status';
                spinner.ariaHidden = 'true';
                confirmBtn.appendChild(spinner);
            }
        } else if (spinner) {
            confirmBtn.disabled = false;
            spinner.remove();
        }
    }

    openModal(modalId) {
        const modalElement = document.getElementById(modalId);
        if (modalElement && typeof bootstrap !== 'undefined') {
            const modal = bootstrap.Modal.getInstance(modalElement) || new bootstrap.Modal(modalElement);
            modal.show();
        }
        this.removeStuckBackdrop();
    }
    closeModal(modalId) {
        const modalElement = document.getElementById(modalId);
        if (modalElement && typeof bootstrap !== 'undefined') {
            const modal = bootstrap.Modal.getInstance(modalElement);
            if (modal) modal.hide();
        }
        this.removeStuckBackdrop();
    }
    showToast(msg, type = 'info') {
        // Good UX: Use a toast/snackbar if available, else fallback to alert
        if (window.bootstrap && window.bootstrap.Toast && document.querySelector('.toast-container')) {
            const toastDiv = document.createElement('div');
            toastDiv.className = `toast align-items-center text-white bg-${type === 'error' ? 'danger' : (type === 'success' ? 'success' : 'info')} border-0`;
            toastDiv.setAttribute('role', 'alert');
            toastDiv.setAttribute('aria-live', 'assertive');
            toastDiv.setAttribute('aria-atomic', 'true');
            toastDiv.innerHTML = `
                <div class="d-flex">
                  <div class="toast-body">${msg}</div>
                  <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            `;
            document.querySelector('.toast-container').appendChild(toastDiv);
            const toast = new bootstrap.Toast(toastDiv, { delay: 3500 });
            toast.show();
            toastDiv.addEventListener('hidden.bs.toast', () => toastDiv.remove());
        } else {
            alert(msg);
        }
    }

    removeStuckBackdrop() {
        document.querySelectorAll('.modal-backdrop').forEach(e => e.remove());
        document.body.classList.remove('modal-open');
    }

    setupGlobalActions() {
        window.scheduleManager = this;
        window.addScheduleForDay = (day) => this.addScheduleForDay(day);
        window.showScheduleDetails = (id) => this.showScheduleDetails(id);
        window.editSchedule = (id) => this.editSchedule(id);
        window.deleteSchedule = (id) => this.handleDeleteRequest(id);
    }
}

// Ensure only one instance is created even if file is loaded multiple times
if (!window.scheduleManager) {
    window.scheduleManager = new ScheduleManager();
}