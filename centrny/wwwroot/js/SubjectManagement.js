console.log("SubjectManagement.js loaded");

document.addEventListener('DOMContentLoaded', function () {
    console.log("DOMContentLoaded fired, JS running!");

    // Modal & Form Elements
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

    // Add Teacher to Subject Modal & Form
    const addTeacherModal = document.getElementById('addTeacherToSubjectModal');
    const closeAddTeacherModalBtn = document.getElementById('closeAddTeacherToSubjectModal');
    const addTeacherForm = document.getElementById('addTeacherToSubjectForm');
    const teacherSelect = document.getElementById('addTeacherTeacherCode');
    const branchSelect = document.getElementById('addTeacherBranchCode');
    const addTeacherSubjectName = document.getElementById('addTeacherSubjectName');
    const addTeacherYearCode = document.getElementById('addTeacherYearCode');
    const addTeacherEduYearCode = document.getElementById('addTeacherEduYearCode');
    const addTeacherCenterPercentage = document.getElementById('addTeacherCenterPercentage');
    const addTeacherCenterAmount = document.getElementById('addTeacherCenterAmount');

    // Submit buttons
    const addSubjectSubmitBtn = document.querySelector('#addSubjectForm button[type="submit"]');
    const addTeacherSubmitBtn = document.querySelector('#addTeacherToSubjectForm button[type="submit"]');

    const saveChangesText = "Save Changes";
    const addSubjectText = "Add Subject";

    let editMode = false;
    let selectedSubjectData = {};

    function resetSubmitButton(btn, defaultText) {
        if (btn) {
            btn.textContent = defaultText || saveChangesText;
            btn.disabled = false;
        }
    }

    function openModal(isEdit = false, subject = null) {
        modal.style.display = "flex";
        errorDiv.textContent = "";
        form.reset();
        editMode = isEdit;
        modalTitle.textContent = isEdit ? editTitle : addTitle;
        subjectCodeInput.value = '';
        resetSubmitButton(addSubjectSubmitBtn, isEdit ? saveChangesText : addSubjectText);
        if (!isEdit) {
            loadYears();
        } else if (subject) {
            subjectCodeInput.value = subject.subjectCode;
            form.subjectName.value = subject.subjectName;
            form.isPrimary.value = subject.isPrimary ? "true" : "false";
            loadYears(subject.yearCode);
            // If you want to fill centerPercentage/centerAmount here, set their values from subject if present:
            // form.centerPercentage.value = subject.centerPercentage ?? '';
            // form.centerAmount.value = subject.centerAmount ?? '';
        }
    }
    function closeModalFunc() {
        modal.style.display = "none";
        editMode = false;
        resetSubmitButton(addSubjectSubmitBtn, saveChangesText);
    }
    if (openBtn) openBtn.onclick = () => openModal(false);
    if (closeBtn) closeBtn.onclick = closeModalFunc;
    window.onclick = function (event) {
        if (event.target === modal) closeModalFunc();
        if (event.target === addTeacherModal) {
            addTeacherModal.style.display = "none";
            resetSubmitButton(addTeacherSubmitBtn, assignTeacherText);
        }
    };

    function loadYears(selectedYearCode = null) {
        yearSelect.innerHTML = `<option value="">${loadingYearsText}</option>`;
        fetch('/Subject/GetActiveYears')
            .then(resp => {
                if (!resp.ok) throw new Error('Network response was not ok');
                return resp.json();
            })
            .then(data => {
                yearSelect.innerHTML = "";
                if (!data || data.length === 0) {
                    yearSelect.innerHTML = `<option value="">${noActiveYearsText}</option>`;
                    return;
                }
                data.forEach(y => {
                    const selected = (selectedYearCode && y.yearCode == selectedYearCode) ? 'selected' : '';
                    yearSelect.innerHTML += `<option value="${y.yearCode}" ${selected}>${y.yearName}</option>`;
                });
            })
            .catch(() => {
                yearSelect.innerHTML = `<option value="">${errorLoadingYearsText}</option>`;
            });
    }

    form.onsubmit = function (e) {
        e.preventDefault();
        errorDiv.textContent = "";
        addSubjectSubmitBtn.textContent = processingText;
        addSubjectSubmitBtn.disabled = true;

        const subjectName = form.subjectName.value.trim();
        const isPrimary = form.isPrimary.value === "true";
        const yearCode = parseInt(form.yearCode.value);
        const subjectCode = subjectCodeInput.value;

        // If you add centerPercentage/centerAmount fields to the edit modal, get them here:
        // const centerPercentage = form.centerPercentage ? form.centerPercentage.value : null;
        // const centerAmount = form.centerAmount ? form.centerAmount.value : null;

        if (!subjectName || !yearCode) {
            resetSubmitButton(addSubjectSubmitBtn, editMode ? saveChangesText : addSubjectText);
            errorDiv.textContent = pleaseFillFieldsText;
            return;
        }

        let requestBody = { subjectName, isPrimary, yearCode };
        if (editMode && subjectCode) {
            requestBody.subjectCode = subjectCode;
            // If you add center fields:
            // requestBody.centerPercentage = centerPercentage ? parseFloat(centerPercentage) : null;
            // requestBody.centerAmount = centerAmount ? parseFloat(centerAmount) : null;
            fetch('/Subject/EditSubject', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestBody)
            })
                .then(r => {
                    if (!r.ok) return r.text().then(t => { throw new Error(t); });
                    return r.json();
                })
                .then(editedSubject => {
                    updateSubjectRow(editedSubject);
                    resetSubmitButton(addSubjectSubmitBtn, saveChangesText);
                    closeModalFunc();
                })
                .catch(err => {
                    resetSubmitButton(addSubjectSubmitBtn, saveChangesText);
                    errorDiv.textContent = couldNotEditText + ": " + (err.message || "Unknown error");
                });
        } else {
            fetch('/Subject/AddSubject', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestBody)
            })
                .then(r => {
                    if (!r.ok) return r.text().then(t => { throw new Error(t); });
                    return r.json();
                })
                .then(newSubject => {
                    addSubjectRow(newSubject);
                    resetSubmitButton(addSubjectSubmitBtn, addSubjectText);
                    closeModalFunc();
                })
                .catch(err => {
                    resetSubmitButton(addSubjectSubmitBtn, addSubjectText);
                    errorDiv.textContent = couldNotAddText + ": " + (err.message || "Unknown error");
                });
        }
    };

    function addSubjectRow(subject) {
        if (!tbody) return;
        const tr = document.createElement('tr');
        tr.classList.add('subject-row');
        tr.setAttribute('data-code', subject.subjectCode);
        tr.setAttribute('data-subject-code', subject.subjectCode);
        tr.setAttribute('data-year-code', subject.yearCode);
        tr.setAttribute('data-eduyear-code', subject.eduYearCode ?? "");
        tr.innerHTML = subjectRowHTML(subject);
        tbody.appendChild(tr);

        const trTeachers = document.createElement('tr');
        trTeachers.classList.add('teachers-row');
        trTeachers.setAttribute('data-subject-code', subject.subjectCode);
        trTeachers.style.display = "none";
        trTeachers.innerHTML = `<td colspan="4" class="teachers-list-td"></td>`;
        tbody.appendChild(trTeachers);

        if (subjectMsg) subjectMsg.textContent = "";
        addActionListeners(tr, subject);
    }

    function updateSubjectRow(subject) {
        const tr = tbody.querySelector(`tr[data-code="${subject.subjectCode}"]`);
        if (tr) {
            tr.setAttribute('data-subject-code', subject.subjectCode);
            tr.setAttribute('data-year-code', subject.yearCode);
            tr.setAttribute('data-eduyear-code', subject.eduYearCode ?? "");
            tr.innerHTML = subjectRowHTML(subject);
            addActionListeners(tr, subject);
        }
    }

    function subjectRowHTML(subject) {
        return `
        <td class="subject-name-cell">${subject.subjectName ?? ''}</td>
        <td>${subject.isPrimary ? yesText : noText}</td>
        <td>${subject.yearName ?? ''}</td>
        <td style="text-align:center;">
            <div class="subject-btn-row">
                <button class="modern-btn edit-btn" data-code="${subject.subjectCode}">${editTitle}</button>
                <button class="modern-btn delete-btn" data-code="${subject.subjectCode}">${closeText}</button>
                <button class="modern-btn primary-btn add-teacher-btn" data-code="${subject.subjectCode}">${addTeacherText}</button>
                <button class="modern-btn secondary-btn show-teachers-btn" data-code="${subject.subjectCode}">${showTeachersText}</button>
            </div>
        </td>
    `;
    }

    function addActionListeners(tr, subject) {
        const editBtn = tr.querySelector('.edit-btn');
        const deleteBtn = tr.querySelector('.delete-btn');
        const addTeacherBtn = tr.querySelector('.add-teacher-btn');
        const showTeachersBtn = tr.querySelector('.show-teachers-btn');
        if (editBtn) {
            editBtn.onclick = function () {
                openModal(true, subject);
            };
        }
        if (deleteBtn) {
            deleteBtn.onclick = function () {
                if (confirm(deleteConfirmText)) {
                    fetch('/Subject/DeleteSubject', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ subjectCode: subject.subjectCode })
                    })
                        .then(r => {
                            if (!r.ok) return r.text().then(t => { throw new Error(t); });
                            removeSubjectRow(subject.subjectCode);
                        })
                        .catch(err => alert(couldNotDeleteText + ": " + (err.message || "Unknown error")));
                }
            };
        }
        if (addTeacherBtn) {
            addTeacherBtn.onclick = function () {
                selectedSubjectData = {
                    SubjectCode: tr.getAttribute('data-subject-code'),
                    SubjectName: tr.querySelector('.subject-name-cell').textContent,
                    YearCode: tr.getAttribute('data-year-code'),
                    EduYearCode: tr.getAttribute('data-eduyear-code')
                };

                addTeacherSubjectName.value = selectedSubjectData.SubjectName;
                addTeacherYearCode.value = selectedSubjectData.YearCode;
                addTeacherEduYearCode.value = selectedSubjectData.EduYearCode;
                resetSubmitButton(addTeacherSubmitBtn, assignTeacherText);

                fetch('/Subject/GetTeachersByRoot')
                    .then(resp => resp.json())
                    .then(teachers => {
                        teacherSelect.innerHTML = "";
                        teachers.forEach(t => {
                            teacherSelect.innerHTML += `<option value="${t.teacherCode}">${t.teacherName}</option>`;
                        });
                    });

                fetch('/Subject/GetBranchesByRoot')
                    .then(resp => resp.json())
                    .then(branches => {
                        branchSelect.innerHTML = "";
                        branches.forEach(b => {
                            branchSelect.innerHTML += `<option value="${b.branchCode}">${b.branchName}</option>`;
                        });
                    });

                addTeacherCenterPercentage.value = "";
                addTeacherCenterAmount.value = "";

                addTeacherModal.style.display = "flex";
            };
        }
        if (showTeachersBtn) {
            showTeachersBtn.onclick = function () {
                const subjectCode = tr.getAttribute('data-subject-code');
                const trTeachers = document.querySelector(`tr.teachers-row[data-subject-code="${subjectCode}"]`);
                if (!trTeachers) return;
                const td = trTeachers.querySelector('.teachers-list-td');

                if (trTeachers.style.display === "none" || trTeachers.style.display === "") {
                    td.innerHTML = `<div class="text-muted">${processingText}</div>`;
                    fetch('/Subject/GetTeachersForSubject?subjectCode=' + subjectCode)
                        .then(resp => resp.json())
                        .then(data => {
                            if (!data || data.length === 0) {
                                td.innerHTML = `<span class="text-danger">${noTeachersAssignedText}</span>`;
                            } else {
                                let html = '<ul style="margin-bottom:0">';
                                data.forEach(teacher => {
                                    html += `<li>
                                        ${teacher.teacherName} <span style="color:#888;font-size:0.97em;">(${teacher.branchName || ''})</span>
                                    </li>`;
                                });
                                html += '</ul>';
                                td.innerHTML = html;
                            }
                        });
                    trTeachers.style.display = "";
                } else {
                    trTeachers.style.display = "none";
                }
            };
        }
    }

    function removeSubjectRow(subjectCode) {
        const tr = tbody.querySelector(`tr[data-code="${subjectCode}"]`);
        const trTeachers = tbody.querySelector(`tr.teachers-row[data-subject-code="${subjectCode}"]`);
        if (tr) tr.remove();
        if (trTeachers) trTeachers.remove();
    }

    function loadSubjects() {
        fetch('/Subject/GetSubjects')
            .then(response => {
                if (response.status === 401) {
                    if (subjectMsg) subjectMsg.textContent = unauthorizedText;
                    return [];
                }
                if (response.status === 404) {
                    if (subjectMsg) subjectMsg.textContent = notFoundText;
                    return [];
                }
                return response.json();
            })
            .then(data => {
                if (!tbody) return;
                tbody.innerHTML = '';
                if (subjectMsg) subjectMsg.textContent = '';

                if (!data || data.length === 0) {
                    if (subjectMsg) subjectMsg.textContent = noSubjectsText;
                    return;
                }

                data.forEach(subject => {
                    const tr = document.createElement('tr');
                    tr.classList.add('subject-row');
                    tr.setAttribute('data-code', subject.subjectCode);
                    tr.setAttribute('data-subject-code', subject.subjectCode);
                    tr.setAttribute('data-year-code', subject.yearCode);
                    tr.setAttribute('data-eduyear-code', subject.eduYearCode ?? "");
                    tr.innerHTML = subjectRowHTML(subject);
                    tbody.appendChild(tr);

                    const trTeachers = document.createElement('tr');
                    trTeachers.classList.add('teachers-row');
                    trTeachers.setAttribute('data-subject-code', subject.subjectCode);
                    trTeachers.style.display = "none";
                    trTeachers.innerHTML = `<td colspan="4" class="teachers-list-td"></td>`;
                    tbody.appendChild(trTeachers);

                    addActionListeners(tr, subject);
                });
            })
            .catch(error => {
                if (subjectMsg) subjectMsg.textContent = errorLoadingText;
                console.error('Error fetching subjects:', error);
            });
    }

    addTeacherForm.onsubmit = function (e) {
        e.preventDefault();
        addTeacherSubmitBtn.textContent = processingText;
        addTeacherSubmitBtn.disabled = true;
        const data = {
            TeacherCode: parseInt(teacherSelect.value),
            SubjectCode: parseInt(selectedSubjectData.SubjectCode),
            EduYearCode: parseInt(selectedSubjectData.EduYearCode),
            BranchCode: parseInt(branchSelect.value),
            RootCode: 0,
            YearCode: parseInt(selectedSubjectData.YearCode),
            CenterPercentage: addTeacherCenterPercentage.value ? parseFloat(addTeacherCenterPercentage.value) : null,
            CenterAmount: addTeacherCenterAmount.value ? parseFloat(addTeacherCenterAmount.value) : null
        };

        fetch('/Subject/AddTeacherToSubject', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        })
            .then(r => {
                if (!r.ok) return r.text().then(t => { throw new Error(t); });
                return r.json();
            })
            .then(resp => {
                resetSubmitButton(addTeacherSubmitBtn, assignTeacherText);
                addTeacherModal.style.display = "none";
            })
            .catch(err => {
                resetSubmitButton(addTeacherSubmitBtn, assignTeacherText);
                alert(couldNotAssignTeacherText + ": " + (err.message || "Unknown error"));
            });
    };

    if (closeAddTeacherModalBtn) {
        closeAddTeacherModalBtn.onclick = function () {
            addTeacherModal.style.display = "none";
            resetSubmitButton(addTeacherSubmitBtn, assignTeacherText);
        };
    }

    loadSubjects();
});