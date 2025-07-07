console.log('SubjectManagment.js loaded');

document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const modal = document.getElementById('addSubjectModal');
    const openBtn = document.getElementById('add-subject-btn');
    const closeBtn = document.getElementById('closeModal');
    const form = document.getElementById('addSubjectForm');
    const errorDiv = document.getElementById('addSubjectError');
    const yearSelect = document.getElementById('yearCode');
    const tbody = document.getElementById('subject-body');
    const subjectMsg = document.getElementById('subject-message');
    const modalTitle = document.getElementById('modalTitle');
    const subjectCodeInput = document.getElementById('subjectCode');

    let editMode = false;

    // Modal open/close logic
    function openModal(isEdit = false, subject = null) {
        modal.style.display = "flex";
        errorDiv.textContent = "";
        form.reset();
        editMode = isEdit;
        modalTitle.textContent = isEdit ? "Edit Subject" : "Add Subject";
        subjectCodeInput.value = '';
        if (!isEdit) {
            loadYears();
        } else if (subject) {
            subjectCodeInput.value = subject.subjectCode;
            form.subjectName.value = subject.subjectName;
            form.isPrimary.value = subject.isPrimary ? "true" : "false";
            loadYears(subject.yearCode);
        }
    }
    function closeModalFunc() {
        modal.style.display = "none";
        editMode = false;
    }
    if (openBtn) openBtn.onclick = () => openModal(false);
    if (closeBtn) closeBtn.onclick = closeModalFunc;
    window.onclick = function (event) {
        if (event.target === modal) closeModalFunc();
    };

    // Load all active years for the dropdown, optionally select a value
    function loadYears(selectedYearCode = null) {
        yearSelect.innerHTML = `<option value="">Loading...</option>`;
        fetch('/Subject/GetActiveYears')
            .then(resp => {
                if (!resp.ok) throw new Error('Network response was not ok');
                return resp.json();
            })
            .then(data => {
                yearSelect.innerHTML = "";
                if (!data || data.length === 0) {
                    yearSelect.innerHTML = `<option value="">No active years</option>`;
                    return;
                }
                data.forEach(y => {
                    const selected = (selectedYearCode && y.yearCode == selectedYearCode) ? 'selected' : '';
                    yearSelect.innerHTML += `<option value="${y.yearCode}" ${selected}>${y.yearName}</option>`;
                });
            })
            .catch(() => {
                yearSelect.innerHTML = `<option value="">Error loading years</option>`;
            });
    }

    // Add/Edit Subject Form Submission
    form.onsubmit = function (e) {
        e.preventDefault();
        errorDiv.textContent = "";

        const subjectName = form.subjectName.value.trim();
        const isPrimary = form.isPrimary.value === "true";
        const yearCode = parseInt(form.yearCode.value);
        const subjectCode = subjectCodeInput.value;

        if (!subjectName || !yearCode) {
            errorDiv.textContent = "Please fill in all fields.";
            return;
        }

        if (editMode && subjectCode) {
            // Edit
            fetch('/Subject/EditSubject', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ subjectCode, subjectName, isPrimary, yearCode })
            })
                .then(r => {
                    if (!r.ok) return r.text().then(t => { throw new Error(t); });
                    return r.json();
                })
                .then(editedSubject => {
                    updateSubjectRow(editedSubject);
                    closeModalFunc();
                })
                .catch(err => {
                    errorDiv.textContent = "Could not edit subject: " + (err.message || "Unknown error");
                });
        } else {
            // Add
            fetch('/Subject/AddSubject', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ subjectName, isPrimary, yearCode })
            })
                .then(r => {
                    if (!r.ok) return r.text().then(t => { throw new Error(t); });
                    return r.json();
                })
                .then(newSubject => {
                    addSubjectRow(newSubject);
                    closeModalFunc();
                })
                .catch(err => {
                    errorDiv.textContent = "Could not add subject: " + (err.message || "Unknown error");
                });
        }
    };

    // Add a new row to the subject table
    function addSubjectRow(subject) {
        if (!tbody) return;
        const tr = document.createElement('tr');
        tr.classList.add('subject-row');
        tr.setAttribute('data-code', subject.subjectCode);
        tr.innerHTML = subjectRowHTML(subject);
        tbody.appendChild(tr);
        if (subjectMsg) subjectMsg.textContent = "";
        addActionListeners(tr, subject);
    }

    // Update an existing row
    function updateSubjectRow(subject) {
        const tr = tbody.querySelector(`tr[data-code="${subject.subjectCode}"]`);
        if (tr) {
            tr.innerHTML = subjectRowHTML(subject);
            addActionListeners(tr, subject);
        }
    }

    function subjectRowHTML(subject) {
        return `
            <td>${subject.subjectCode ?? ''}</td>
            <td>${subject.subjectName ?? ''}</td>
            <td>${subject.isPrimary ? 'Yes' : 'No'}</td>
            <td>${subject.rootName ?? ''}</td>
            <td>${subject.yearName ?? ''}</td>
            <td>
                <button class="action-btn edit-btn" data-code="${subject.subjectCode}">Edit</button>
                <button class="action-btn delete-btn" data-code="${subject.subjectCode}">Delete</button>
            </td>
        `;
    }

    function addActionListeners(tr, subject) {
        const editBtn = tr.querySelector('.edit-btn');
        const deleteBtn = tr.querySelector('.delete-btn');
        if (editBtn) {
            editBtn.onclick = function () {
                openModal(true, subject);
            };
        }
        if (deleteBtn) {
            deleteBtn.onclick = function () {
                if (confirm('Are you sure you want to delete this subject?')) {
                    fetch('/Subject/DeleteSubject', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ subjectCode: subject.subjectCode })
                    })
                        .then(r => {
                            if (!r.ok) return r.text().then(t => { throw new Error(t); });
                            removeSubjectRow(subject.subjectCode);
                        })
                        .catch(err => alert("Could not delete subject: " + (err.message || "Unknown error")));
                }
            };
        }
    }

    function removeSubjectRow(subjectCode) {
        const tr = tbody.querySelector(`tr[data-code="${subjectCode}"]`);
        if (tr) tr.remove();
    }

    // Load subjects for the subject table
    function loadSubjects() {
        fetch('/Subject/GetSubjects')
            .then(response => {
                if (response.status === 401) {
                    if (subjectMsg) subjectMsg.textContent = "Unauthorized. Please log in.";
                    return [];
                }
                if (response.status === 404) {
                    if (subjectMsg) subjectMsg.textContent = "Your group or root was not found.";
                    return [];
                }
                return response.json();
            })
            .then(data => {
                if (!tbody) return;
                tbody.innerHTML = '';
                if (subjectMsg) subjectMsg.textContent = '';

                if (!data || data.length === 0) {
                    if (subjectMsg) subjectMsg.textContent = "No subjects found for your group.";
                    return;
                }

                data.forEach(subject => {
                    const tr = document.createElement('tr');
                    tr.classList.add('subject-row');
                    tr.setAttribute('data-code', subject.subjectCode);
                    tr.innerHTML = subjectRowHTML(subject);
                    tbody.appendChild(tr);
                    addActionListeners(tr, subject);
                });
            })
            .catch(error => {
                if (subjectMsg) subjectMsg.textContent = "An error occurred loading subjects.";
                console.error('Error fetching subjects:', error);
            });
    }

    // Init: Load subjects table on page load
    loadSubjects();
});