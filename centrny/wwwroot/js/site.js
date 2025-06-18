// Modernized Site.js - Enhanced version with better performance and UX
class SiteManager {
    constructor() {
        this.cache = new Map();
        this.loadingStates = new Set();

        this.init();
    }

    init() {
        this.initializeCSRFToken();
        this.setupGlobalErrorHandling();
        this.loadInitialData();
        this.setupNavigationEnhancements();
        this.initializeProgressiveEnhancements();
    }

    // ==================== SECURITY & SETUP ====================

    initializeCSRFToken() {
        this.csrfToken = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ||
            document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    }

    getRequestHeaders() {
        const headers = {
            'Content-Type': 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
        };

        if (this.csrfToken) {
            headers['RequestVerificationToken'] = this.csrfToken;
        }

        return headers;
    }

    setupGlobalErrorHandling() {
        // Handle unhandled promise rejections
        window.addEventListener('unhandledrejection', (event) => {
            console.error('Unhandled promise rejection:', event.reason);
            this.showToast('An unexpected error occurred. Please try again.', 'error');

            // Prevent the default browser error reporting
            event.preventDefault();
        });

        // Handle global JavaScript errors
        window.addEventListener('error', (event) => {
            console.error('JavaScript error:', event.error);

            // Only show user-friendly message for non-development environments
            if (!this.isDevelopment()) {
                this.showToast('Something went wrong. Please refresh the page.', 'error');
            }
        });
    }

    isDevelopment() {
        return window.location.hostname === 'localhost' ||
            window.location.hostname.includes('dev') ||
            window.location.protocol === 'file:';
    }

    // ==================== API MANAGEMENT ====================

