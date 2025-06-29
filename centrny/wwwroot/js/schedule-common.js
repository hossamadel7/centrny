// Complete schedule-common.js - Enhanced with Teacher Filtering Logic
// This file handles all schedule management functionality for both Calendar and Index views

class ScheduleManager {
    constructor() {
        this.currentScheduleId = null;
        this.isEditMode = false;
        this.allSchedules = [];
        this.isInitialized = false;

        // Initialize the manager
        this.initialize();
    }

    async initialize() {
        try {
            console.log('🚀 Initializing Schedule Manager...', {
                userType: window.userContext?.isCenter ? 'Center' : 'Teacher',
                rootCode: window.userContext?.currentUserRootCode,
                hasError: window.userContext?.hasError
            });

            this.setupCommonEventListeners();
            this.setupFormHandlers();

            // Initialize based on user type
            if (window.userContext && !window.userContext.hasError) {
                await this.initializeForUserType();
            }

            this.isInitialized = true;
            console.log('✅ Schedule Manager initialized successfully');

        } catch (error) {
            console.error('❌ Error initializing Schedule Manager:', error);
            this.showToast('Error', 'Failed to initialize schedule manager', 'error');
        }
    }

    async initializeForUserType() {
        try {
            if (window.userContext.isTeacher) {
                console.log('🎓 Setting up Teacher-specific functionality...');
                this.setupTeacherSpecificHandlers();
                // Don't auto-load years for teachers - they need to select edu year first
            } else if (window.userContext.isCenter) {
                console.log('🏢 Setting up Center-specific functionality...');
                await this.loadBranchesForCenterUser();
                await this.loadTeachersForCenterUser();
            }
        } catch (error) {
            console.error('❌ Error in initializeForUserType:', error);
        }
    }

    setupTeacherSpecificHandlers() {
        // Set up change handlers for teacher users
        const eduYearSelect = document.getElementById('eduYearCode');
        const yearSelect = document.getElementById('yearCode');
        const branchSelect = document.getElementById('branchCode');
        const centerSelect = document.getElementById('centerCode');

        if (eduYearSelect) {
            eduYearSelect.addEventListener('change', () => {
                console.log('📅 Educational year changed:', eduYearSelect.value);
                this.onEduYearChange();
            });
        }

        if (yearSelect) {
            yearSelect.addEventListener('change', () => {
                console.log('📚 Academic year changed:', yearSelect.value);
                this.onYearChange();
            });
        }

        if (branchSelect) {
            branchSelect.addEventListener('change', () => {
                console.log('🏪 Branch changed:', branchSelect.value);
                this.onBranchChange();
            });
        }

        if (centerSelect) {
            centerSelect.addEventListener('change', () => {
                console.log('🏢 Center changed:', centerSelect.value);
                this.loadBranchesForCenter();
            });
        }

        console.log('✅ Teacher-specific handlers set up');
    }

    async onEduYearChange() {
        const eduYearCode = document.getElementById('eduYearCode')?.value;
        const yearSelect = document.getElementById('yearCode');
        const subjectSelect = document.getElementById('subjectCode');

        console.log('🔄 Processing educational year change:', eduYearCode);

        // Reset dependent dropdowns
        if (yearSelect) {
            yearSelect.innerHTML = '<option value="">Select Year</option>';
        }
        if (subjectSelect) {
            subjectSelect.innerHTML = '<option value="">Select Year First</option>';
        }

        if (!eduYearCode || eduYearCode === '') {
            console.log('⚠️ No educational year selected');
            return;
        }

        try {
            console.log('📡 Fetching years for educational year:', eduYearCode);
            const response = await fetch(`/Schedule/GetYearsByEduYear?eduYearCode=${eduYearCode}`);
            const data = await response.json();

            console.log('📊 Years response:', data);

            if (data.success && yearSelect) {
                yearSelect.innerHTML = '<option value="">Select Year</option>';
                data.years.forEach(year => {
                    const option = document.createElement('option');
                    option.value = year.value;
                    option.textContent = year.text;
                    yearSelect.appendChild(option);
                });
                console.log(`✅ Loaded ${data.years.length} years`);
            } else {
                console.error('❌ Failed to load years:', data.error);
                this.showToast('Error', data.error || 'Failed to load years', 'error');
            }
        } catch (error) {
            console.error('❌ Error loading years:', error);
            this.showToast('Error', 'Failed to load years', 'error');
        }
    }

    async onYearChange() {
        const yearCode = document.getElementById('yearCode')?.value;
        const subjectSelect = document.getElementById('subjectCode');

        console.log('🔄 Processing year change:', yearCode);

        // Reset subject dropdown
        if (subjectSelect) {
            subjectSelect.innerHTML = '<option value="">Select Subject (Optional)</option>';
        }

        if (!yearCode || yearCode === '') {
            console.log('⚠️ No year selected');
            return;
        }

        // For teacher users, load subjects based on year and branch
        if (window.userContext.isTeacher) {
            await this.loadSubjectsForTeacherByYearAndBranch();
        }
    }

