// === Localized Group-Page Permissions Management JS ===

function getJsString(key) {
    key = key.replace(/-([a-z])/g, function (g) { return g[1].toUpperCase(); });
    return $('#js-localization').data(key);
}

function setLabels() {
    $('#user-info-select-root').text(getJsString('userInfoSelectRoot'));
    $('#label-select-root').text(getJsString('labelSelectRoot'));
    $('#dropdown-select-root-default').text(getJsString('dropdownSelectRootDefault'));
    $('#groups-header').text(getJsString('groupsHeader'));
    $('#pages-header').text(getJsString('pagesHeader'));
    $('#button-add-selected-pages').text(getJsString('buttonAddSelectedPages'));
    $('#permissions-set').text(getJsString('permissionsSet'));
    $('#label-insert').text(getJsString('labelInsert'));
    $('#label-update').text(getJsString('labelUpdate'));
    $('#label-delete').text(getJsString('labelDelete'));
    $('#button-save-permission').text(getJsString('buttonSavePermission'));
    $('#existing-group-pages-header').text(getJsString('existingGroupPagesHeader'));
    $('#table-page').text(getJsString('tablePage'));
    $('#table-insert').text(getJsString('tableInsert'));
    $('#table-update').text(getJsString('tableUpdate'));
    $('#table-delete').text(getJsString('tableDelete'));
    $('#table-delete-page').text(getJsString('tableDeletePage'));
}

