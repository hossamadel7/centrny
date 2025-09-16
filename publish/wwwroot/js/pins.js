/* Pins Manager - robust script
   - Client-side pagination over ALL pins for the current root.
   - Delegated pagination (no inline onclick required), but window.goToPage is also exported for compatibility.
   - Filters (type/status) + search.
   - Status toggle between Active <-> Sold.
   - Generate/Edit modals with basic scroll lock.
   - Reads localized strings from #js-localization dataset.
*/
(function () {
    'use strict';

    // Global shim so inline onclick="goToPage(n)" won't break if it still exists in markup
    window.goToPage = function (page) {
        if (typeof window.__pins_goToPageInternal === 'function') {
            window.__pins_goToPageInternal(page);
        }
    };

    // Localization helper
    const L = (function () {
        const el = document.getElementById('js-localization');
        const d = el ? el.dataset : {};
        const t = (k, def = '') => (d && (k in d) ? d[k] : def);
        const fmt = (template, ...args) =>
            (template || '').replace(/\{(\d+)\}/g, (_, i) => (args[i] ?? ''));
        return { d, t, fmt };
    })();

    // DOM elements
    const pinPage = document.getElementById('pin-page');
    if (!pinPage) {
        // Not on Pins page; safely no-op
        return;
    }

    const generateModal = document.getElementById('generateModal');
    const editModal = document.getElementById('editModal');

    const tableBody = document.querySelector('#pinsTable tbody');
    const searchInput = document.getElementById('searchInput');
    const alertsArea = document.getElementById('alertsArea');
    const filterType = document.getElementById('filterType');
    const filterStatus = document.getElementById('filterStatus');
    const paginationContainer = document.getElementById('paginationContainer');

    // Forms
    const generateForm = document.getElementById('generatePinsForm');
    const editForm = document.getElementById('editPinForm');

    // Wallet stat elements (optional)
    const elCountTotal = document.getElementById('walletCountTotal');
    const elCount1 = document.getElementById('walletCount1');
    const elCount4 = document.getElementById('walletCount4');
    const elCount16 = document.getElementById('walletCount16');
    const elLegacyWalletTotal = document.getElementById('walletCodesCount');

    // Generate modal controls
    const generateTimesSelect = generateForm ? generateForm.querySelector('select[name="Times"]') : null;
    const generateNumberInput = generateForm ? generateForm.querySelector('input[name="Number"]') : null;
    const generateHint = document.getElementById('availableForTimesHint');
    const submitGenerateBtn = document.getElementById('submitGenerateBtn');

    // State
    let currentPage = 1;
    let itemsPerPage = 10;
    let allPins = [];
    let filteredPins = [];
    let isLoading = false;
    let walletCounts = { times1: 0, times4: 0, times16: 0, total: 0 };

    // Init
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        wireEvents();
        loadPinsData();
    }

    function wireEvents() {
        // Open/close modals
        document.getElementById('openGenerateModal')?.addEventListener('click', () => openModal('generate'));
        document.getElementById('closeGenerateModal')?.addEventListener('click', () => closeModal('generate'));
        document.getElementById('cancelGenerateBtn')?.addEventListener('click', () => closeModal('generate'));
        document.getElementById('closeEditModal')?.addEventListener('click', () => closeModal('edit'));
        document.getElementById('cancelEditBtn')?.addEventListener('click', () => closeModal('edit'));

        // Forms
        generateForm?.addEventListener('submit', handleGenerateSubmit);
        editForm?.addEventListener('submit', handleEditSubmit);

        // Filters/Search
        searchInput?.addEventListener('input', debounce(applyFilters, 250));
        filterType?.addEventListener('change', applyFilters);
        filterStatus?.addEventListener('change', applyFilters);

        // Refresh
        document.getElementById('refreshPinsBtn')?.addEventListener('click', refreshPins);

        // Close modals by clicking overlay
        generateModal?.addEventListener('click', e => { if (e.target === generateModal) closeModal('generate'); });
        editModal?.addEventListener('click', e => { if (e.target === editModal) closeModal('edit'); });

        // Esc to close
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') {
                closeModal('generate');
                closeModal('edit');
            }
        });

        // Delegated pagination (CSP-safe)
        paginationContainer?.addEventListener('click', (e) => {
            const btn = e.target.closest('button[data-page]');
            if (!btn || btn.disabled) return;
            const page = parseInt(btn.getAttribute('data-page') || '', 10);
            if (!Number.isNaN(page)) {
                window.__pins_goToPageInternal(page);
            }
        });

        // Toggle status by clicking badge
        tableBody?.addEventListener('click', async (e) => {
            const badge = e.target.closest('.status-badge');
            if (!badge) return;

            const tr = badge.closest('tr[data-pin]');
            if (!tr) return;

            const pinCode = parseInt(tr.getAttribute('data-pin'), 10);
            const pin = allPins.find(p => p.pinCode === pinCode);
            if (!pin) return;

            // 0 Used (don't toggle), 1 Active, 2 Sold
            if (pin.status === 0) {
                showAlert(L.t('msgUsedPinsCannotToggle', 'Used pins cannot be toggled.'), 'info');
                return;
            }

            const newStatus = (pin.status === 1) ? 2 : 1;
            try {
                await togglePinStatus(pin, newStatus);
                const statusText = newStatus === 1 ? L.t('statusActive', 'Active') : L.t('statusSold', 'Sold');
                showAlert(L.fmt('Pin {0} set to {1}.', pinCode, statusText), 'success');
                await loadPinsData();
            } catch (err) {
                console.error(err);
                showAlert(L.t('msgToggleFailed', 'Failed to toggle pin status.'), 'danger');
            }
        });

        // Generate constraints
        generateTimesSelect?.addEventListener('change', syncGenerateLimits);
        generateNumberInput?.addEventListener('input', syncGenerateLimits);
    }

    // Modal management
    function openModal(type) {
        const modal = type === 'generate' ? generateModal : editModal;
        if (!modal) return;
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        modal.style.display = 'flex';
        document.body.classList.add('modal-open');
        setTimeout(() => modal.querySelector('input, select')?.focus(), 50);
        if (type === 'generate') syncGenerateLimits();
    }

    function closeModal(type) {
        const modal = type === 'generate' ? generateModal : editModal;
        if (!modal) return;
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
        modal.style.display = 'none';
        document.body.classList.remove('modal-open');
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

    // Alerts
    function showAlert(message, type = 'success') {
        if (window.Swal) {
            const icon = type === 'danger' ? 'error' : (type === 'info' ? 'info' : 'success');
            Swal.fire({ toast: true, position: 'top-end', showConfirmButton: false, timer: 2500, timerProgressBar: true, icon, title: message });
        } else if (alertsArea) {
            const iconClass = { success: 'bi-check-circle', danger: 'bi-exclamation-triangle', info: 'bi-info-circle' }[type] || 'bi-info-circle';
            alertsArea.innerHTML = `<div class="alert alert-${type}"><i class="${iconClass} me-2"></i>${escapeHtml(message)}</div>`;
            setTimeout(() => { alertsArea.innerHTML = ''; }, 3000);
        }
    }

    // API helpers
    const getToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    async function fetchJson(url, options = {}) {
        const response = await fetch(url, {
            ...options,
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', ...(options.headers || {}) }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        return response.json();
    }

    async function generatePins(formData) {
        if (!formData.has('__RequestVerificationToken')) {
            const token = getToken();
            if (token) formData.append('__RequestVerificationToken', token);
        }
        const body = new URLSearchParams();
        formData.forEach((v, k) => body.append(k, v));
        return fetchJson('/Pin/Generate', { method: 'POST', body: body.toString() });
    }

    async function updatePin(formData) {
        if (!formData.has('__RequestVerificationToken')) {
            const token = getToken();
            if (token) formData.append('__RequestVerificationToken', token);
        }
        const body = new URLSearchParams();
        formData.forEach((v, k) => body.append(k, v));
        return fetchJson('/Pin/Update', { method: 'POST', body: body.toString() });
    }

    async function deletePin(pinCode) {
        const token = getToken();
        return fetchJson('/Pin/Delete', {
            method: 'POST',
            body: new URLSearchParams({ pinCode: String(pinCode), __RequestVerificationToken: token }).toString()
        });
    }

    async function loadAllPins() {
        return fetchJson('/Pin/All');
    }

    // Handlers
    async function handleGenerateSubmit(e) {
        e.preventDefault();
        if (isLoading) return;

        const times = parseInt(generateTimesSelect?.value || '1', 10);
        const number = parseInt(generateNumberInput?.value || '0', 10) || 0;
        const available = getAvailableForTimes(times);

        if (number > available) {
            showAlert(L.fmt(L.t('msgExceedsAvailableTemplate', 'Requested quantity ({0}) exceeds available balance ({1}) for {2} sessions.'), number, available, times), 'danger');
            return;
        }

        setLoading('generate', true);
        try {
            const formData = new FormData(generateForm);
            const result = await generatePins(formData);
            if (result.success) {
                showAlert(result.message || L.t('msgPinsGeneratedSuccess', 'Pins generated successfully!'));
                await loadPinsData();
                closeModal('generate');
            } else {
                showAlert(result.error || L.t('msgFailedGeneratePins', 'Failed to generate pins'), 'danger');
            }
        } catch (err) {
            console.error(err);
            showAlert(L.t('msgUnexpectedGenerate', 'Unexpected error during generation'), 'danger');
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
                showAlert(L.t('msgPinUpdatedSuccess', 'Pin updated successfully!'));
                await loadPinsData();
                closeModal('edit');
            } else {
                showAlert(result.error || L.t('msgFailedUpdatePin', 'Failed to update pin'), 'danger');
            }
        } catch (err) {
            console.error(err);
            showAlert(L.t('msgUnexpectedUpdatePin', 'Unexpected error during update'), 'danger');
        } finally {
            setLoading('edit', false);
        }
    }

    async function refreshPins() {
        showAlert(L.t('msgRefreshingPins', 'Refreshing pins...'), 'info');
        await loadPinsData();
        showAlert(L.t('msgPinsRefreshed', 'Pins refreshed successfully!'));
    }

    // Toggle helper
    async function togglePinStatus(pin, newStatus) {
        const formData = new FormData();
        formData.append('PinCode', String(pin.pinCode));
        formData.append('Watermark', pin.watermark || '');
        formData.append('Type', pin.type ? 'true' : 'false');
        formData.append('Times', String(pin.times));
        formData.append('Status', String(newStatus));
        formData.append('IsActive', String(pin.isActive));
        const result = await updatePin(formData);
        if (!result.success) throw new Error(result.error || 'Toggle failed');
        return result;
    }

    // Data management
    async function loadPinsData() {
        try {
            const result = await loadAllPins();
            if (result.success) {
                const raw = Array.isArray(result.data) ? result.data : [];
                allPins = raw.map(p => ({
                    pinCode: Number(p.pinCode ?? p.PinCode ?? 0),
                    watermark: String(p.watermark ?? p.Watermark ?? ''),
                    // Normalize type to boolean (true=exam, false=session)
                    type: typeof p.type === 'boolean'
                        ? p.type
                        : (p.type === 1 || p.type === '1' || p.Type === 1 || p.Type === '1' || p.Type === true),
                    times: Number(p.times ?? p.Times ?? 0) || 0,
                    status: Number(p.status ?? p.Status ?? 0) || 0,
                    isActive: Number(p.isActive ?? p.IsActive ?? 0) || 0,
                    insertTime: p.insertTime ?? p.InsertTime ?? null
                }));

                filteredPins = [...allPins];
                currentPage = 1;
                renderTable();
                renderPagination();
                updateStats();

                if (result.walletCounts) {
                    walletCounts = {
                        times1: Number(result.walletCounts.times1 ?? 0),
                        times4: Number(result.walletCounts.times4 ?? 0),
                        times16: Number(result.walletCounts.times16 ?? 0),
                        total: Number(result.walletCounts.total ?? 0)
                    };
                } else {
                    const total = Number(result.walletCodesCount ?? 0);
                    walletCounts = { times1: 0, times4: 0, times16: 0, total };
                }
                updateWalletCountsUI();
                syncGenerateLimits();
            } else {
                showAlert(result.error || L.t('msgLoadPinsFailed', 'Failed to load pins'), 'danger');
            }
        } catch (err) {
            console.error('Load pins error:', err);
            showAlert(L.t('msgFailedLoadPinsData', 'Failed to load pins data'), 'danger');
        }
    }

    function applyFilters() {
        let pins = [...allPins];

        // Type filter
        const typeVal = filterType?.value || '';
        if (typeVal === 'exam') pins = pins.filter(p => p.type === true);
        else if (typeVal === 'session') pins = pins.filter(p => p.type === false);

        // Status filter
        const statusVal = filterStatus?.value || '';
        if (statusVal === 'used') pins = pins.filter(p => p.status === 0);
        else if (statusVal === 'active') pins = pins.filter(p => p.status === 1);
        else if (statusVal === 'sold') pins = pins.filter(p => p.status === 2);

        // Search
        const q = (searchInput?.value || '').toLowerCase().trim();
        if (q) {
            pins = pins.filter(p =>
                (p.watermark || '').toLowerCase().includes(q) ||
                String(p.pinCode).includes(q)
            );
        }

        filteredPins = pins;
        currentPage = 1;
        renderTable();
        renderPagination();
    }

    // Rendering
    function renderTable() {
        if (!tableBody) return;

        const totalPages = Math.max(1, Math.ceil(filteredPins.length / itemsPerPage));
        if (currentPage > totalPages) currentPage = totalPages;
        if (currentPage < 1) currentPage = 1;

        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = Math.min(startIndex + itemsPerPage, filteredPins.length);
        const pageData = filteredPins.slice(startIndex, endIndex);

        if (!pageData.length) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center py-5">
                        <i class="bi bi-inbox text-muted" style="font-size:3rem;"></i>
                        <p class="text-muted mt-3 mb-0">${escapeHtml(L.t('noPinsFound', 'No pins found'))}</p>
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
                        ${pin.type ? escapeHtml(L.t('typeExam', 'Exam')) : escapeHtml(L.t('typeSession', 'Session'))}
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
        // 0 Used (not clickable), 1 Active, 2 Sold
        const cfg = {
            0: { cls: 'status-used', text: L.t('statusUsed', 'Used'), clickable: false },
            1: { cls: 'status-active', text: L.t('statusActive', 'Active'), clickable: true },
            2: { cls: 'status-sold', text: L.t('statusSold', 'Sold'), clickable: true }
        }[status] || { cls: 'status-used', text: L.t('statusUsed', 'Used'), clickable: false };

        const clickableClass = cfg.clickable ? ' clickable' : '';
        const title = cfg.clickable ? L.t('columnStatusToggleHint', 'Click to toggle between Active and Sold') : '';
        return `
            <span class="status-badge ${cfg.cls}${clickableClass}" title="${escapeHtml(title)}" style="${cfg.clickable ? 'cursor:pointer;' : ''}">
                <span class="status-dot"></span>${escapeHtml(cfg.text)}
            </span>
        `;
    }

    function renderPagination() {
        if (!paginationContainer) return;

        const totalPages = Math.max(1, Math.ceil(filteredPins.length / itemsPerPage));
        if (currentPage > totalPages) currentPage = totalPages;
        if (currentPage < 1) currentPage = 1;

        const parts = [];

        // Prev
        parts.push(`
            <button type="button" class="pins-page-btn" ${currentPage === 1 ? 'disabled' : ''} data-page="${Math.max(1, currentPage - 1)}">
                <i class="bi bi-chevron-left"></i>
            </button>`);

        // Page numbers
        const pagesToShow = getPageNumbers(currentPage, totalPages);
        pagesToShow.forEach(p => {
            if (p === '...') {
                parts.push(`<span class="pins-page-btn disabled">...</span>`);
            } else {
                parts.push(`
                    <button type="button" class="pins-page-btn ${p === currentPage ? 'active' : ''}" data-page="${p}">
                        ${p}
                    </button>`);
            }
        });

        // Next
        parts.push(`
            <button type="button" class="pins-page-btn" ${currentPage === totalPages ? 'disabled' : ''} data-page="${Math.min(totalPages, currentPage + 1)}">
                <i class="bi bi-chevron-right"></i>
            </button>`);

        paginationContainer.innerHTML = parts.join('');
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
        const showingStart = document.getElementById('showingStart');
        const showingEnd = document.getElementById('showingEnd');
        const totalPins = document.getElementById('totalPins');
        const currentPageDisplay = document.getElementById('currentPageDisplay');

        if (showingStart) showingStart.textContent = filteredPins.length ? String(startIndex + 1) : '0';
        if (showingEnd) showingEnd.textContent = String(endIndex);
        if (totalPins) totalPins.textContent = String(filteredPins.length);
        if (currentPageDisplay) currentPageDisplay.textContent = String(currentPage);
    }

    function updateStats() {
        const used = allPins.filter(p => p.status === 0).length;
        const active = allPins.filter(p => p.status === 1).length;
        const sold = allPins.filter(p => p.status === 2).length;

        const usedEl = document.getElementById('usedPinsCount');
        const activeEl = document.getElementById('activePinsCount');
        const soldEl = document.getElementById('soldPinsCount');
        const totalEl = document.getElementById('pinTotal');

        if (usedEl) usedEl.textContent = String(used);
        if (activeEl) activeEl.textContent = String(active);
        if (soldEl) soldEl.textContent = String(sold);
        if (totalEl) totalEl.textContent = String(allPins.length);
    }

    function updateWalletCountsUI() {
        if (elCountTotal) elCountTotal.textContent = String(walletCounts.total);
        if (elCount1) elCount1.textContent = String(walletCounts.times1);
        if (elCount4) elCount4.textContent = String(walletCounts.times4);
        if (elCount16) elCount16.textContent = String(walletCounts.times16);
        if (elLegacyWalletTotal) elLegacyWalletTotal.textContent = String(walletCounts.total);
    }

    // Generate constraints
    function getAvailableForTimes(times) {
        if (times === 1) return walletCounts.times1;
        if (times === 4) return walletCounts.times4;
        if (times === 16) return walletCounts.times16;
        return 0;
    }

    function syncGenerateLimits() {
        if (!generateTimesSelect || !generateNumberInput) return;
        const times = parseInt(generateTimesSelect.value || '1', 10);
        const available = getAvailableForTimes(times);
        generateNumberInput.max = String(Math.max(available, 0));
        if (generateHint) {
            const template = L.t('generateAvailableHintTemplate', 'Available for {0} sessions: {1}');
            generateHint.textContent = L.fmt(template, times, available);
        }
        if (submitGenerateBtn) submitGenerateBtn.disabled = available <= 0;
        const currentVal = parseInt(generateNumberInput.value || '0', 10);
        if (currentVal > available) generateNumberInput.value = String(available);
        if (available <= 0) generateNumberInput.value = '0';
    }

    // Utilities
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text ?? '';
        return div.innerHTML;
    }

    function formatDate(dateString) {
        if (!dateString) return '';
        const date = new Date(dateString);
        return date.toLocaleDateString('en-US', {
            month: 'short', day: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
        });
    }

    function debounce(fn, wait) {
        let t;
        return (...args) => {
            clearTimeout(t);
            t = setTimeout(() => fn(...args), wait);
        };
    }

    // Expose for row actions
    window.editPin = function (pinCode) {
        const pin = allPins.find(p => p.pinCode === pinCode);
        if (!pin) return;
        const codeEl = document.getElementById('editPinCode');
        const watermarkEl = document.getElementById('editWatermark');
        const typeEl = document.getElementById('editType');
        const timesEl = document.getElementById('editTimes');
        const statusEl = document.getElementById('editStatus');
        const activeEl = document.getElementById('editIsActive');

        if (codeEl) codeEl.value = String(pin.pinCode);
        if (watermarkEl) watermarkEl.value = pin.watermark || '';
        if (typeEl) typeEl.value = pin.type ? 'true' : 'false';
        if (timesEl) timesEl.value = String(pin.times);
        if (statusEl) statusEl.value = String(pin.status);
        if (activeEl) activeEl.value = String(pin.isActive);
        openModal('edit');
    };

    window.confirmDeletePin = async function (pinCode) {
        const proceed = window.Swal
            ? await Swal.fire({
                title: 'Are you sure?',
                text: `Delete pin ${pinCode}?`,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Yes, delete',
                cancelButtonText: 'Cancel'
            }).then(r => r.isConfirmed)
            : confirm('Are you sure you want to delete this pin?');
        if (!proceed) return;
        try {
            const result = await deletePin(pinCode);
            if (result.success) {
                showAlert(L.t('msgPinDeletedSuccess', 'Pin deleted successfully!'));
                await loadPinsData();
            } else {
                showAlert(result.error || L.t('msgFailedDeletePin', 'Failed to delete pin'), 'danger');
            }
        } catch (err) {
            console.error('Delete error:', err);
            showAlert(L.t('msgUnexpectedDeletePin', 'Unexpected error deleting pin'), 'danger');
        }
    };

    // Internal pager used by the global shim
    window.__pins_goToPageInternal = function (page) {
        const totalPages = Math.max(1, Math.ceil(filteredPins.length / itemsPerPage));
        if (!Number.isFinite(page) || page < 1 || page > totalPages) return;
        currentPage = page;
        renderTable();
        renderPagination();
    };
})();