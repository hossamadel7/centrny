document.addEventListener('DOMContentLoaded', () => {
    setupEventListeners();
    loadRoots('center');
    setupAddWalletExamSubmit();
    setupEditWalletExamSubmit();
});

function setupEventListeners() {
    const search = document.getElementById('searchInput');
    if (search) search.addEventListener('input', debounce(filterData, 300));

    const expiryFilter = document.getElementById('expiryFilter');
    if (expiryFilter) expiryFilter.addEventListener('change', filterData);

    const tableBody = document.getElementById('tableBody');
    if (tableBody) {
        tableBody.addEventListener('click', function (e) {
            if (e.target.closest('.btn-edit')) {
                onEditClick(e);
            } else if (e.target.closest('.btn-delete')) {
                onDeleteClick(e);
            }
        });
    }

    document.querySelectorAll('input[name="rootType"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
            loadRoots(e.target.value);
        });
    });

    const rootSelect = document.getElementById('rootSelect');
    if (rootSelect) rootSelect.addEventListener('change', filterData);

    // Ensure Bootstrap JS is available for modals
    if (!window.bootstrap || !bootstrap.Modal) {
        console.warn('Bootstrap Modal JS is not available. Modals may not function correctly.');
    }
}

function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func(...args), wait);
    };
}

async function loadRoots(type) {
    const rootSelect = document.getElementById('rootSelect');
    if (!rootSelect) return;

    rootSelect.innerHTML = `<option value="">Loading...</option>`;

    try {
        const isCenter = type === 'center' ? 'true' : 'false';
        const url = `/api/roots?isCenter=${isCenter}&isActive=true`;
        const res = await fetch(url);
        if (!res.ok) throw new Error('Failed to load data');
        const data = await res.json();

        rootSelect.innerHTML = `<option value="">-- Select ${capitalize(type)} --</option>`;

        data.forEach(item => {
            rootSelect.innerHTML += `<option value="${item.rootCode}">${item.rootName}</option>`;
        });

        // Update modal dropdowns as well
        const modals = [document.getElementById('modalRootSelect'), document.getElementById('editModalRootSelect')];
        modals.forEach(sel => {
            if (sel) sel.innerHTML = rootSelect.innerHTML;
        });
    } catch (error) {
        rootSelect.innerHTML = `<option value="">Failed to load ${type}s</option>`;
        const modals = [document.getElementById('modalRootSelect'), document.getElementById('editModalRootSelect')];
        modals.forEach(sel => {
            if (sel) sel.innerHTML = rootSelect.innerHTML;
        });
        console.error(error);
    }

    filterData();
}

function capitalize(str) {
    return str.charAt(0).toUpperCase() + str.slice(1);
}

function filterData() {
    const query = (document.getElementById('searchInput')?.value || '').toLowerCase();
    const expiryFilter = document.getElementById('expiryFilter')?.value || '';
    const rootCodeFilter = document.getElementById('rootSelect')?.value || '';

    const rows = document.querySelectorAll('#tableBody tr');
    rows.forEach(row => {
        const amount = row.querySelector('.amount')?.textContent.toLowerCase() || '';
        const daysLeft = row.querySelector('.daysLeft')?.textContent.trim() || '';
        const rootCode = row.querySelector('.rootCode')?.textContent.trim() || '';
        const times = row.querySelector('.times')?.textContent.trim() || '';

        const matchSearch = amount.includes(query) || times.includes(query);
        const matchExpiry = !expiryFilter ||
            (expiryFilter === 'expired' && daysLeft === 'Expired') ||
            (expiryFilter === 'active' && daysLeft !== 'Expired');
        const matchRootCode = !rootCodeFilter || rootCode === rootCodeFilter;

        row.style.display = (matchSearch && matchExpiry && matchRootCode) ? '' : 'none';
    });
}

