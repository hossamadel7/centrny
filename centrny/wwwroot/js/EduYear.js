console.log('EduYear.js loaded');

// Resource strings from Razor
const resxEdit = document.getElementById("resxEdit")?.value || "Edit";
const resxDelete = document.getElementById("resxDelete")?.value || "Delete";
const resxSubmit = document.getElementById("resxSubmit")?.value || "Submit";
const resxAddEduYear = document.getElementById("resxAddEduYear")?.value || "Add Education Year";
const resxAddYear = document.getElementById("resxAddYear")?.value || "Add Year";
const resxAddLevel = document.getElementById("resxAddLevel")?.value || "Add Level";
const resxYes = document.getElementById("resxYes")?.value || "Yes";
const resxNo = document.getElementById("resxNo")?.value || "No";
const resxEduYearName = document.getElementById("resxEduYearName")?.value || "Edu Year Name";
const resxIsActive = document.getElementById("resxIsActive")?.value || "Active";
const resxActions = document.getElementById("resxActions")?.value || "Actions";
const resxYearCode = document.getElementById("resxYearCode")?.value || "Year Code";
const resxYearName = document.getElementById("resxYearName")?.value || "Year Name";
const resxYearSort = document.getElementById("resxYearSort")?.value || "Sort";
const resxLevelName = document.getElementById("resxLevelName")?.value || "Level";
const resxLoading = document.getElementById("resxLoading")?.value || "Loading...";
const resxNoYears = document.getElementById("resxNoYears")?.value || "No years found for this level";
const resxNoLevels = document.getElementById("resxNoLevels")?.value || "No levels found for this root.";
const resxNoActiveEduYear = document.getElementById("resxNoActiveEduYear")?.value || "No active Educational Year found. Please activate an Edu Year first.";

// DOM elements
const eduModal = document.getElementById('addEduYearModal');
const openEduBtn = document.getElementById('add-eduyear-btn');
const closeEduBtn = document.getElementById('closeEduModal');
const eduForm = document.getElementById('addEduYearForm');
const eduErrorDiv = document.getElementById('addEduYearError');
const eduTbody = document.getElementById('eduyear-body');
const eduMsg = document.getElementById('eduyear-message');
const eduModalTitle = document.getElementById('eduModalTitle');

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
const yearLevelCodeInput = document.getElementById('yearLevelCode');

// Modal for adding level
const levelModal = document.getElementById('addLevelModal');
const levelForm = document.getElementById('addLevelForm');
const closeLevelBtn = document.getElementById('closeLevelModal');
const levelModalTitle = document.getElementById('levelModalTitle');
const levelErrorDiv = document.getElementById('addLevelError');
const levelNameInput = document.getElementById('levelName');

// State
let eduEditMode = false;
let activeEduYear = null;
let yearEditMode = false;
let editingYearLevelCode = null;
let editingYearObj = null;

// ----------- EduYear Functions -----------

function openEduModal(isEdit = false, eduYear = null) {
    if (!eduModal) return;
    eduModal.style.display = "flex";
    if (eduErrorDiv) eduErrorDiv.textContent = "";
    if (eduForm) eduForm.reset();
    eduEditMode = isEdit;
    if (eduModalTitle) eduModalTitle.textContent = isEdit ? resxEdit + " " + resxAddEduYear : resxAddEduYear;

    const submitBtn = eduForm?.querySelector('button[type="submit"]');
    if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = `<i class="fas fa-save"></i> ${resxSubmit}`;
    }

    if (isEdit && eduYear && eduForm) {
        if (eduForm.eduName) eduForm.eduName.value = eduYear.eduName;
        if (eduForm.isActive) eduForm.isActive.value = eduYear.isActive ? "true" : "false";
        eduForm.setAttribute("data-edit-code", eduYear.eduCode);
    } else if (eduForm) {
        eduForm.removeAttribute("data-edit-code");
    }
}

function closeEduModalFunc() {
    if (!eduModal) return;
    eduModal.style.display = "none";
    eduEditMode = false;
    if (eduForm) eduForm.removeAttribute("data-edit-code");
}