    async onBranchChange() {
        console.log('🔄 Processing branch change');

        // For teacher users, reload subjects when branch changes
        if (window.userContext.isTeacher) {
            const yearCode = document.getElementById('yearCode')?.value;
            if (yearCode) {
                await this.loadSubjectsForTeacherByYearAndBranch();
            }
        }

        // Load halls for the selected branch
        await this.loadHallsForBranch();
    }

    async loadSubjectsForTeacherByYearAndBranch() {
        if (!window.userContext.isTeacher) {
            console.log('⚠️ Not a teacher user, skipping subject loading');
            return;
        }

        const yearCode = document.getElementById('yearCode')?.value;
        const branchCode = document.getElementById('branchCode')?.value;
        const subjectSelect = document.getElementById('subjectCode');

        console.log('📡 Loading subjects for teacher:', { yearCode, branchCode });

        if (!yearCode || !subjectSelect) {
            console.log('⚠️ Missing year code or subject select element');
            return;
        }

        try {
            // Get current teacher code (will be auto-assigned by backend)
            const response = await fetch('/Schedule/GetYearsForTeacherRoot');
            const teacherData = await response.json();

            if (!teacherData.success || !teacherData.teacherCode) {
                console.error('❌ Failed to get teacher code:', teacherData.error);
                this.showToast('Error', 'Failed to get teacher information', 'error');
                return;
            }

            const teacherCode = teacherData.teacherCode;
            console.log('👨‍🏫 Teacher code:', teacherCode);

            // Build URL with optional branch parameter
            let url = `/Schedule/GetSubjectsForTeacherByYearAndBranch?teacherCode=${teacherCode}&yearCode=${yearCode}`;
            if (branchCode && branchCode !== '') {
                url += `&branchCode=${branchCode}`;
            }

            console.log('📡 Fetching subjects from:', url);
            const subjectResponse = await fetch(url);
            const subjectData = await subjectResponse.json();

            console.log('📊 Subjects response:', subjectData);

            if (subjectData.success) {
                subjectSelect.innerHTML = '<option value="">Select Subject (Optional)</option>';
                subjectData.subjects.forEach(subject => {
                    const option = document.createElement('option');
                    option.value = subject.value;
                    option.textContent = subject.text;
                    subjectSelect.appendChild(option);
                });
                console.log(`✅ Loaded ${subjectData.subjects.length} subjects`);
            } else {
                console.error('❌ Failed to load subjects:', subjectData.error);
                subjectSelect.innerHTML = '<option value="">No subjects available</option>';
            }
        } catch (error) {
            console.error('❌ Error loading subjects:', error);
            if (subjectSelect) {
                subjectSelect.innerHTML = '<option value="">Error loading subjects</option>';
            }
            this.showToast('Error', 'Failed to load subjects', 'error');
        }
    }

    async loadTeacherYears() {
        if (!window.userContext.isTeacher) {
            console.log('⚠️ Not a teacher user, skipping year loading');
            return;
        }

        console.log('📡 Loading years for teacher...');

        try {
            const response = await fetch('/Schedule/GetYearsForTeacherRoot');
            const data = await response.json();

            console.log('📊 Teacher years response:', data);

            if (data.success) {
                const yearSelect = document.getElementById('yearCode');
                if (yearSelect && data.years) {
                    yearSelect.innerHTML = '<option value="">Select Year</option>';
                    data.years.forEach(year => {
                        const option = document.createElement('option');
                        option.value = year.value;
                        option.textContent = year.text;
                        yearSelect.appendChild(option);
                    });
                    console.log(`✅ Loaded ${data.years.length} years for teacher`);
                }
            } else {
                console.error('❌ Failed to load teacher years:', data.error);
                this.showToast('Error', data.error || 'Failed to load years', 'error');
            }
        } catch (error) {
            console.error('❌ Error loading teacher years:', error);
            this.showToast('Error', 'Failed to load years', 'error');
        }
    }

    async loadBranchesForCenterUser() {
        if (!window.userContext.isCenter) {
            console.log('⚠️ Not a center user, skipping branch loading');
            return;
        }

        console.log('📡 Loading branches for center user...');

        try {
            const response = await fetch('/Schedule/GetBranchesForCenterUser');
            const data = await response.json();

            console.log('📊 Center branches response:', data);

            if (data.success) {
                const branchSelect = document.getElementById('branchCode');
                if (branchSelect && data.branches) {
                    branchSelect.innerHTML = '<option value="">Select Branch (Optional)</option>';
                    data.branches.forEach(branch => {
                        const option = document.createElement('option');
                        option.value = branch.value;
                        option.textContent = branch.text;
                        branchSelect.appendChild(option);
                    });
                    console.log(`✅ Loaded ${data.branches.length} branches for center`);
                }
            } else {
                console.error('❌ Failed to load branches:', data.error);
                this.showToast('Error', data.error || 'Failed to load branches', 'error');
            }
        } catch (error) {
            console.error('❌ Error loading branches:', error);
            this.showToast('Error', 'Failed to load branches', 'error');
        }
    }

