document.addEventListener('DOMContentLoaded', () => {
    setupEventListeners();
    loadRoots('center'); // Load Centers by default on page load
});

function setupEventListeners() {
    document.getElementById('searchInput').addEventListener('input', debounce(filterData, 300));
    document.getElementById('statusFilter').addEventListener('change', filterData);
    document.getElementById('expiryFilter').addEventListener('change', filterData);

    document.querySelectorAll('.btn-edit').forEach(btn => btn.addEventListener('click', onEditClick));
    document.querySelectorAll('.btn-delete').forEach(btn => btn.addEventListener('click', onDeleteClick));

    // New listeners for rootType radio and rootSelect dropdown
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
        // Fetch centers or teachers from your API endpoints:
        // Adjust URLs as per your backend routes
        const url = type === 'center' ? '/api/centers' : '/api/teachers';
        const res = await fetch(url);
        if (!res.ok) throw new Error('Failed to load data');
        const data = await res.json();

        // Clear and add a default option
        rootSelect.innerHTML = `<option value="">-- Select ${capitalize(type)} --</option>`;

        data.forEach(item => {
            // Assuming your centers/teachers have ID and Name properties
            rootSelect.innerHTML += `<option value="${item.id}">${item.name}</option>`;
        });

    } catch (error) {
        rootSelect.innerHTML = `<option value="">Failed to load ${type}s</option>`;
        console.error(error);
    }

    filterData(); // Apply filter with updated dropdown values
}

function capitalize(str) {
    return str.charAt(0).toUpperCase() + str.slice(1);
}

function filterData() {
    const query = document.getElementById('searchInput').value.toLowerCase();
    const statusFilter = document.getElementById('statusFilter').value;
    const expiryFilter = document.getElementById('expiryFilter').value;

    const rootCodeFilter = document.getElementById('rootSelect').value;

    const rows = document.querySelectorAll('#tableBody tr');
    rows.forEach(row => {
        const code = row.querySelector('.walletExamCode').textContent.toLowerCase();
        const status = row.querySelector('.status').textContent.trim();
        const daysLeft = row.querySelector('.daysLeft').textContent.trim();
        const rootCode = row.querySelector('.rootCode').textContent.trim();

        const matchSearch = code.includes(query);
        const matchStatus = !statusFilter || status === statusFilter;
        const matchExpiry = !expiryFilter ||
            (expiryFilter === 'expired' && daysLeft === 'Expired') ||
            (expiryFilter === 'active' && daysLeft !== 'Expired');
        // New: rootCode filtering
        const matchRootCode = !rootCodeFilter || rootCode === rootCodeFilter;

        row.style.display = (matchSearch && matchStatus && matchExpiry && matchRootCode) ? '' : 'none';
    });
}

function onEditClick(e) {
    const btn = e.target;
    const row = btn.closest('tr');

    // If already editing, ignore
    if (row.classList.contains('editing')) return;

    row.classList.add('editing');

    // Store current values to revert if needed
    row.dataset.original = JSON.stringify({
        Amount: row.querySelector('.amount').textContent.trim(),
        Count: row.querySelector('.count').textContent.trim(),
        OriginalCount: row.querySelector('.originalCount').textContent.trim(),
        DateStart: row.querySelector('.dateStart').textContent.trim(),
        ExpireDate: row.querySelector('.expireDate').textContent.trim(),
        IsActive: row.querySelector('.status').textContent.trim() === 'Active',
        RootCode: row.querySelector('.rootCode').textContent.trim(),
    });

    // Replace cells with inputs
    row.querySelector('.amount').innerHTML = `<input type="number" class="form-control form-control-sm" name="Amount" value="${row.querySelector('.amount').textContent.trim()}" />`;
    row.querySelector('.count').innerHTML = `<input type="number" class="form-control form-control-sm" name="Count" value="${row.querySelector('.count').textContent.trim()}" />`;
    row.querySelector('.originalCount').innerHTML = `<input type="number" class="form-control form-control-sm" name="OriginalCount" value="${row.querySelector('.originalCount').textContent.trim()}" />`;
    row.querySelector('.dateStart').innerHTML = `<input type="date" class="form-control form-control-sm" name="DateStart" value="${formatDateForInput(row.querySelector('.dateStart').textContent.trim())}" />`;
    row.querySelector('.expireDate').innerHTML = `<input type="date" class="form-control form-control-sm" name="ExpireDate" value="${formatDateForInput(row.querySelector('.expireDate').textContent.trim())}" />`;

    row.querySelector('.status').innerHTML = `
        <select class="form-select form-select-sm" name="IsActive">
            <option value="true" ${row.querySelector('.status').textContent.trim() === 'Active' ? 'selected' : ''}>Active</option>
            <option value="false" ${row.querySelector('.status').textContent.trim() === 'Inactive' ? 'selected' : ''}>Inactive</option>
        </select>
    `;

    row.querySelector('.rootCode').innerHTML = `<input type="number" class="form-control form-control-sm" name="RootCode" value="${row.querySelector('.rootCode').textContent.trim()}" />`;

    // Change buttons to Save / Cancel
    const actionCell = row.querySelector('td:last-child');
    actionCell.innerHTML = `
        <button class="btn btn-sm btn-success btn-save">Save</button>
        <button class="btn btn-sm btn-secondary btn-cancel">Cancel</button>
    `;

    actionCell.querySelector('.btn-save').addEventListener('click', () => onSaveClick(row));
    actionCell.querySelector('.btn-cancel').addEventListener('click', () => onCancelClick(row));
}