function setupAddWalletExamSubmit() {
    const form = document.getElementById('addWalletExamForm');
    if (!form) return;

    form.addEventListener('submit', async function (event) {
        event.preventDefault();

        const formData = new FormData(form);
        const payload = {
            Amount: Number(formData.get('amount')),
            DateStart: formData.get('dateStart'),    // yyyy-MM-dd
            ExpireDate: formData.get('expireDate'),  // yyyy-MM-dd
            Count: Number(formData.get('count')),
            OriginalCount: Number(formData.get('originalCount')),
            Times: Number(formData.get('times')),
            IsActive: true,
            RootCode: Number(formData.get('modalRootSelect'))
        };

        try {
            const res = await fetch(window.addWalletExamUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });

            if (res.ok) {
                await Swal.fire({ icon: 'success', title: 'Added!', timer: 1200, showConfirmButton: false });
                const addModalEl = document.getElementById('addWalletExamModal');
                if (addModalEl && window.bootstrap?.Modal) {
                    bootstrap.Modal.getOrCreateInstance(addModalEl).hide();
                }
                setTimeout(() => location.reload(), 1000);
            } else {
                let errorText = '';
                try { errorText = (await res.json()).message || ''; } catch { }
                Swal.fire('Error', 'Failed to add wallet exam' + (errorText ? ': ' + errorText : '.'), 'error');
            }
        } catch (err) {
            Swal.fire('Error', 'Failed to add wallet exam.', 'error');
        }
    });
}

function setupEditWalletExamSubmit() {
    const form = document.getElementById('editWalletExamForm');
    if (!form) return;

    form.addEventListener('submit', async function (event) {
        event.preventDefault();

        const formData = new FormData(form);
        const payload = {
            WalletCode1: Number(formData.get('walletCode1')),
            Amount: Number(formData.get('amount')),
            DateStart: formData.get('dateStart'),
            ExpireDate: formData.get('expireDate'),
            Count: Number(formData.get('count')),
            OriginalCount: Number(formData.get('originalCount')),
            Times: Number(formData.get('times')),
            IsActive: true,
            RootCode: Number(formData.get('modalRootSelect'))
        };

        try {
            const res = await fetch(window.updateWalletExamUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });

            if (res.ok) {
                await Swal.fire({ icon: 'success', title: 'Saved!', timer: 1200, showConfirmButton: false });
                const editModalEl = document.getElementById('editWalletExamModal');
                if (editModalEl && window.bootstrap?.Modal) {
                    bootstrap.Modal.getOrCreateInstance(editModalEl).hide();
                }
                setTimeout(() => location.reload(), 1000);
            } else {
                let errorText = '';
                try { errorText = (await res.json()).message || ''; } catch { }
                Swal.fire('Error', 'Failed to update wallet exam' + (errorText ? ': ' + errorText : '.'), 'error');
            }
        } catch (err) {
            Swal.fire('Error', 'Failed to update wallet exam.', 'error');
        }
    });
}

function getAntiForgeryToken() {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

function onEditClick(e) {
    const tr = e.target.closest('tr');
    document.getElementById('editWalletCode1').value = tr.getAttribute('data-id');
    document.getElementById('editAmount').value = tr.getAttribute('data-amount');
    document.getElementById('editDateStart').value = tr.getAttribute('data-datestart');
    document.getElementById('editExpireDate').value = tr.getAttribute('data-expiredate');
    document.getElementById('editCount').value = tr.getAttribute('data-count');
    document.getElementById('editOriginalCount').value = tr.getAttribute('data-originalcount');
    document.getElementById('editTimes').value = tr.getAttribute('data-times');

    const rootCode = tr.getAttribute('data-rootcode');
    const editSelect = document.getElementById('editModalRootSelect');
    if (editSelect) {
        for (const opt of editSelect.options) {
            opt.selected = (opt.value == rootCode);
        }
    }

    // Use Bootstrap's Modal API to show the modal
    const editModalEl = document.getElementById('editWalletExamModal');
    if (editModalEl && window.bootstrap?.Modal) {
        bootstrap.Modal.getOrCreateInstance(editModalEl).show();
    }
}

function onDeleteClick(e) {
    const tr = e.target.closest('tr');
    const walletExamCode = tr.getAttribute('data-id');

    Swal.fire({
        title: 'Delete Wallet Exam?',
        text: "Are you sure you want to delete this wallet exam?",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#e17055',
        cancelButtonColor: '#a29bfe',
        confirmButtonText: 'Yes, delete it!'
    }).then(async (result) => {
        if (result.isConfirmed) {
            await softDeleteWalletExam(walletExamCode);
        }
    });
}

async function softDeleteWalletExam(id) {
    try {
        const url = window.softDeleteWalletExamUrl.replace('__ID__', id);
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getAntiForgeryToken()
            }
        });
        if (response.ok) {
            await Swal.fire({ icon: 'success', title: 'Deleted!', timer: 1000, showConfirmButton: false });
            setTimeout(() => location.reload(), 500);
        } else {
            Swal.fire('Error', 'Failed to delete wallet exam.', 'error');
        }
    } catch (error) {
        Swal.fire('Error', 'Failed to delete wallet exam.', 'error');
    }
}