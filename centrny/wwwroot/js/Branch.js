// === Localized Branch Management JS with Center/Branch Limit and NESTED Layout, RTL/LTR action alignment, edit/delete for branches/halls ===

// SweetAlert2 helpers
function swalSuccess(msg) {
    Swal.fire({
        icon: 'success',
        title: '',
        text: msg,
        showConfirmButton: false,
        timer: 1600
    });
}
function swalError(msg) {
    Swal.fire({
        icon: 'error',
        title: '',
        text: msg,
        showConfirmButton: true
    });
}
function swalWarning(msg) {
    Swal.fire({
        icon: 'warning',
        title: '',
        text: msg,
        showConfirmButton: true
    });
}
function swalInfo(msg) {
    Swal.fire({
        icon: 'info',
        title: '',
        text: msg,
        showConfirmButton: true
    });
}
function swalConfirm(msg, confirmBtnText, cancelBtnText) {
    return Swal.fire({
        icon: 'warning',
        title: '',
        text: msg,
        showCancelButton: true,
        confirmButtonText: confirmBtnText || 'Yes',
        cancelButtonText: cancelBtnText || 'Cancel'
    });
}

function getJsString(key) {
    key = key.replace(/-([a-z])/g, function (g) { return g[1].toUpperCase(); });
    let v = $('#js-localization').data(key);
    if (typeof v === "undefined") v = $('#js-localization').data(key.charAt(0).toUpperCase() + key.slice(1));
    if (typeof v === "undefined") v = $('#js-localization').data(key.replace(/[A-Z]/g, m => '-' + m.toLowerCase()));
    return v;
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

    $('#addCenterModalLabel').text(getJsString('modalTitleAddCenter'));
    $('#label-center-name').text(getJsString('labelCenterName'));
    $('#button-save-center').html('<i class="fas fa-save"></i> ' + getJsString('buttonSave'));

    $('#addBranchModalLabel').text(getJsString('modalTitleAddBranch'));
    $('#label-branch-name').text(getJsString('labelBranchName'));
    $('#button-save-branch').html('<i class="fas fa-save"></i> ' + getJsString('buttonSave'));

    $('#editBranchModalLabel').text(getJsString('modalTitleEditBranch'));
    $('#label-branch-name-edit').text(getJsString('labelBranchName'));
    $('#label-branch-address-edit').text(getJsString('labelBranchAddress'));
    $('#label-branch-phone-edit').text(getJsString('labelBranchPhone'));
    $('#label-branch-starttime-edit').text(getJsString('labelBranchStartTime'));
    $('#button-cancel-edit-branch').text(getJsString('buttonCancel'));
    $('#button-save-branch-edit').html('<i class="fas fa-pencil"></i> ' + getJsString('buttonSaveChanges'));
}

// ---- Global State -----
let currentRootCode = null;
let rootLimit = 0;
let isCenterUser = false;
let ownerNameByRoot = {}; // rootCode => ownerName

$(document).ready(function () {
    setBranchLabels();

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
            await loadRootConfig(rootCode);
            await loadCentersAndBranches(rootCode);
        });
    } else {
        const fixedRootCode = $('#fixedRootCode').val();
        loadRootConfig(parseInt(fixedRootCode));
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

    $('#addCenterForm').on('submit', async function (e) {
        e.preventDefault();
        await submitAddCenter();
    });
    $('#addBranchForm').on('submit', async function (e) {
        e.preventDefault();
        await submitAddBranch();
    });
    $('#editBranchForm').on('submit', async function (e) {
        e.preventDefault();
        await submitEditBranch();
    });
});

async function loadRoots() {
    const res = await fetch('/Branch/GetRootsIsCenterTrue', { cache: "reload" });
    const roots = await res.json();
    const dropdown = $('#rootDropdown');
    dropdown.html(`<option value="">${getJsString('dropdownSelectRootDefault')}</option>`);
    roots.forEach(root => {
        dropdown.append(`<option value="${root.rootCode}">${root.rootName}</option>`);
        // API returns rootOwner, not ownerName
        ownerNameByRoot[root.rootCode] = root.rootOwner;
    });
}

async function loadRootConfig(rootCode) {
    const res = await fetch(`/Branch/GetRootConfig?rootCode=${rootCode}`, { cache: "reload" });
    if (!res.ok) { rootLimit = 0; isCenterUser = false; return; }
    const data = await res.json();
    rootLimit = data.no_Of_Center || 0;
    isCenterUser = data.isCenter;
    ownerNameByRoot[rootCode] = data.ownerName;
}

