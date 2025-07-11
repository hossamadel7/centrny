console.log('EduYearManagment.js loaded');

// DOM elements
const eduModal = document.getElementById('addEduYearModal');
const openEduBtn = document.getElementById('add-eduyear-btn');
const closeEduBtn = document.getElementById('closeEduModal');
const eduForm = document.getElementById('addEduYearForm');
const eduErrorDiv = document.getElementById('addEduYearError');
const eduTbody = document.getElementById('eduyear-body');
const eduMsg = document.getElementById('eduyear-message');
const eduModalTitle = document.getElementById('eduModalTitle');
const eduCodeInput = document.getElementById('eduCode');

// State
let eduEditMode = false;
let activeEduYear = null;

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
    if (event.target === levelModal) closeLevelModalFunc();
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
            closeEduModalFunc();
            loadEduYears();
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
                        loadEduYears();
                    })
                    .catch(err => alert("Could not delete edu year: " + (err.message || "Unknown error")));
            }
        };
    }
}

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
                loadLevelsAndYears(); // clear years panel
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
            loadLevelsAndYears();
        })
        .catch(error => {
            if (eduMsg) eduMsg.textContent = "An error occurred loading edu years.";
            console.error('Error fetching edu years:', error);
            loadLevelsAndYears(); // clear years panel
        });
}

// ----------- Levels & Years Section -----------

const levelsContainer = document.getElementById('levels-years-container');

// Modal for adding year
const yearModal = document.getElementById('addYearModal');
const yearForm = document.getElementById('addYearForm');
const closeYearBtn = document.getElementById('closeYearModal');
const yearModalTitle = document.getElementById('yearModalTitle');
const yearErrorDiv = document.getElementById('addYearError');
const yearCodeInput = document.getElementById('yearCode');
const yearNameInput = document.getElementById('yearName');
const yearSortInput = document.getElementById('yearSort');
const yearLevelCodeInput = document.getElementById('yearLevelCode'); // hidden input to hold level for add

let yearEditMode = false;
let editingYearLevelCode = null;
let editingYearObj = null;

function openYearModal(levelCode, isEdit = false, year = null) {
    yearModal.style.display = "flex";
    yearErrorDiv.textContent = "";
    yearForm.reset();
    yearEditMode = isEdit;
    yearLevelCodeInput.value = levelCode;
    editingYearLevelCode = levelCode;
    editingYearObj = year;
    yearModalTitle.textContent = isEdit ? "Edit Year" : "Add Year";
    yearCodeInput.value = '';
    if (isEdit && year) {
        yearCodeInput.value = year.yearCode;
        yearNameInput.value = year.yearName;
        yearSortInput.value = year.yearSort;
    }
}
function closeYearModalFunc() {
    yearModal.style.display = "none";
    yearEditMode = false;
    editingYearObj = null;
    editingYearLevelCode = null;
}
if (closeYearBtn) closeYearBtn.onclick = closeYearModalFunc;

// Modal for adding level
const levelModal = document.getElementById('addLevelModal');
const levelForm = document.getElementById('addLevelForm');
const closeLevelBtn = document.getElementById('closeLevelModal');
const levelModalTitle = document.getElementById('levelModalTitle');
const levelErrorDiv = document.getElementById('addLevelError');
const levelNameInput = document.getElementById('levelName');

function openLevelModal() {
    levelModal.style.display = "flex";
    levelErrorDiv.textContent = "";
    levelForm.reset();
    levelModalTitle.textContent = "Add New Educational Stage";
}
function closeLevelModalFunc() {
    levelModal.style.display = "none";
}
if (closeLevelBtn) closeLevelBtn.onclick = closeLevelModalFunc;

// Add Year submit
yearForm.onsubmit = function (e) {
    e.preventDefault();
    yearErrorDiv.textContent = "";

    const submitBtn = yearForm.querySelector('button[type="submit"]');
    const originalText = submitBtn.textContent;
    submitBtn.disabled = true;
    submitBtn.textContent = 'Processing...';

    const yearName = yearNameInput.value.trim();
    const yearSort = parseInt(yearSortInput.value);
    const yearCode = yearCodeInput.value;
    const levelCode = parseInt(yearLevelCodeInput.value);

    if (!yearName || isNaN(yearSort) || isNaN(levelCode)) {
        yearErrorDiv.textContent = "Please fill in all fields.";
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
            closeYearModalFunc();
            loadLevelsAndYears();
        })
        .catch(err => {
            yearErrorDiv.textContent = "Could not " + (yearEditMode ? "edit" : "add") + " year: " + (err.message || "Unknown error");
        })
        .finally(() => {
            submitBtn.disabled = false;
            submitBtn.textContent = originalText;
        });
};

