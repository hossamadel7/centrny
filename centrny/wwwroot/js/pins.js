/* pins-manager-advanced.js
   Client-side pagination over ALL pins for the current root.
   Stat cards (Used / Active / Sold / Total) always reflect ALL pins (not just current page).
*/

(function () {
    // DOM Elements
    const generateModal = document.getElementById('generateModal');
    const editModal = document.getElementById('editModal');
    const generateForm = document.getElementById('generatePinsForm');
    const editForm = document.getElementById('editPinForm');
    const tableBody = document.querySelector('#pinsTable tbody');
    const searchInput = document.getElementById('searchInput');
    const alertsArea = document.getElementById('alertsArea');
    const filterType = document.getElementById('filterType');
    const filterStatus = document.getElementById('filterStatus');

    // State
    let currentPage = 1;
    let itemsPerPage = 10;          // Adjustable page size
    let allPins = [];               // Complete dataset
    let filteredPins = [];          // After search/filter
    let isLoading = false;

    document.addEventListener('DOMContentLoaded', () => {
        initializeEventListeners();
        loadPinsData();             // Load ALL pins once
    });

    function initializeEventListeners() {
        document.getElementById('openGenerateModal')?.addEventListener('click', () => openModal('generate'));
        document.getElementById('closeGenerateModal')?.addEventListener('click', () => closeModal('generate'));
        document.getElementById('cancelGenerateBtn')?.addEventListener('click', () => closeModal('generate'));
        document.getElementById('closeEditModal')?.addEventListener('click', () => closeModal('edit'));
        document.getElementById('cancelEditBtn')?.addEventListener('click', () => closeModal('edit'));

        generateForm?.addEventListener('submit', handleGenerateSubmit);
        editForm?.addEventListener('submit', handleEditSubmit);

        searchInput?.addEventListener('input', debounce(() => applyFilters(), 300));
        filterType?.addEventListener('change', applyFilters);
        filterStatus?.addEventListener('change', applyFilters);

        document.getElementById('refreshPinsBtn')?.addEventListener('click', refreshPins);

        generateModal?.addEventListener('click', e => { if (e.target === generateModal) closeModal('generate'); });
        editModal?.addEventListener('click', e => { if (e.target === editModal) closeModal('edit'); });

        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') {
                closeModal('generate');
                closeModal('edit');
            }
        });
    }

    // -------- Modal Management --------
    function openModal(type) {
        const modal = type === 'generate' ? generateModal : editModal;
        if (!modal) return;
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        setTimeout(() => {
            modal.querySelector('input, select')?.focus();
        }, 75);
    }

    function closeModal(type) {
        const modal = type === 'generate' ? generateModal : editModal;
        if (!modal) return;
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
        if (type === 'generate') {
            generateForm?.reset();
            setLoading('generate', false);
        } else {
            editForm?.reset();
            setLoading('edit', false);
        }
    }

    function setLoading(context, loading) {
        const spinnerId = context === 'generate' ? 'generateSpinner' : 'editSpinner';
        const spinner = document.getElementById(spinnerId);
        if (!spinner) return;
        const btn = spinner.closest('button');
        const btnText = btn?.querySelector('.btn-text');
        spinner.classList.toggle('d-none', !loading);
        if (btn) btn.disabled = loading;
        if (btnText) btnText.style.opacity = loading ? '0' : '1';
        isLoading = loading;
    }

    function showAlert(message, type = 'success') {
        if (!alertsArea) return;
        const iconClass = {
            success: 'bi-check-circle',
            danger: 'bi-exclamation-triangle',
            info: 'bi-info-circle'
        }[type] || 'bi-info-circle';

        alertsArea.innerHTML = `
            <div class="alert alert-${type}">
                <i class="${iconClass} me-2"></i>${escapeHtml(message)}
            </div>
        `;
        setTimeout(() => { if (alertsArea.innerHTML.includes(message)) alertsArea.innerHTML = ''; }, 5000);
    }

    // -------- API Helpers --------
    async function fetchJson(url, options = {}) {
        const response = await fetch(url, {
            ...options,
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                ...(options.headers || {})
            }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        return response.json();
    }

    async function generatePins(formData) {
        const body = new URLSearchParams();
        formData.forEach((v, k) => body.append(k, v));
        return fetchJson('/Pin/Generate', { method: 'POST', body: body.toString() });
    }

    async function updatePin(formData) {
        const body = new URLSearchParams();
        formData.forEach((v, k) => body.append(k, v));
        return fetchJson('/Pin/Update', { method: 'POST', body: body.toString() });
    }

    async function deletePin(pinCode) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        return fetchJson('/Pin/Delete', {
            method: 'POST',
            body: new URLSearchParams({ pinCode: pinCode.toString(), __RequestVerificationToken: token }).toString()
        });
    }

    async function loadAllPins() {
        return fetchJson('/Pin/All'); // returns all pins for current root
    }

    // -------- Event Handlers --------
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
                closeModal('generate');
            } else showAlert(result.error || 'Failed to generate pins', 'danger');
        } catch (err) {
            console.error(err);
            showAlert('Unexpected error during generation', 'danger');
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
            } else showAlert(result.error || 'Failed to update pin', 'danger');
        } catch (err) {
            console.error(err);
            showAlert('Unexpected error during update', 'danger');
        } finally {
            setLoading('edit', false);
        }
    }

    async function refreshPins() {
        showAlert('Refreshing pins...', 'info');
        await loadPinsData();
        showAlert('Pins refreshed successfully!');
    }

    // -------- Data Management --------
    async function loadPinsData() {
        try {
            const result = await loadAllPins();
            if (result.success) {
                allPins = result.data || [];
                filteredPins = [...allPins];
                currentPage = 1;
                renderTable();
                renderPagination();
                updateStats();
                updateWalletCount(result.walletCodesCount ?? result.WalletCodesCount);
            } else {
                showAlert(result.error || 'Failed to load pins', 'danger');
            }
        } catch (err) {
            console.error('Load pins error:', err);
            showAlert('Failed to load pins data', 'danger');
        }
    }

    function applyFilters() {
        let pins = [...allPins];

        // Type filter
        const typeVal = filterType?.value;
        if (typeVal === 'exam') pins = pins.filter(p => p.type === true);
        else if (typeVal === 'session') pins = pins.filter(p => p.type === false);

        // Status filter
        const statusVal = filterStatus?.value;
        if (statusVal === 'used') pins = pins.filter(p => p.status === 0);
        else if (statusVal === 'active') pins = pins.filter(p => p.status === 1);
        else if (statusVal === 'sold') pins = pins.filter(p => p.status === 2);

        // Search
        const q = (searchInput?.value || '').toLowerCase().trim();
        if (q) {
            pins = pins.filter(p =>
                (p.watermark || '').toLowerCase().includes(q) ||
                p.pinCode.toString().includes(q)
            );
        }

        filteredPins = pins;
        currentPage = 1;
        renderTable();
        renderPagination();
        // NOTE: Do NOT call updateStats() here because stats must reflect ALL pins, not filtered subset
    }

    // -------- Rendering --------
    function renderTable() {
        if (!tableBody) return;
        const totalPages = Math.max(1, Math.ceil(filteredPins.length / itemsPerPage));
        if (currentPage > totalPages) currentPage = totalPages;
        if (currentPage < 1) currentPage = 1;

        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = startIndex + itemsPerPage;
        const pageData = filteredPins.slice(startIndex, endIndex);

        if (!pageData.length) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center py-5">
                        <i class="bi bi-inbox text-muted" style="font-size:3rem;"></i>
                        <p class="text-muted mt-3 mb-0">No pins found</p>
                    </td>
                </tr>`;
            updatePaginationInfo();
            return;
        }

        tableBody.innerHTML = pageData.map(pin => `
            <tr data-pin="${pin.pinCode}">
                <td><strong>${pin.pinCode}</strong></td>
                <td>
                    <span class="text-truncate" style="max-width:150px;display:block;" title="${escapeHtml(pin.watermark)}">
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
                <td>${pin.isActive === 1
                ? '<i class="bi bi-check-circle text-success"></i>'
                : '<i class="bi bi-x-circle text-danger"></i>'}
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
        const cfg = {
            0: { cls: 'status-used', text: 'Used' },
            1: { cls: 'status-active', text: 'Active' },
            2: { cls: 'status-sold', text: 'Sold' }
        }[status] || { cls: 'status-used', text: 'Used' };
        return `
            <span class="status-badge ${cfg.cls}">
                <span class="status-dot"></span>${cfg.text}
            </span>
        `;
    }

    function renderPagination() {
        const container = document.getElementById('paginationContainer');
        if (!container) return;
        const totalPages = Math.max(1, Math.ceil(filteredPins.length / itemsPerPage));
        if (totalPages <= 1) {
            container.innerHTML = '';
            return;
        }

        if (currentPage > totalPages) currentPage = totalPages;
        if (currentPage < 1) currentPage = 1;

        const parts = [];

        // Previous
        parts.push(`
            <button class="pins-page-btn" ${currentPage === 1 ? 'disabled' : ''} onclick="goToPage(${currentPage - 1})">
                <i class="bi bi-chevron-left"></i>
            </button>`);

        // Page numbers logic
        const pagesToShow = getPageNumbers(currentPage, totalPages);
        pagesToShow.forEach(p => {
            if (p === '...') {
                parts.push(`<span class="pins-page-btn disabled">...</span>`);
            } else {
                parts.push(`
                    <button class="pins-page-btn ${p === currentPage ? 'active' : ''}" onclick="goToPage(${p})">
                        ${p}
                    </button>`);
            }
        });

        // Next
        parts.push(`
            <button class="pins-page-btn" ${currentPage === totalPages ? 'disabled' : ''} onclick="goToPage(${currentPage + 1})">
                <i class="bi bi-chevron-right"></i>
            </button>`);

        container.innerHTML = parts.join('');
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
        document.getElementById('showingStart').textContent = filteredPins.length ? (startIndex + 1).toString() : '0';
        document.getElementById('showingEnd').textContent = endIndex.toString();
        document.getElementById('totalPins').textContent = filteredPins.length.toString();
        document.getElementById('currentPageDisplay').textContent = currentPage.toString();
    }

    function updateStats() {
        // Always reflect ALL pins for root (not filteredPins)
        document.getElementById('usedPinsCount').textContent = allPins.filter(p => p.status === 0).length;
        document.getElementById('activePinsCount').textContent = allPins.filter(p => p.status === 1).length;
        document.getElementById('soldPinsCount').textContent = allPins.filter(p => p.status === 2).length;
        document.getElementById('pinTotal').textContent = allPins.length;
    }

    function updateWalletCount(count) {
        const el = document.getElementById('walletCodesCount');
        if (el && typeof count !== 'undefined') el.textContent = count;
    }

    // -------- Global functions (HTML onclick) --------
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
        if (confirm('Are you sure you want to delete this pin?')) {
            deletePinById(pinCode);
        }
    };

    window.goToPage = function (page) {
        const totalPages = Math.max(1, Math.ceil(filteredPins.length / itemsPerPage));
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
        } catch (err) {
            console.error('Delete error:', err);
            showAlert('Unexpected error deleting pin', 'danger');
        }
    }

    // -------- Utilities --------
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text ?? '';
        return div.innerHTML;
    }

    function formatDate(dateString) {
        if (!dateString) return '';
        const date = new Date(dateString);
        return date.toLocaleDateString('en-US', {
            month: 'short',
            day: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    function debounce(fn, wait) {
        let t;
        return (...args) => {
            clearTimeout(t);
            t = setTimeout(() => fn(...args), wait);
        };
    }

    // Public debug helpers
    window.__debugPins = () => ({ all: allPins.length, sample: allPins.slice(0, 3) });
    window.__debugPagination = () => ({
        filteredPins: filteredPins.length,
        currentPage,
        itemsPerPage,
        totalPages: Math.ceil(filteredPins.length / itemsPerPage)
    });

    window.pinsManager = {
        refresh: refreshPins,
        loadAll: loadPinsData
    };
})();