async function loadCentersAndBranches(rootCode) {
    currentRootCode = rootCode;
    const centerRes = await fetch(`/Branch/GetCentersByRoot?rootCode=${rootCode}`, { cache: "reload" });
    const centers = await centerRes.json();
    const branchRes = await fetch(`/Branch/GetBranchesByRootCode?rootCode=${rootCode}`, { cache: "reload" });
    const branches = await branchRes.json();

    // Alignment for LTR/RTL
    const isRtl = $("html").attr("dir") === "rtl";
    const branchActionClass = "branch-actions";
    const hallActionClass = "hall-actions";
    const userId = window.userId || 0;

    // Limit logic
    const total = centers.length + branches.length;
    const atLimit = total >= rootLimit;

    if (atLimit) {
        if ($('#limit-alert').length === 0)
            $('#centerBranchSection').prepend('<div id="limit-alert" class="alert alert-warning"></div>');
        $('#limit-alert').text(getJsString('alertCenterBranchLimit')).show();
        $('#add-center-btn').hide();
        swalWarning(getJsString('alertCenterBranchLimit'));
    } else {
        $('#limit-alert').hide();
        $('#add-center-btn').show();
    }

    // Add Center Modal setup (now fills hidden fields)
    $('#add-center-btn').off().on('click', function () {
        $('#centerRootCode').val(currentRootCode);
        $('#centerOwnerName').val(ownerNameByRoot[currentRootCode] || '');
        $('#centerInsertUser').val(window.userId);
        $('#centerInsertTime').val(new Date().toISOString());
        $('#centerIsActive').val(true);
        $('#CenterName').val('');
        $('#CenterAddress').val('');
        $('#CenterPhone').val('');
        $('#addCenterModal').modal('show');
    });

    const centerList = $('#centerList');
    centerList.html('');
    if (centers.length > 0) {
        for (const center of centers) {
            let centerHtml = `
                <li class="list-group-item" style="background:#f8f9fa">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                        <span style="font-weight:bold">${center.centerName}</span>
                        <span class="${branchActionClass}">
                            <button class="btn-table add add-branch-btn" data-center-code="${center.centerCode}">
                                <i class="fas fa-plus"></i> ${getJsString('buttonAddBranch')}
                            </button>
                            <button class="btn-table delete delete-center-btn"
                                data-center-code="${center.centerCode}">
                                <i class="fas fa-trash"></i> ${getJsString('buttonDeleteCenter')}
                            </button>
                        </span>
                    </div>
                    <ul class="list-group mt-2 ms-3" id="center-${center.centerCode}-branches"></ul>
                </li>`;
            centerList.append(centerHtml);

            let branchList = $(`#center-${center.centerCode}-branches`);
            let centerBranches = branches.filter(b => Number(b.centerCode) === Number(center.centerCode));
            if (centerBranches.length > 0) {
                for (const branch of centerBranches) {
                    let hallsHtml = '';
                    let addHallBtnHtml = '';
                    if (isCenterUser) {
                        let halls = await fetchHalls(branch.branchCode);
                        if (halls.length > 0) {
                            hallsHtml = `<ul class="list-group mt-2 ms-3">` +
                                halls.map(hall =>
                                    `<li class="list-group-item d-flex justify-content-between align-items-center">
                                        <span>
                                            ${hall.hallName} <span class="badge bg-secondary">${hall.hallCapacity}</span>
                                        </span>
                                        <span class="hall-actions">
                                            <button class="btn-table edit edit-hall-btn" data-hall-code="${hall.hallCode}" data-hall-name="${hall.hallName}" data-hall-capacity="${hall.hallCapacity}">
                                                <i class="fas fa-pencil"></i>
                                            </button>
                                            <button class="btn-table delete delete-hall-btn" data-hall-code="${hall.hallCode}">
                                                <i class="fas fa-trash"></i>
                                            </button>
                                        </span>
                                    </li>`
                                ).join('') +
                                `</ul>`;
                        } else {
                            hallsHtml = `<div class="text-muted ms-3">${getJsString('listNoHalls')}</div>`;
                        }
                        addHallBtnHtml = `
                        <button class="btn btn-outline-primary add-hall-btn mt-2" data-branch-code="${branch.branchCode}">
                            <i class="fas fa-plus"></i> ${getJsString('buttonAddHall')}
                        </button>`;
                    }

                    // Get all branch props for edit modal, add as data attributes
                    branchList.append(`
                        <li class="list-group-item branch-card d-flex flex-column align-items-start">
                            <div class="d-flex align-items-center" style="width:100%;">
                                <span style="flex:1;text-align:left;">${branch.branchName}</span>
                                <span class="${branchActionClass}">
                                    <button class="btn-table edit edit-branch-btn"
                                        data-branch-code="${branch.branchCode}"
                                        data-branch-name="${branch.branchName}"
                                        data-branch-address="${branch.address || ''}"
                                        data-branch-phone="${branch.phone || ''}"
                                        data-branch-starttime="${branch.startTime || ''}">
                                        <i class="fas fa-pencil"></i>
                                    </button>
                                    <button class="btn-table delete delete-branch-btn"
                                        data-branch-code="${branch.branchCode}">
                                        <i class="fas fa-trash"></i>
                                    </button>
                                </span>
                            </div>
                            ${hallsHtml}
                            ${addHallBtnHtml}
                        </li>
                    `);
                }
            } else {
                branchList.append(`<li class="list-group-item text-muted">${getJsString('listNoBranchesFound')}</li>`);
            }
        }
    } else {
        centerList.html(`<li class="list-group-item text-muted">${getJsString('listNoCentersFound')}</li>`);
    }

    $('.delete-center-btn').off().on('click', function () {
        const centerCode = $(this).attr('data-center-code');
        swalConfirm(getJsString('confirmDeleteCenter')).then((result) => {
            if (result.isConfirmed) {
                fetch(`/Branch/DeleteCenter?centerCode=${centerCode}`, { method: 'DELETE' })
                    .then(r => {
                        if (r.ok) {
                            loadCentersAndBranches(currentRootCode);
                            swalSuccess(getJsString('alertCenterDeleteSuccess'));
                        } else {
                            swalError(getJsString('alertCenterBranchLimit'));
                        }
                    })
                    .catch(() => swalError(getJsString('alertCenterBranchLimit')));
            }
        });
    });

    $('.delete-branch-btn').off().on('click', function () {
        const branchCode = $(this).attr('data-branch-code');
        swalConfirm(getJsString('confirmDeleteBranch')).then((result) => {
            if (result.isConfirmed) {
                fetch(`/Branch/DeleteBranch?branchCode=${branchCode}`, { method: 'DELETE' })
                    .then(r => {
                        if (r.ok) {
                            loadCentersAndBranches(currentRootCode);
                            swalSuccess(getJsString('alertBranchDeleteSuccess'));
                        } else {
                            swalError(getJsString('alertCenterBranchLimit'));
                        }
                    })
                    .catch(() => swalError(getJsString('alertCenterBranchLimit')));
            }
        });
    });

    // Edit Branch button - now opens modal with details
    $('.edit-branch-btn').off().on('click', function () {
        $('#editBranchCode').val($(this).attr('data-branch-code'));
        $('#editBranchName').val($(this).attr('data-branch-name'));
        $('#editBranchAddress').val($(this).attr('data-branch-address'));
        $('#editBranchPhone').val($(this).attr('data-branch-phone'));
        $('#editBranchStartTime').val($(this).attr('data-branch-starttime'));

        var myModal = new bootstrap.Modal(document.getElementById('editBranchModal'));
        myModal.show();
    });

    $('.add-branch-btn').off().on('click', function () {
        const centerCode = $(this).attr('data-center-code');
        $('#branchRootCode').val(currentRootCode);
        $('#branchCenterCode').val(centerCode);
        $('#branchInsertUser').val(window.userId);
        $('#branchInsertTime').val(new Date().toISOString());
        $('#branchIsActive').val(true);
        $('#BranchName').val('');
        $('#BranchAddress').val('');
        $('#BranchPhone').val('');
        $('#BranchStartTime').val('');
        $('#addBranchModal').modal('show');
    });

    $('.add-hall-btn').off().on('click', function () {
        const branchCode = $(this).attr('data-branch-code');
        openAddHallModal(currentRootCode, branchCode);
    });

    $('.edit-hall-btn').off().on('click', function () {
        openEditHallModal(this);
    });

    $('.delete-hall-btn').off().on('click', function () {
        const hallCode = $(this).attr('data-hall-code');
        swalConfirm(getJsString('confirmDeleteHall')).then((result) => {
            if (result.isConfirmed) {
                deleteHall(hallCode);
            }
        });
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

// Helper: safely hide a Bootstrap modal without throwing
function safeHideModalById(id) {
    try {
        const el = document.getElementById(id);
        if (!el) return;
        const hasBootstrap = typeof window.bootstrap !== 'undefined' && bootstrap.Modal;
        if (hasBootstrap && typeof bootstrap.Modal.getInstance === 'function') {
            const instance = bootstrap.Modal.getInstance(el);
            if (instance) {
                instance.hide();
                return;
            }
        }
        // Fallback to jQuery plugin if available
        if (typeof $ !== 'undefined' && typeof $('#' + id).modal === 'function') {
            $('#' + id).modal('hide');
        }
    } catch (e) {
        // swallow UI errors
        console && console.debug && console.debug('safeHideModalById error:', e);
    }
}

async function submitAddHall() {
    const hallName = $('#HallName').val();
    const hallCapacity = parseInt($('#HallCapacity').val(), 10);
    const rootCode = parseInt($('#hallRootCode').val(), 10);
    const branchCode = parseInt($('#hallBranchCode').val(), 10);

    if (!hallName || isNaN(hallCapacity) || isNaN(rootCode) || isNaN(branchCode)) {
        swalWarning(getJsString('alertFillAllFields'));
        return;
    }

    const submitBtn = $('#addHallForm button[type="submit"]');
    const originalText = '<i class="fas fa-plus"></i> ' + getJsString('buttonAddHall');
    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

    let res;
    try {
        res = await fetch('/Branch/AddHall', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                hallName, hallCapacity, rootCode, branchCode
            })
        });
    } catch (err) {
        swalError(getJsString('alertHallAddError'));
        submitBtn.prop('disabled', false).html(originalText);
        return;
    }

    if (res.ok) {
        safeHideModalById('addHallModal');
        setTimeout(async () => {
            await loadCentersAndBranches(currentRootCode);
            swalSuccess(getJsString('alertHallAddSuccess'));
        }, 250);
    } else {
        const text = await res.text();
        swalError(text || getJsString('alertHallAddFailed'));
    }
    submitBtn.prop('disabled', false).html(originalText);
}