// Add Level submit
levelForm.onsubmit = function (e) {
    e.preventDefault();
    levelErrorDiv.textContent = "";

    const submitBtn = levelForm.querySelector('button[type="submit"]');
    const originalText = submitBtn.textContent;
    submitBtn.disabled = true;
    submitBtn.textContent = 'Processing...';

    const levelName = levelNameInput.value.trim();

    if (!levelName) {
        levelErrorDiv.textContent = "Please enter the level name.";
        submitBtn.disabled = false;
        submitBtn.textContent = originalText;
        return;
    }

    fetch('/EduYear/AddLevel', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ levelName })
    })
        .then(r => {
            if (!r.ok) return r.text().then(t => { throw new Error(t); });
            return r.json();
        })
        .then(result => {
            closeLevelModalFunc();
            loadLevelsAndYears();
        })
        .catch(err => {
            levelErrorDiv.textContent = "Could not add level: " + (err.message || "Unknown error");
        })
        .finally(() => {
            submitBtn.disabled = false;
            submitBtn.textContent = originalText;
        });
};

function levelBoxHTML(level, years) {
    let yearsHTML = `
        <table class="years-table">
            <thead>
                <tr>
                    <th>Year Name</th>
                    <th>Year Sort</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
            ${years && years.length > 0 ? years.map(yearRowHTML).join('') : `<tr><td colspan="3" style="text-align:center;color:#888;">No years found for this level</td></tr>`}
            </tbody>
        </table>
    `;
    return `
        <div class="level-box" data-level-code="${level.levelCode}">
            <div class="level-header">
                <h3 style="display:inline-block;margin-right:16px;">${level.levelName}</h3>
                <button class="modern-btn add-year-btn" data-level-code="${level.levelCode}" style="margin-right:8px;">Add Year</button>
            </div>
            <div class="years-list">${yearsHTML}</div>
            <div style="text-align:right;margin-top:8px;">
                <button class="modern-btn add-level-btn">New Educational Stage</button>
            </div>
        </div>
    `;
}
function yearRowHTML(year) {
    return `
        <tr data-year-code="${year.yearCode}">
            <td>${year.yearName ?? ''}</td>
            <td>${year.yearSort ?? ''}</td>
            <td>
                <button class="action-btn edit-year-btn" data-year-code="${year.yearCode}" data-level-code="${year.levelCode}">Edit</button>
                <button class="action-btn delete-year-btn" data-year-code="${year.yearCode}" data-level-code="${year.levelCode}">Delete</button>
            </td>
        </tr>
    `;
}

function loadLevelsAndYears() {
    if (!levelsContainer) return;
    levelsContainer.innerHTML = '';
    fetch('/EduYear/GetLevelsAndYearsForActiveEduYear')
        .then(r => r.json())
        .then(data => {
            if (!data.activeEduYear) {
                levelsContainer.innerHTML = `<div style="color:#b33c3c;font-weight:600;">No active Educational Year found. Please activate an Edu Year first.</div>`;
                return;
            }
            if (!data.levels || data.levels.length === 0) {
                levelsContainer.innerHTML = `<div style="color:#b33c3c;font-weight:600;">No levels found for this root. Add a new stage below.</div>
                <div style="margin-top:16px;"><button class="modern-btn add-level-btn">New Educational Stage</button></div>`;
                return;
            }
            data.levels.forEach(level => {
                const box = document.createElement('div');
                box.innerHTML = levelBoxHTML(level, level.years);
                // Add listeners after adding to DOM
                levelsContainer.appendChild(box.firstElementChild);
            });
            addLevelsAndYearsListeners();
        })
        .catch(err => {
            levelsContainer.innerHTML = `<div style="color:#b33c3c;font-weight:600;">An error occurred loading levels and years.</div>`;
        });
}

// Attach listeners for add year/level, edit/delete year
function addLevelsAndYearsListeners() {
    // Add Year
    document.querySelectorAll('.add-year-btn').forEach(btn => {
        btn.onclick = function () {
            const levelCode = parseInt(this.getAttribute('data-level-code'));
            openYearModal(levelCode, false);
        };
    });
    // Add Level
    document.querySelectorAll('.add-level-btn').forEach(btn => {
        btn.onclick = function () {
            openLevelModal();
        };
    });
    // Edit Year
    document.querySelectorAll('.edit-year-btn').forEach(btn => {
        btn.onclick = function () {
            const yearCode = parseInt(this.getAttribute('data-year-code'));
            const levelCode = parseInt(this.getAttribute('data-level-code'));
            // Find year object
            fetch('/EduYear/GetLevelsAndYearsForActiveEduYear')
                .then(r => r.json())
                .then(data => {
                    let foundYear = null;
                    if (data.levels && data.levels.length > 0) {
                        for (let lvl of data.levels) {
                            if (lvl.levelCode === levelCode && lvl.years) {
                                foundYear = lvl.years.find(y => y.yearCode === yearCode);
                                break;
                            }
                        }
                    }
                    if (foundYear) {
                        openYearModal(levelCode, true, foundYear);
                    }
                });
        };
    });
    // Delete Year
    document.querySelectorAll('.delete-year-btn').forEach(btn => {
        btn.onclick = function () {
            const yearCode = parseInt(this.getAttribute('data-year-code'));
            if (confirm('Are you sure you want to delete this year?')) {
                fetch('/EduYear/DeleteYear', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ yearCode })
                })
                    .then(r => {
                        if (!r.ok) return r.text().then(t => { throw new Error(t); });
                        loadLevelsAndYears();
                    })
                    .catch(err => alert("Could not delete year: " + (err.message || "Unknown error")));
            }
        };
    });
}

// Initial load
loadEduYears();