    async loadTeachersForCenterUser() {
        if (!window.userContext.isCenter) {
            console.log('⚠️ Not a center user, skipping teacher loading');
            return;
        }

        console.log('📡 Loading teachers for center user...');

        try {
            const response = await fetch('/Schedule/GetTeachersForCenterUser');
            const data = await response.json();

            console.log('📊 Center teachers response:', data);

            if (data.success) {
                const teacherSelect = document.getElementById('teacherCode');
                if (teacherSelect && data.teachers) {
                    teacherSelect.innerHTML = '<option value="">Select Teacher</option>';
                    data.teachers.forEach(teacher => {
                        const option = document.createElement('option');
                        option.value = teacher.value;
                        option.textContent = teacher.text;
                        teacherSelect.appendChild(option);
                    });
                    console.log(`✅ Loaded ${data.teachers.length} teachers for center`);
                }
            } else {
                console.error('❌ Failed to load teachers:', data.error);
                this.showToast('Error', data.error || 'Failed to load teachers', 'error');
            }
        } catch (error) {
            console.error('❌ Error loading teachers:', error);
            this.showToast('Error', 'Failed to load teachers', 'error');
        }
    }

    setupCommonEventListeners() {
        console.log('🔧 Setting up common event listeners...');

        // Save button
        const saveBtn = document.getElementById('saveScheduleBtn');
        if (saveBtn) {
            saveBtn.addEventListener('click', (e) => {
                e.preventDefault();
                this.saveSchedule();
            });
        }

        // Edit and Delete buttons in details modal
        const editBtn = document.getElementById('editEventBtn');
        const deleteBtn = document.getElementById('deleteEventBtn');
        const confirmDeleteBtn = document.getElementById('confirmDeleteBtn');

        if (editBtn) {
            editBtn.addEventListener('click', () => this.editCurrentSchedule());
        }

        if (deleteBtn) {
            deleteBtn.addEventListener('click', () => this.showDeleteConfirmation());
        }

        if (confirmDeleteBtn) {
            confirmDeleteBtn.addEventListener('click', () => this.confirmDelete());
        }

        // Modal reset handlers
        const scheduleModal = document.getElementById('scheduleModal');
        if (scheduleModal) {
            scheduleModal.addEventListener('hidden.bs.modal', () => this.resetForm());
        }

        console.log('✅ Common event listeners set up');
    }

    setupFormHandlers() {
        console.log('🔧 Setting up form handlers...');

        // For center users only - teacher change handler
        if (window.userContext.isCenter) {
            const teacherSelect = document.getElementById('teacherCode');
            if (teacherSelect) {
                teacherSelect.addEventListener('change', () => {
                    console.log('👨‍🏫 Teacher changed for center user:', teacherSelect.value);
                    this.loadSubjectsForTeacher();
                });
            }

            // Branch change handler for center users
            const branchSelect = document.getElementById('branchCode');
            if (branchSelect) {
                branchSelect.addEventListener('change', () => {
                    console.log('🏪 Branch changed for center user:', branchSelect.value);
                    this.loadHallsForBranch();
                });
            }
        }

        console.log('✅ Form handlers set up');
    }

    // For center users - load subjects based on selected teacher
    async loadSubjectsForTeacher() {
        if (!window.userContext.isCenter) {
            console.log('⚠️ Not a center user, skipping subject loading');
            return;
        }

        const teacherCode = document.getElementById('teacherCode')?.value;
        const yearCode = document.getElementById('yearCode')?.value;
        const subjectSelect = document.getElementById('subjectCode');

        console.log('📡 Loading subjects for center user:', { teacherCode, yearCode });

        if (!teacherCode || !subjectSelect) {
            if (subjectSelect) {
                subjectSelect.innerHTML = '<option value="">Select Teacher First</option>';
            }
            console.log('⚠️ No teacher selected or subject select not found');
            return;
        }

        try {
            let url = `/Schedule/GetSubjectsForTeacher?teacherCode=${teacherCode}`;
            if (yearCode) {
                url += `&yearCode=${yearCode}`;
            }

            console.log('📡 Fetching subjects from:', url);
            const response = await fetch(url);
            const data = await response.json();

            console.log('📊 Subjects response for center:', data);

            if (data.success) {
                subjectSelect.innerHTML = '<option value="">Select Subject (Optional)</option>';
                data.subjects.forEach(subject => {
                    const option = document.createElement('option');
                    option.value = subject.value;
                    option.textContent = subject.text;
                    subjectSelect.appendChild(option);
                });
                console.log(`✅ Loaded ${data.subjects.length} subjects for center user`);
            } else {
                console.error('❌ Failed to load subjects:', data.error);
                subjectSelect.innerHTML = '<option value="">No subjects available</option>';
            }
        } catch (error) {
            console.error('❌ Error loading subjects:', error);
            if (subjectSelect) {
                subjectSelect.innerHTML = '<option value="">Error loading subjects</option>';
            }
            this.showToast('Error', 'Failed to load subjects', 'error');
        }
    }

