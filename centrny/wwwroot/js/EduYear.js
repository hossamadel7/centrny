console.log('EduYearManagment.js loaded');

document.addEventListener('DOMContentLoaded', function () {
    // EduYear Elements
    const eduModal = document.getElementById('addEduYearModal');
    const openEduBtn = document.getElementById('add-eduyear-btn');
    const closeEduBtn = document.getElementById('closeEduModal');
    const eduForm = document.getElementById('addEduYearForm');
    const eduErrorDiv = document.getElementById('addEduYearError');
    const eduTbody = document.getElementById('eduyear-body');
    const eduMsg = document.getElementById('eduyear-message');
    const eduModalTitle = document.getElementById('eduModalTitle');
    const eduCodeInput = document.getElementById('eduCode');

    // Year Elements
    const yearModal = document.getElementById('addYearModal');
    const openYearBtn = document.getElementById('add-year-btn');
    const closeYearBtn = document.getElementById('closeYearModal');
    const yearForm = document.getElementById('addYearForm');
    const yearErrorDiv = document.getElementById('addYearError');
    const yearTbody = document.getElementById('year-body');
    const yearMsg = document.getElementById('year-message');
    const yearModalTitle = document.getElementById('yearModalTitle');
    const yearCodeInput = document.getElementById('yearCode');
    const yearNameInput = document.getElementById('yearName');
    const yearSortInput = document.getElementById('yearSort');
    const levelSelect = document.getElementById('levelCode');

    // State
    let eduEditMode = false;
    let yearEditMode = false;
    let activeEduYear = null;
    let levelsList = [];

    // ----------- EduYear Functions -----------

    function openEduModal(isEdit = false, eduYear = null) {
        eduModal.style.display = "flex";
        eduErrorDiv.textContent = "";
        eduForm.reset();
        eduEditMode = isEdit;
        eduModalTitle.textContent = isEdit ? "Edit Edu Year" : "Add Edu Year";
        eduCodeInput.value = '';
        const submitBtn = eduForm.querySelector('button[type="submit"]');
        submitBtn.disabled = false;
        submitBtn.textContent = 'Submit';
        if (isEdit && eduYear) {
            eduCodeInput.value = eduYear.eduCode;
            eduForm.eduName.value = eduYear.eduName;
            eduForm.isActive.value = eduYear.isActive ? "true" : "false";
        }
    }
    function closeEduModalFunc() {
        eduModal.style.display = "none";
        eduEditMode = false;
    }
    if (openEduBtn) openEduBtn.onclick = () => openEduModal(false);
    if (closeEduBtn) closeEduBtn.onclick = closeEduModalFunc;
    window.onclick = function (event) {
        if (event.target === eduModal) closeEduModalFunc();
        if (event.target === yearModal) closeYearModalFunc();
    };

    eduForm.onsubmit = function (e) {
        e.preventDefault();
        eduErrorDiv.textContent = "";

        const submitBtn = eduForm.querySelector('button[type="submit"]');
        const originalText = submitBtn.textContent;
        submitBtn.disabled = true;
        submitBtn.textContent = 'Processing...';

        const eduName = eduForm.eduName.value.trim();
        const isActive = eduForm.isActive.value === "true";
        const eduCode = eduCodeInput.value;

        if (!eduName) {
            eduErrorDiv.textContent = "Please fill in all fields.";
            submitBtn.disabled = false;
            submitBtn.textContent = originalText;
            return;
        }

        let url, body;
        if (eduEditMode && eduCode) {
            url = '/EduYear/EditEduYear';
            body = JSON.stringify({ eduCode, eduName, isActive });
        } else {
            url = '/EduYear/AddEduYear';
            body = JSON.stringify({ eduName, isActive });
        }

        fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body
        })
            .then(r => {
                if (!r.ok) return r.text().then(t => { throw new Error(t); });
                return r.json();
            })
            .then(result => {
                if (eduEditMode)
                    updateEduYearRow(result);
                else
                    addEduYearRow(result);
                closeEduModalFunc();
                loadEduYears(); // reload EduYears and Years (active may have changed)
            })
            .catch(err => {
                eduErrorDiv.textContent = "Could not " + (eduEditMode ? "edit" : "add") + " edu year: " + (err.message || "Unknown error");
            })
            .finally(() => {
                submitBtn.disabled = false;
                submitBtn.textContent = originalText;
            });
    };

    function eduYearRowHTML(eduYear) {
        return `
         
            <td>${eduYear.eduName ?? ''}</td>
            <td>${eduYear.isActive ? 'Yes' : 'No'}</td>
            <td>
                <button class="action-btn edit-btn" data-code="${eduYear.eduCode}">Edit</button>
                <button class="action-btn delete-btn" data-code="${eduYear.eduCode}">Delete</button>
            </td>
        `;
    }

    function addEduYearRow(eduYear) {
        if (!eduTbody) return;
        const tr = document.createElement('tr');
        tr.classList.add('eduyear-row');
        tr.setAttribute('data-code', eduYear.eduCode);
        tr.innerHTML = eduYearRowHTML(eduYear);
        eduTbody.appendChild(tr);
        if (eduMsg) eduMsg.textContent = "";
        addEduYearActionListeners(tr, eduYear);
    }

    function updateEduYearRow(eduYear) {
        const tr = eduTbody.querySelector(`tr[data-code="${eduYear.eduCode}"]`);
        if (tr) {
            tr.innerHTML = eduYearRowHTML(eduYear);
            addEduYearActionListeners(tr, eduYear);
        }
    }

    function removeEduYearRow(eduCode) {
        const tr = eduTbody.querySelector(`tr[data-code="${eduCode}"]`);
        if (tr) tr.remove();
    }

    function addEduYearActionListeners(tr, eduYear) {
        const editBtn = tr.querySelector('.edit-btn');
        const deleteBtn = tr.querySelector('.delete-btn');
        if (editBtn) {
            editBtn.onclick = function () {
                openEduModal(true, eduYear);
            };
        }
        if (deleteBtn) {
            deleteBtn.onclick = function () {
                if (confirm('Are you sure you want to delete this edu year?')) {
                    fetch('/EduYear/DeleteEduYear', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ eduCode: eduYear.eduCode })
                    })
                        .then(r => {
                            if (!r.ok) return r.text().then(t => { throw new Error(t); });
                            removeEduYearRow(eduYear.eduCode);
                            loadEduYears(); // reload, in case active changed
                        })
                        .catch(err => alert("Could not delete edu year: " + (err.message || "Unknown error")));
                }
            };
        }
    }

    // ----------- Year Functions -----------

    function openYearModal(isEdit = false, year = null) {
        yearModal.style.display = "flex";
        yearErrorDiv.textContent = "";
        yearForm.reset();
        yearEditMode = isEdit;
        yearModalTitle.textContent = isEdit ? "Edit Year" : "Add Year";
        yearCodeInput.value = '';
        const submitBtn = yearForm.querySelector('button[type="submit"]');
        submitBtn.disabled = false;
        submitBtn.textContent = 'Submit';
        // Load levels (dropdown) and set value if editing
        loadLevelsDropdown().then(() => {
            if (isEdit && year) {
                yearCodeInput.value = year.yearCode;
                yearNameInput.value = year.yearName;
                yearSortInput.value = year.yearSort;
                levelSelect.value = year.levelCode;
            }
        });
    }
    function closeYearModalFunc() {
        yearModal.style.display = "none";
        yearEditMode = false;
    }
    if (openYearBtn) openYearBtn.onclick = () => {
        if (!activeEduYear || !activeEduYear.eduCode) {
            yearMsg.textContent = "No active Edu Year found. Please activate an Edu Year first.";
            return;
        }
        openYearModal(false);
    };
    if (closeYearBtn) closeYearBtn.onclick = closeYearModalFunc;

    yearForm.onsubmit = function (e) {
        e.preventDefault();
        yearErrorDiv.textContent = "";

        const submitBtn = yearForm.querySelector('button[type="submit"]');
        const originalText = submitBtn.textContent;
        submitBtn.disabled = true;
        submitBtn.textContent = 'Processing...';

        const yearName = yearNameInput.value.trim();
        const yearSort = parseInt(yearSortInput.value);
        const levelCode = parseInt(levelSelect.value);
        const yearCode = yearCodeInput.value;

        if (!yearName || isNaN(yearSort) || isNaN(levelCode)) {
            yearErrorDiv.textContent = "Please fill in all fields.";
            submitBtn.disabled = false;
            submitBtn.textContent = originalText;
            return;
        }
        if (!activeEduYear || !activeEduYear.eduCode) {
            yearErrorDiv.textContent = "No active Edu Year found. Please activate an Edu Year first.";
            submitBtn.disabled = false;
            submitBtn.textContent = originalText;
            return;
        }

        let url, body;
        if (yearEditMode && yearCode) {
            url = '/EduYear/EditYear';
            body = JSON.stringify({ yearCode, yearName, yearSort, levelCode });
        } else {
            url = '/EduYear/AddYear';
            body = JSON.stringify({ yearName, yearSort, levelCode });
        }

        fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body
        })
            .then(r => {
                if (!r.ok) return r.text().then(t => { throw new Error(t); });
                return r.json();
            })
            .then(result => {
                if (yearEditMode)
                    updateYearRow(result);
                else
                    addYearRow(result);
                closeYearModalFunc();
                loadYears(); // reload Years
            })
            .catch(err => {
                yearErrorDiv.textContent = "Could not " + (yearEditMode ? "edit" : "add") + " year: " + (err.message || "Unknown error");
            })
            .finally(() => {
                submitBtn.disabled = false;
                submitBtn.textContent = originalText;
            });
    };

    function yearRowHTML(year) {
        return `
         
            <td>${year.yearName ?? ''}</td>
            <td>${year.yearSort ?? ''}</td>
            <td>${year.levelName ?? ''}</td>
            <td>
                <button class="action-btn edit-btn" data-code="${year.yearCode}">Edit</button>
                <button class="action-btn delete-btn" data-code="${year.yearCode}">Delete</button>
            </td>
        `;
    }

    function addYearRow(year) {
        if (!yearTbody) return;
        const tr = document.createElement('tr');
        tr.classList.add('year-row');
        tr.setAttribute('data-code', year.yearCode);
        tr.innerHTML = yearRowHTML(year);
        yearTbody.appendChild(tr);
        if (yearMsg) yearMsg.textContent = "";
        addYearActionListeners(tr, year);
    }

    function updateYearRow(year) {
        const tr = yearTbody.querySelector(`tr[data-code="${year.yearCode}"]`);
        if (tr) {
            tr.innerHTML = yearRowHTML(year);
            addYearActionListeners(tr, year);
        }
    }

    function removeYearRow(yearCode) {
        const tr = yearTbody.querySelector(`tr[data-code="${yearCode}"]`);
        if (tr) tr.remove();
    }

    function addYearActionListeners(tr, year) {
        const editBtn = tr.querySelector('.edit-btn');
        const deleteBtn = tr.querySelector('.delete-btn');
        if (editBtn) {
            editBtn.onclick = function () {
                openYearModal(true, year);
            };
        }
        if (deleteBtn) {
            deleteBtn.onclick = function () {
                if (confirm('Are you sure you want to delete this year?')) {
                    fetch('/EduYear/DeleteYear', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ yearCode: year.yearCode })
                    })
                        .then(r => {
                            if (!r.ok) return r.text().then(t => { throw new Error(t); });
                            removeYearRow(year.yearCode);
                            loadYears();
                        })
                        .catch(err => alert("Could not delete year: " + (err.message || "Unknown error")));
                }
            };
        }
    }

    async function loadLevelsDropdown() {
        // Only reload if not loaded, or force reload on open
        levelSelect.innerHTML = `<option value="">Loading...</option>`;
        return fetch('/EduYear/GetLevelsForRoot')
            .then(r => r.json())
            .then(levels => {
                levelsList = levels;
                levelSelect.innerHTML = "";
                if (!levels || levels.length === 0) {
                    levelSelect.innerHTML = `<option value="">No levels found</option>`;
                    return;
                }
                levels.forEach(l => {
                    levelSelect.innerHTML += `<option value="${l.levelCode}">${l.levelName}</option>`;
                });
            })
            .catch(() => {
                levelSelect.innerHTML = `<option value="">Error loading levels</option>`;
            });
    }

    // ----------- Loaders -----------

    function loadEduYears() {
        fetch('/EduYear/GetEduYears')
            .then(response => {
                if (response.status === 401) {
                    if (eduMsg) eduMsg.textContent = "Unauthorized. Please log in.";
                    return [];
                }
                if (response.status === 404) {
                    if (eduMsg) eduMsg.textContent = "Your group or root was not found.";
                    return [];
                }
                return response.json();
            })
            .then(data => {
                if (!eduTbody) return;
                eduTbody.innerHTML = '';
                if (eduMsg) eduMsg.textContent = '';
                activeEduYear = null;

                if (!data || data.length === 0) {
                    if (eduMsg) eduMsg.textContent = "No edu years found for your root.";
                    loadYears(); // clear years table
                    return;
                }

                data.forEach(eduYear => {
                    const tr = document.createElement('tr');
                    tr.classList.add('eduyear-row');
                    tr.setAttribute('data-code', eduYear.eduCode);
                    tr.innerHTML = eduYearRowHTML(eduYear);
                    eduTbody.appendChild(tr);
                    addEduYearActionListeners(tr, eduYear);

                    if (eduYear.isActive) activeEduYear = eduYear;
                });
                loadYears();
            })
            .catch(error => {
                if (eduMsg) eduMsg.textContent = "An error occurred loading edu years.";
                console.error('Error fetching edu years:', error);
                loadYears(); // clear years table
            });
    }

    function loadYears() {
        yearTbody.innerHTML = '';
        if (!activeEduYear || !activeEduYear.eduCode) {
            yearMsg.textContent = "No active Edu Year found. Please activate an Edu Year first.";
            return;
        }
        fetch('/EduYear/GetYearsForActiveEduYear')
            .then(r => r.json())
            .then(data => {
                if (!yearTbody) return;
                yearTbody.innerHTML = '';
                yearMsg.textContent = '';
                if (!data || data.length === 0) {
                    yearMsg.textContent = "No years found for the active Edu Year.";
                    return;
                }
                data.forEach(year => {
                    const tr = document.createElement('tr');
                    tr.classList.add('year-row');
                    tr.setAttribute('data-code', year.yearCode);
                    tr.innerHTML = yearRowHTML(year);
                    yearTbody.appendChild(tr);
                    addYearActionListeners(tr, year);
                });
            })
            .catch(error => {
                if (yearMsg) yearMsg.textContent = "An error occurred loading years.";
                console.error('Error fetching years:', error);
            });
    }

    // Init: Load both tables on page load
    loadEduYears();
});