$(document).ready(function () {
    setLabels();

    var rootSelect = document.getElementById('rootSelect');
    var infoForms = document.getElementById('infoForms');
    var groupsList = document.getElementById('groupsList');
    var pagesList = document.getElementById('pagesList');
    var permissionForm = document.getElementById('permissionForm');
    var permForm = document.getElementById('permForm');
    var permGroupName = document.getElementById('permGroupName');
    var permPageName = document.getElementById('permPageName');
    var canInsert = document.getElementById('canInsert');
    var canUpdate = document.getElementById('canUpdate');
    var canDelete = document.getElementById('canDelete');
    var existingTableSection = document.getElementById('existingGroupPagesSection');
    var existingTableBody = document.getElementById('existingGroupPagesBody');
    var addSelectedPagesBtn = document.getElementById('addSelectedPagesBtn');

    var selectedGroup = null, selectedGroupName = "";
    var selectedPages = [];
    var currentRootId = null;
    var allGroups = [];
    var allPages = [];
    var existingGroupPages = [];

    if (!rootSelect) return;

    rootSelect.addEventListener('change', function () {
        var rootId = rootSelect.value;
        currentRootId = rootId;
        groupsList.innerHTML = '';
        pagesList.innerHTML = '';
        selectedGroup = null;
        selectedGroupName = "";
        selectedPages = [];
        permissionForm.style.display = "none";
        existingTableSection.style.display = "none";
        if (addSelectedPagesBtn) addSelectedPagesBtn.style.display = "none";
        if (!rootId) {
            infoForms.style.display = "none";
            return;
        }

        Promise.all([
            fetch('/ViewAuthority/GetGroupsForRoot?rootId=' + encodeURIComponent(rootId)).then(res => res.json()),
            fetch('/ViewAuthority/GetPagesForRoot?rootId=' + encodeURIComponent(rootId)).then(res => res.json()),
            fetch('/ViewAuthority/GetGroupPagesForRoot?rootId=' + encodeURIComponent(rootId)).then(res => res.json())
        ])
            .then(function ([groups, pages, groupPages]) {
                allGroups = groups;
                allPages = pages;
                existingGroupPages = groupPages;

                if (groups.length === 0) {
                    groupsList.innerHTML = `<li class="list-group-item text-muted">${getJsString('noGroupsFound')}</li>`;
                } else {
                    groupsList.innerHTML = '';
                    groups.forEach(g => {
                        var li = document.createElement('li');
                        li.className = "list-group-item";
                        li.innerHTML = `<span>${g.groupName}</span>
                        <button type="button" class="btn btn-xs btn-primary choose-btn" data-code="${g.groupCode}" data-name="${g.groupName}">+</button>`;
                        groupsList.appendChild(li);
                    });
                }

                if (pages.length === 0) {
                    pagesList.innerHTML = `<li class="list-group-item text-muted">${getJsString('noPagesFound')}</li>`;
                } else {
                    pagesList.innerHTML = '';
                    pages.forEach(p => {
                        var li = document.createElement('li');
                        li.className = "list-group-item";
                        li.innerHTML = `<label>
                          <input type="checkbox" class="page-checkbox" value="${p.pageCode}" data-name="${p.pageName}"> ${p.pageName}
                        </label>`;
                        pagesList.appendChild(li);
                    });
                }
                infoForms.style.display = "";
                if (addSelectedPagesBtn) addSelectedPagesBtn.style.display = "none";
            });
    });

    groupsList.addEventListener('click', function (e) {
        if (e.target.classList.contains('choose-btn')) {
            Array.from(groupsList.querySelectorAll('.choose-btn')).forEach(btn => btn.classList.remove('selected'));
            e.target.classList.add('selected');
            selectedGroup = e.target.getAttribute('data-code');
            selectedGroupName = e.target.getAttribute('data-name');
            filterAvailablePages();
            if (addSelectedPagesBtn) addSelectedPagesBtn.style.display = "";
            selectedPages = [];
            permissionForm.style.display = "none";
            loadExistingGroupPagesTable();
        }
    });

    pagesList.addEventListener('change', function (e) {
        if (e.target.classList.contains('page-checkbox')) {
            selectedPages = Array.from(pagesList.querySelectorAll('.page-checkbox:checked')).map(cb => ({
                pageCode: parseInt(cb.value),
                pageName: cb.getAttribute('data-name')
            }));
        }
    });

    function filterAvailablePages() {
        if (!selectedGroup) return;
        pagesList.innerHTML = '';
        var takenPageCodes = existingGroupPages
            .filter(gp => gp.groupCode == selectedGroup)
            .map(gp => gp.pageCode);

        var availablePages = allPages.filter(p => !takenPageCodes.includes(p.pageCode));
        if (availablePages.length === 0) {
            pagesList.innerHTML = `<li class="list-group-item text-muted">${getJsString('noPagesAvailable')}</li>`;
            if (addSelectedPagesBtn) addSelectedPagesBtn.style.display = "none";
        } else {
            availablePages.forEach(p => {
                var li = document.createElement('li');
                li.className = "list-group-item";
                li.innerHTML = `<label>
                  <input type="checkbox" class="page-checkbox" value="${p.pageCode}" data-name="${p.pageName}"> ${p.pageName}
                </label>`;
                pagesList.appendChild(li);
            });
            if (addSelectedPagesBtn) addSelectedPagesBtn.style.display = "";
        }
        selectedPages = [];
        permissionForm.style.display = "none";
    }

    if (addSelectedPagesBtn) {
        addSelectedPagesBtn.addEventListener('click', function () {
            if (!selectedGroup) return;
            selectedPages = Array.from(pagesList.querySelectorAll('.page-checkbox:checked')).map(cb => ({
                pageCode: parseInt(cb.value),
                pageName: cb.getAttribute('data-name')
            }));
            if (selectedPages.length === 0) {
                alert(getJsString('alertSelectAtLeastOnePage'));
                return;
            }
            permGroupName.textContent = selectedGroupName;
            permPageName.textContent = selectedPages.map(p => p.pageName).join(', ');
            canInsert.checked = false;
            canUpdate.checked = false;
            canDelete.checked = false;
            permissionForm.style.display = "";
            permissionForm.style.display = "";
            permissionForm.style.display = "";
            permissionForm.style.display = "";
            permissionForm.style.display = "block";
        });
    }

    function loadExistingGroupPagesTable() {
        if (!selectedGroup) {
            existingTableSection.style.display = "none";
            return;
        }
        fetch('/ViewAuthority/GetGroupPagesForGroup?groupCode=' + encodeURIComponent(selectedGroup))
            .then(res => res.json())
            .then(function (records) {
                existingTableBody.innerHTML = '';
                if (records.length === 0) {
                    existingTableBody.innerHTML = `<tr><td colspan="5" class="text-muted">${getJsString('noGroupPagePermissions')}</td></tr>`;
                } else {
                    records.forEach(function (row) {
                        var tr = document.createElement('tr');
                        tr.innerHTML =
                            `<td>${row.pageName}</td>
                            <td>${row.insertFlag ? "✔️" : ""}</td>
                            <td>${row.updateFlag ? "✔️" : ""}</td>
                            <td>${row.deleteFlag ? "✔️" : ""}</td>
                            <td><button class="btn btn-danger btn-xs delete-gp-btn" data-pagecode="${row.pageCode}" title="${getJsString('tableDeletePage')}">🗑️</button></td>`;
                        existingTableBody.appendChild(tr);
                    });
                }
                existingTableSection.style.display = "";
            });
    }

    existingTableBody.addEventListener('click', function (e) {
        if (e.target.classList.contains('delete-gp-btn')) {
            var pageCode = e.target.getAttribute('data-pagecode');
            if (confirm(getJsString('confirmDeletePermission'))) {
                fetch('/ViewAuthority/DeleteGroupPage', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        GroupCode: parseInt(selectedGroup),
                        PageCodes: [parseInt(pageCode)]
                    })
                })
                    .then(res => res.json())
                    .then(data => {
                        if (data.success) {
                            loadExistingGroupPagesTable();
                            if (currentRootId) rootSelect.dispatchEvent(new Event('change'));
                        } else {
                            alert(getJsString('alertDeleteFailed'));
                        }
                    })
                    .catch(() => {
                        alert(getJsString('alertDeleteFailed'));
                    });
            }
        }
    });

    if (permForm) {
        permForm.addEventListener('submit', function (e) {
            e.preventDefault();
            if (!selectedGroup || !selectedPages || selectedPages.length === 0) return;

            const saveBtn = permForm.querySelector('button[type="submit"]');
            if (saveBtn) saveBtn.disabled = true;

            var payload = {
                GroupCode: parseInt(selectedGroup),
                PageCodes: selectedPages.map(p => p.pageCode),
                InsertFlag: !!canInsert.checked,
                UpdateFlag: !!canUpdate.checked,
                DeleteFlag: !!canDelete.checked
            };

            fetch('/ViewAuthority/SaveGroupPagePermission', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        alert(getJsString('alertPermissionsSaved'));
                        permissionForm.style.display = "none";
                        Array.from(groupsList.querySelectorAll('.choose-btn')).forEach(btn => btn.classList.remove('selected'));
                        Array.from(pagesList.querySelectorAll('.page-checkbox')).forEach(cb => cb.checked = false);
                        permForm.reset();
                        selectedGroup = null;
                        selectedPages = [];
                        if (currentRootId) {
                            rootSelect.dispatchEvent(new Event('change'));
                        }
                        loadExistingGroupPagesTable();
                        if (addSelectedPagesBtn) addSelectedPagesBtn.style.display = "none";
                    } else {
                        alert(getJsString('alertSaveFailed'));
                    }
                })
                .catch(() => {
                    alert(getJsString('alertSaveFailed'));
                })
                .finally(() => {
                    if (saveBtn) saveBtn.disabled = false;
                });
        });
    }
});