    async loadBranchesForCenter() {
        const centerCode = document.getElementById('centerCode')?.value;
        const branchSelect = document.getElementById('branchCode');
        const hallSelect = document.getElementById('hallCode');

        console.log('📡 Loading branches for center:', centerCode);

        // Reset dependent dropdowns
        if (branchSelect) {
            branchSelect.innerHTML = '<option value="">Select Center First</option>';
        }
        if (hallSelect) {
            hallSelect.innerHTML = '<option value="">Select Branch First</option>';
        }

        if (!centerCode) {
            console.log('⚠️ No center selected');
            return;
        }

        try {
            const response = await fetch(`/Schedule/GetBranchesForCenter?centerCode=${centerCode}`);
            const data = await response.json();

            console.log('📊 Branches for center response:', data);

            if (data.success && branchSelect) {
                branchSelect.innerHTML = '<option value="">Select Branch (Optional)</option>';
                data.branches.forEach(branch => {
                    const option = document.createElement('option');
                    option.value = branch.value;
                    option.textContent = branch.text;
                    branchSelect.appendChild(option);
                });
                console.log(`✅ Loaded ${data.branches.length} branches for center`);
            }
        } catch (error) {
            console.error('❌ Error loading branches:', error);
            this.showToast('Error', 'Failed to load branches', 'error');
        }
    }

    async loadHallsForBranch() {
        const branchCode = document.getElementById('branchCode')?.value;
        const hallSelect = document.getElementById('hallCode');

        console.log('📡 Loading halls for branch:', branchCode);

        if (hallSelect) {
            hallSelect.innerHTML = '<option value="">Select Branch First</option>';
        }

        if (!branchCode) {
            console.log('⚠️ No branch selected');
            return;
        }

        try {
            const response = await fetch(`/Schedule/GetHallsForBranch?branchCode=${branchCode}`);
            const data = await response.json();

            console.log('📊 Halls for branch response:', data);

            if (data.success && hallSelect) {
                hallSelect.innerHTML = '<option value="">Select Hall (Optional)</option>';
                data.halls.forEach(hall => {
                    const option = document.createElement('option');
                    option.value = hall.value;
                    option.textContent = `${hall.text} (${hall.capacity || 'N/A'})`;
                    hallSelect.appendChild(option);
                });
                console.log(`✅ Loaded ${data.halls.length} halls for branch`);
            }
        } catch (error) {
            console.error('❌ Error loading halls:', error);
            this.showToast('Error', 'Failed to load halls', 'error');
        }
    }