async function submitEditHall() {
    const hallCode = $('#editHallCode').val();
    const hallName = $('#editHallName').val();
    const hallCapacity = parseInt($('#editHallCapacity').val(), 10);

    const submitBtn = $('#editHallForm button[type="submit"]');
    const originalText = '<i class="fas fa-pencil"></i> ' + getJsString('buttonSaveChanges');
    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

    let res;
    try {
        res = await fetch('/Branch/EditHall', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                hallCode, hallName, hallCapacity
            })
        });
    } catch (err) {
        swalError(getJsString('alertHallUpdateError'));
        submitBtn.prop('disabled', false).html(originalText);
        return;
    }

    if (res.ok) {
        safeHideModalById('editHallModal');
        setTimeout(async () => {
            await loadCentersAndBranches(currentRootCode);
            swalSuccess(getJsString('alertHallUpdateSuccess'));
        }, 250);
    } else {
        const text = await res.text();
        swalError(text || getJsString('alertHallUpdateFailed'));
    }
    submitBtn.prop('disabled', false).html(originalText);
}

async function deleteHall(hallCode) {
    try {
        const res = await fetch(`/Branch/DeleteHall?hallCode=${hallCode}`, { method: 'DELETE', cache: "reload" });
        if (res.ok) {
            await loadCentersAndBranches(currentRootCode);
            swalSuccess(getJsString('alertHallDeleteSuccess'));
        } else {
            const text = await res.text();
            swalError(text || getJsString('alertHallDeleteFailed'));
        }
    } catch (err) {
        swalError(getJsString('alertHallDeleteError'));
    }
}

