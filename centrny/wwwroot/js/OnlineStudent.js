// OnlineStudent.js - synchronized with OnlineStudentController endpoints

class OnlineStudentDashboard {
    constructor() {
        this.subjects = [];
        this.exams = [];
        this.stats = null;
        this.subscription = null;
        this.init();
    }

    init() {
        document.addEventListener('DOMContentLoaded', () => {
            this.loadAll();
        });
    }

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
        const cls = type === 'success' ? 'alert-success' : type === 'error' ? 'alert-error' : 'alert-info';
        const icon = type === 'success' ? 'fa-check-circle' : type === 'error' ? 'fa-exclamation-triangle' : 'fa-info-circle';
        container.innerHTML = `
            <div class="alert-message ${cls}">
                <i class="fas ${icon}"></i>
                <span>${this.escape(msg)}</span>
            </div>`;
        setTimeout(() => { container.innerHTML = ''; }, 5000);
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

window.onlineStudentDashboard = new OnlineStudentDashboard();