    async makeRequest(url, options = {}) {
        const defaultOptions = {
            method: 'GET',
            headers: this.getRequestHeaders(),
            credentials: 'same-origin'
        };

        const mergedOptions = { ...defaultOptions, ...options };

        if (mergedOptions.body && typeof mergedOptions.body === 'object') {
            mergedOptions.body = JSON.stringify(mergedOptions.body);
        }

        try {
            const response = await fetch(url, mergedOptions);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                return await response.json();
            }

            return await response.text();
        } catch (error) {
            console.error('Request failed:', error);
            throw error;
        }
    }

    async cachedRequest(url, options = {}, cacheTime = 10 * 60 * 1000) { // 10 minutes default
        const cacheKey = `${url}_${JSON.stringify(options)}`;
        const cached = this.cache.get(cacheKey);

        if (cached && Date.now() - cached.timestamp < cacheTime) {
            return cached.data;
        }

        const response = await this.makeRequest(url, options);
        this.cache.set(cacheKey, {
            data: response,
            timestamp: Date.now()
        });

        return response;
    }

    // ==================== DATA LOADING ====================

    async loadInitialData() {
        await Promise.all([
            this.loadRootModules(),
            this.loadNavigationData(),
            this.initializeUserSession()
        ]);
    }

    async loadRootModules() {
        const tableContainer = document.getElementById('rootModulesTable');
        if (!tableContainer) return;

        const tableBody = tableContainer.querySelector('tbody');
        if (!tableBody) return;

        try {
            this.showLoadingState('rootModules');

            // Update the API endpoint to match your actual backend
            const data = await this.cachedRequest('/api/root-modules');

            this.renderRootModulesTable(data, tableBody);
            this.showToast('Data loaded successfully', 'success', 2000);

        } catch (error) {
            console.error('Error loading root modules:', error);
            this.showErrorState(tableBody, 'Failed to load root modules data');
            this.showToast('Failed to load data. Please refresh the page.', 'error');
        } finally {
            this.hideLoadingState('rootModules');
        }
    }

    renderRootModulesTable(data, tableBody) {
        if (!Array.isArray(data) || data.length === 0) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-muted py-4">
                        <i class="fas fa-inbox fa-2x mb-2"></i><br>
                        No root modules found
                    </td>
                </tr>
            `;
            return;
        }

        tableBody.innerHTML = data.map(item => `
            <tr class="table-row-hover" data-id="${item.rootCode}">
                <td class="fw-bold">${this.sanitizeHTML(item.rootCode)}</td>
                <td>${this.sanitizeHTML(item.rootName || 'N/A')}</td>
                <td>${this.sanitizeHTML(item.rootPhone || 'N/A')}</td>
                <td>${this.sanitizeHTML(item.rootOwner || 'N/A')}</td>
                <td>
                    ${item.isCenter ?
                '<span class="badge bg-primary"><i class="fas fa-building me-1"></i>Center</span>' :
                '<span class="badge bg-info"><i class="fas fa-user me-1"></i>Individual</span>'
            }
                </td>
            </tr>
        `).join('');

        // Add click handlers for row interaction
        this.attachTableRowHandlers(tableBody);
    }

    attachTableRowHandlers(tableBody) {
        tableBody.querySelectorAll('tr[data-id]').forEach(row => {
            row.addEventListener('click', (e) => {
                const rootCode = row.dataset.id;
                this.showRootDetails(rootCode);
            });

            // Add hover effects
            row.addEventListener('mouseenter', () => {
                row.style.backgroundColor = 'rgba(0, 123, 255, 0.05)';
            });

            row.addEventListener('mouseleave', () => {
                row.style.backgroundColor = '';
            });
        });
    }

    async showRootDetails(rootCode) {
        try {
            const rootData = await this.cachedRequest(`/api/root/${rootCode}`);
            this.displayRootModal(rootData);
        } catch (error) {
            console.error('Error loading root details:', error);
            this.showToast('Failed to load root details', 'error');
        }
    }

    displayRootModal(rootData) {
        // Create and show a modal with root details
        const modalHtml = `
            <div class="modal fade" id="rootDetailsModal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">
                                <i class="fas ${rootData.isCenter ? 'fa-building' : 'fa-user'} me-2"></i>
                                Root Details: ${this.sanitizeHTML(rootData.rootName)}
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="row">
                                <div class="col-sm-4"><strong>Code:</strong></div>
                                <div class="col-sm-8">${this.sanitizeHTML(rootData.rootCode)}</div>
                            </div>
                            <div class="row mt-2">
                                <div class="col-sm-4"><strong>Name:</strong></div>
                                <div class="col-sm-8">${this.sanitizeHTML(rootData.rootName || 'N/A')}</div>
                            </div>
                            <div class="row mt-2">
                                <div class="col-sm-4"><strong>Phone:</strong></div>
                                <div class="col-sm-8">${this.sanitizeHTML(rootData.rootPhone || 'N/A')}</div>
                            </div>
                            <div class="row mt-2">
                                <div class="col-sm-4"><strong>Owner:</strong></div>
                                <div class="col-sm-8">${this.sanitizeHTML(rootData.rootOwner || 'N/A')}</div>
                            </div>
                            <div class="row mt-2">
                                <div class="col-sm-4"><strong>Type:</strong></div>
                                <div class="col-sm-8">
                                    ${rootData.isCenter ?
                '<span class="badge bg-primary">Center</span>' :
                '<span class="badge bg-info">Individual</span>'
            }
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal if any
        const existingModal = document.getElementById('rootDetailsModal');
        if (existingModal) {
            existingModal.remove();
        }

        // Add new modal to body
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Show modal
        const modal = new bootstrap.Modal(document.getElementById('rootDetailsModal'));
        modal.show();

        // Clean up when modal is hidden
        document.getElementById('rootDetailsModal').addEventListener('hidden.bs.modal', function () {
            this.remove();
        });
    }

    async loadNavigationData() {
        // Load user-specific navigation items
        try {
            const navData = await this.cachedRequest('/api/user-navigation');
            this.enhanceNavigation(navData);
        } catch (error) {
            console.warn('Could not load navigation data:', error);
            // Navigation enhancement is optional
        }
    }

    enhanceNavigation(navData) {
        if (!navData || !navData.menuItems) return;

        const navbar = document.querySelector('.navbar-nav');
        if (!navbar) return;

        // Add user-specific menu items
        navData.menuItems.forEach(item => {
            if (item.hasPermission) {
                const li = document.createElement('li');
                li.className = 'nav-item';
                li.innerHTML = `
                    <a class="nav-link" href="${item.url}">
                        <i class="${item.icon} me-1"></i>
                        ${this.sanitizeHTML(item.title)}
                    </a>
                `;
                navbar.appendChild(li);
            }
        });
    }

    async initializeUserSession() {
        try {
            const sessionData = await this.cachedRequest('/api/user-session', {}, 60000); // 1 minute cache
            this.updateUserInterface(sessionData);
        } catch (error) {
            console.warn('Could not load session data:', error);
        }
    }

    updateUserInterface(sessionData) {
        if (!sessionData) return;

        // Update user display name
        const userDisplay = document.getElementById('userDisplay');
        if (userDisplay && sessionData.userName) {
            userDisplay.textContent = sessionData.userName;
        }

        // Update user avatar
        const userAvatar = document.getElementById('userAvatar');
        if (userAvatar && sessionData.avatarUrl) {
            userAvatar.src = sessionData.avatarUrl;
        }

        // Store user info for later use
        window.currentUser = sessionData;
    }

    // ==================== UI ENHANCEMENTS ====================

    setupNavigationEnhancements() {
        // Add active page highlighting
        const currentPath = window.location.pathname;
        document.querySelectorAll('.nav-link').forEach(link => {
            if (link.getAttribute('href') === currentPath) {
                link.classList.add('active');
            }
        });

        // Add smooth scrolling for anchor links
        document.querySelectorAll('a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', function (e) {
                e.preventDefault();
                const target = document.querySelector(this.getAttribute('href'));
                if (target) {
                    target.scrollIntoView({
                        behavior: 'smooth'
                    });
                }
            });
        });

        // Add loading indicators for form submissions
        document.querySelectorAll('form').forEach(form => {
            form.addEventListener('submit', (e) => {
                const submitButton = form.querySelector('button[type="submit"]');
                if (submitButton) {
                    this.showLoadingButton(submitButton, 'Processing...');
                }
            });
        });
    }

    initializeProgressiveEnhancements() {
        // Add tooltips to elements with title attribute
        document.querySelectorAll('[title]').forEach(element => {
            if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
                new bootstrap.Tooltip(element);
            }
        });

        // Add confirmation to dangerous actions
        document.querySelectorAll('.btn-danger, [data-confirm]').forEach(button => {
            button.addEventListener('click', (e) => {
                const message = button.dataset.confirm || 'Are you sure you want to perform this action?';
                if (!confirm(message)) {
                    e.preventDefault();
                    e.stopPropagation();
                }
            });
        });

        // Initialize auto-refresh for dynamic content
        this.setupAutoRefresh();

        // Add keyboard shortcuts
        this.setupKeyboardShortcuts();
    }

    setupAutoRefresh() {
        // Refresh data every 5 minutes for active tabs
        setInterval(() => {
            if (!document.hidden) {
                this.refreshCriticalData();
            }
        }, 5 * 60 * 1000);

        // Refresh when tab becomes visible again
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) {
                this.refreshCriticalData();
            }
        });
    }

    async refreshCriticalData() {
        // Clear cache for critical data
        this.cache.clear();

        // Reload if we're on a data-heavy page
        const currentPage = window.location.pathname;
        if (currentPage === '/' || currentPage.includes('dashboard')) {
            try {
                await this.loadRootModules();
            } catch (error) {
                console.warn('Auto-refresh failed:', error);
            }
        }
    }

    setupKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            // Ignore if typing in input fields
            if (e.target.matches('input, textarea, select')) return;

            switch (true) {
                case e.ctrlKey && e.key === 'r':
                    e.preventDefault();
                    this.refreshPage();
                    break;
                case e.ctrlKey && e.key === 'h':
                    e.preventDefault();
                    window.location.href = '/';
                    break;
                case e.key === '?':
                    e.preventDefault();
                    this.showKeyboardShortcuts();
                    break;
            }
        });
    }

    showKeyboardShortcuts() {
        const shortcuts = `
            <div class="keyboard-shortcuts">
                <h6 class="mb-3">Keyboard Shortcuts</h6>
                <div class="row">
                    <div class="col-6">
                        <div class="shortcut-item"><kbd>Ctrl</kbd> + <kbd>R</kbd> - Refresh Data</div>
                        <div class="shortcut-item"><kbd>Ctrl</kbd> + <kbd>H</kbd> - Go Home</div>
                    </div>
                    <div class="col-6">
                        <div class="shortcut-item"><kbd>?</kbd> - Show Shortcuts</div>
                        <div class="shortcut-item"><kbd>Esc</kbd> - Close Modals</div>
                    </div>
                </div>
            </div>
        `;

        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: 'Keyboard Shortcuts',
                html: shortcuts,
                icon: 'info',
                confirmButtonText: 'Got it!'
            });
        } else {
            this.showToast('Ctrl+R: Refresh | Ctrl+H: Home | ?: Shortcuts | Esc: Close', 'info', 5000);
        }
    }

    refreshPage() {
        this.showToast('Refreshing data...', 'info', 2000);
        this.cache.clear();
        this.loadInitialData();
    }

    // ==================== UI UTILITIES ====================

    showLoadingState(identifier) {
        this.loadingStates.add(identifier);
        const element = document.getElementById(identifier) || document.querySelector(`[data-loading="${identifier}"]`);

        if (element) {
            element.style.opacity = '0.6';
            element.style.pointerEvents = 'none';

            // Add loading spinner if not exists
            if (!element.querySelector('.loading-spinner')) {
                const spinner = document.createElement('div');
                spinner.className = 'loading-spinner position-absolute top-50 start-50 translate-middle';
                spinner.innerHTML = '<i class="fas fa-spinner fa-spin fa-2x text-primary"></i>';
                element.style.position = 'relative';
                element.appendChild(spinner);
            }
        }
    }

    hideLoadingState(identifier) {
        this.loadingStates.delete(identifier);
        const element = document.getElementById(identifier) || document.querySelector(`[data-loading="${identifier}"]`);

        if (element) {
            element.style.opacity = '1';
            element.style.pointerEvents = 'auto';

            const spinner = element.querySelector('.loading-spinner');
            if (spinner) {
                spinner.remove();
            }
        }
    }

    showErrorState(container, message) {
        if (!container) return;

        container.innerHTML = `
            <tr>
                <td colspan="100%" class="text-center text-danger py-4">
                    <i class="fas fa-exclamation-triangle fa-2x mb-2"></i><br>
                    ${this.sanitizeHTML(message)}
                    <br>
                    <button type="button" class="btn btn-outline-primary btn-sm mt-2" onclick="location.reload()">
                        <i class="fas fa-redo"></i> Retry
                    </button>
                </td>
            </tr>
        `;
    }

    showLoadingButton(button, loadingText = 'Loading...') {
        if (!button) return;
        button.dataset.originalContent = button.innerHTML;
        button.innerHTML = `<i class="fas fa-spinner fa-spin me-2"></i>${loadingText}`;
        button.disabled = true;
    }

    hideLoadingButton(button) {
        if (!button) return;
        button.innerHTML = button.dataset.originalContent || button.innerHTML;
        button.disabled = false;
    }

    showToast(message, type = 'info', duration = 4000) {
        // Create toast container if it doesn't exist
        let container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'position-fixed top-0 end-0 p-3';
            container.style.zIndex = '1055';
            document.body.appendChild(container);
        }

        const toastElement = document.createElement('div');
        toastElement.className = `toast align-items-center text-white bg-${type} border-0 show`;
        toastElement.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fas ${this.getToastIcon(type)} me-2"></i>
                    ${this.sanitizeHTML(message)}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" aria-label="Close"></button>
            </div>
        `;

        container.appendChild(toastElement);

        // Auto remove
        setTimeout(() => {
            if (toastElement.parentNode) {
                toastElement.classList.add('fade-out');
                setTimeout(() => toastElement.remove(), 300);
            }
        }, duration);

        // Manual close
        toastElement.querySelector('.btn-close').addEventListener('click', () => {
            toastElement.classList.add('fade-out');
            setTimeout(() => toastElement.remove(), 300);
        });
    }

    getToastIcon(type) {
        const icons = {
            success: 'fa-check-circle',
            error: 'fa-exclamation-circle',
            warning: 'fa-exclamation-triangle',
            info: 'fa-info-circle'
        };
        return icons[type] || icons.info;
    }

    sanitizeHTML(str) {
        if (typeof str !== 'string') return str;
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.siteManager = new SiteManager();
});

// Add enhanced CSS
const style = document.createElement('style');
style.textContent = `
    .table-row-hover {
        cursor: pointer;
        transition: background-color 0.2s ease;
    }
    
    .table-row-hover:hover {
        background-color: rgba(0, 123, 255, 0.05) !important;
    }
    
    .loading-spinner {
        z-index: 1000;
    }
    
    .fade-out {
        opacity: 0;
        transform: translateX(100%);
        transition: all 0.3s ease-out;
    }
    
    .toast {
        transition: all 0.3s ease-in-out;
    }
    
    .keyboard-shortcuts .shortcut-item {
        margin-bottom: 8px;
        font-size: 14px;
    }
    
    .keyboard-shortcuts kbd {
        background: #f4f4f4;
        border: 1px solid #ccc;
        border-radius: 3px;
        padding: 2px 6px;
        font-family: monospace;
        font-size: 12px;
        box-shadow: 0 1px 0 rgba(0,0,0,0.2);
    }
    
    .nav-link.active {
        background-color: rgba(255, 255, 255, 0.1);
        border-radius: 0.375rem;
    }
    
    .btn:disabled {
        cursor: not-allowed;
        opacity: 0.6;
    }
    
    #toast-container .toast {
        margin-bottom: 0.5rem;
    }
    
    .modal-content {
        border-radius: 0.5rem;
        border: none;
        box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
    }
    
    .modal-header {
        border-bottom: 1px solid rgba(0, 0, 0, 0.1);
    }
    
    .modal-footer {
        border-top: 1px solid rgba(0, 0, 0, 0.1);
    }
`;
document.head.appendChild(style);