// Event listeners
if (openEduBtn) openEduBtn.onclick = () => openEduModal(false);
if (closeEduBtn) closeEduBtn.onclick = closeEduModalFunc;

window.onclick = function (event) {
    if (event.target === eduModal) closeEduModalFunc();
    if (event.target === yearModal) closeYearModalFunc();
    if (event.target === levelModal) closeLevelModalFunc();
};

if (eduForm) {
    eduForm.onsubmit = function (e) {
        e.preventDefault();
        if (eduErrorDiv) eduErrorDiv.textContent = "";

        const submitBtn = eduForm.querySelector('button[type="submit"]');
        const originalHtml = submitBtn?.innerHTML || '';
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> ${resxLoading}`;
        }

        const eduName = eduForm.eduName?.value.trim() || '';
        const isActive = eduForm.isActive?.value === "true";
        const editCode = eduForm.getAttribute("data-edit-code");

        if (!eduName) {
            if (eduErrorDiv) eduErrorDiv.textContent = "Please enter education year name";
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalHtml;
            }
            return;
        }

        let url, body;
        if (eduEditMode && editCode) {
            url = '/EduYear/EditEduYear';
            body = JSON.stringify({ eduCode: editCode, eduName, isActive });
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
                Swal.fire({
                    icon: 'success',
                    title: resxAddEduYear,
                    text: eduEditMode ? 'Education year updated successfully.' : 'Education year added successfully.',
                    timer: 1200,
                    showConfirmButton: false
                });
                loadEduYears();
            })
            .catch(err => {
                Swal.fire({
                    icon: 'error',
                    title: resxAddEduYear,
                    text: err.message || resxLoading,
                });
                if (eduErrorDiv) eduErrorDiv.textContent = resxAddEduYear + ": " + (err.message || resxLoading);
            })
            .finally(() => {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalHtml;
                }
            });
    };
}

function eduYearRowHTML(eduYear) {
    return `
        <td>${eduYear.eduName ?? ''}</td>
        <td>${eduYear.isActive ? resxYes : resxNo}</td>
        <td>
            <button class="btn-table edit edit-btn" data-code="${eduYear.eduCode}">
                <i class="fas fa-pencil"></i> ${resxEdit}
            </button>
            <button class="btn-table delete delete-btn" data-code="${eduYear.eduCode}">
                <i class="fas fa-trash"></i> ${resxDelete}
            </button>
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
            Swal.fire({
                title: resxDelete + ": " + resxEduYearName + "?",
                text: "This action cannot be undone.",
                icon: "warning",
                showCancelButton: true,
                confirmButtonText: resxDelete,
                cancelButtonText: resxNo,
                confirmButtonColor: "#d33"
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch('/EduYear/DeleteEduYear', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ eduCode: eduYear.eduCode })
                    })
                        .then(r => {
                            if (!r.ok) return r.text().then(t => { throw new Error(t); });
                            Swal.fire({
                                icon: 'success',
                                title: resxDelete,
                                text: 'Education year deleted successfully.',
                                timer: 1200,
                                showConfirmButton: false
                            });
                            loadEduYears();
                        })
                        .catch(err => {
                            Swal.fire({
                                icon: 'error',
                                title: resxDelete,
                                text: err.message || resxLoading,
                            });
                        });
                }
            });
        };
    }
}

function loadEduYears() {
    fetch('/EduYear/GetEduYears')
        .then(response => {
            if (response.status === 401 || response.status === 404) {
                if (eduMsg) eduMsg.textContent = "No education years found";
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
                if (eduMsg) eduMsg.textContent = "No education years found";
                loadLevelsAndYears();
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
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Error loading education years',
            });
            if (eduMsg) eduMsg.textContent = "Error loading education years";
            console.error('Error fetching edu years:', error);
            loadLevelsAndYears();
        });
}

// ----------- Levels & Years Section -----------

