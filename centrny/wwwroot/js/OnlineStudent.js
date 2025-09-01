// OnlineStudent.js - Fixed version with proper initialization and debugging
// This replaces your existing OnlineStudent.js file

console.log('OnlineStudent.js loading...');

class OnlineStudentDashboard {
    constructor() {
        console.log('OnlineStudentDashboard constructor called');
        this.subjects = [];
        this.exams = [];
        this.stats = null;
        this.subscription = null;

        // Learning system properties
        this.learningSubjects = [];
        this.chapters = [];
        this.lessons = [];
        this.currentSubject = null;
        this.currentChapter = null;

        // Initialize when DOM is ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.init());
        } else {
            // DOM already loaded
            this.init();
        }
    }

    init() {
        console.log('OnlineStudentDashboard initializing...');
        console.log('Current pathname:', window.location.pathname);

        // Check which page we're on and initialize accordingly
        const currentPath = window.location.pathname.toLowerCase();

        if (currentPath.includes('learning') || currentPath.endsWith('/learning')) {
            console.log('Detected Learning page, initializing learning system');
            this.initLearningSystem();
        } else if (currentPath.includes('onlinestudent') || currentPath === '/' || currentPath.includes('index')) {
            console.log('Detected Dashboard page, initializing dashboard');
            this.loadAll();
        } else {
            console.log('Unknown page, attempting to detect by elements');
            // Fallback: detect by elements present
            if (document.getElementById('subjectSelect')) {
                console.log('Found subjectSelect element, initializing learning system');
                this.initLearningSystem();
            } else if (document.getElementById('subjectsGrid')) {
                console.log('Found subjectsGrid element, initializing dashboard');
                this.loadAll();
            }
        }
    }

    // ============ DASHBOARD FUNCTIONALITY ============

    async loadAll() {
        console.log('Loading dashboard data...');
        try {
            this.showLoading();
            await Promise.all([
                this.loadStats(),
                this.loadSubscription(),
                this.loadSubjects(),
                this.loadExams()
            ]);
            this.hideLoading();
            this.showAlert('Dashboard loaded', 'success');
        } catch (e) {
            console.error('Dashboard load error:', e);
            this.showAlert('Failed to load dashboard', 'error');
            this.hideLoading();
        }
    }

    async loadStats() {
        try {
            const res = await fetch('/OnlineStudent/GetStudentStats');
            if (!res.ok) throw new Error('Stats fetch failed');
            this.stats = await res.json();
            if (this.stats.error) {
                this.showAlert(this.stats.error, 'error');
                return;
            }
            this.updateStats();
        } catch (error) {
            console.error('Error loading stats:', error);
        }
    }

    updateStats() {
        if (!this.stats) return;
        this.setCounter('subjectsCount', this.stats.subjectsCount);
        this.setCounter('examsCount', this.stats.examsCount);
        this.setCounter('attendanceCount', this.stats.totalAttendance);
        const avg = document.getElementById('averageGrade');
        if (avg) avg.textContent = (this.stats.averageGrade && this.stats.averageGrade > 0)
            ? `${this.stats.averageGrade}%`
            : '--';
    }

    setCounter(id, value) {
        const el = document.getElementById(id);
        if (!el) return;
        const target = parseInt(value || 0, 10);
        const start = 0;
        const duration = 600;
        const step = Math.max(1, Math.floor(target / (duration / 30)));
        let current = start;
        const timer = setInterval(() => {
            current += step;
            if (current >= target) {
                current = target;
                clearInterval(timer);
            }
            el.textContent = current;
        }, 30);
    }

    async loadSubscription() {
        try {
            const res = await fetch('/OnlineStudent/GetSubscriptionStatus');
            if (!res.ok) throw new Error('Subscription fetch failed');
            this.subscription = await res.json();
            if (this.subscription.error) {
                this.showAlert(this.subscription.error, 'error');
                return;
            }
            const badge = document.getElementById('subscription-status');
            if (badge) {
                badge.textContent = this.subscription.status;
                badge.className = `badge ${this.subscription.isSubscribed ? 'bg-success' : 'bg-secondary'}`;
            }
        } catch (error) {
            console.error('Error loading subscription:', error);
        }
    }

    async loadSubjects() {
        try {
            const grid = document.getElementById('subjectsGrid');
            const res = await fetch('/OnlineStudent/GetStudentSubjects');
            if (!res.ok) throw new Error('Subjects fetch failed');
            this.subjects = await res.json();
            if (this.subjects.error) {
                this.renderError(grid, this.subjects.error);
                return;
            }
            this.renderSubjects();
        } catch (error) {
            console.error('Error loading subjects:', error);
        }
    }

    renderSubjects() {
        const grid = document.getElementById('subjectsGrid');
        if (!grid) return;
        if (!this.subjects || this.subjects.length === 0) {
            grid.innerHTML = this.emptyState('fa-book', 'No Subjects', 'No enrolled subjects yet');
            return;
        }
        grid.innerHTML = this.subjects.map(s => `
            <div class="subject-card">
                <div class="subject-header">
                    <h4>${this.escape(s.subjectName)}</h4>
                    <span class="teacher-badge">
                        <i class="fas fa-chalkboard-teacher"></i>${this.escape(s.teacherName)}
                    </span>
                </div>
                <div class="subject-details">
                    <div class="detail-item">
                        <i class="fas fa-calendar"></i>
                        <span>${s.scheduleDay || 'Day TBD'}</span>
                    </div>
                    <div class="detail-item">
                        <i class="fas fa-clock"></i>
                        <span>${this.formatTimeRange(s.scheduleStartTime, s.scheduleEndTime)}</span>
                    </div>
                    <div class="detail-item">
                        <i class="fas fa-map-marker-alt"></i>
                        <span>${s.hallName || 'Hall TBD'}</span>
                    </div>
                    <div class="detail-item">
                        <i class="fas fa-building"></i>
                        <span>${this.escape(s.branchName)}</span>
                    </div>
                    <div class="detail-item">
                        <i class="fas fa-phone"></i>
                        <span>${s.teacherPhone || 'N/A'}</span>
                    </div>
                    ${s.isOnline ? `<div class="online-badge"><i class="fas fa-wifi"></i> Online</div>` : ''}
                    ${(s.studentFee ? `<div class="detail-item"><i class="fas fa-money-bill"></i><span>Fee: ${s.studentFee}</span></div>` : '')}
                </div>
            </div>
        `).join('');
    }

    async loadExams() {
        try {
            const grid = document.getElementById('examsGrid');
            const res = await fetch('/OnlineStudent/GetAttendedExams');
            if (!res.ok) throw new Error('Exams fetch failed');
            this.exams = await res.json();
            if (this.exams.error) {
                this.renderError(grid, this.exams.error);
                return;
            }
            this.renderExams();
        } catch (error) {
            console.error('Error loading exams:', error);
        }
    }

    renderExams() {
        const grid = document.getElementById('examsGrid');
        if (!grid) return;
        if (!this.exams || this.exams.length === 0) {
            grid.innerHTML = this.emptyState('fa-clipboard-list', 'No Exams', 'No exam results yet');
            return;
        }
        grid.innerHTML = this.exams.map(e => {
            const degree = e.examDegree || 0;
            const result = e.studentResult || 0;
            const pct = degree > 0 ? ((result / degree) * 100).toFixed(1) : '0.0';
            const pass = parseFloat(pct) >= 50;
            return `
                <div class="exam-card">
                    <div class="exam-header">
                        <h4>${this.escape(e.examName)}</h4>
                        <div class="grade-badge ${pass ? 'grade-pass' : 'grade-fail'}">
                            ${result}/${degree} (${pct}%)
                        </div>
                    </div>
                    <div class="exam-details">
                        <div class="detail-row">
                            <span class="label">Subject</span>
                            <span class="value">${this.escape(e.subjectName)}</span>
                        </div>
                        <div class="detail-row">
                            <span class="label">Teacher</span>
                            <span class="value">${this.escape(e.teacherName)}</span>
                        </div>
                        <div class="detail-row">
                            <span class="label">Date</span>
                            <span class="value">${this.formatDate(e.examDate)}</span>
                        </div>
                        <div class="detail-row">
                            <span class="label">Type</span>
                            <span class="value">${e.isExam ? 'Exam' : 'Quiz'}</span>
                        </div>
                        <div class="detail-row">
                            <span class="label">Status</span>
                            <span class="value ${pass ? 'text-success' : 'text-danger'}">${pass ? 'Passed' : 'Failed'}</span>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    // ============ LEARNING SYSTEM FUNCTIONALITY ============

    initLearningSystem() {
        console.log('Initializing learning system...');
        this.setupLearningEventListeners();
        // Small delay to ensure DOM is fully ready
        setTimeout(() => {
            this.loadLearningSubjects();
        }, 100);
    }

    setupLearningEventListeners() {
        console.log('Setting up learning event listeners...');

        // Subject selection
        const subjectSelect = document.getElementById('subjectSelect');
        const loadChaptersBtn = document.getElementById('loadChaptersBtn');

        if (subjectSelect) {
            console.log('Found subjectSelect, adding event listener');
            subjectSelect.addEventListener('change', () => {
                const selected = subjectSelect.value;
                console.log('Subject selected:', selected);
                if (loadChaptersBtn) {
                    loadChaptersBtn.disabled = !selected;
                }
            });
        } else {
            console.warn('subjectSelect element not found');
        }

        if (loadChaptersBtn) {
            console.log('Found loadChaptersBtn, adding event listener');
            loadChaptersBtn.addEventListener('click', () => {
                const selectedSubject = subjectSelect.value;
                console.log('Load chapters clicked for subject:', selectedSubject);
                if (selectedSubject) {
                    this.loadChapters(parseInt(selectedSubject));
                }
            });
        } else {
            console.warn('loadChaptersBtn element not found');
        }

        // Navigation buttons
        const backToSubjectsBtn = document.getElementById('backToSubjectsBtn');
        const backToChaptersBtn = document.getElementById('backToChaptersBtn');

        if (backToSubjectsBtn) {
            backToSubjectsBtn.addEventListener('click', () => {
                this.showSubjectSelection();
            });
        }

        if (backToChaptersBtn) {
            backToChaptersBtn.addEventListener('click', () => {
                this.showChapters();
            });
        }
    }

    async loadLearningSubjects() {
        console.log('Loading learning subjects...');

        try {
            const subjectSelect = document.getElementById('subjectSelect');
            if (!subjectSelect) {
                console.error('subjectSelect element not found');
                return;
            }

            // Show loading state
            subjectSelect.innerHTML = '<option value="">Loading subjects...</option>';
            console.log('Set loading state in dropdown');

            const response = await fetch('/OnlineStudent/GetLearningSubjects');
            console.log('API response status:', response.status);

            if (!response.ok) {
                throw new Error(`Failed to load subjects: ${response.status}`);
            }

            this.learningSubjects = await response.json();
            console.log('Learning subjects data:', this.learningSubjects);

            if (this.learningSubjects.error) {
                console.error('API returned error:', this.learningSubjects.error);
                subjectSelect.innerHTML = `<option value="">Error: ${this.learningSubjects.error}</option>`;
                this.showAlert(this.learningSubjects.error, 'error');
                return;
            }

            this.renderLearningSubjects();
            console.log('Learning subjects rendered successfully');
        } catch (error) {
            console.error('Error loading learning subjects:', error);
            const subjectSelect = document.getElementById('subjectSelect');
            if (subjectSelect) {
                subjectSelect.innerHTML = '<option value="">Failed to load subjects</option>';
            }
            this.showAlert('Failed to load subjects', 'error');
        }
    }

    renderLearningSubjects() {
        const subjectSelect = document.getElementById('subjectSelect');
        if (!subjectSelect) {
            console.error('subjectSelect not found for rendering');
            return;
        }

        if (!this.learningSubjects || this.learningSubjects.length === 0) {
            console.log('No learning subjects to render');
            subjectSelect.innerHTML = '<option value="">No subjects available</option>';
            return;
        }

        const options = ['<option value="">Select a subject...</option>'];
        this.learningSubjects.forEach(subject => {
            options.push(`
                <option value="${subject.subjectCode}">
                    ${this.escape(subject.subjectName)} - ${this.escape(subject.eduYearName)}
                </option>
            `);
        });

        subjectSelect.innerHTML = options.join('');
        console.log(`Rendered ${this.learningSubjects.length} subjects in dropdown`);
    }

    async loadChapters(subjectCode) {
        console.log('Loading chapters for subject:', subjectCode);

        try {
            const selectedSubject = this.learningSubjects.find(s => s.subjectCode === subjectCode);
            this.currentSubject = selectedSubject;

            this.showChaptersSection();
            this.showLearningLoading('chaptersSection');

            const response = await fetch(`/OnlineStudent/GetSubjectChapters?subjectCode=${subjectCode}`);

            if (!response.ok) {
                throw new Error('Failed to load chapters');
            }

            this.chapters = await response.json();
            console.log('Chapters loaded:', this.chapters);

            if (this.chapters.error) {
                this.showAlert(this.chapters.error, 'error');
                return;
            }

            this.renderChapters();
            this.updateBreadcrumb(['Learning Center', this.currentSubject?.subjectName || 'Subject']);
            this.hideLearningLoading('chaptersSection');
        } catch (error) {
            console.error('Error loading chapters:', error);
            this.showAlert('Failed to load chapters', 'error');
            this.hideLearningLoading('chaptersSection');
        }
    }

    renderChapters() {
        const chaptersGrid = document.getElementById('chaptersGrid');
        if (!chaptersGrid) return;

        if (!this.chapters || this.chapters.length === 0) {
            chaptersGrid.innerHTML = this.emptyState('fa-layer-group', 'No Chapters', 'No chapters available for this subject');
            return;
        }

        const cards = this.chapters.map(chapter => `
            <div class="col-md-6 col-lg-4">
                <div class="chapter-card card h-100" data-chapter-code="${chapter.lessonCode}">
                    <div class="card-body">
                        <div class="chapter-header">
                            <h5 class="chapter-title">${this.escape(chapter.chapterName)}</h5>
                            <span class="lessons-count-badge">
                                ${chapter.lessonsCount} ${chapter.lessonsCount === 1 ? 'lesson' : 'lessons'}
                            </span>
                        </div>
                        <p class="chapter-description">Subject: ${this.escape(chapter.subjectName)}</p>
                        <div class="d-flex justify-content-between align-items-center">
                            <small class="text-muted">
                                <i class="fas fa-clock me-1"></i>
                                ${this.formatDate(chapter.insertTime)}
                            </small>
                            <i class="fas fa-arrow-right text-primary"></i>
                        </div>
                    </div>
                </div>
            </div>
        `).join('');

        chaptersGrid.innerHTML = cards;

        // Add click listeners to chapter cards
        chaptersGrid.querySelectorAll('.chapter-card').forEach(card => {
            card.addEventListener('click', () => {
                const chapterCode = parseInt(card.dataset.chapterCode);
                this.loadLessons(chapterCode);
            });
        });
    }

    async loadLessons(chapterCode) {
        console.log('Loading lessons for chapter:', chapterCode);

        try {
            const selectedChapter = this.chapters.find(c => c.lessonCode === chapterCode);
            this.currentChapter = selectedChapter;

            this.showLessonsSection();
            this.showLearningLoading('lessonsSection');

            const response = await fetch(`/OnlineStudent/GetChapterLessons?chapterCode=${chapterCode}`);

            if (!response.ok) {
                throw new Error('Failed to load lessons');
            }

            const data = await response.json();
            console.log('Lessons loaded:', data);

            if (data.error) {
                this.showAlert(data.error, 'error');
                return;
            }

            this.lessons = data.lessons;
            this.renderLessons(data.chapter);
            this.updateBreadcrumb([
                'Learning Center',
                this.currentSubject?.subjectName || 'Subject',
                data.chapter?.chapterName || 'Chapter'
            ]);
            this.hideLearningLoading('lessonsSection');
        } catch (error) {
            console.error('Error loading lessons:', error);
            this.showAlert('Failed to load lessons', 'error');
            this.hideLearningLoading('lessonsSection');
        }
    }

    renderLessons(chapter) {
        const lessonsContainer = document.getElementById('lessonsContainer');
        if (!lessonsContainer) return;

        if (!this.lessons || this.lessons.length === 0) {
            lessonsContainer.innerHTML = this.emptyState('fa-play-circle', 'No Lessons', 'No lessons available for this chapter');
            return;
        }

        const chapterInfo = `
            <div class="mb-4 p-3 bg-light rounded">
                <h4>${this.escape(chapter.chapterName)}</h4>
                <p class="text-muted mb-0">Subject: ${this.escape(chapter.subjectName)}</p>
                <small class="text-muted">Total lessons: ${this.lessons.length}</small>
            </div>
        `;

        const lessonsList = this.lessons.map((lesson, index) => `
            <div class="lesson-item">
                <div class="lesson-header">
                    <h6 class="lesson-title">
                        <span class="me-2 text-primary">${index + 1}.</span>
                        ${this.escape(lesson.lessonName)}
                    </h6>
                    <div class="lesson-actions">
                        <button class="btn btn-primary btn-sm" onclick="window.onlineStudentDashboard.accessLesson(${lesson.lessonCode}, '${this.escape(lesson.lessonName)}')">
                            <i class="fas fa-key me-1"></i>Access Lesson
                        </button>
                    </div>
                </div>
                <p class="lesson-description">
                    <small class="text-muted">
                        <i class="fas fa-calendar me-1"></i>
                        Added: ${this.formatDate(lesson.insertTime)}
                    </small>
                </p>
            </div>
        `).join('');

        lessonsContainer.innerHTML = chapterInfo + lessonsList;
    }

    // Enhanced lesson access function
    accessLesson(lessonCode, lessonName) {
        console.log('Accessing lesson:', lessonCode, lessonName);

        // Show confirmation dialog with lesson details
        const confirmed = confirm(
            `Access Lesson: "${lessonName}"\n\n` +
            `Lesson Code: ${lessonCode}\n\n` +
            `You will be redirected to enter your PIN code.\n` +
            `Make sure you have a valid PIN from your teacher.\n\n` +
            `Continue?`
        );

        if (confirmed) {
            // Add loading state
            this.showAlert('Redirecting to lesson access...', 'info');

            // Redirect to StudentViewer with lesson code pre-filled
            setTimeout(() => {
                window.location.href = `/LessonContent/StudentViewer?lessonCode=${lessonCode}`;
            }, 500);
        }
    }

    // Learning Navigation Methods
    showSubjectSelection() {
        document.getElementById('subjectSelection').style.display = 'block';
        document.getElementById('chaptersSection').style.display = 'none';
        document.getElementById('lessonsSection').style.display = 'none';
        this.updateBreadcrumb(['Learning Center']);
    }

    showChaptersSection() {
        document.getElementById('subjectSelection').style.display = 'none';
        document.getElementById('chaptersSection').style.display = 'block';
        document.getElementById('lessonsSection').style.display = 'none';
    }

    showChapters() {
        document.getElementById('chaptersSection').style.display = 'block';
        document.getElementById('lessonsSection').style.display = 'none';
        this.updateBreadcrumb(['Learning Center', this.currentSubject?.subjectName || 'Subject']);
    }

    showLessonsSection() {
        document.getElementById('chaptersSection').style.display = 'none';
        document.getElementById('lessonsSection').style.display = 'block';
    }

    updateBreadcrumb(items) {
        const breadcrumb = document.getElementById('breadcrumb');
        if (!breadcrumb) return;

        const breadcrumbItems = items.map((item, index) => {
            const isLast = index === items.length - 1;
            return isLast
                ? `<li class="breadcrumb-item active">${this.escape(item)}</li>`
                : `<li class="breadcrumb-item">${this.escape(item)}</li>`;
        }).join('');

        breadcrumb.innerHTML = breadcrumbItems;
    }

    showLearningLoading(section) {
        const loadingSpinner = document.querySelector(`#${section} .loading-spinner`);
        if (loadingSpinner) {
            loadingSpinner.style.display = 'flex';
        }
    }

    hideLearningLoading(section) {
        const loadingSpinner = document.querySelector(`#${section} .loading-spinner`);
        if (loadingSpinner) {
            loadingSpinner.style.display = 'none';
        }
    }

    // ============ SHARED UTILITY METHODS ============

    formatTimeRange(start, end) {
        if (!start || !end) return 'Time TBD';
        return `${start} - ${end}`;
    }

    formatDate(d) {
        if (!d || d === 'N/A') return 'N/A';
        try {
            const dt = new Date(d);
            return dt.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
        } catch { return d; }
    }

    showLoading() {
        document.querySelectorAll('.loading-spinner').forEach(el => el.style.display = 'flex');
    }

    hideLoading() {
        document.querySelectorAll('.loading-spinner').forEach(el => el.style.display = 'none');
    }

    showAlert(msg, type = 'info') {
        const container = document.getElementById('alertContainer');
        if (!container) {
            console.log(`Alert (${type}): ${msg}`);
            return;
        }

        const alertClass = type === 'success' ? 'alert-success' : type === 'error' ? 'alert-danger' : 'alert-info';
        const icon = type === 'success' ? 'fa-check-circle' : type === 'error' ? 'fa-exclamation-triangle' : 'fa-info-circle';

        container.innerHTML = `
            <div class="alert ${alertClass} alert-dismissible fade show" role="alert">
                <i class="fas ${icon} me-2"></i>
                ${this.escape(msg)}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;

        setTimeout(() => {
            const alert = container.querySelector('.alert');
            if (alert) {
                alert.remove();
            }
        }, 5000);
    }

    renderError(container, message) {
        if (!container) return;
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-exclamation-triangle"></i>
                <h4>Error</h4>
                <p>${this.escape(message)}</p>
            </div>`;
    }

    emptyState(icon, title, message) {
        return `
            <div class="empty-state">
                <i class="fas ${icon}"></i>
                <h4>${this.escape(title)}</h4>
                <p>${this.escape(message)}</p>
            </div>`;
    }

    escape(text) {
        if (text === null || text === undefined) return '';
        return String(text).replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));
    }
}

// Initialize the dashboard system
console.log('Creating OnlineStudentDashboard instance...');
window.onlineStudentDashboard = new OnlineStudentDashboard();

// Global function for backward compatibility and easy access
function accessLesson(lessonCode, lessonName) {
    console.log('Global accessLesson called:', lessonCode, lessonName);
    if (window.onlineStudentDashboard) {
        window.onlineStudentDashboard.accessLesson(lessonCode, lessonName);
    } else {
        console.error('OnlineStudentDashboard not initialized');
        alert('System not ready. Please refresh the page.');
    }
}

// Debug helper
window.debugLearning = function () {
    console.log('=== Learning System Debug Info ===');
    console.log('Dashboard instance:', window.onlineStudentDashboard);
    console.log('Current pathname:', window.location.pathname);
    console.log('Subject select element:', document.getElementById('subjectSelect'));
    console.log('Learning subjects data:', window.onlineStudentDashboard?.learningSubjects);
    console.log('==================================');
};

console.log('OnlineStudent.js loaded successfully. Use debugLearning() for troubleshooting.');