async function fetchHalls(branchCode) {
    const res = await fetch(`/Branch/GetHallsByBranch?branchCode=${branchCode}`, { cache: "reload" });
    if (!res.ok) return [];
    return await res.json();
}

// --------- Add Center with all required fields -------------
async function submitAddCenter() {
    const centerName = $('#CenterName').val();
    const centerAddress = $('#CenterAddress').val();
    const centerPhone = $('#CenterPhone').val();
    const ownerName = $('#centerOwnerName').val();
    const rootCode = parseInt($('#centerRootCode').val(), 10);
    const insertUser = parseInt($('#centerInsertUser').val(), 10) || 0;
    const insertTime = $('#centerInsertTime').val();
    const isActive = true;

    if (!centerName || !centerAddress || !centerPhone || isNaN(rootCode)) {
        swalWarning(getJsString('alertFillAllFields'));
        return;
    }
    const submitBtn = $('#addCenterForm button[type="submit"]');
    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');
    try {
        const res = await fetch('/Branch/AddCenter', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                CenterName: centerName,
                CenterAddress: centerAddress,
                CenterPhone: centerPhone,
                OwnerName: ownerName,
                RootCode: rootCode,
                InsertUser: insertUser,
                InsertTime: insertTime,
                IsActive: isActive
            })
        });
        if (res.ok) {
            $('#addCenterModal').modal('hide');
            setTimeout(async () => {
                await loadCentersAndBranches(currentRootCode);
                swalSuccess(getJsString('alertCenterAddSuccess'));
            }, 250);
        } else {
            const text = await res.text();
            swalError(text || getJsString('alertCenterBranchLimit'));
        }
    } finally {
        submitBtn.prop('disabled', false).html('<i class="fas fa-save"></i> ' + getJsString('buttonSave'));
    }
}