function openYearModal(levelCode, isEdit = false, year = null) {
    if (!yearModal) return;
    yearModal.style.display = "flex";
    if (yearErrorDiv) yearErrorDiv.textContent = "";
    if (yearForm) yearForm.reset();
    yearEditMode = isEdit;
    if (yearLevelCodeInput) yearLevelCodeInput.value = levelCode;
    editingYearLevelCode = levelCode;
    editingYearObj = year;
    if (yearModalTitle) yearModalTitle.textContent = isEdit ? resxEdit + " " + resxAddYear : resxAddYear;
    if (yearCodeInput) yearCodeInput.value = '';

    const submitBtn = yearForm?.querySelector('button[type="submit"]');
    if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = `<i class="fas fa-save"></i> ${resxSubmit}`;
    }

    if (isEdit && year) {
        if (yearCodeInput) yearCodeInput.value = year.yearCode;
        if (yearNameInput) yearNameInput.value = year.yearName;
        if (yearSortInput) yearSortInput.value = year.yearSort;
    }
}

function closeYearModalFunc() {
    if (!yearModal) return;
    yearModal.style.display = "none";
    yearEditMode = false;
    editingYearObj = null;
    editingYearLevelCode = null;
}

if (closeYearBtn) closeYearBtn.onclick = closeYearModalFunc;

function openLevelModal() {
    if (!levelModal) return;
    levelModal.style.display = "flex";
    if (levelErrorDiv) levelErrorDiv.textContent = "";
    if (levelForm) levelForm.reset();
    if (levelModalTitle) levelModalTitle.textContent = resxAddLevel;

    const submitBtn = levelForm?.querySelector('button[type="submit"]');
    if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.innerHTML = `<i class="fas fa-save"></i> ${resxSubmit}`;
    }
}

function closeLevelModalFunc() {
    if (!levelModal) return;
    levelModal.style.display = "none";
}

if (closeLevelBtn) closeLevelBtn.onclick = closeLevelModalFunc;

// Year form submit
if (yearForm) {
    yearForm.onsubmit = function (e) {
        e.preventDefault();
        if (yearErrorDiv) yearErrorDiv.textContent = "";

        const submitBtn = yearForm.querySelector('button[type="submit"]');
        const originalHtml = submitBtn?.innerHTML || '';
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> ${resxLoading}`;
        }

        const yearName = yearNameInput?.value.trim() || '';
        const yearSort = parseInt(yearSortInput?.value || '0');
        const yearCode = yearCodeInput?.value || '';
        const levelCode = parseInt(yearLevelCodeInput?.value || '0');

        if (!yearName || isNaN(yearSort) || isNaN(levelCode)) {
            Swal.fire({
                icon: 'warning',
                title: 'Missing Fields',
                text: 'Please fill all required fields.',
            });
            if (yearErrorDiv) yearErrorDiv.textContent = "Please fill all required fields";
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalHtml;
            }
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
                Swal.fire({
                    icon: 'success',
                    title: yearEditMode ? resxEdit : resxAddYear,
                    text: yearEditMode ? 'Year updated successfully.' : 'Year added successfully.',
                    timer: 1200,
                    showConfirmButton: false
                });
                loadLevelsAndYears();
            })
            .catch(err => {
                Swal.fire({
                    icon: 'error',
                    title: yearEditMode ? resxEdit : resxAddYear,
                    text: err.message || resxLoading,
                });
                if (yearErrorDiv) yearErrorDiv.textContent = resxAddYear + ": " + (err.message || resxLoading);
            })
            .finally(() => {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalHtml;
                }
            });
    };
}

// Level form submit
if (levelForm) {
    levelForm.onsubmit = function (e) {
        e.preventDefault();
        if (levelErrorDiv) levelErrorDiv.textContent = "";

        const submitBtn = levelForm.querySelector('button[type="submit"]');
        const originalHtml = submitBtn?.innerHTML || '';
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> ${resxLoading}`;
        }

        const levelName = levelNameInput?.value.trim() || '';

        if (!levelName) {
            Swal.fire({
                icon: 'warning',
                title: 'Missing Fields',
                text: 'Please enter level name.',
            });
            if (levelErrorDiv) levelErrorDiv.textContent = "Please enter level name";
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalHtml;
            }
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
                Swal.fire({
                    icon: 'success',
                    title: resxAddLevel,
                    text: 'Level added successfully.',
                    timer: 1200,
                    showConfirmButton: false
                });
                loadLevelsAndYears();
            })
            .catch(err => {
                Swal.fire({
                    icon: 'error',
                    title: resxAddLevel,
                    text: err.message || resxLoading,
                });
                if (levelErrorDiv) levelErrorDiv.textContent = resxAddLevel + ": " + (err.message || resxLoading);
            })
            .finally(() => {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalHtml;
                }
            });
    };
}

