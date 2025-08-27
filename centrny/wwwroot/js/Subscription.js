let planModal;
let yearsCache = [];
let subjectsCache = {}; // key: yearCode, value: subjects[]
let rowIdCounter = 0;

window.addEventListener('DOMContentLoaded', function () {
    planModal = new bootstrap.Modal(document.getElementById('planModal'));
    document.getElementById('planForm').addEventListener('submit', submitForm);
    document.getElementById('add-subject-btn')?.addEventListener('click', () => addSubjectRow());
    document.getElementById('main-year-select').addEventListener('change', onYearChange);
    fetchAndRenderPlans();
});

function fetchAndRenderPlans() {
    document.getElementById('loading-state').style.display = 'flex';
    document.getElementById('plans-container').style.display = 'none';
    document.getElementById('empty-state').style.display = 'none';

    fetch('/Subscription/GetSubscriptions')
        .then(response => response.json())
        .then(data => {
            document.getElementById('loading-state').style.display = 'none';
            if (data.success === false) {
                document.getElementById('plans-container').innerHTML = `<div class="alert alert-danger">${data.message}</div>`;
                document.getElementById('plans-container').style.display = 'grid';
                return;
            }
            renderPlans(data);
        })
        .catch(error => {
            document.getElementById('loading-state').style.display = 'none';
            document.getElementById('plans-container').innerHTML = `<div class="alert alert-danger">Error loading plans: ${error.message}</div>`;
            document.getElementById('plans-container').style.display = 'grid';
        });
}

function renderPlans(plans) {
    const container = document.getElementById('plans-container');
    if (!plans || plans.length === 0) {
        container.style.display = 'none';
        document.getElementById('empty-state').style.display = 'block';
        return;
    }
    container.style.display = 'grid';
    document.getElementById('empty-state').style.display = 'none';
    container.innerHTML = '';
    plans.forEach((plan, index) => {
        // Subjects section (assumes plan.subjects from controller is an array of {subjectCode, subjectName, count})
        let subjectsHtml = '';
        if (plan.subjects && plan.subjects.length > 0) {
            subjectsHtml = `<ul class="plan-subjects-list" style="margin:1rem 0 0 0; padding-left:0; list-style:none;">` +
                plan.subjects.map(subj =>
                    `<li style="margin-bottom:6px;"><span style="color:#6c5ce7;font-weight:600;">${escapeHtml(subj.subjectName)}</span> <span class="subject-session-count" style="color:#222;background:#f6f7fb;padding:2px 12px;border-radius:12px;font-size:0.95em;margin-left:8px;">${subj.count} sessions</span></li>`
                ).join('') +
                `</ul>`;
        } else {
            subjectsHtml = `<div class="text-muted" style="margin-top:1rem;">No subjects.</div>`;
        }

        const card = document.createElement('div');
        card.className = 'subscription-card fade-in';
        card.style.animationDelay = `${index * 0.1}s`;
        card.innerHTML = `
            <div class="card-header-subscription">
                <div class="plan-name">${escapeHtml(plan.subPlanName)}</div>
                <div class="plan-price">$${plan.price}</div>
                <div class="price-period">Per ${plan.expiryMonths} Month${plan.expiryMonths > 1 ? 's' : ''}</div>
            </div>
            <div class="card-content-subscription">
                <div class="plan-description">${escapeHtml(plan.description)}</div>
                <ul class="plan-features">
                    <li>Total Sessions <span class="feature-value">${plan.totalCount}</span></li>
                </ul>
                <div class="plan-subjects">
                    <h6 style="margin-top:1rem;color:#6c5ce7;font-weight:bold;">Subjects:</h6>
                    ${subjectsHtml}
                </div>
            </div>
            <div class="card-actions-subscription">
                <button class="action-btn-subscription edit-btn-subscription" onclick="openEditModal(${plan.subPlanCode})">
                    <i class="fas fa-edit"></i> Edit
                </button>
                <button class="action-btn-subscription delete-btn-subscription" onclick="deletePlan(${plan.subPlanCode})">
                    <i class="fas fa-trash"></i> Delete
                </button>
            </div>
        `;
        container.appendChild(card);
    });
}

function openCreateModal() {
    document.getElementById('planModalLabel').innerHTML = '<i class="fas fa-plus"></i> Create Subscription Plan';
    document.getElementById('planForm').reset();
    document.getElementById('SubPlanCode').value = "";
    resetSubjectsSection();
    planModal.show();
    fetchYearsAndInitSubjects();
}

