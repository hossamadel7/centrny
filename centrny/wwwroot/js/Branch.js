// === Localized Branch Management JS ===

// Helper to access localized strings from #js-localization
function getJsString(key) {
    // Convert dash-case to camelCase
    key = key.replace(/-([a-z])/g, function (g) { return g[1].toUpperCase(); });
    return $('#js-localization').data(key);
}

function setBranchLabels() {
    $('#branch-title').text(getJsString('titleManageBranches'));
    $('#label-select-root').text(getJsString('labelSelectRoot'));
    $('#dropdown-select-root-default').text(getJsString('dropdownSelectRootDefault'));
    $('#alert-center').text(getJsString('alertCenter'));
    $('#alert-user').text(getJsString('alertUser'));
    $('#section-centers').text(getJsString('sectionCenters'));
    $('#section-branches').text(getJsString('sectionBranches'));

    $('#addHallModalLabel').text(getJsString('modalTitleAddHall'));
    $('#label-hall-name').text(getJsString('labelHallName'));
    $('#label-hall-capacity').text(getJsString('labelHallCapacity'));
    $('#button-cancel').text(getJsString('buttonCancel'));
    $('#button-add-hall').html('<i class="fas fa-plus"></i> ' + getJsString('buttonAddHall'));

    $('#editHallModalLabel').text(getJsString('modalTitleEditHall'));
    $('#label-hall-name-edit').text(getJsString('labelHallName'));
    $('#label-hall-capacity-edit').text(getJsString('labelHallCapacity'));
    $('#button-cancel-edit').text(getJsString('buttonCancel'));
    $('#button-save-changes').html('<i class="fas fa-pencil"></i> ' + getJsString('buttonSaveChanges'));
}

$(document).ready(function () {
    setBranchLabels();

    // Branch JS logic (all UI strings are localized)
    if (typeof userRootCode === 'undefined') {
        $('#centerBranchSection').hide();
        return;
    }

    if (userRootCode === 1) {
        loadRoots();
        $('#rootDropdown').on('change', async function () {
            const rootCode = this.value;
            if (!rootCode) {
                $('#centerBranchSection').hide();
                return;
            }
            await loadCentersAndBranches(rootCode);
        });
    } else {
        const fixedRootCode = $('#fixedRootCode').val();
        loadCentersAndBranches(parseInt(fixedRootCode));
    }

    $('#addHallForm').on('submit', async function (e) {
        e.preventDefault();
        await submitAddHall();
    });
    $('#editHallForm').on('submit', async function (e) {
        e.preventDefault();
        await submitEditHall();
    });
});

let currentRootCode = null;

async function loadRoots() {
    const res = await fetch('/Branch/GetRootsIsCenterTrue');
    const roots = await res.json();
    const dropdown = $('#rootDropdown');
    dropdown.html(`<option value="">${getJsString('dropdownSelectRootDefault')}</option>`);
    roots.forEach(root => {
        dropdown.append(`<option value="${root.rootCode}">${root.rootName}</option>`);
    });
}

async function loadCentersAndBranches(rootCode) {
    currentRootCode = rootCode;

    const centerRes = await fetch(`/Branch/GetCentersByRoot?rootCode=${rootCode}`);
    const centers = await centerRes.json();

    const branchRes = await fetch(`/Branch/GetBranchesByRootCode?rootCode=${rootCode}`);
    const branches = await branchRes.json();

    for (const branch of branches) {
        branch.halls = await fetchHalls(branch.branchCode);
        branch.rootCode = rootCode;
    }

    const centerList = $('#centerList');
    centerList.html('');
    if (centers.length > 0) {
        centers.forEach(center => {
            centerList.append(`<li class="list-group-item">${center.centerName}</li>`);
        });
    } else {
        centerList.html(`<li class="list-group-item text-muted">${getJsString('listNoCentersFound')}</li>`);
    }

    const branchList = $('#branchList');
    branchList.html('');
    if (branches.length > 0) {
        branches.forEach(branch => {
            let hallHtml = '';
            if (branch.halls.length > 0) {
                hallHtml = `<ul class="list-group mt-2 ms-3">` +
                    branch.halls.map(hall =>
                        `<li class="list-group-item py-1 px-2 d-flex justify-content-between align-items-center">
                            <span>
                                ${hall.hallName} <span class="badge bg-secondary">${hall.hallCapacity}</span>
                            </span>
                            <span class="hall-actions">
                                <button class="btn-table edit edit-hall-btn"
                                    data-hall-code="${hall.hallCode}" 
                                    data-hall-name="${hall.hallName}" 
                                    data-hall-capacity="${hall.hallCapacity}">
                                    <i class="fas fa-pencil"></i> ${getJsString('buttonEditHall')}
                                </button>
                                <button class="btn-table delete delete-hall-btn ms-2"
                                    data-hall-code="${hall.hallCode}">
                                    <i class="fas fa-trash"></i> ${getJsString('buttonDeleteHall')}
                                </button>
                            </span>
                        </li>`
                    ).join('') +
                    `</ul>`;
            } else {
                hallHtml = `<div class="text-muted ms-3">${getJsString('listNoHalls')}</div>`;
            }

            branchList.append(`
                <li class="list-group-item d-flex flex-column align-items-start">
                    <div class="d-flex w-100 justify-content-between align-items-center">
                        <span>${branch.branchName}</span>
                        <button class="btn-table add add-hall-btn ms-2"
                            data-branch-code="${branch.branchCode}"
                            data-root-code="${rootCode}">
                            <i class="fas fa-plus"></i> ${getJsString('buttonAddHall')}
                        </button>
                    </div>
                    ${hallHtml}
                </li>`);
        });
    } else {
        branchList.html(`<li class="list-group-item text-muted">${getJsString('listNoBranchesFound')}</li>`);
    }

    $('.add-hall-btn').off().on('click', function () {
        const branchCode = $(this).attr('data-branch-code');
        const rootCode = $(this).attr('data-root-code');
        openAddHallModal(rootCode, branchCode);
    });

    $('.edit-hall-btn').off().on('click', function () {
        openEditHallModal(this);
    });

    $('.delete-hall-btn').off().on('click', async function () {
        const hallCode = $(this).attr('data-hall-code');
        if (confirm(getJsString('confirmDeleteHall'))) {
            await deleteHall(hallCode);
        }
    });

    $('#centerBranchSection').show();
}

