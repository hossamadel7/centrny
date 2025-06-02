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

// The rest of your existing functions here (onEditClick, onSaveClick, onCancelClick, onDeleteClick, etc.) remain unchanged.