function levelBoxHTML(level, years) {
    let yearsHTML = `
        <table class="years-table">
            <thead>
                <tr>
                    <th>${resxYearName}</th>
                    <th>${resxYearSort}</th>
                    <th>${resxActions}</th>
                </tr>
            </thead>
            <tbody>
            ${years && years.length > 0 ? years.map(yearRowHTML).join('') : `<tr><td colspan="3" style="text-align:center;color:#888;">${resxNoYears}</td></tr>`}
            </tbody>
        </table>
    `;
    return `
        <div class="level-box" data-level-code="${level.levelCode}">
            <div class="level-header">
                <h3 style="display:inline-block;margin-right:16px;">${level.levelName}</h3>
                <button class="btn-table add add-year-btn" data-level-code="${level.levelCode}" style="margin-right:8px;">
                    <i class="fas fa-plus"></i> ${resxAddYear}
                </button>
            </div>
            <div class="years-list">${yearsHTML}</div>
            <div style="text-align:right;margin-top:8px;">
                <button class="btn-table add add-level-btn">
                    <i class="fas fa-plus"></i> ${resxAddLevel}
                </button>
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
                <button class="btn-table edit edit-year-btn" data-year-code="${year.yearCode}" data-level-code="${year.levelCode}">
                    <i class="fas fa-pencil"></i> ${resxEdit}
                </button>
                <button class="btn-table delete delete-year-btn" data-year-code="${year.yearCode}" data-level-code="${year.levelCode}">
                    <i class="fas fa-trash"></i> ${resxDelete}
                </button>
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
                levelsContainer.innerHTML = `<div style="color:#b33c3c;font-weight:600;">${resxNoActiveEduYear}</div>`;
                return;
            }
            if (!data.levels || data.levels.length === 0) {
                levelsContainer.innerHTML = `
                    <div style="color:#b33c3c;font-weight:600;">${resxNoLevels}</div>
                    <div style="margin-top:16px;">
                        <button class="btn-table add add-level-btn">
                            <i class="fas fa-plus"></i> ${resxAddLevel}
                        </button>
                    </div>`;
                addLevelsAndYearsListeners();
                return;
            }
            data.levels.forEach(level => {
                const box = document.createElement('div');
                box.innerHTML = levelBoxHTML(level, level.years);
                levelsContainer.appendChild(box.firstElementChild);
            });
            addLevelsAndYearsListeners();
        })
        .catch(err => {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Error loading data',
            });
            levelsContainer.innerHTML = `<div style="color:#b33c3c;font-weight:600;">Error loading data</div>`;
        });
}

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
            Swal.fire({
                title: resxDelete + ": " + resxYearName + "?",
                text: "This action cannot be undone.",
                icon: "warning",
                showCancelButton: true,
                confirmButtonText: resxDelete,
                cancelButtonText: resxNo,
                confirmButtonColor: "#d33"
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch('/EduYear/DeleteYear', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ yearCode })
                    })
                        .then(r => {
                            if (!r.ok) return r.text().then(t => { throw new Error(t); });
                            Swal.fire({
                                icon: 'success',
                                title: resxDelete,
                                text: 'Year deleted successfully.',
                                timer: 1200,
                                showConfirmButton: false
                            });
                            loadLevelsAndYears();
                        })
                        .catch(err => {
                            Swal.fire({
                                icon: 'error',
                                title: resxDelete,
                                text: err.message || resxLoading,
                            });
                        });
                }
            });
        };
    });
}

// Initial load (use only one way to avoid duplicate call)
document.addEventListener('DOMContentLoaded', function () {
    loadEduYears();
});