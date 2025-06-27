document.addEventListener('DOMContentLoaded', () => {
    // Show correct UI based on userRootCode
    if (typeof userRootCode === 'undefined') {
        // fallback: show nothing
        document.getElementById('centerBranchSection').style.display = "none";
        return;
    }

    if (userRootCode === 1) {
        // Admin-like: select root
        loadRoots();

        document.getElementById('rootDropdown').addEventListener('change', async function () {
            const rootCode = this.value;
            if (!rootCode) {
                document.getElementById('centerBranchSection').style.display = "none";
                return;
            }
            await loadCentersAndBranches(rootCode);
        });
    } else {
        // Restricted user: only see their root
        const fixedRootCode = document.getElementById('fixedRootCode').value;
        loadCentersAndBranches(parseInt(fixedRootCode));
    }

    // Form submission for Add Hall
    document.getElementById('addHallForm').addEventListener('submit', async function (e) {
        e.preventDefault();
        await submitAddHall();
    });
    // Form submission for Edit Hall
    document.getElementById('editHallForm').addEventListener('submit', async function (e) {
        e.preventDefault();
        await submitEditHall();
    });
});

let currentRootCode = null;

async function loadRoots() {
    const res = await fetch('/Branch/GetRootsIsCenterTrue');
    const roots = await res.json();
    const dropdown = document.getElementById('rootDropdown');
    dropdown.innerHTML = '<option value="">-- Select Root --</option>';
    roots.forEach(root => {
        dropdown.innerHTML += `<option value="${root.rootCode}">${root.rootName}</option>`;
    });
}

async function loadCentersAndBranches(rootCode) {
    currentRootCode = rootCode;

    const centerRes = await fetch(`/Branch/GetCentersByRoot?rootCode=${rootCode}`);
    const centers = await centerRes.json();

    const branchRes = await fetch(`/Branch/GetBranchesByRootCode?rootCode=${rootCode}`);
    const branches = await branchRes.json();

    // For each branch, also fetch its halls
    for (const branch of branches) {
        branch.halls = await fetchHalls(branch.branchCode);
        branch.rootCode = rootCode;
    }

    const centerList = document.getElementById('centerList');
    centerList.innerHTML = '';
    if (centers.length > 0) {
        centers.forEach(center => {
            centerList.innerHTML += `<li class="list-group-item">${center.centerName}</li>`;
        });
    } else {
        centerList.innerHTML = '<li class="list-group-item text-muted">No centers found.</li>';
    }

    const branchList = document.getElementById('branchList');
    branchList.innerHTML = '';
    if (branches.length > 0) {
        branches.forEach(branch => {
            // List halls under each branch
            let hallHtml = '';
            if (branch.halls.length > 0) {
                hallHtml = `<ul class="list-group mt-2 ms-3">` +
                    branch.halls.map(hall =>
                        `<li class="list-group-item py-1 px-2 d-flex justify-content-between align-items-center">
                            <span>
                                ${hall.hallName} <span class="badge bg-secondary">${hall.hallCapacity}</span>
                            </span>
                            <span class="hall-actions">
                                <button class="btn btn-sm edit-hall-btn" 
                                    data-hall-code="${hall.hallCode}" 
                                    data-hall-name="${hall.hallName}" 
                                    data-hall-capacity="${hall.hallCapacity}">
                                    Edit
                                </button>
                                <button class="btn btn-sm delete-hall-btn ms-2" 
                                    data-hall-code="${hall.hallCode}">
                                    Delete
                                </button>
                            </span>
                        </li>`
                    ).join('') +
                    `</ul>`;
            } else {
                hallHtml = `<div class="text-muted ms-3">No halls</div>`;
            }

            branchList.innerHTML += `
                <li class="list-group-item d-flex flex-column align-items-start">
                    <div class="d-flex w-100 justify-content-between align-items-center">
                        <span>${branch.branchName}</span>
                        <button class="btn btn-sm btn-outline-primary ms-2 add-hall-btn"
                            data-branch-code="${branch.branchCode}"
                            data-root-code="${rootCode}">
                            Add Hall
                        </button>
                    </div>
                    ${hallHtml}
                </li>`;
        });
    } else {
        branchList.innerHTML = '<li class="list-group-item text-muted">No branches found.</li>';
    }

    // Attach click event for Add Hall buttons
    document.querySelectorAll('.add-hall-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const branchCode = this.getAttribute('data-branch-code');
            const rootCode = this.getAttribute('data-root-code');
            openAddHallModal(rootCode, branchCode);
        });
    });

    // Attach click event for Edit Hall buttons
    document.querySelectorAll('.edit-hall-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            openEditHallModal(this);
        });
    });

    // Attach click event for Delete Hall buttons
    document.querySelectorAll('.delete-hall-btn').forEach(btn => {
        btn.addEventListener('click', async function () {
            const hallCode = this.getAttribute('data-hall-code');
            if (confirm('Are you sure you want to delete this hall?')) {
                await deleteHall(hallCode);
            }
        });
    });

    document.getElementById('centerBranchSection').style.display = "";
}