    async saveSchedule() {
        console.log('💾 Saving schedule...', { isEditMode: this.isEditMode, scheduleId: this.currentScheduleId });

        try {
            const formData = this.collectFormData();
            console.log('📝 Form data collected:', formData);

            if (!this.validateFormData(formData)) {
                console.log('❌ Form validation failed');
                return;
            }

            const url = this.isEditMode ?
                `/Schedule/EditScheduleEvent/${this.currentScheduleId}` :
                '/Schedule/CreateScheduleEvent';

            const method = 'POST';

            console.log('📡 Sending request to:', url);

            const response = await fetch(url, {
                method: method,
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                },
                body: JSON.stringify(formData)
            });

            const result = await response.json();
            console.log('📊 Save response:', result);

            if (result.success) {
                this.showToast('Success',
                    this.isEditMode ? 'Schedule updated successfully!' : 'Schedule created successfully!',
                    'success');

                this.closeModal('scheduleModal');
                this.resetForm();

                // Call the appropriate callback
                if (this.onScheduleSaved) {
                    this.onScheduleSaved();
                }
            } else {
                console.error('❌ Save failed:', result.error);
                this.showToast('Error', result.error || 'Failed to save schedule', 'error');
                if (this.onScheduleError) {
                    this.onScheduleError(result.error);
                }
            }
        } catch (error) {
            console.error('❌ Error saving schedule:', error);
            this.showToast('Error', 'An unexpected error occurred', 'error');
            if (this.onScheduleError) {
                this.onScheduleError(error.message);
            }
        }
    }

    collectFormData() {
        const data = {
            title: document.getElementById('scheduleName')?.value?.trim() || '',
            dayOfWeek: document.getElementById('dayOfWeek')?.value || '',
            startTime: document.getElementById('startTime')?.value || '',
            endTime: document.getElementById('endTime')?.value || '',
            rootCode: parseInt(document.getElementById('rootCode')?.value) || null,
            centerCode: parseInt(document.getElementById('centerCode')?.value) || null,
            branchCode: parseInt(document.getElementById('branchCode')?.value) || null,
            hallCode: parseInt(document.getElementById('hallCode')?.value) || null,
            eduYearCode: parseInt(document.getElementById('eduYearCode')?.value) || null,
            teacherCode: parseInt(document.getElementById('teacherCode')?.value) || null,
            subjectCode: parseInt(document.getElementById('subjectCode')?.value) || null,
            yearCode: parseInt(document.getElementById('yearCode')?.value) || null,
            scheduleAmount: parseFloat(document.getElementById('scheduleAmount')?.value) || null
        };

        console.log('📝 Collected form data:', data);
        return data;
    }

    validateFormData(data) {
        console.log('✅ Validating form data...');

        if (!data.title) {
            this.showToast('Validation Error', 'Schedule name is required', 'error');
            return false;
        }

        if (!data.dayOfWeek) {
            this.showToast('Validation Error', 'Day of week is required', 'error');
            return false;
        }

        if (!data.startTime || !data.endTime) {
            this.showToast('Validation Error', 'Start and end times are required', 'error');
            return false;
        }

        if (!data.yearCode) {
            this.showToast('Validation Error', 'Year is required', 'error');
            return false;
        }

        // For center users, teacher is required
        if (window.userContext.isCenter && !data.teacherCode) {
            this.showToast('Validation Error', 'Teacher selection is required', 'error');
            return false;
        }

        // For teacher users, educational year is required
        if (window.userContext.isTeacher && !data.eduYearCode) {
            this.showToast('Validation Error', 'Educational year is required', 'error');
            return false;
        }

        console.log('✅ Form validation passed');
        return true;
    }

    addScheduleForDay(day) {
        console.log('➕ Adding schedule for day:', day);

        const daySelect = document.getElementById('dayOfWeek');
        if (daySelect) {
            daySelect.value = day;
        }

        this.isEditMode = false;
        this.currentScheduleId = null;

        const modalTitle = document.getElementById('scheduleModalTitle');
        if (modalTitle) {
            modalTitle.innerHTML = `<i class="fas fa-calendar-plus me-2"></i>Add New Schedule for ${day}`;
        }

        const modal = new bootstrap.Modal(document.getElementById('scheduleModal'));
        modal.show();
    }

    showScheduleDetails(scheduleCode) {
        console.log('👁️ Showing schedule details for:', scheduleCode);

        const schedule = this.allSchedules.find(s => s.scheduleCode === scheduleCode);
        if (!schedule) {
            console.error('❌ Schedule not found:', scheduleCode);
            this.showToast('Error', 'Schedule not found', 'error');
            return;
        }

        this.currentScheduleId = scheduleCode;

        const content = document.getElementById('eventDetailsContent');
        if (content) {
            content.innerHTML = this.generateScheduleDetailsHTML(schedule);
        }

        const modal = new bootstrap.Modal(document.getElementById('eventDetailsModal'));
        modal.show();
    }

    generateScheduleDetailsHTML(schedule) {
        return `
            <div class="schedule-details">
                <h5><i class="fas fa-calendar me-2"></i>${schedule.scheduleName}</h5>
                <div class="detail-grid">
                    <div class="detail-item">
                        <strong>Day:</strong> ${schedule.dayOfWeek}
                    </div>
                    <div class="detail-item">
                        <strong>Time:</strong> ${schedule.startTime} - ${schedule.endTime}
                    </div>
                    ${schedule.hallName ? `<div class="detail-item"><strong>Hall:</strong> ${schedule.hallName}</div>` : ''}
                    ${schedule.teacherName ? `<div class="detail-item"><strong>Teacher:</strong> ${schedule.teacherName}</div>` : ''}
                    ${schedule.subjectName ? `<div class="detail-item"><strong>Subject:</strong> ${schedule.subjectName}</div>` : ''}
                    ${schedule.centerName ? `<div class="detail-item"><strong>Center:</strong> ${schedule.centerName}</div>` : ''}
                    ${schedule.branchName ? `<div class="detail-item"><strong>Branch:</strong> ${schedule.branchName}</div>` : ''}
                    ${schedule.scheduleAmount ? `<div class="detail-item"><strong>Amount:</strong> $${schedule.scheduleAmount.toFixed(2)}</div>` : ''}
                </div>
            </div>
        `;
    }

    editSchedule(scheduleCode) {
        console.log('✏️ Editing schedule:', scheduleCode);
        this.editCurrentSchedule(scheduleCode);
    }

    editCurrentSchedule(scheduleCode = null) {
        const id = scheduleCode || this.currentScheduleId;
        const schedule = this.allSchedules.find(s => s.scheduleCode === id);

        console.log('✏️ Editing current schedule:', id, schedule);

        if (!schedule) {
            console.error('❌ Schedule not found for edit:', id);
            this.showToast('Error', 'Schedule not found', 'error');
            return;
        }

        this.isEditMode = true;
        this.currentScheduleId = id;

        // Populate form with existing data
        this.populateFormWithSchedule(schedule);

        // Update modal title
        const modalTitle = document.getElementById('scheduleModalTitle');
        if (modalTitle) {
            modalTitle.innerHTML = `<i class="fas fa-edit me-2"></i>Edit Schedule: ${schedule.scheduleName}`;
        }

        // Close details modal and open edit modal
        this.closeModal('eventDetailsModal');

        const modal = new bootstrap.Modal(document.getElementById('scheduleModal'));
        modal.show();
    }

    async populateFormWithSchedule(schedule) {
        console.log('📝 Populating form with schedule:', schedule);

        try {
            // Set basic fields
            document.getElementById('scheduleName').value = schedule.scheduleName || '';
            document.getElementById('dayOfWeek').value = schedule.dayOfWeek || '';

            // Convert time format if needed
            if (schedule.startTime && schedule.endTime) {
                const startTime = this.convertTo24Hour(schedule.startTime);
                const endTime = this.convertTo24Hour(schedule.endTime);
                document.getElementById('startTime').value = startTime;
                document.getElementById('endTime').value = endTime;
            }

            // Set other fields
            if (schedule.scheduleAmount) {
                document.getElementById('scheduleAmount').value = schedule.scheduleAmount;
            }

            // For teacher users, need to handle the cascading selects
            if (window.userContext.isTeacher) {
                console.log('👨‍🏫 Populating form for teacher user');

                // Set edu year first
                if (schedule.eduYearCode) {
                    document.getElementById('eduYearCode').value = schedule.eduYearCode;
                    // Load years for this edu year
                    await this.onEduYearChange();
                }

                // Set year
                if (schedule.yearCode) {
                    document.getElementById('yearCode').value = schedule.yearCode;
                    // Load subjects for this year
                    await this.onYearChange();
                }

                // Set center and branch
                if (schedule.centerCode) {
                    document.getElementById('centerCode').value = schedule.centerCode;
                    await this.loadBranchesForCenter();
                }

                if (schedule.branchCode) {
                    document.getElementById('branchCode').value = schedule.branchCode;
                    await this.loadHallsForBranch();
                }

                // Set hall and subject
                if (schedule.hallCode) {
                    document.getElementById('hallCode').value = schedule.hallCode;
                }

                if (schedule.subjectCode) {
                    document.getElementById('subjectCode').value = schedule.subjectCode;
                }
            } else {
                // For center users - simpler logic
                console.log('🏢 Populating form for center user');

                if (schedule.branchCode) {
                    document.getElementById('branchCode').value = schedule.branchCode;
                    await this.loadHallsForBranch();
                }

                if (schedule.hallCode) {
                    document.getElementById('hallCode').value = schedule.hallCode;
                }

                if (schedule.teacherCode) {
                    document.getElementById('teacherCode').value = schedule.teacherCode;
                    await this.loadSubjectsForTeacher();
                }

                if (schedule.subjectCode) {
                    document.getElementById('subjectCode').value = schedule.subjectCode;
                }

                if (schedule.yearCode) {
                    document.getElementById('yearCode').value = schedule.yearCode;
                }

                if (schedule.eduYearCode) {
                    document.getElementById('eduYearCode').value = schedule.eduYearCode;
                }
            }

            console.log('✅ Form populated successfully');

        } catch (error) {
            console.error('❌ Error populating form:', error);
            this.showToast('Error', 'Error loading schedule data', 'error');
        }
    }

    convertTo24Hour(time12h) {
        if (!time12h || typeof time12h !== 'string') {
            console.warn('⚠️ Invalid time format:', time12h);
            return '';
        }

        try {
            const [time, modifier] = time12h.split(' ');
            let [hours, minutes] = time.split(':');

            if (hours === '12') {
                hours = '00';
            }

            if (modifier && modifier.toUpperCase() === 'PM') {
                hours = parseInt(hours, 10) + 12;
            }

            return `${hours.toString().padStart(2, '0')}:${minutes}`;
        } catch (error) {
            console.error('❌ Error converting time:', error, time12h);
            return '';
        }
    }

    deleteSchedule(scheduleCode) {
        console.log('🗑️ Deleting schedule:', scheduleCode);
        this.currentScheduleId = scheduleCode;
        this.showDeleteConfirmation();
    }

    showDeleteConfirmation() {
        const schedule = this.allSchedules.find(s => s.scheduleCode === this.currentScheduleId);
        if (!schedule) {
            console.error('❌ Schedule not found for delete:', this.currentScheduleId);
            this.showToast('Error', 'Schedule not found', 'error');
            return;
        }

        console.log('⚠️ Showing delete confirmation for:', schedule.scheduleName);

        const summary = document.getElementById('deleteScheduleSummary');
        if (summary) {
            summary.innerHTML = `
                <div class="mt-3">
                    <strong>${schedule.scheduleName}</strong><br>
                    <small class="text-muted">${schedule.dayOfWeek} at ${schedule.startTime} - ${schedule.endTime}</small>
                </div>
            `;
        }

        this.closeModal('eventDetailsModal');
        const modal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
        modal.show();
    }

    async confirmDelete() {
        console.log('🗑️ Confirming delete for schedule:', this.currentScheduleId);

        try {
            const response = await fetch(`/Schedule/DeleteScheduleEvent/${this.currentScheduleId}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                }
            });

            const result = await response.json();
            console.log('📊 Delete response:', result);

            if (result.success) {
                console.log('✅ Schedule deleted successfully');
                this.showToast('Success', 'Schedule deleted successfully!', 'success');
                this.closeModal('deleteConfirmModal');

                if (this.onScheduleDeleted) {
                    this.onScheduleDeleted();
                }
            } else {
                console.error('❌ Delete failed:', result.error);
                this.showToast('Error', result.error || 'Failed to delete schedule', 'error');
            }
        } catch (error) {
            console.error('❌ Error deleting schedule:', error);
            this.showToast('Error', 'An unexpected error occurred', 'error');
        }
    }

    resetForm() {
        console.log('🔄 Resetting form...');

        this.isEditMode = false;
        this.currentScheduleId = null;

        // Reset all form fields
        const form = document.getElementById('scheduleForm');
        if (form) {
            const inputs = form.querySelectorAll('input, select');
            inputs.forEach(input => {
                if (input.type === 'hidden' && input.id === 'rootCode') {
                    // Keep root code
                    return;
                }
                if (input.type === 'hidden' && input.id === 'centerCode' && window.userContext.isCenter) {
                    // Keep center code for center users
                    return;
                }
                if (input.type === 'hidden' && (input.id === 'teacherCode' || input.id === 'hallCode') && window.userContext.isTeacher) {
                    // Keep hidden fields for teacher users
                    return;
                }
                input.value = '';
            });
        }

        // Reset modal title
        const modalTitle = document.getElementById('scheduleModalTitle');
        if (modalTitle) {
            modalTitle.innerHTML = '<i class="fas fa-calendar-plus me-2"></i>Add New Schedule';
        }

        // Reset dependent dropdowns
        this.resetDependentDropdowns();

        console.log('✅ Form reset complete');
    }

    resetDependentDropdowns() {
        console.log('🔄 Resetting dependent dropdowns...');

        const yearSelect = document.getElementById('yearCode');
        const subjectSelect = document.getElementById('subjectCode');
        const branchSelect = document.getElementById('branchCode');
        const hallSelect = document.getElementById('hallCode');

        if (window.userContext.isTeacher) {
            if (yearSelect) {
                yearSelect.innerHTML = '<option value="">Select Educational Year First</option>';
            }
            if (subjectSelect) {
                subjectSelect.innerHTML = '<option value="">Select Year First</option>';
            }
            if (branchSelect) {
                branchSelect.innerHTML = '<option value="">Select Center First</option>';
            }
        } else {
            // For center users
            if (branchSelect) {
                branchSelect.innerHTML = '<option value="">Select Branch (Optional)</option>';
            }
            if (subjectSelect) {
                subjectSelect.innerHTML = '<option value="">Select Teacher First</option>';
            }
        }

        if (hallSelect) {
            hallSelect.innerHTML = '<option value="">Select Branch First</option>';
        }

        console.log('✅ Dependent dropdowns reset');
    }

    closeModal(modalId) {
        console.log('❌ Closing modal:', modalId);
        const modal = bootstrap.Modal.getInstance(document.getElementById(modalId));
        if (modal) {
            modal.hide();
        }
    }

    showToast(title, message, type = 'info') {
        console.log('🍞 Showing toast:', { title, message, type });

        const toastContainer = document.querySelector('.toast-container') || this.createToastContainer();

        const toastId = 'toast-' + Date.now();
        const bgClass = type === 'success' ? 'bg-success' : type === 'error' ? 'bg-danger' : 'bg-info';

        const toastHTML = `
            <div id="${toastId}" class="toast ${bgClass} text-white" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="toast-header ${bgClass} text-white border-0">
                    <strong class="me-auto">${title}</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;

        toastContainer.insertAdjacentHTML('beforeend', toastHTML);

        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement, { delay: 5000 });

        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });

        toast.show();
    }

    createToastContainer() {
        let container = document.querySelector('.toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container position-fixed top-0 end-0 p-3';
            container.style.zIndex = '9999';
            document.body.appendChild(container);
        }
        return container;
    }

    // Utility method to wait for element to be available
    async waitForElement(selector, timeout = 5000) {
        return new Promise((resolve, reject) => {
            const element = document.querySelector(selector);
            if (element) {
                resolve(element);
                return;
            }

            const observer = new MutationObserver((mutations) => {
                const element = document.querySelector(selector);
                if (element) {
                    observer.disconnect();
                    resolve(element);
                }
            });

            observer.observe(document.body, {
                childList: true,
                subtree: true
            });

            setTimeout(() => {
                observer.disconnect();
                reject(new Error(`Element ${selector} not found within ${timeout}ms`));
            }, timeout);
        });
    }

    // Utility method to debug current state
    debugState() {
        console.log('🐛 Current Schedule Manager State:', {
            isInitialized: this.isInitialized,
            currentScheduleId: this.currentScheduleId,
            isEditMode: this.isEditMode,
            allSchedulesCount: this.allSchedules.length,
            userContext: window.userContext,
            formElements: {
                scheduleName: !!document.getElementById('scheduleName'),
                dayOfWeek: !!document.getElementById('dayOfWeek'),
                startTime: !!document.getElementById('startTime'),
                endTime: !!document.getElementById('endTime'),
                rootCode: !!document.getElementById('rootCode'),
                centerCode: !!document.getElementById('centerCode'),
                branchCode: !!document.getElementById('branchCode'),
                hallCode: !!document.getElementById('hallCode'),
                eduYearCode: !!document.getElementById('eduYearCode'),
                teacherCode: !!document.getElementById('teacherCode'),
                subjectCode: !!document.getElementById('subjectCode'),
                yearCode: !!document.getElementById('yearCode'),
                scheduleAmount: !!document.getElementById('scheduleAmount')
            }
        });
    }

    // Callback methods that can be overridden by page-specific managers
    onScheduleSaved() {
        console.log('💾 Schedule saved callback - override in page-specific manager');
    }

    onScheduleDeleted() {
        console.log('🗑️ Schedule deleted callback - override in page-specific manager');
    }

    onScheduleError(error) {
        console.log('❌ Schedule error callback - override in page-specific manager:', error);
    }
}

