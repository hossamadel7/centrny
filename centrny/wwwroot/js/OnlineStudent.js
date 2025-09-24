console.log('OnlineStudent.js loading...');

class OnlineStudentDashboard {
    constructor() {
        this.subjects = [];
        this.exams = [];
        this.stats = null;
        this.subscription = null;
        this.learningSubjects = [];
        this.chapters = [];
        this.lessons = [];
        this.currentSubject = null;
        this.currentChapter = null;

        // Auto-load chapters immediately if there is only one subject
        this.autoLoadSingleSubject = true;

        this.locNode = document.getElementById('js-online-localization');
        this.locale = this.locNode?.dataset.locale || 'en';
        this.dir = this.locNode?.dataset.dir || 'ltr';

        this._buildLocalizationIndex();

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.init());
        } else {
            this.init();
        }
    }

    _buildLocalizationIndex() {
        this._locMap = {};
        if (!this.locNode) return;
        const ds = this.locNode.dataset;
        const toSnakeFromCamel = (k) => k.replace(/([A-Z])/g, '_$1').toLowerCase();
        const snakeToDash = (s) => s.replace(/_/g, '-');
        for (const camelKey of Object.keys(ds)) {
            const value = ds[camelKey];
            const snake = toSnakeFromCamel(camelKey);
            const dash = snakeToDash(snake);
            const upperSnake = snake.toUpperCase();
            this._locMap[camelKey] = value;
            this._locMap[snake] = value;
            this._locMap[dash] = value;
            this._locMap[upperSnake] = value;
            this._locMap[snake.replace(/_/g, '')] = value;
            this._locMap[dash.replace(/-/g, '')] = value;
        }
    }

    _normalizeKeyVariants(key) {
        const base = key.trim();
        const lower = base.toLowerCase();
        const snake = lower.replace(/-/g, '_').replace(/ /g, '_');
        const dash = snake.replace(/_/g, '-');
        const toCamel = (s) => s.split(/[_-]/).map((p, i) => i === 0 ? p : (p.charAt(0).toUpperCase() + p.slice(1))).join('');
        const camelFromSnake = toCamel(snake);
        const camelFromDash = toCamel(dash);
        return [base, lower, snake, dash, camelFromSnake, camelFromDash, snake.replace(/_/g, ''), dash.replace(/-/g, '')];
    }

    loc(key, fallback = '') {
        if (!key) return fallback;
        const variants = this._normalizeKeyVariants(key);
        for (const v of variants) if (v in this._locMap) return this._locMap[v];
        if (!this._locMissingWarned) this._locMissingWarned = new Set();
        if (!this._locMissingWarned.has(key)) {
            console.warn(`[Localization] Missing key: "${key}" (tried: ${variants.join(', ')})`);
            this._locMissingWarned.add(key);
        }
        return fallback || key;
    }

    init() {
        const currentPath = window.location.pathname.toLowerCase();
        if (currentPath.includes('learning') || currentPath.endsWith('/learning')) {
            this.initLearningSystem();
        } else if (currentPath.includes('onlinestudent') || currentPath === '/' || currentPath.includes('index')) {
            this.loadAll();
        } else {
            if (document.getElementById('subjectSelect')) this.initLearningSystem();
            else if (document.getElementById('subjectsGrid')) this.loadAll();
        }
    }

    // DASHBOARD
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
            this.showAlert(this.loc('Alert_DashboardLoaded', 'Dashboard loaded'));
        } catch (e) {
            console.error(e);
            this.showAlert(this.loc('Alert_DashboardFailed', 'Failed to load dashboard'), 'error');
            this.hideLoading();
        }
    }

    async loadStats() {
        try {
            const res = await fetch('/OnlineStudent/GetStudentStats');
            if (!res.ok) throw new Error();
            this.stats = await res.json();
            if (this.stats.error) return;
            this.updateStats();
        } catch { }
    }

    updateStats() {
        if (!this.stats) return;
        this.setCounter('subjectsCount', this.stats.subjectsCount);
        this.setCounter('examsCount', this.stats.examsCount);
        this.setCounter('attendanceCount', this.stats.totalAttendance);
        const avg = document.getElementById('averageGrade');
        if (avg) avg.textContent = (this.stats.averageGrade && this.stats.averageGrade > 0) ? `${this.stats.averageGrade}%` : '--';
    }

    setCounter(id, value) {
        const el = document.getElementById(id);
        if (!el) return;
        const target = parseInt(value || 0, 10);
        let current = 0;
        const duration = 500;
        const stepTime = 30;
        const step = Math.max(1, Math.floor(target / (duration / stepTime)));
        const timer = setInterval(() => {
            current += step;
            if (current >= target) {
                current = target;
                clearInterval(timer);
            }
            el.textContent = current;
        }, stepTime);
    }

    async loadSubscription() {
        try {
            const res = await fetch('/OnlineStudent/GetSubscriptionStatus');
            if (!res.ok) throw new Error();
            this.subscription = await res.json();
            if (this.subscription.error) return;
            const badge = document.getElementById('subscription-status');
            if (badge) {
                badge.textContent = this.subscription.isSubscribed
                    ? this.loc('Badge_Subscribed', 'Subscribed')
                    : this.loc('Badge_Regular', 'Regular');
                badge.className = `badge ${this.subscription.isSubscribed ? 'bg-success' : 'bg-secondary'}`;
            }
        } catch { }
    }

    async loadSubjects() {
        const grid = document.getElementById('subjectsGrid');
        try {
            const res = await fetch('/OnlineStudent/GetStudentSubjects');
            if (!res.ok) throw new Error();
            this.subjects = await res.json();
            if (this.subjects.error) {
                this.renderError(grid, this.subjects.error);
                return;
            }
            this.renderSubjects();
        } catch {
            this.renderError(grid, this.loc('Alert_LoadSubjectsFailed', 'Failed to load subjects'));
        }
    }

    renderSubjects() {
        const grid = document.getElementById('subjectsGrid');
        if (!grid) return;
        if (!this.subjects || this.subjects.length === 0) {
            grid.innerHTML = this.emptyState(
                'fa-book',
                this.loc('Empty_NoSubjectsTitle', 'No Subjects'),
                this.loc('Empty_NoSubjectsMessage', 'You are not enrolled in any subjects yet.')
            );
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
                    <div class="detail-item"><i class="fas fa-calendar"></i><span>${s.scheduleDay || '--'}</span></div>
                    <div class="detail-item"><i class="fas fa-clock"></i><span>${this.formatTimeRange(s.scheduleStartTime, s.scheduleEndTime)}</span></div>
                    <div class="detail-item"><i class="fas fa-map-marker-alt"></i><span>${s.hallName || '--'}</span></div>
                    <div class="detail-item"><i class="fas fa-building"></i><span>${this.escape(s.branchName)}</span></div>
                    <div class="detail-item"><i class="fas fa-phone"></i><span>${s.teacherPhone || 'N/A'}</span></div>
                    ${s.isOnline ? `<div class="online-badge"><i class="fas fa-wifi"></i> ${this.loc('Online_Badge', 'Online')}</div>` : ''}
                    ${(s.studentFee ? `<div class="detail-item"><i class="fas fa-money-bill"></i><span>${this.loc('Fee_Label', 'Fee')}: ${s.studentFee}</span></div>` : '')}
                </div>
            </div>
        `).join('');
    }

    async loadExams() {
        const grid = document.getElementById('examsGrid');
        try {
            const res = await fetch('/OnlineStudent/GetAttendedExams');
            if (!res.ok) throw new Error();
            this.exams = await res.json();
            if (this.exams.error) {
                this.renderError(grid, this.exams.error);
                return;
            }
            this.renderExams();
        } catch {
            this.renderError(grid, this.loc('Alert_LoadExamsFailed', 'Failed to load exams'));
        }
    }

    renderExams() {
        const grid = document.getElementById('examsGrid');
        if (!grid) return;
        if (!this.exams || this.exams.length === 0) {
            grid.innerHTML = this.emptyState(
                'fa-clipboard-list',
                this.loc('Empty_NoExamsTitle', 'No Exams'),
                this.loc('Empty_NoExamsMessage', 'You have not taken any exams yet.')
            );
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
                        <div class="detail-row"><span class="label">${this.loc('Label_Subject', 'Subject')}</span><span class="value">${this.escape(e.subjectName)}</span></div>
                        <div class="detail-row"><span class="label">${this.loc('Label_Teacher', 'Teacher')}</span><span class="value">${this.escape(e.teacherName)}</span></div>
                        <div class="detail-row"><span class="label">${this.loc('Label_Date', 'Date')}</span><span class="value">${this.formatDate(e.examDate)}</span></div>
                        <div class="detail-row"><span class="label">${this.loc('Label_Type', 'Type')}</span><span class="value">${e.isExam ? this.loc('Type_Exam', 'Exam') : this.loc('Type_Quiz', 'Quiz')}</span></div>
                        <div class="detail-row"><span class="label">${this.loc('Label_Status', 'Status')}</span>
                            <span class="value ${pass ? 'text-success' : 'text-danger'}">${pass ? this.loc('Status_Passed', 'Passed') : this.loc('Status_Failed', 'Failed')}</span>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    // LEARNING
    initLearningSystem() {
        this.setupLearningEventListeners();
        setTimeout(() => this.loadLearningSubjects(), 50);
    }

    setupLearningEventListeners() {
        const subjectSelect = document.getElementById('subjectSelect');
        const loadChaptersBtn = document.getElementById('loadChaptersBtn');
        if (subjectSelect) {
            subjectSelect.addEventListener('change', () => {
                if (loadChaptersBtn) loadChaptersBtn.disabled = !subjectSelect.value;
            });
        }
        if (loadChaptersBtn) {
            loadChaptersBtn.addEventListener('click', () => {
                if (subjectSelect.value) this.loadChapters(parseInt(subjectSelect.value));
            });
        }
        const backToSubjectsBtn = document.getElementById('backToSubjectsBtn');
        if (backToSubjectsBtn) backToSubjectsBtn.addEventListener('click', () => this.showSubjectSelection());
        const backToChaptersBtn = document.getElementById('backToChaptersBtn');
        if (backToChaptersBtn) backToChaptersBtn.addEventListener('click', () => this.showChapters());
    }

    async loadLearningSubjects() {
        try {
            const subjectSelect = document.getElementById('subjectSelect');
            if (!subjectSelect) return;
            subjectSelect.innerHTML = `<option value="">${this.loc('Loading_Subjects', 'Loading subjects...')}</option>`;
            const response = await fetch('/OnlineStudent/GetLearningSubjects');
            if (!response.ok) throw new Error();
            this.learningSubjects = await response.json();
            if (this.learningSubjects.error) {
                subjectSelect.innerHTML = `<option value="">${this.learningSubjects.error}</option>`;
                this.showAlert(this.learningSubjects.error, 'error');
                return;
            }
            this.renderLearningSubjects();
        } catch {
            const subjectSelect = document.getElementById('subjectSelect');
            if (subjectSelect) subjectSelect.innerHTML = `<option value="">${this.loc('Alert_LoadSubjectsFailed', 'Failed to load')}</option>`;
            this.showAlert(this.loc('Alert_LoadSubjectsFailed', 'Failed to load subjects'), 'error');
        }
    }

    renderLearningSubjects() {
        const subjectSelect = document.getElementById('subjectSelect');
        if (!subjectSelect) return;

        if (!this.learningSubjects || this.learningSubjects.length === 0) {
            subjectSelect.innerHTML = `<option value="">${this.loc('Select_SubjectPlaceholder', 'Select a subject')}</option>`;
            return;
        }

        const single = this.learningSubjects.length === 1;
        if (single) {
            const s = this.learningSubjects[0];
            // Only one subject: select it
            subjectSelect.innerHTML = `<option value="${s.subjectCode}" selected>${this.escape(s.subjectName)} - ${this.escape(s.eduYearName)}</option>`;
            subjectSelect.value = s.subjectCode;
            const loadBtn = document.getElementById('loadChaptersBtn');
            if (loadBtn) loadBtn.disabled = false;

            // Auto-load chapters immediately if configured
            if (this.autoLoadSingleSubject) {
                setTimeout(() => this.loadChapters(s.subjectCode), 80);
            }
        } else {
            const options = [`<option value="">${this.loc('Select_SubjectPlaceholder', 'Select a subject')}</option>`];
            this.learningSubjects.forEach(subject => {
                options.push(`<option value="${subject.subjectCode}">${this.escape(subject.subjectName)} - ${this.escape(subject.eduYearName)}</option>`);
            });
            subjectSelect.innerHTML = options.join('');
        }
    }

    async loadChapters(subjectCode) {
        try {
            this.currentSubject = this.learningSubjects.find(s => s.subjectCode === subjectCode);
            this.showChaptersSection();
            this.showLearningLoading('chaptersSection');
            const response = await fetch(`/OnlineStudent/GetSubjectChapters?subjectCode=${subjectCode}`);
            if (!response.ok) throw new Error();
            this.chapters = await response.json();
            if (this.chapters.error) {
                this.showAlert(this.chapters.error, 'error');
                return;
            }
            this.renderChapters();
            this.updateBreadcrumb(['Title_LearningCenter', this.currentSubject?.subjectName || '...']);
        } catch {
            this.showAlert(this.loc('Alert_LoadChaptersFailed', 'Failed to load chapters'), 'error');
        } finally {
            this.hideLearningLoading('chaptersSection');
        }
    }

    renderChapters() {
        const chaptersGrid = document.getElementById('chaptersGrid');
        if (!chaptersGrid) return;
        if (!this.chapters || this.chapters.length === 0) {
            chaptersGrid.innerHTML = this.emptyState(
                'fa-layer-group',
                this.loc('Empty_NoChaptersTitle', 'No Chapters'),
                this.loc('Empty_NoChaptersMessage', 'No chapters available for this subject.')
            );
            return;
        }
        chaptersGrid.innerHTML = this.chapters.map(chapter => `
            <div class="col-md-6 col-lg-4">
                <div class="chapter-card card h-100" data-chapter-code="${chapter.lessonCode}">
                    <div class="card-body">
                        <div class="chapter-header">
                            <h5 class="chapter-title">${this.escape(chapter.chapterName)}</h5>
                            <span class="lessons-count-badge">${chapter.lessonsCount}</span>
                        </div>
                        <p class="chapter-description">${this.loc('Label_Subject', 'Subject')}: ${this.escape(chapter.subjectName)}</p>
                        <div class="d-flex justify-content-between align-items-center">
                            <small class="text-muted"><i class="fas fa-clock me-1"></i>${this.formatDate(chapter.insertTime)}</small>
                            <i class="fas fa-arrow-right text-primary"></i>
                        </div>
                    </div>
                </div>
            </div>
        `).join('');
        chaptersGrid.querySelectorAll('.chapter-card').forEach(card => {
            card.addEventListener('click', () => {
                const chapterCode = parseInt(card.dataset.chapterCode);
                this.loadLessons(chapterCode);
            });
        });
    }

    async loadLessons(chapterCode) {
        try {
            this.currentChapter = this.chapters.find(c => c.lessonCode === chapterCode);
            this.showLessonsSection();
            this.showLearningLoading('lessonsSection');
            const response = await fetch(`/OnlineStudent/GetChapterLessons?chapterCode=${chapterCode}`);
            if (!response.ok) throw new Error();
            const data = await response.json();
            if (data.error) {
                this.showAlert(data.error, 'error');
                return;
            }
            this.lessons = data.lessons;
            this.renderLessons(data.chapter);
            this.updateBreadcrumb([
                'Title_LearningCenter',
                this.currentSubject?.subjectName || '...',
                data.chapter?.chapterName || '...'
            ]);
        } catch {
            this.showAlert(this.loc('Alert_LoadLessonsFailed', 'Failed to load lessons'), 'error');
        } finally {
            this.hideLearningLoading('lessonsSection');
        }
    }

    renderLessons(chapter) {
        const lessonsContainer = document.getElementById('lessonsContainer');
        if (!lessonsContainer) return;
        if (!this.lessons || this.lessons.length === 0) {
            lessonsContainer.innerHTML = this.emptyState(
                'fa-play-circle',
                this.loc('Empty_NoLessonsTitle', 'No Lessons'),
                this.loc('Empty_NoLessonsMessage', 'No lessons available in this chapter.')
            );
            return;
        }
        const chapterInfo = `
            <div class="mb-4 p-3 bg-light rounded">
                <h4>${this.escape(chapter.chapterName)}</h4>
                <p class="text-muted mb-0">${this.loc('Label_Subject', 'Subject')}: ${this.escape(chapter.subjectName)}</p>
                <small class="text-muted">${this.lessons.length}</small>
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
                        <button class="btn btn-primary btn-sm"
                            onclick="window.onlineStudentDashboard.accessLesson(${lesson.lessonCode}, '${this.escape(lesson.lessonName)}')">
                            <i class="fas fa-key me-1"></i>${this.loc('Button_AccessLesson', 'Access')}
                        </button>
                    </div>
                </div>
                <p class="lesson-description">
                    <small class="text-muted">
                        <i class="fas fa-calendar me-1"></i>${this.loc('Added_Label', 'Added')}: ${this.formatDate(lesson.insertTime)}
                    </small>
                </p>
            </div>
        `).join('');
        lessonsContainer.innerHTML = chapterInfo + lessonsList;
    }

    accessLesson(lessonCode, lessonName) {
        const msg = `${this.loc('Confirm_AccessLesson', 'Access this lesson?')}\n${lessonName}`;
        if (!confirm(msg)) return;

        fetch(`/LessonContent/CanAccessLesson?lessonCode=${lessonCode}`)
            .then(r => r.json())
            .then(res => {
                if (res.canAttend) {
                    this.showAlert(this.loc('Redirecting', 'Redirecting...'), 'info');
                    setTimeout(() => {
                        window.location.href = `/LessonContent/StudentViewer?lessonCode=${lessonCode}`;
                    }, 300);
                } else {
                    this.showAlert(res.message || this.loc('Access_Expired', 'Access expired. Enter PIN.'), 'error');
                    setTimeout(() => {
                        window.location.href = `/LessonContent/StudentViewer?lessonCode=${lessonCode}`;
                    }, 1200);
                }
            })
            .catch(() => {
                this.showAlert(this.loc('Error_CheckingAccess', 'Error checking lesson access'), 'error');
            });
    }

    showSubjectSelection() {
        this.toggleDisplay('subjectSelection', true);
        this.toggleDisplay('chaptersSection', false);
        this.toggleDisplay('lessonsSection', false);
        this.updateBreadcrumb(['Title_LearningCenter']);
    }
    showChaptersSection() {
        this.toggleDisplay('subjectSelection', false);
        this.toggleDisplay('chaptersSection', true);
        this.toggleDisplay('lessonsSection', false);
    }
    showChapters() {
        this.toggleDisplay('chaptersSection', true);
        this.toggleDisplay('lessonsSection', false);
    }
    showLessonsSection() {
        this.toggleDisplay('chaptersSection', false);
        this.toggleDisplay('lessonsSection', true);
    }

    toggleDisplay(id, show) {
        const el = document.getElementById(id);
        if (!el) return;
        el.classList.toggle('d-none', !show);
    }

    updateBreadcrumb(items) {
        const breadcrumb = document.getElementById('breadcrumb');
        if (!breadcrumb) return;
        const html = items.map((k, i) => {
            const text = this.loc(k, k);
            return `<li class="breadcrumb-item ${i === items.length - 1 ? 'active' : ''}">${this.escape(text)}</li>`;
        }).join('');
        breadcrumb.innerHTML = html;
    }

    showLearningLoading(section) {
        const sp = document.querySelector(`#${section} .loading-spinner`);
        if (sp) sp.classList.remove('d-none');
    }
    hideLearningLoading(section) {
        const sp = document.querySelector(`#${section} .loading-spinner`);
        if (sp) sp.classList.add('d-none');
    }

    formatTimeRange(start, end) {
        if (!start || !end) return '--';
        return `${start} - ${end}`;
    }

    formatDate(d) {
        if (!d || d === 'N/A') return 'N/A';
        try {
            const dt = new Date(d);
            return dt.toLocaleDateString(this.locale || 'en', { year: 'numeric', month: 'short', day: 'numeric' });
        } catch {
            return d;
        }
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
        const alertClass = type === 'success' ? 'alert-success'
            : type === 'error' ? 'alert-danger'
                : 'alert-info';
        const icon = type === 'success' ? 'fa-check-circle'
            : type === 'error' ? 'fa-exclamation-triangle'
                : 'fa-info-circle';
        container.innerHTML = `
            <div class="alert ${alertClass} alert-dismissible fade show" role="alert">
                <i class="fas ${icon} me-2"></i>${this.escape(msg)}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>`;
        setTimeout(() => {
            const alert = container.querySelector('.alert');
            if (alert) alert.remove();
        }, 5000);
    }

    renderError(container, message) {
        if (!container) return;
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-exclamation-triangle"></i>
                <h4>${this.escape(this.loc('Error', 'Error'))}</h4>
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
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
        }[c]));
    }
}

window.onlineStudentDashboard = new OnlineStudentDashboard();

function switchLanguage(lang) {
    document.cookie = `.AspNetCore.Culture=c=${lang}|uic=${lang}; path=/; max-age=31536000`;
    location.reload();
}

function closeMobileNav() {
    document.querySelector('.mobile-nav-sidebar')?.classList.remove('open');
    document.querySelector('.mobile-nav-overlay')?.classList.remove('show');
}

document.addEventListener('DOMContentLoaded', () => {
    const navToggler = document.querySelector('.navbar-toggler');
    if (navToggler) {
        navToggler.addEventListener('click', () => {
            document.querySelector('.mobile-nav-sidebar')?.classList.add('open');
            document.querySelector('.mobile-nav-overlay')?.classList.add('show');
        });
    }
});

console.log('OnlineStudent.js loaded with single-subject auto-select + auto chapter loading.');