function openEditModal(id) {
    fetch(`/Subscription/GetSubscription?id=${id}`)
        .then(response => response.json())
        .then(plan => {
            if (!plan || plan.success === false) {
                alert("Error loading plan.");
                return;
            }
            document.getElementById('planModalLabel').innerHTML = '<i class="fas fa-edit"></i> Edit Subscription Plan';
            document.getElementById('SubPlanCode').value = plan.subPlanCode;
            document.getElementById('SubPlanName').value = plan.subPlanName;
            document.getElementById('Price').value = plan.price;
            document.getElementById('Description').value = plan.description;
            document.getElementById('ExpiryMonths').value = plan.expiryMonths;
            document.getElementById('TotalCount').value = plan.totalCount;
            resetSubjectsSection();
            fetchYearsAndInitSubjects(plan.Subjects || []);
            planModal.show();
        })
        .catch(error => {
            alert("Error loading plan: " + error.message);
        });
}

function fetchYearsAndInitSubjects(existingSubjects = []) {
    fetch('/Subscription/GetYears')
        .then(response => response.json())
        .then(years => {
            yearsCache = years;
            fillYearDropdown();
            // If editing, pick year from first subject (all must match)
            if (existingSubjects.length > 0) {
                let selectedYear = existingSubjects[0].yearCode;
                document.getElementById('main-year-select').value = selectedYear;
                fetchSubjects(selectedYear, function (subjects) {
                    subjectsCache[selectedYear] = subjects;
                    existingSubjects.forEach(subj => addSubjectRow(selectedYear, subj.subjectCode, subj.count));
                });
            } else {
                document.getElementById('main-year-select').value = "";
                document.getElementById('subject-rows').innerHTML = "";
            }
        });
}

function fillYearDropdown() {
    const yearSelect = document.getElementById('main-year-select');
    yearSelect.innerHTML = `<option value="">Select Year</option>`;
    for (const year of yearsCache) {
        yearSelect.innerHTML += `<option value="${year.yearCode}">${year.yearName}</option>`;
    }
}

function onYearChange() {
    const yearCode = this.value;
    if (!yearCode) {
        document.getElementById('subject-rows').innerHTML = "";
        updateTotalCount();
        return;
    }
    fetchSubjects(yearCode, function (subjects) {
        subjectsCache[yearCode] = subjects;
        // Reset subject rows and add one row for new year
        document.getElementById('subject-rows').innerHTML = "";
        addSubjectRow(yearCode);
    });
    updateTotalCount();
}

function fetchSubjects(yearCode, cb) {
    if (subjectsCache[yearCode]) {
        cb(subjectsCache[yearCode]);
        return;
    }
    fetch(`/Subscription/GetSubjects?yearCode=${yearCode}`)
        .then(response => response.json())
        .then(subjects => {
            subjectsCache[yearCode] = subjects;
            cb(subjects);
        });
}

function resetSubjectsSection() {
    rowIdCounter = 0;
    document.getElementById('subject-rows').innerHTML = '';
    document.getElementById('main-year-select').value = '';
    updateTotalCount();
}

// DEFENSIVE FIX HERE: ignore event objects
function addSubjectRow(selectedYearCode = null, selectedSubjectCode = null, countValue = null) {
    rowIdCounter++;
    const rowId = 'subject-row-' + rowIdCounter;
    const rowDiv = document.createElement('div');
    rowDiv.className = "row g-2 mb-2 align-items-center subject-row";
    rowDiv.id = rowId;

    // Defensive yearCode: ignore event objects
    let yearCode;
    if (typeof selectedYearCode === 'number' || (typeof selectedYearCode === 'string' && selectedYearCode !== "")) {
        yearCode = selectedYearCode;
    } else {
        yearCode = document.getElementById('main-year-select').value;
    }

    // Subject dropdown
    const subjectDiv = document.createElement('div');
    subjectDiv.className = "col-md-6";
    const subjectSelect = document.createElement('select');
    subjectSelect.className = "form-select subject-subject";
    subjectSelect.required = true;
    subjectDiv.appendChild(subjectSelect);

    // Count input
    const countDiv = document.createElement('div');
    countDiv.className = "col-md-5";
    const countInput = document.createElement('input');
    countInput.type = "number";
    countInput.className = "form-control subject-count";
    countInput.placeholder = "Session Count";
    countInput.required = true;
    countInput.min = 1;
    if (countValue) countInput.value = countValue;
    countDiv.appendChild(countInput);

    // Remove button
    const removeDiv = document.createElement('div');
    removeDiv.className = "col-md-1";
    const removeBtn = document.createElement('button');
    removeBtn.className = "btn btn-danger";
    removeBtn.type = "button";
    removeBtn.innerHTML = '<i class="fas fa-trash"></i>';
    removeBtn.onclick = function () {
        document.getElementById(rowId).remove();
        updateTotalCount();
    };
    removeDiv.appendChild(removeBtn);

    // Append to row
    rowDiv.appendChild(subjectDiv);
    rowDiv.appendChild(countDiv);
    rowDiv.appendChild(removeDiv);
    document.getElementById('subject-rows').appendChild(rowDiv);

    // Fill subject dropdown using cached subjects for selected year
    if (yearCode && subjectsCache[yearCode]) {
        fillSubjectDropdown(subjectSelect, subjectsCache[yearCode], selectedSubjectCode);
    } else if (yearCode) {
        fetchSubjects(yearCode, function (subjects) {
            fillSubjectDropdown(subjectSelect, subjects, selectedSubjectCode);
        });
    }

    subjectSelect.addEventListener('change', updateTotalCount);
    countInput.addEventListener('input', updateTotalCount);
}