function formatDateForInput(displayDate) {
    // Convert "MMM dd, yyyy" to "yyyy-MM-dd" for input[type=date]
    const d = new Date(displayDate);
    if (isNaN(d)) return '';
    const yyyy = d.getFullYear();
    let mm = d.getMonth() + 1;
    let dd = d.getDate();
    if (mm < 10) mm = '0' + mm;
    if (dd < 10) dd = '0' + dd;
    return `${yyyy}-${mm}-${dd}`;
}

async function onSaveClick(row) {
    // Collect input values
    const walletExamCode = row.dataset.id;
    const amount = parseInt(row.querySelector('input[name=Amount]').value, 10);
    const count = parseInt(row.querySelector('input[name=Count]').value, 10);
    const originalCount = parseInt(row.querySelector('input[name=OriginalCount]').value, 10);
    const dateStart = row.querySelector('input[name=DateStart]').value;
    const expireDate = row.querySelector('input[name=ExpireDate]').value;
    const isActive = row.querySelector('select[name=IsActive]').value === 'true';
    const rootCode = parseInt(row.querySelector('input[name=RootCode]').value, 10);

    // Simple validation (add more if needed)
    if (!dateStart || !expireDate || isNaN(amount) || isNaN(count) || isNaN(originalCount) || isNaN(rootCode)) {
        showAlert('Please fill in all fields correctly.', 'danger');
        return;
    }

    const updatedData = {
        WalletExamCode: parseInt(walletExamCode, 10),
        Amount: amount,
        Count: count,
        OriginalCount: originalCount,
        DateStart: dateStart,
        ExpireDate: expireDate,
        IsActive: isActive,
        RootCode: rootCode
    };

    try {
        const res = await fetch(window.updateWalletExamUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify(updatedData)
        });

        if (!res.ok) {
            const error = await res.text();
            showAlert('Update failed: ' + error, 'danger');
            return;
        }

        // Update UI with new values
        row.querySelector('.amount').textContent = amount;
        row.querySelector('.count').textContent = count;
        row.querySelector('.originalCount').textContent = originalCount;
        row.querySelector('.dateStart').textContent = formatDisplayDate(dateStart);
        row.querySelector('.expireDate').textContent = formatDisplayDate(expireDate);

        const daysLeft = calculateDaysLeft(expireDate);
        row.querySelector('.daysLeft').textContent = daysLeft;

        row.querySelector('.status').textContent = isActive ? 'Active' : 'Inactive';
        row.querySelector('.rootCode').textContent = rootCode;

        // Restore buttons
        row.querySelector('td:last-child').innerHTML = `
            <button class="btn btn-sm btn-primary btn-edit">Edit</button>
            <button class="btn btn-sm btn-danger btn-delete">Delete</button>
        `;
        row.classList.remove('editing');

        // Re-attach listeners to new buttons
        row.querySelector('.btn-edit').addEventListener('click', onEditClick);
        row.querySelector('.btn-delete').addEventListener('click', onDeleteClick);

        showAlert('WalletExam updated successfully!', 'success');
    } catch (ex) {
        showAlert('Error: ' + ex.message, 'danger');
    }
}

function onCancelClick(row) {
    const original = JSON.parse(row.dataset.original);

    row.querySelector('.amount').textContent = original.Amount;
    row.querySelector('.count').textContent = original.Count;
    row.querySelector('.originalCount').textContent = original.OriginalCount;
    row.querySelector('.dateStart').textContent = original.DateStart;
    row.querySelector('.expireDate').textContent = original.ExpireDate;
    row.querySelector('.status').textContent = original.IsActive ? 'Active' : 'Inactive';
    row.querySelector('.rootCode').textContent = original.RootCode;

    row.querySelector('td:last-child').innerHTML = `
        <button class="btn btn-sm btn-primary btn-edit">Edit</button>
        <button class="btn btn-sm btn-danger btn-delete">Delete</button>
    `;

    row.classList.remove('editing');

    // Reattach event listeners
    row.querySelector('.btn-edit').addEventListener('click', onEditClick);
    row.querySelector('.btn-delete').addEventListener('click', onDeleteClick);
}

function onDeleteClick(e) {
    const row = e.target.closest('tr');
    const id = row.dataset.id;
    if (confirm('Are you sure you want to delete this record?')) {
        window.location.href = `/WalletExam/Delete/${id}`;
    }
}

function calculateDaysLeft(expireDateStr) {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const expireDate = new Date(expireDateStr);
    expireDate.setHours(0, 0, 0, 0);

    const diff = Math.floor((expireDate - today) / (1000 * 60 * 60 * 24));
    return diff < 0 ? 'Expired' : diff + ' days';
}

function formatDisplayDate(dateStr) {
    const d = new Date(dateStr);
    if (isNaN(d)) return '';
    return d.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
}

function showAlert(message, type = 'info') {
    const alertPlaceholder = document.getElementById('alertPlaceholder');
    const wrapper = document.createElement('div');
    wrapper.innerHTML = `
        <div class="alert alert-${type} alert-dismissible fade show" role="alert">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
    alertPlaceholder.append(wrapper);
    setTimeout(() => {
        bootstrap.Alert.getOrCreateInstance(wrapper.querySelector('.alert')).close();
    }, 4000);
}

function getAntiForgeryToken() {
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    return token ? token.value : '';
}