function openAddHallModal(rootCode, branchCode) {
    document.getElementById('hallRootCode').value = rootCode;
    document.getElementById('hallBranchCode').value = branchCode;
    document.getElementById('HallName').value = '';
    document.getElementById('HallCapacity').value = '';
    // Always reset button when opening the modal
    const submitBtn = document.querySelector('#addHallForm button[type="submit"]');
    submitBtn.innerHTML = 'Add Hall';
    submitBtn.disabled = false;
    var myModal = new bootstrap.Modal(document.getElementById('addHallModal'));
    myModal.show();
}

function openEditHallModal(btn) {
    document.getElementById('editHallCode').value = btn.getAttribute('data-hall-code');
    document.getElementById('editHallName').value = btn.getAttribute('data-hall-name');
    document.getElementById('editHallCapacity').value = btn.getAttribute('data-hall-capacity');
    const submitBtn = document.querySelector('#editHallForm button[type="submit"]');
    submitBtn.innerHTML = 'Save Changes';
    submitBtn.disabled = false;
    var myModal = new bootstrap.Modal(document.getElementById('editHallModal'));
    myModal.show();
}

async function submitAddHall() {
    const hallName = document.getElementById('HallName').value;
    const hallCapacity = parseInt(document.getElementById('HallCapacity').value, 10);
    const rootCode = parseInt(document.getElementById('hallRootCode').value, 10);
    const branchCode = parseInt(document.getElementById('hallBranchCode').value, 10);

    if (!hallName || isNaN(hallCapacity) || isNaN(rootCode) || isNaN(branchCode)) {
        alert('Please fill all fields.');
        return;
    }

    // Get the submit button in the modal
    const submitBtn = document.querySelector('#addHallForm button[type="submit"]');
    const originalText = 'Add Hall';
    submitBtn.disabled = true;
    submitBtn.innerHTML = 'Processing...';

    try {
        const res = await fetch('/Branch/AddHall', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                hallName,
                hallCapacity,
                rootCode,
                branchCode
            })
        });

        if (res.ok) {
            // Hide modal
            var myModalEl = document.getElementById('addHallModal');
            var modal = bootstrap.Modal.getInstance(myModalEl);
            modal.hide();
            // Refresh branches and halls
            await loadCentersAndBranches(currentRootCode);
            alert('Hall added successfully!');
        } else {
            alert('Failed to add hall.');
        }
    } catch (err) {
        alert('Failed to add hall due to an error.');
    } finally {
        // Always re-enable the button and restore text
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalText;
    }
}

async function submitEditHall() {
    const hallCode = document.getElementById('editHallCode').value;
    const hallName = document.getElementById('editHallName').value;
    const hallCapacity = parseInt(document.getElementById('editHallCapacity').value, 10);

    const submitBtn = document.querySelector('#editHallForm button[type="submit"]');
    const originalText = 'Save Changes';
    submitBtn.disabled = true;
    submitBtn.innerHTML = 'Processing...';

    try {
        const res = await fetch('/Branch/EditHall', {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                hallCode,
                hallName,
                hallCapacity
            })
        });

        if (res.ok) {
            var myModalEl = document.getElementById('editHallModal');
            var modal = bootstrap.Modal.getInstance(myModalEl);
            modal.hide();
            await loadCentersAndBranches(currentRootCode);
            alert('Hall updated successfully!');
        } else {
            alert('Failed to update hall.');
        }
    } catch (err) {
        alert('Failed to update hall due to an error.');
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalText;
    }
}

async function deleteHall(hallCode) {
    try {
        const res = await fetch(`/Branch/DeleteHall?hallCode=${hallCode}`, { method: 'DELETE' });
        if (res.ok) {
            await loadCentersAndBranches(currentRootCode);
            alert('Hall deleted successfully!');
        } else {
            alert('Failed to delete hall.');
        }
    } catch (err) {
        alert('Failed to delete hall due to an error.');
    }
}

async function fetchHalls(branchCode) {
    const res = await fetch(`/Branch/GetHallsByBranch?branchCode=${branchCode}`);
    if (!res.ok) return [];
    return await res.json();
}