function fillSubjectDropdown(subjectSelect, subjects, selectedSubjectCode) {
    subjectSelect.innerHTML = `<option value="">Select Subject</option>`;
    for (const subj of subjects) {
        subjectSelect.innerHTML += `<option value="${subj.subjectCode}" ${selectedSubjectCode == subj.subjectCode ? "selected" : ""}>${subj.subjectName}</option>`;
    }
}

function updateTotalCount() {
    let total = 0;
    const subjectRows = document.querySelectorAll('.subject-row');
    subjectRows.forEach(row => {
        const count = parseInt(row.querySelector('.subject-count')?.value) || 0;
        total += count;
    });
    document.getElementById('TotalCount').value = total;
}

function submitForm(e) {
    e.preventDefault();

    // Get form data
    const planData = {
        SubPlanCode: document.getElementById('SubPlanCode').value,
        SubPlanName: document.getElementById('SubPlanName').value,
        Price: parseInt(document.getElementById('Price').value),
        Description: document.getElementById('Description').value,
        ExpiryMonths: parseFloat(document.getElementById('ExpiryMonths').value),
        Subjects: []
    };

    // Gather subject rows
    const subjectRows = document.querySelectorAll('.subject-row');
    let valid = true;
    let yearCode = document.getElementById('main-year-select').value;
    if (!yearCode) valid = false;

    subjectRows.forEach(row => {
        const subjectSelect = row.querySelector('.subject-subject');
        const countInput = row.querySelector('.subject-count');
        const subjectCode = parseInt(subjectSelect.value);
        const count = parseInt(countInput.value);

        if (!subjectCode || !count || count < 1) {
            valid = false;
        }

        planData.Subjects.push({
            yearCode: parseInt(yearCode),
            subjectCode: subjectCode,
            count: count
        });
    });

    // Validate main fields
    if (!planData.SubPlanName || !planData.Price || !planData.Description || !planData.ExpiryMonths || planData.Subjects.length === 0 || !valid) {
        alert("Please fill in all required fields and add subjects.");
        return;
    }

    planData.TotalCount = planData.Subjects.reduce((acc, s) => acc + s.count, 0);

    const isEdit = !!planData.SubPlanCode;
    let url = isEdit ? '/Subscription/Edit' : '/Subscription/Create';

    // Disable submit button to prevent double submission
    const submitBtn = document.querySelector('#planForm button[type="submit"]');
    const originalText = submitBtn.innerHTML;
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Saving...';

    fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(planData)
    })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                planModal.hide();
                fetchAndRenderPlans();
            } else {
                alert(result.message || "Error saving plan.");
            }
        })
        .catch(error => {
            alert("Error saving plan: " + error.message);
        })
        .finally(() => {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalText;
        });
}

function deletePlan(id) {
    if (!confirm('Are you sure you want to delete this subscription plan? This action cannot be undone.')) {
        return;
    }
    fetch('/Subscription/Delete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ SubPlanCode: id })
    })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                fetchAndRenderPlans();
            } else {
                alert(result.message || "Error deleting plan.");
            }
        })
        .catch(error => {
            alert("Error deleting plan: " + error.message);
        });
}

// Utility for escaping HTML
function escapeHtml(text) {
    if (!text) return "";
    return text
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}