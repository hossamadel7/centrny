/* pins.js - Premium Pins Management System */

(function () {
    // DOM Elements
    const generateModal = document.getElementById('generateModal');
    const editModal = document.getElementById('editModal');
    const generateForm = document.getElementById('generatePinsForm');
    const editForm = document.getElementById('editPinForm');
    const tableBody = document.querySelector('#pinsTable tbody');
    const searchInput = document.getElementById('searchInput');
    const alertsArea = document.getElementById('alertsArea');

    // State
    let currentPage = 1;
    let itemsPerPage = 10;
    let allPins = [];
    let filteredPins = [];
    let isLoading = false;

    // Initialize
    document.addEventListener('DOMContentLoaded', () => {
        initializeEventListeners();
        loadPinsData();
    });

    function initializeEventListeners() {
        // Modal controls
        document.getElementById('openGenerateModal')?.addEventListener('click', () => openModal('generate'));
        document.getElementById('closeGenerateModal')?.addEventListener('click', () => closeModal('generate'));
        document.getElementById('cancelGenerateBtn')?.addEventListener('click', () => closeModal('generate'));
        document.getElementById('closeEditModal')?.addEventListener('click', () => closeModal('edit'));
        document.getElementById('cancelEditBtn')?.addEventListener('click', () => closeModal('edit'));

        // Forms
        generateForm?.addEventListener('submit', handleGenerateSubmit);
        editForm?.addEventListener('submit', handleEditSubmit);

        // Search
        searchInput?.addEventListener('input', debounce(handleSearch, 300));

        // Refresh
        document.getElementById('refreshPinsBtn')?.addEventListener('click', refreshPins);

        // Modal overlay clicks
        generateModal?.addEventListener('click', e => {
            if (e.target === generateModal) closeModal('generate');
        });
        editModal?.addEventListener('click', e => {
            if (e.target === editModal) closeModal('edit');
        });

        // Escape key
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') {
                closeModal('generate');
                closeModal('edit');
            }
        });
    }

    // Modal Management
    function openModal(type) {
        const modal = type === 'generate' ? generateModal : editModal;
        if (!modal) return;

        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');

        // Focus first input
        setTimeout(() => {
            const firstInput = modal.querySelector('input, select');
            firstInput?.focus();
        }, 100);
    }

    function closeModal(type) {
        const modal = type === 'generate' ? generateModal : editModal;
        if (!modal) return;

        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');

        // Reset forms
        if (type === 'generate') {
            generateForm?.reset();
            setLoading('generate', false);
        } else {
            editForm?.reset();
            setLoading('edit', false);
        }
    }

    // Loading States
    function setLoading(type, loading) {
        const spinner = document.getElementById(type === 'generate' ? 'generateSpinner' : 'editSpinner');
        const btn = spinner?.closest('button');
        const btnText = btn?.querySelector('.btn-text');

        if (!spinner || !btn) return;

        spinner.classList.toggle('d-none', !loading);
        btn.disabled = loading;
        if (btnText) btnText.style.opacity = loading ? '0' : '1';

        isLoading = loading;
    }

    // Alert System
    function showAlert(message, type = 'success') {
        if (!alertsArea) return;

        const iconClass = type === 'success' ? 'bi-check-circle' : 'bi-exclamation-triangle';
        alertsArea.innerHTML = `
            <div class="alert alert-${type}">
                <i class="${iconClass} me-2"></i>${escapeHtml(message)}
            </div>
        `;

        // Auto hide
        setTimeout(() => {
            alertsArea.innerHTML = '';
        }, 5000);

        // Scroll to alert
        alertsArea.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    // API Calls
    async function fetchJson(url, options = {}) {
        const response = await fetch(url, {
            ...options,
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                ...options.headers
            }
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        return response.json();
    }

    async function generatePins(formData) {
        const body = new URLSearchParams();
        formData.forEach((value, key) => body.append(key, value));

        return fetchJson('/Pin/Generate', {
            method: 'POST',
            body: body.toString()
        });
    }

    async function updatePin(formData) {
        const body = new URLSearchParams();
        formData.forEach((value, key) => body.append(key, value));

        return fetchJson('/Pin/Update', {
            method: 'POST',
            body: body.toString()
        });
    }

    async function deletePin(pinCode) {
        return fetchJson('/Pin/Delete', {
            method: 'POST',
            body: new URLSearchParams({ pinCode: pinCode.toString() }).toString()
        });
    }

    async function loadPins() {
        return fetchJson('/Pin/List');
    }

    // Event Handlers
    async function handleGenerateSubmit(e) {
        e.preventDefault();
        if (isLoading) return;

        setLoading('generate', true);

        try {
            const formData = new FormData(generateForm);
            const result = await generatePins(formData);

            if (result.success) {
                showAlert(result.message || 'Pins generated successfully!');
                await loadPinsData();
                updateStats(result.pins);
                closeModal('generate');
            } else {
                showAlert(result.error || 'Failed to generate pins', 'danger');
            }
        } catch (error) {
            console.error('Generate error:', error);
            showAlert('An unexpected error occurred', 'danger');
        } finally {
            setLoading('generate', false);
        }
    }

    async function handleEditSubmit(e) {
        e.preventDefault();
        if (isLoading) return;

        setLoading('edit', true);

        try {
            const formData = new FormData(editForm);
            const result = await updatePin(formData);

            if (result.success) {
                showAlert('Pin updated successfully!');
                await loadPinsData();
                closeModal('edit');
            } else {
                showAlert(result.error || 'Failed to update pin', 'danger');
            }
        } catch (error) {
            console.error('Edit error:', error);
            showAlert('An unexpected error occurred', 'danger');
        } finally {
            setLoading('edit', false);
        }
    }

    function handleSearch(e) {
        const query = e.target.value.toLowerCase().trim();

        if (!query) {
            filteredPins = [...allPins];
        } else {
            filteredPins = allPins.filter(pin =>
                pin.watermark.toLowerCase().includes(query) ||
                pin.pinCode.toString().includes(query)
            );
        }

        currentPage = 1;
        renderTable();
        renderPagination();
    }

    // Data Management
    async function loadPinsData() {
        try {
            const result = await loadPins();
            if (result.success) {
                allPins = result.pins || [];
                filteredPins = [...allPins];
                renderTable();
                renderPagination();
                updateStats();
                updateWalletCount(result.walletCodesCount);
            } else {
                showAlert(result.error || 'Failed to load pins', 'danger');
            }
        } catch (error) {
            console.error('Load error:', error);
            showAlert('Failed to load pins data', 'danger');
        }
    }

    async function refreshPins() {
        showAlert('Refreshing pins...', 'info');
        await loadPinsData();
        showAlert('Pins refreshed successfully!');
    }

    // Rendering
    function renderTable() {
        if (!tableBody) return;

        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = startIndex + itemsPerPage;
        const pageData = filteredPins.slice(startIndex, endIndex);

        if (pageData.length === 0) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center py-5">
                        <i class="bi bi-inbox text-muted" style="font-size: 3rem;"></i>
                        <p class="text-muted mt-3 mb-0">No pins found</p>
                    </td>
                </tr>
            `;
            return;
        }

        tableBody.innerHTML = pageData.map(pin => `
            <tr data-pin="${pin.pinCode}">
                <td><strong>${pin.pinCode}</strong></td>
                <td>
                    <span class="text-truncate" style="max-width: 150px; display: block;" title="${escapeHtml(pin.watermark)}">
                        ${escapeHtml(pin.watermark)}
                    </span>
                </td>
                <td>
                    <span class="type-badge ${pin.type ? 'type-exam' : 'type-session'}">
                        ${pin.type ? 'Exam' : 'Session'}
                    </span>
                </td>
                <td><strong>${pin.times}</strong></td>
                <td>${renderStatusBadge(pin.status)}</td>
                <td>
                    ${pin.isActive === 1
                ? '<i class="bi bi-check-circle text-success"></i>'
                : '<i class="bi bi-x-circle text-danger"></i>'
            }
                </td>
                <td>${formatDate(pin.insertTime)}</td>
                <td>
                    <div class="pins-actions-cell">
                        <button class="btn-icon btn-edit" onclick="editPin(${pin.pinCode})" title="Edit Pin">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button class="btn-icon btn-delete" onclick="confirmDeletePin(${pin.pinCode})" title="Delete Pin">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `).join('');

        updatePaginationInfo();
    }

    function renderStatusBadge(status) {
        const statusConfig = {
            0: { class: 'status-used', text: 'Used' },
            1: { class: 'status-sold', text: 'Sold' },
            2: { class: 'status-active', text: 'Active' }
        };

        const config = statusConfig[status] || statusConfig[0];
        return `
            <span class="status-badge ${config.class}">
                <span class="status-dot"></span>
                ${config.text}
            </span>
        `;
    }

    function renderPagination() {
        const container = document.getElementById('paginationContainer');
        if (!container) return;

        const totalPages = Math.ceil(filteredPins.length / itemsPerPage);
        if (totalPages <= 1) {
            container.innerHTML = '';
            return;
        }

        let pages = [];

        // Previous button
        pages.push(`
            <button class="pins-page-btn" ${currentPage === 1 ? 'disabled' : ''} onclick="goToPage(${currentPage - 1})">
                <i class="bi bi-chevron-left"></i>
            </button>
        `);

        // Page numbers
        const showPages = getPageNumbers(currentPage, totalPages);
        showPages.forEach(page => {
            if (page === '...') {
                pages.push(`<span class="pins-page-btn">...</span>`);
            } else {
                pages.push(`
                    <button class="pins-page-btn ${page === currentPage ? 'active' : ''}" onclick="goToPage(${page})">
                        ${page}
                    </button>
                `);
            }
        });

        // Next button
        pages.push(`
            <button class="pins-page-btn" ${currentPage === totalPages ? 'disabled' : ''} onclick="goToPage(${currentPage + 1})">
                <i class="bi bi-chevron-right"></i>
            </button>
        `);

        container.innerHTML = pages.join('');
    }

    function getPageNumbers(current, total) {
        if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

        if (current <= 4) return [1, 2, 3, 4, 5, '...', total];
        if (current >= total - 3) return [1, '...', total - 4, total - 3, total - 2, total - 1, total];

        return [1, '...', current - 1, current, current + 1, '...', total];
    }

    function updatePaginationInfo() {
        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = Math.min(startIndex + itemsPerPage, filteredPins.length);

        document.getElementById('showingStart').textContent = filteredPins.length ? startIndex + 1 : 0;
        document.getElementById('showingEnd').textContent = endIndex;
        document.getElementById('totalPins').textContent = filteredPins.length;
        document.getElementById('currentPageDisplay').textContent = currentPage;
    }

    function updateStats(pins = allPins) {
        document.getElementById('activePinsCount').textContent = pins.filter(p => p.status === 2).length;
        document.getElementById('soldPinsCount').textContent = pins.filter(p => p.status === 1).length;
        document.getElementById('usedPinsCount').textContent = pins.filter(p => p.status === 0).length;
        document.getElementById('pinTotal').textContent = pins.length;
    }

    function updateWalletCount(count) {
        const el = document.getElementById('walletCodesCount');
        if (el) el.textContent = count;
    }

    // Global Functions (called from HTML)
    window.editPin = function (pinCode) {
        const pin = allPins.find(p => p.pinCode === pinCode);
        if (!pin) return;

        document.getElementById('editPinCode').value = pin.pinCode;
        document.getElementById('editWatermark').value = pin.watermark;
        document.getElementById('editType').value = pin.type.toString();
        document.getElementById('editTimes').value = pin.times;
        document.getElementById('editStatus').value = pin.status;
        document.getElementById('editIsActive').value = pin.isActive;

        openModal('edit');
    };

    window.confirmDeletePin = function (pinCode) {
        if (confirm('Are you sure you want to delete this pin? This action cannot be undone.')) {
            deletePinById(pinCode);
        }
    };

    window.goToPage = function (page) {
        const totalPages = Math.ceil(filteredPins.length / itemsPerPage);
        if (page < 1 || page > totalPages) return;

        currentPage = page;
        renderTable();
        renderPagination();
    };

    async function deletePinById(pinCode) {
        try {
            const result = await deletePin(pinCode);
            if (result.success) {
                showAlert('Pin deleted successfully!');
                await loadPinsData();
            } else {
                showAlert(result.error || 'Failed to delete pin', 'danger');
            }
        } catch (error) {
            console.error('Delete error:', error);
            showAlert('An unexpected error occurred', 'danger');
        }
    }

    // Utility Functions
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function formatDate(dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString('en-US', {
            month: 'short',
            day: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Export for external use
    window.pinsManager = {
        refresh: refreshPins,
        loadData: loadPinsData
    };
})();