// Global functions for backward compatibility
window.loadBranchesForCenter = function () {
    console.log('🔗 Global loadBranchesForCenter called');
    if (window.scheduleManager && window.scheduleManager.loadBranchesForCenter) {
        window.scheduleManager.loadBranchesForCenter();
    } else {
        console.warn('⚠️ Schedule manager not available');
    }
};

window.loadHallsForBranch = function () {
    console.log('🔗 Global loadHallsForBranch called');
    if (window.scheduleManager && window.scheduleManager.loadHallsForBranch) {
        window.scheduleManager.loadHallsForBranch();
    } else {
        console.warn('⚠️ Schedule manager not available');
    }
};

window.loadSubjectsForTeacher = function () {
    console.log('🔗 Global loadSubjectsForTeacher called');
    if (window.scheduleManager && window.scheduleManager.loadSubjectsForTeacher) {
        window.scheduleManager.loadSubjectsForTeacher();
    } else {
        console.warn('⚠️ Schedule manager not available');
    }
};

// Debug function
window.debugScheduleManager = function () {
    if (window.scheduleManager) {
        window.scheduleManager.debugState();
    } else {
        console.log('❌ Schedule manager not initialized');
    }
};

// Initialize the schedule manager when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    console.log('🚀 DOM Content Loaded - Initializing Schedule Manager...');

    if (typeof window.userContext !== 'undefined' && !window.userContext.hasError) {
        window.scheduleManager = new ScheduleManager();
        console.log('✅ Schedule Manager initialized for', window.userContext.isCenter ? 'Center' : 'Teacher', 'user');

        // Expose debug function globally
        window.debugScheduleManager = () => window.scheduleManager.debugState();

    } else {
        console.warn('⚠️ User context not available or has errors, skipping Schedule Manager initialization');
        console.log('User context:', window.userContext);
    }
});

// Handle page unload
window.addEventListener('beforeunload', function () {
    console.log('📄 Page unloading - cleaning up Schedule Manager');
    if (window.scheduleManager) {
        // Cleanup if needed
        window.scheduleManager = null;
    }
});