// --------- Add Branch with all required fields -------------
async function submitAddBranch() {
    const branchName = $('#BranchName').val();
    const address = $('#BranchAddress').val();
    const phone = $('#BranchPhone').val();
    const startTime = $('#BranchStartTime').val();
    const rootCode = parseInt($('#branchRootCode').val(), 10);
    const centerCode = parseInt($('#branchCenterCode').val(), 10);
    const insertUser = parseInt($('#branchInsertUser').val(), 10) || 0;
    const insertTime = $('#branchInsertTime').val();
    const isActive = true;

    if (!branchName || !address || !phone || !startTime || isNaN(rootCode) || isNaN(centerCode)) {
        swalWarning(getJsString('alertFillAllFields'));
        return;
    }
    const submitBtn = $('#addBranchForm button[type="submit"]');
    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');
    try {
        const res = await fetch('/Branch/AddBranch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                BranchName: branchName,
                Address: address,
                Phone: phone,
                StartTime: startTime,
                CenterCode: centerCode,
                RootCode: rootCode,
                InsertUser: insertUser,
                InsertTime: insertTime,
                IsActive: isActive
            })
        });
        if (res.ok) {
            $('#addBranchModal').modal('hide');
            setTimeout(async () => {
                await loadCentersAndBranches(currentRootCode);
                swalSuccess(getJsString('alertBranchAddSuccess'));
            }, 250);
        } else {
            const text = await res.text();
            swalError(text || getJsString('alertCenterBranchLimit'));
        }
    } finally {
        submitBtn.prop('disabled', false).html('<i class="fas fa-save"></i> ' + getJsString('buttonSave'));
    }
}
function updateBranchInDOM(branchCode, branchName, address, phone, startTime) {
    // Find the edit button for this branch
    var editBtn = $('.edit-branch-btn[data-branch-code="' + branchCode + '"]');
    if (editBtn.length === 0) return; // Not found

    // Update data attributes for edit modal
    editBtn.attr('data-branch-name', branchName);
    editBtn.attr('data-branch-address', address);
    editBtn.attr('data-branch-phone', phone);
    editBtn.attr('data-branch-starttime', startTime);

    // Update branch name in DOM (update the first span inside .d-flex)
    var branchLi = editBtn.closest('li.list-group-item.branch-card');
    branchLi.find('> .d-flex > span').first().text(branchName);

    // If you display address/phone/startTime in the branch card, update them here too
    // Example:
    // branchLi.find('.branch-address').text(address);
    // branchLi.find('.branch-phone').text(phone);
    // branchLi.find('.branch-starttime').text(startTime);
}
// --------- Edit Branch handler -------------
async function submitEditBranch() {
    const branchCode = $('#editBranchCode').val();
    const branchName = $('#editBranchName').val();
    const address = $('#editBranchAddress').val();
    const phone = $('#editBranchPhone').val();
    const startTime = $('#editBranchStartTime').val();

    if (!branchName || !address || !phone || !startTime) {
        swalWarning(getJsString('alertFillAllFields'));
        return;
    }
    const submitBtn = $('#button-save-branch-edit');
    submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

    // Network try/catch only
    let res;
    try {
        res = await fetch('/Branch/EditBranch', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                BranchCode: branchCode,
                BranchName: branchName,
                Address: address,
                Phone: phone,
                StartTime: startTime
            })
        });
    } catch (err) {
        // Real network error
        swalError(getJsString('alertBranchUpdateError'));
        submitBtn.prop('disabled', false).html('<i class="fas fa-pencil"></i> ' + getJsString('buttonSaveChanges'));
        return;
    }

    if (res.ok) {
        // Update DOM first so user sees the change even if hiding the modal fails
        updateBranchInDOM(branchCode, branchName, address, phone, startTime);

        // Safely hide the modal without throwing
        safeHideModalById('editBranchModal');

        swalSuccess(getJsString('alertBranchUpdateSuccess'));
    } else {
        const text = await res.text();
        swalError(text || getJsString('alertBranchUpdateFailed'));
    }

    submitBtn.prop('disabled', false).html('<i class="fas fa-pencil"></i> ' + getJsString('buttonSaveChanges'));
}