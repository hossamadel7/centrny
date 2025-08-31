// OnlineStudent.js - synchronized with OnlineStudentController endpoints

class OnlineStudentDashboard {
    constructor() {
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

        this.init();
    }

    init() {
        document.addEventListener('DOMContentLoaded', () => {
            // Check if we're on the learning page
            if (window.location.pathname.includes('/Learning')) {
                this.initLearningSystem();
            } else {
                this.loadAll();
            }
        });
    }

    // ============ DASHBOARD FUNCTIONALITY ============

    async loadAll() {
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
            console.error(e);
            this.showAlert('Failed to load dashboard', 'error');
            this.hideLoading();
        }
    }

    async loadStats() {
        const res = await fetch('/OnlineStudent/GetStudentStats');
        if (!res.ok) throw new Error('Stats fetch failed');
        this.stats = await res.json();
        if (this.stats.error) {
            this.showAlert(this.stats.error, 'error');
            return;
        }
        this.updateStats();
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
    }

    async loadSubjects() {
        const grid = document.getElementById('subjectsGrid');
        const res = await fetch('/OnlineStudent/GetStudentSubjects');
        if (!res.ok) throw new Error('Subjects fetch failed');
        this.subjects = await res.json();
        if (this.subjects.error) {
            this.renderError(grid, this.subjects.error);
            return;
        }
        this.renderSubjects();
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
        const grid = document.getElementById('examsGrid');
        const res = await fetch('/OnlineStudent/GetAttendedExams');
        if (!res.ok) throw new Error('Exams fetch failed');
        this.exams = await res.json();
        if (this.exams.error) {
            this.renderError(grid, this.exams.error);
            return;
        }
        this.renderExams();
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
        this.setupLearningEventListeners();
        this.loadLearningSubjects();
    }

    setupLearningEventListeners() {
        // Subject selection
        const subjectSelect = document.getElementById('subjectSelect');
        const loadChaptersBtn = document.getElementById('loadChaptersBtn');

        if (subjectSelect) {
            subjectSelect.addEventListener('change', () => {
                const selected = subjectSelect.value;
                if (loadChaptersBtn) {
                    loadChaptersBtn.disabled = !selected;
                }
            });
        }

        if (loadChaptersBtn) {
            loadChaptersBtn.addEventListener('click', () => {
                const selectedSubject = subjectSelect.value;
                if (selectedSubject) {
                    this.loadChapters(parseInt(selectedSubject));
                }
            });
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
        try {
            this.showLearningLoading('subjectSelection');
            const response = await fetch('/OnlineStudent/GetLearningSubjects');

            if (!response.ok) {
                throw new Error('Failed to load subjects');
            }

            this.learningSubjects = await response.json();

            if (this.learningSubjects.error) {
                this.showAlert(this.learningSubjects.error, 'error');
                return;
            }

            this.renderLearningSubjects();
            this.hideLearningLoading('subjectSelection');
        } catch (error) {
            console.error('Error loading learning subjects:', error);
            this.showAlert('Failed to load subjects', 'error');
            this.hideLearningLoading('subjectSelection');
        }
    }

    renderLearningSubjects() {
        const subjectSelect = document.getElementById('subjectSelect');
        if (!subjectSelect) return;

        if (!this.learningSubjects || this.learningSubjects.length === 0) {
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
    }

    async loadChapters(subjectCode) {
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
                        <p class="chapter-description">${this.escape(chapter.chapterDescription)}</p>
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
                <p class="text-muted mb-0">${this.escape(chapter.chapterDescription)}</p>
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
                        ${lesson.lessonVideo ? `
                            <button class="btn btn-sm btn-outline-primary" onclick="window.open('${lesson.lessonVideo}', '_blank')">
                                <i class="fas fa-play me-1"></i>Video
                            </button>
                        ` : ''}
                        ${lesson.lessonPdf ? `
                            <button class="btn btn-sm btn-outline-secondary" onclick="window.open('${lesson.lessonPdf}', '_blank')">
                                <i class="fas fa-file-pdf me-1"></i>PDF
                            </button>
                        ` : ''}
                    </div>
                </div>
                <p class="lesson-description">${this.escape(lesson.lessonDescription)}</p>
                <small class="text-muted">
                    <i class="fas fa-calendar me-1"></i>
                    Added: ${this.formatDate(lesson.insertTime)}
                </small>
            </div>
        `).join('');

        lessonsContainer.innerHTML = chapterInfo + lessonsList;
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
        if (!container) return;

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
window.onlineStudentDashboard = new OnlineStudentDashboard();