function openAddHallModal(rootCode, branchCode) {
    $('#hallRootCode').val(rootCode);
    $('#hallBranchCode').val(branchCode);
    $('#HallName').val('');
    $('#HallCapacity').val('');
    const submitBtn = $('#addHallForm button[type="submit"]');
    submitBtn.html('<i class="fas fa-plus"></i> ' + getJsString('buttonAddHall'));
    submitBtn.prop('disabled', false);
    var myModal = new bootstrap.Modal(document.getElementById('addHallModal'));
    myModal.show();
}

function openEditHallModal(btn) {
    $('#editHallCode').val($(btn).attr('data-hall-code'));
    $('#editHallName').val($(btn).attr('data-hall-name'));
    $('#editHallCapacity').val($(btn).attr('data-hall-capacity'));
    const submitBtn = $('#editHallForm button[type="submit"]');
    submitBtn.html('<i class="fas fa-pencil"></i> ' + getJsString('buttonSaveChanges'));
    submitBtn.prop('disabled', false);
    var myModal = new bootstrap.Modal(document.getElementById('editHallModal'));
    myModal.show();
}

async function submitAddHall() {
    const hallName = $('#HallName').val();
    const hallCapacity = parseInt($('#HallCapacity').val(), 10);
    const rootCode = parseInt($('#hallRootCode').val(), 10);
    const branchCode = parseInt($('#hallBranchCode').val(), 10);

    if (!hallName || isNaN(hallCapacity) || isNaN(rootCode) || isNaN(branchCode)) {
        alert(getJsString('alertFillAllFields'));
        return;
    }

    const submitBtn = $('#addHallForm button[type="submit"]');
    const originalText = '<i class="fas fa-plus"></i> ' + getJsString('buttonAddHall');
    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

    try {
        const res = await fetch('/Branch/AddHall', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                hallName, hallCapacity, rootCode, branchCode
            })
        });

        if (res.ok) {
            var myModalEl = document.getElementById('addHallModal');
            var modal = bootstrap.Modal.getInstance(myModalEl);
            modal.hide();
            await loadCentersAndBranches(currentRootCode);
            alert(getJsString('alertHallAddSuccess'));
        } else {
            alert(getJsString('alertHallAddFailed'));
        }
    } catch (err) {
        alert(getJsString('alertHallAddError'));
    } finally {
        submitBtn.prop('disabled', false).html(originalText);
    }
}

async function submitEditHall() {
    const hallCode = $('#editHallCode').val();
    const hallName = $('#editHallName').val();
    const hallCapacity = parseInt($('#editHallCapacity').val(), 10);

    const submitBtn = $('#editHallForm button[type="submit"]');
    const originalText = '<i class="fas fa-pencil"></i> ' + getJsString('buttonSaveChanges');
    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

    try {
        const res = await fetch('/Branch/EditHall', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                hallCode, hallName, hallCapacity
            })
        });

        if (res.ok) {
            var myModalEl = document.getElementById('editHallModal');
            var modal = bootstrap.Modal.getInstance(myModalEl);
            modal.hide();
            await loadCentersAndBranches(currentRootCode);
            alert(getJsString('alertHallUpdateSuccess'));
        } else {
            alert(getJsString('alertHallUpdateFailed'));
        }
    } catch (err) {
        alert(getJsString('alertHallUpdateError'));
    } finally {
        submitBtn.prop('disabled', false).html(originalText);
    }
}

async function deleteHall(hallCode) {
    try {
        const res = await fetch(`/Branch/DeleteHall?hallCode=${hallCode}`, { method: 'DELETE' });
        if (res.ok) {
            await loadCentersAndBranches(currentRootCode);
            alert(getJsString('alertHallDeleteSuccess'));
        } else {
            alert(getJsString('alertHallDeleteFailed'));
        }
    } catch (err) {
        alert(getJsString('alertHallDeleteError'));
    }
}

async function fetchHalls(branchCode) {
    const res = await fetch(`/Branch/GetHallsByBranch?branchCode=${branchCode}`);
    if (!res.ok) return [];
    return await res.json();
}