console.log("SubjectManagement.js loaded");

document.addEventListener('DOMContentLoaded', function () {
    console.log("DOMContentLoaded fired, JS running!");

    // ---------- ELEMENTS ----------
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

    // Add Teacher modal elements
    const addTeacherModal = document.getElementById('addTeacherToSubjectModal');
    const closeAddTeacherModalBtn = document.getElementById('closeAddTeacherToSubjectModal');
    const addTeacherForm = document.getElementById('addTeacherToSubjectForm');
    const teacherSelect = document.getElementById('addTeacherTeacherCode');
    const branchSelect = document.getElementById('addTeacherBranchCode');
    const addTeacherSubjectName = document.getElementById('addTeacherSubjectName');
    const addTeacherYearCode = document.getElementById('addTeacherYearCode');
    const addTeacherEduYearCode = document.getElementById('addTeacherEduYearCode'); // legacy
    const addTeacherCenterPercentage = document.getElementById('addTeacherCenterPercentage');
    const addTeacherCenterAmount = document.getElementById('addTeacherCenterAmount');

    const addSubjectSubmitBtn = document.querySelector('#addSubjectForm button[type="submit"]');
    const addTeacherSubmitBtn = document.querySelector('#addTeacherToSubjectForm button[type="submit"]');

    // ---------- HELPERS ----------
    function unwrap(p) {
        if (Array.isArray(p)) return p;
        if (p && Array.isArray(p.data)) return p.data;
        return [];
    }
    function safe(name, fallback) {
        try {
            if (typeof window[name] !== 'undefined') return window[name];
        } catch { }
        return fallback;
    }
    function resetSubmitButton(btn, defaultText) {
        if (btn) {
            btn.textContent = defaultText;
            btn.disabled = false;
        }
    }

    let editMode = false;
    let selectedSubjectData = {};

    // ---------- MODAL ----------
    function openModal(isEdit = false, subject = null) {
        if (!modal || !form) return;
        modal.style.display = "flex";
        if (errorDiv) errorDiv.textContent = "";
        form.reset();
        editMode = isEdit;
        if (modalTitle) modalTitle.textContent = isEdit ? safe('editTitle', 'Edit Subject') : safe('addTitle', 'Add Subject');
        if (subjectCodeInput) subjectCodeInput.value = '';
        resetSubmitButton(addSubjectSubmitBtn, isEdit ? safe('saveChangesText', 'Save Changes') : safe('addSubjectText', 'Add Subject'));
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
        if (modal) modal.style.display = "none";
        editMode = false;
        resetSubmitButton(addSubjectSubmitBtn, safe('saveChangesText', 'Save Changes'));
    }

    if (openBtn) openBtn.onclick = () => openModal(false);
    if (closeBtn) closeBtn.onclick = closeModalFunc;
    window.onclick = function (e) {
        if (e.target === modal) closeModalFunc();
        if (e.target === addTeacherModal) {
            addTeacherModal.style.display = "none";
            resetSubmitButton(addTeacherSubmitBtn, safe('assignTeacherText', 'Assign'));
        }
    };

    // ---------- YEARS ----------
    function loadYears(selectedYearCode = null) {
        if (!yearSelect) return;
        yearSelect.innerHTML = `<option value="">${safe('loadingYearsText', 'Loading years...')}</option>`;
        fetch('/Subject/GetActiveYears', { cache: 'no-store' })
            .then(resp => {
                if (!resp.ok) throw new Error('HTTP ' + resp.status);
                return resp.json();
            })
            .then(payload => {
                console.log('GetActiveYears payload:', payload); // debug
                const list = unwrap(payload);
                yearSelect.innerHTML = "";
                if (!list.length) {
                    yearSelect.innerHTML = `<option value="">${safe('noActiveYearsText', 'No years available')}</option>`;
                    return;
                }
                list.forEach(y => {
                    const selected = (selectedYearCode && y.yearCode == selectedYearCode) ? 'selected' : '';
                    const label = (y.yearName || '').replace(/</g, "&lt;");
                    yearSelect.innerHTML += `<option value="${y.yearCode}" ${selected}>${label}</option>`;
                });
            })
            .catch(err => {
                console.error('loadYears error:', err);
                yearSelect.innerHTML = `<option value="">${safe('errorLoadingYearsText', 'Error loading years')}</option>`;
            });
    }

    // ---------- FORM SUBMIT (ADD / EDIT SUBJECT) ----------
    if (form) {
        form.onsubmit = function (e) {
            e.preventDefault();
            if (errorDiv) errorDiv.textContent = "";
            if (addSubjectSubmitBtn) {
                addSubjectSubmitBtn.textContent = safe('processingText', 'Processing...');
                addSubjectSubmitBtn.disabled = true;
            }

            const subjectName = form.subjectName.value.trim();
            const isPrimary = form.isPrimary.value === "true";
            const yearCode = parseInt(form.yearCode.value);
            const subjectCode = subjectCodeInput.value;

            if (!subjectName || !yearCode) {
                resetSubmitButton(addSubjectSubmitBtn, editMode ? safe('saveChangesText', 'Save Changes') : safe('addSubjectText', 'Add Subject'));
                if (errorDiv) errorDiv.textContent = safe('pleaseFillFieldsText', 'Please fill required fields.');
                return;
            }

            const endpoint = (editMode && subjectCode) ? '/Subject/EditSubject' : '/Subject/AddSubject';
            const body = (editMode && subjectCode)
                ? { subjectCode, subjectName, isPrimary, yearCode }
                : { subjectName, isPrimary, yearCode };

            fetch(endpoint, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            })
                .then(r => {
                    if (!r.ok) return r.text().then(t => { throw new Error(t); });
                    return r.json();
                })
                .then(obj => {
                    if (editMode) {
                        updateSubjectRow(obj);
                        resetSubmitButton(addSubjectSubmitBtn, safe('saveChangesText', 'Save Changes'));
                    } else {
                        addSubjectRow(obj);
                        resetSubmitButton(addSubjectSubmitBtn, safe('addSubjectText', 'Add Subject'));
                    }
                    closeModalFunc();
                })
                .catch(err => {
                    resetSubmitButton(addSubjectSubmitBtn, editMode ? safe('saveChangesText', 'Save Changes') : safe('addSubjectText', 'Add Subject'));
                    if (errorDiv) {
                        errorDiv.textContent =
                            (editMode ? safe('couldNotEditText', 'Could not edit subject')
                                : safe('couldNotAddText', 'Could not add subject')) +
                            ": " + (err.message || "Unknown error");
                    }
                });
        };
    }

    // ---------- SUBJECT ROW HELPERS ----------
    function subjectRowHTML(subject) {
        const name = (subject.subjectName ?? '').replace(/</g, "&lt;");
        const yearName = (subject.yearName ?? '').replace(/</g, "&lt;");
        return `
            <td class="subject-name-cell">${name}</td>
            <td>${subject.isPrimary ? safe('yesText', 'Yes') : safe('noText', 'No')}</td>
            <td>${yearName}</td>
            <td style="text-align:center;">
                <div class="subject-btn-row">
                    <button class="modern-btn edit-btn" data-code="${subject.subjectCode}">${safe('editTitle', 'Edit')}</button>
                    <button class="modern-btn delete-btn" data-code="${subject.subjectCode}">${safe('closeText', 'Delete')}</button>
                    <button class="modern-btn primary-btn add-teacher-btn" data-code="${subject.subjectCode}">${safe('addTeacherText', 'Add Teacher')}</button>
                    <button class="modern-btn secondary-btn show-teachers-btn" data-code="${subject.subjectCode}">${safe('showTeachersText', 'Show Teachers')}</button>
                </div>
            </td>`;
    }

    function addSubjectRow(subject) {
        if (!tbody) return;
        const tr = document.createElement('tr');
        tr.classList.add('subject-row');
        tr.setAttribute('data-code', subject.subjectCode);
        tr.setAttribute('data-subject-code', subject.subjectCode);
        tr.setAttribute('data-year-code', subject.yearCode);
        tr.setAttribute('data-eduyear-code', subject.eduYearCode ?? ""); // legacy
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
        if (!tbody) return;
        const tr = tbody.querySelector(`tr[data-code="${subject.subjectCode}"]`);
        if (!tr) return;
        tr.setAttribute('data-subject-code', subject.subjectCode);
        tr.setAttribute('data-year-code', subject.yearCode);
        tr.setAttribute('data-eduyear-code', subject.eduYearCode ?? "");
        tr.innerHTML = subjectRowHTML(subject);
        addActionListeners(tr, subject);
    }

    function removeSubjectRow(subjectCode) {
        if (!tbody) return;
        const tr = tbody.querySelector(`tr[data-code="${subjectCode}"]`);
        const trTeachers = tbody.querySelector(`tr.teachers-row[data-subject-code="${subjectCode}"]`);
        if (tr) tr.remove();
        if (trTeachers) trTeachers.remove();
    }

    // ---------- ACTION LISTENERS ----------
    function addActionListeners(tr, subject) {
        if (!tr) return;
        const editBtn = tr.querySelector('.edit-btn');
        const deleteBtn = tr.querySelector('.delete-btn');
        const addTeacherBtn = tr.querySelector('.add-teacher-btn');
        const showTeachersBtn = tr.querySelector('.show-teachers-btn');

        if (editBtn) editBtn.onclick = () => openModal(true, subject);

        if (deleteBtn) {
            deleteBtn.onclick = function () {
                if (confirm(safe('deleteConfirmText', 'Are you sure?'))) {
                    fetch('/Subject/DeleteSubject', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ subjectCode: subject.subjectCode })
                    })
                        .then(r => {
                            if (!r.ok) return r.text().then(t => { throw new Error(t); });
                            removeSubjectRow(subject.subjectCode);
                        })
                        .catch(err => alert(safe('couldNotDeleteText', 'Could not delete') + ": " + (err.message || "Unknown error")));
                }
            };
        }

        if (addTeacherBtn) {
            addTeacherBtn.onclick = function () {
                selectedSubjectData = {
                    SubjectCode: tr.getAttribute('data-subject-code'),
                    SubjectName: tr.querySelector('.subject-name-cell').textContent,
                    YearCode: tr.getAttribute('data-year-code'),
                    EduYearCode: tr.getAttribute('data-eduyear-code') // legacy
                };

                if (addTeacherSubjectName) addTeacherSubjectName.value = selectedSubjectData.SubjectName;
                if (addTeacherYearCode) addTeacherYearCode.value = selectedSubjectData.YearCode;
                if (addTeacherEduYearCode) addTeacherEduYearCode.value = selectedSubjectData.EduYearCode || '';
                resetSubmitButton(addTeacherSubmitBtn, safe('assignTeacherText', 'Assign'));

                fetch('/Subject/GetTeachersByRoot')
                    .then(r => r.json())
                    .then(p => {
                        const teachers = unwrap(p);
                        if (teacherSelect) {
                            teacherSelect.innerHTML = "";
                            teachers.forEach(t => {
                                teacherSelect.innerHTML += `<option value="${t.teacherCode}">${(t.teacherName || '').replace(/</g, "&lt;")}</option>`;
                            });
                        }
                    });

                fetch('/Subject/GetBranchesByRoot')
                    .then(r => r.json())
                    .then(p => {
                        const branches = unwrap(p);
                        if (branchSelect) {
                            branchSelect.innerHTML = "";
                            branches.forEach(b => {
                                branchSelect.innerHTML += `<option value="${b.branchCode}">${(b.branchName || '').replace(/</g, "&lt;")}</option>`;
                            });
                        }
                    });

                if (addTeacherCenterPercentage) addTeacherCenterPercentage.value = "";
                if (addTeacherCenterAmount) addTeacherCenterAmount.value = "";
                if (addTeacherModal) addTeacherModal.style.display = "flex";
            };
        }

        if (showTeachersBtn) {
            showTeachersBtn.onclick = function () {
                const subjectCode = tr.getAttribute('data-subject-code');
                const trTeachers = tbody.querySelector(`tr.teachers-row[data-subject-code="${subjectCode}"]`);
                if (!trTeachers) return;
                const td = trTeachers.querySelector('.teachers-list-td');
                if (trTeachers.style.display === "none" || trTeachers.style.display === "") {
                    td.innerHTML = `<div class="text-muted">${safe('processingText', 'Loading...')}</div>`;
                    fetch('/Subject/GetTeachersForSubject?subjectCode=' + subjectCode)
                        .then(r => r.json())
                        .then(p => {
                            const data = unwrap(p);
                            if (!data.length) {
                                td.innerHTML = `<span class="text-danger">${safe('noTeachersAssignedText', 'No teachers assigned')}</span>`;
                            } else {
                                let html = '<ul style="margin-bottom:0">';
                                data.forEach(t => {
                                    html += `<li>${(t.teacherName || '').replace(/</g, "&lt;")} <span style="color:#888;font-size:0.95em;">(${(t.branchName || '').replace(/</g, "&lt;")})</span></li>`;
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

    // ---------- LOAD SUBJECTS ----------
    function loadSubjects() {
        fetch('/Subject/GetSubjects')
            .then(r => {
                if (r.status === 401) {
                    if (subjectMsg) subjectMsg.textContent = safe('unauthorizedText', 'Unauthorized');
                    return [];
                }
                if (r.status === 404) {
                    if (subjectMsg) subjectMsg.textContent = safe('notFoundText', 'Not found');
                    return [];
                }
                return r.json();
            })
            .then(p => {
                if (!tbody) return;
                tbody.innerHTML = '';
                if (subjectMsg) subjectMsg.textContent = '';
                const data = unwrap(p); // in case you later wrap this endpoint
                if (!data.length) {
                    if (subjectMsg) subjectMsg.textContent = safe('noSubjectsText', 'No subjects');
                    return;
                }
                data.forEach(subject => addSubjectRow(subject));
            })
            .catch(err => {
                if (subjectMsg) subjectMsg.textContent = safe('errorLoadingText', 'Error loading subjects');
                console.error('loadSubjects error:', err);
            });
    }

    // ---------- ADD TEACHER SUBMIT ----------
    if (addTeacherForm) {
        addTeacherForm.onsubmit = function (e) {
            e.preventDefault();
            if (addTeacherSubmitBtn) {
                addTeacherSubmitBtn.textContent = safe('processingText', 'Processing...');
                addTeacherSubmitBtn.disabled = true;
            }

            // Legacy EduYearCode retained; set to 0 if missing
            const eduYearParsed = selectedSubjectData.EduYearCode && !isNaN(parseInt(selectedSubjectData.EduYearCode))
                ? parseInt(selectedSubjectData.EduYearCode)
                : 0;

            const payload = {
                TeacherCode: parseInt(teacherSelect.value),
                SubjectCode: parseInt(selectedSubjectData.SubjectCode),
                EduYearCode: eduYearParsed,
                BranchCode: parseInt(branchSelect.value),
                RootCode: 0, // backend derives
                YearCode: parseInt(selectedSubjectData.YearCode),
                CenterPercentage: addTeacherCenterPercentage && addTeacherCenterPercentage.value
                    ? parseFloat(addTeacherCenterPercentage.value)
                    : null,
                CenterAmount: addTeacherCenterAmount && addTeacherCenterAmount.value
                    ? parseFloat(addTeacherCenterAmount.value)
                    : null
            };

            fetch('/Subject/AddTeacherToSubject', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
                .then(r => {
                    if (!r.ok) return r.text().then(t => { throw new Error(t); });
                    return r.json();
                })
                .then(() => {
                    resetSubmitButton(addTeacherSubmitBtn, safe('assignTeacherText', 'Assign'));
                    if (addTeacherModal) addTeacherModal.style.display = "none";
                })
                .catch(err => {
                    resetSubmitButton(addTeacherSubmitBtn, safe('assignTeacherText', 'Assign'));
                    alert(safe('couldNotAssignTeacherText', 'Could not assign teacher') + ": " + (err.message || "Unknown error"));
                });
        };
    }

    if (closeAddTeacherModalBtn) {
        closeAddTeacherModalBtn.onclick = function () {
            if (addTeacherModal) addTeacherModal.style.display = "none";
            resetSubmitButton(addTeacherSubmitBtn, safe('assignTeacherText', 'Assign'));
        };
    }

    // ---------- INITIAL LOAD ----------
    loadSubjects();
});