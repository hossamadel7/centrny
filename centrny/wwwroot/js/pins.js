/* pins.js
   Handles:
   - Submitting pin generation form via AJAX
   - Refreshing pins list
   - UI feedback (alerts, spinner, counts)
*/

(function () {
    const form = document.getElementById('generatePinsForm');
    const spinner = document.getElementById('generateSpinner');
    const alertsArea = document.getElementById('alertsArea');
    const tableBody = document.querySelector('#pinsTable tbody');
    const walletCountEl = document.getElementById('walletCodesCount');
    const pinTotalEl = document.getElementById('pinTotal');
    const refreshBtn = document.getElementById('refreshPinsBtn');

    // ---------- Helpers ----------
    function showSpinner(show) {
        if (!spinner) return;
        spinner.classList.toggle('d-none', !show);
        const btnText = form.querySelector('.btn-text');
        if (btnText) btnText.classList.toggle('invisible', show);
    }

    function showAlert(message, type = 'success') {
        if (!alertsArea) return;
        alertsArea.innerHTML =
            `<div class="alert alert-${type} py-2 mb-2">${escapeHtml(message)}</div>`;
        // Auto hide after 6s
        setTimeout(() => {
            const first = alertsArea.querySelector('.alert');
            if (first) first.classList.add('fade', 'show');
            setTimeout(() => { alertsArea.innerHTML = ""; }, 600);
        }, 6000);
    }

    function escapeHtml(str) {
        return (str || "").replace(/[&<>"']/g, c => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
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
            tableBody.innerHTML = `<tr><td colspan="7" class="text-center text-muted py-3">No pins found.</td></tr>`;
            if (pinTotalEl) pinTotalEl.textContent = '0';
            return;
        }
        const rows = pins.map(p => {
            return `<tr data-pin="${p.pinCode}">
                <td>${p.pinCode}</td>
                <td class="text-truncate" style="max-width:220px;" title="${escapeHtml(p.watermark)}">${escapeHtml(p.watermark)}</td>
                <td>${p.type ? "Exam" : "Session"}</td>
                <td>${p.times}</td>
                <td>${p.status}</td>
                <td>${p.isActive === 1 ? "Yes" : "No"}</td>
                <td>${formatDate(p.insertTime)}</td>
            </tr>`;
        }).join('');
        tableBody.innerHTML = rows;
        if (pinTotalEl) pinTotalEl.textContent = pins.length.toString();
    }

    async function fetchJson(url, options) {
        const response = await fetch(url, options);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    }

    // ---------- AJAX Calls ----------
    async function generatePins(formEl) {
        const formData = new FormData(formEl);
        // The anti-forgery hidden input is automatically included
        const body = new URLSearchParams();
        formData.forEach((v, k) => body.append(k, v.toString()));

        return await fetchJson('/Pin/Generate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
            },
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
            if (walletCountEl) walletCountEl.textContent = data.walletCodesCount;
        } catch (err) {
            console.error(err);
            showAlert('Error refreshing pins.', 'danger');
        }
    }

    // ---------- Event Listeners ----------
    if (form) {
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            showSpinner(true);
            try {
                const result = await generatePins(form);
                if (result.success) {
                    showAlert(result.message || 'Pins generated.');
                    renderPins(result.pins);
                    if (walletCountEl) walletCountEl.textContent = result.walletCodesCount;
                    form.reset();
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
    }

    if (refreshBtn) {
        refreshBtn.addEventListener('click', refreshPins);
    }

    // Expose manual refresh (optional) 
    window.refreshPins = refreshPins;
})();