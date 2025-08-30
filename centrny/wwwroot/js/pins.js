/* pins.js (modal version)
   - Opens a custom modal for generating pins
   - Submits form via AJAX
   - Refreshes table
*/

(function () {
    const modalOverlay = document.getElementById('generateModal');
    const openBtn = document.getElementById('openGenerateModal');
    const closeBtn = document.getElementById('closeGenerateModal');
    const cancelBtn = document.getElementById('cancelGenerateBtn');
    const form = document.getElementById('generatePinsForm');
    const spinner = document.getElementById('generateSpinner');
    const submitBtn = document.getElementById('submitGenerateBtn');
    const alertsArea = document.getElementById('alertsArea');
    const refreshBtn = document.getElementById('refreshPinsBtn');
    const tableBody = document.querySelector('#pinsTable tbody');
    const walletCountEl = document.getElementById('walletCodesCount');
    const pinTotalEl = document.getElementById('pinTotal');

    // ---------- Modal Control ----------
    function openModal() {
        if (!modalOverlay) return;
        modalOverlay.classList.add('is-open');
        modalOverlay.setAttribute('aria-hidden', 'false');
        // Trap focus
        setTimeout(() => {
            const firstField = form?.querySelector('select, input');
            firstField && firstField.focus();
        }, 20);
        document.addEventListener('keydown', escListener);
        document.addEventListener('mousedown', outsideListener);
    }

    function closeModal() {
        if (!modalOverlay) return;
        modalOverlay.classList.remove('is-open');
        modalOverlay.setAttribute('aria-hidden', 'true');
        document.removeEventListener('keydown', escListener);
        document.removeEventListener('mousedown', outsideListener);
    }

    function escListener(e) {
        if (e.key === 'Escape') {
            closeModal();
        }
    }

    function outsideListener(e) {
        if (!modalOverlay) return;
        const dialog = modalOverlay.querySelector('.pins-modal');
        if (dialog && !dialog.contains(e.target)) {
            closeModal();
        }
    }

    openBtn && openBtn.addEventListener('click', openModal);
    closeBtn && closeBtn.addEventListener('click', closeModal);
    cancelBtn && cancelBtn.addEventListener('click', closeModal);

    // ---------- UI Helpers ----------
    function showSpinner(show) {
        if (!spinner || !submitBtn) return;
        spinner.classList.toggle('d-none', !show);
        const textSpan = submitBtn.querySelector('.btn-text');
        textSpan && textSpan.classList.toggle('invisible', show);
        submitBtn.disabled = show;
    }

    function showAlert(message, type = 'success') {
        if (!alertsArea) return;
        alertsArea.innerHTML =
            `<div class="alert alert-${type} py-2 mb-2">${escapeHtml(message)}</div>`;
        setTimeout(() => {
            alertsArea.innerHTML = "";
        }, 6000);
        // Scroll to top if not visible
        alertsArea.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function escapeHtml(str) {
        return (str || "").replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));
    }

    function formatDate(raw) {
        if (!raw) return '';
        const d = new Date(raw);
        if (isNaN(d.getTime())) return raw;
        const pad = n => n < 10 ? '0' + n : n;
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }

    function renderPins(pins) {
        if (!tableBody) return;
        if (!pins || pins.length === 0) {
            tableBody.innerHTML = `<tr><td colspan="7" class="text-center text-muted py-4">No pins found.</td></tr>`;
            pinTotalEl && (pinTotalEl.textContent = '0');
            return;
        }
        tableBody.innerHTML = pins.map(p => `
            <tr data-pin="${p.pinCode}">
                <td>${p.pinCode}</td>
                <td class="text-truncate" style="max-width:220px;" title="${escapeHtml(p.watermark)}">${escapeHtml(p.watermark)}</td>
                <td>${p.type ? "Exam" : "Session"}</td>
                <td>${p.times}</td>
                <td>${p.status}</td>
                <td>${p.isActive === 1 ? "Yes" : "No"}</td>
                <td>${formatDate(p.insertTime)}</td>
            </tr>
        `).join('');
        pinTotalEl && (pinTotalEl.textContent = pins.length.toString());
    }

    async function fetchJson(url, options) {
        const res = await fetch(url, options);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
    }

    // ---------- AJAX ----------
    async function generatePins() {
        const formData = new FormData(form);
        const body = new URLSearchParams();
        formData.forEach((v, k) => body.append(k, v.toString()));

        return await fetchJson('/Pin/Generate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
            body: body.toString()
        });
    }

    async function refreshPins() {
        try {
            const data = await fetchJson('/Pin/List');
            if (!data.success) {
                showAlert(data.error || 'Failed to refresh pins.', 'danger');
                return;
            }
            renderPins(data.pins);
            walletCountEl && (walletCountEl.textContent = data.walletCodesCount);
        } catch (err) {
            console.error(err);
            showAlert('Error refreshing pins.', 'danger');
        }
    }

    // ---------- Events ----------
    form && form.addEventListener('submit', async e => {
        e.preventDefault();
        showSpinner(true);
        try {
            const result = await generatePins();
            if (result.success) {
                showAlert(result.message || 'Pins generated.');
                renderPins(result.pins);
                walletCountEl && (walletCountEl.textContent = result.walletCodesCount);
                form.reset();
                closeModal();
            } else {
                showAlert(result.error || 'Generation failed.', 'danger');
            }
        } catch (err) {
            console.error(err);
            showAlert('Unexpected error while generating pins.', 'danger');
        } finally {
            showSpinner(false);
        }
    });

    refreshBtn && refreshBtn.addEventListener('click', refreshPins);

    // Optional auto-refresh every X minutes (comment out if not needed)
    // setInterval(refreshPins, 60000);

    // Expose for console/manual use
    window.refreshPins = refreshPins;
})();