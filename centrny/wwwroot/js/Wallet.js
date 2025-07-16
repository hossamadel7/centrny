document.addEventListener('DOMContentLoaded', () => {
    setupEventListeners();
    loadRoots('center');
    setupAddWalletExamSubmit();
});

function setupEventListeners() {
    document.getElementById('searchInput').addEventListener('input', debounce(filterData, 300));
    document.getElementById('statusFilter').addEventListener('change', filterData);
    document.getElementById('expiryFilter').addEventListener('change', filterData);

    // Use delegated event listeners for unified design (so new buttons use correct classes)
    document.getElementById('tableBody').addEventListener('click', function (e) {
        if (e.target.closest('.btn-edit')) {
            onEditClick(e);
        } else if (e.target.closest('.btn-delete')) {
            onDeleteClick(e);
        }
    });

    document.querySelectorAll('input[name="rootType"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
            loadRoots(e.target.value);
        });
    });

    document.getElementById('rootSelect').addEventListener('change', filterData);
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

        // Update modal dropdown as well
        const modalRootSelect = document.getElementById('modalRootSelect');
        if (modalRootSelect) {
            modalRootSelect.innerHTML = rootSelect.innerHTML;
        }
    } catch (error) {
        rootSelect.innerHTML = `<option value="">Failed to load ${type}s</option>`;
        const modalRootSelect = document.getElementById('modalRootSelect');
        if (modalRootSelect) {
            modalRootSelect.innerHTML = rootSelect.innerHTML;
        }
        console.error(error);
    }

    filterData();
}

function capitalize(str) {
    return str.charAt(0).toUpperCase() + str.slice(1);
}

function filterData() {
    const query = document.getElementById('searchInput').value.toLowerCase();
    const statusFilter = document.getElementById('statusFilter').value;
    const expiryFilter = document.getElementById('expiryFilter').value;
    const rootCodeFilter = document.getElementById('rootSelect').value;
    const rootTypeFilter = document.querySelector('input[name="rootType"]:checked').value;

    const rows = document.querySelectorAll('#tableBody tr');
    rows.forEach(row => {
        const code = row.querySelector('.walletExamCode').textContent.toLowerCase();
        const status = row.querySelector('.status').textContent.trim();
        const daysLeft = row.querySelector('.daysLeft').textContent.trim();
        const rootCode = row.querySelector('.rootCode').textContent.trim();
        const rootType = row.querySelector('.rootType').textContent.trim().toLowerCase();

        const matchSearch = code.includes(query);
        const matchStatus = !statusFilter || status === statusFilter;
        const matchExpiry = !expiryFilter ||
            (expiryFilter === 'expired' && daysLeft === 'Expired') ||
            (expiryFilter === 'active' && daysLeft !== 'Expired');
        const matchRootCode = !rootCodeFilter || rootCode === rootCodeFilter;
        const matchRootType = !rootTypeFilter || rootType === rootTypeFilter;

        row.style.display = (matchSearch && matchStatus && matchExpiry && matchRootCode && matchRootType) ? '' : 'none';
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
            DateStart: formData.get('dateStart'),
            ExpireDate: formData.get('expireDate'),
            IsActive: formData.get('isActive') === 'true',
            RootCode: Number(formData.get('modalRootSelect'))
            // Count and OriginalCount are not sent
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
                location.reload();
            } else {
                const error = await res.json();
                showAlert('danger', 'Failed to add wallet exam: ' + (error.message || 'Server error'));
            }
        } catch (err) {
            showAlert('danger', 'Failed to add wallet exam.');
        }
    });
}
function getAntiForgeryToken() {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

function showAlert(type, message) {
    const alertPlaceholder = document.getElementById('alertPlaceholder');
    alertPlaceholder.innerHTML = `
        <div class="alert alert-${type} alert-dismissible">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
}

// Unified Button Handlers
function onEditClick(e) {
    const tr = e.target.closest('tr');
    const walletExamCode = tr.getAttribute('data-id');
    window.location.href = `/WalletExam/Edit/${walletExamCode}`;
}

function onDeleteClick(e) {
    const tr = e.target.closest('tr');
    const walletExamCode = tr.getAttribute('data-id');

    if (confirm('Are you sure you want to delete this wallet exam?')) {
        deleteWalletExam(walletExamCode);
    }
}

async function deleteWalletExam(id) {
    try {
        const response = await fetch(`/WalletExam/DeleteConfirmed/${id}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getAntiForgeryToken()
            }
        });
        if (response.ok) {
            location.reload();
        } else {
            showAlert('danger', 'Failed to delete wallet exam.');
        }
    } catch (error) {
        showAlert('danger', 'Failed to delete wallet exam.');
    }
}

// If you dynamically add wallet exam rows, use these unified button classes
function addWalletExamRow(item) {
    const daysLeft = (new Date(item.ExpireDate) - new Date()) / (1000 * 60 * 60 * 24);
    const daysLeftText = daysLeft < 0 ? "Expired" : `${Math.round(daysLeft)} days`;
    const statusText = item.IsActive ? "Active" : "Inactive";
    const rootTypeText = item.IsCenter ? "center" : "teacher";
    return `<tr data-id="${item.WalletExamCode}">
        <td class="walletExamCode">${item.WalletExamCode}</td>
        <td class="amount">${item.Amount}</td>
        <td class="dateStart">${item.DateStart}</td>
        <td class="expireDate">${item.ExpireDate}</td>
        <td class="daysLeft">${daysLeftText}</td>
        <td class="status">${statusText}</td>
        <td class="rootCode">${item.RootCode}</td>
        <td class="rootType">${rootTypeText}</td>
        <td>
            <button class="modern-btn success-btn btn-edit">Edit</button>
            <button class="modern-btn delete-btn btn-delete">Delete</button>
        </td>
    </tr>`;
}