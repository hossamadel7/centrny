(function () {
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

        // Load all necessary data
        Promise.all([
            fetch('/ViewAuthority/GetGroupsForRoot?rootId=' + encodeURIComponent(rootId)).then(res => res.json()),
            fetch('/ViewAuthority/GetPagesForRoot?rootId=' + encodeURIComponent(rootId)).then(res => res.json()),
            fetch('/ViewAuthority/GetGroupPagesForRoot?rootId=' + encodeURIComponent(rootId)).then(res => res.json())
        ])
            .then(function ([groups, pages, groupPages]) {
                allGroups = groups;
                allPages = pages;
                existingGroupPages = groupPages;

                // Render groups
                if (groups.length === 0) {
                    groupsList.innerHTML = '<li class="list-group-item text-muted">No groups found.</li>';
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

                // Render pages (as checkboxes for multi-select)
                if (pages.length === 0) {
                    pagesList.innerHTML = '<li class="list-group-item text-muted">No pages found.</li>';
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

    // Delegate click for group +
    groupsList.addEventListener('click', function (e) {
        if (e.target.classList.contains('choose-btn')) {
            Array.from(groupsList.querySelectorAll('.choose-btn')).forEach(btn => btn.classList.remove('selected'));
            e.target.classList.add('selected');
            selectedGroup = e.target.getAttribute('data-code');
            selectedGroupName = e.target.getAttribute('data-name');
            // After group select, filter out pages that are already mapped with this group
            filterAvailablePages();
            if (addSelectedPagesBtn) addSelectedPagesBtn.style.display = "";
            selectedPages = [];
            permissionForm.style.display = "none";
            loadExistingGroupPagesTable();
        }
    });

    // Listen for checkbox changes to track selected pages
    pagesList.addEventListener('change', function (e) {
        if (e.target.classList.contains('page-checkbox')) {
            // Update selectedPages array
            selectedPages = Array.from(pagesList.querySelectorAll('.page-checkbox:checked')).map(cb => ({
                pageCode: parseInt(cb.value),
                pageName: cb.getAttribute('data-name')
            }));
        }
    });

    function filterAvailablePages() {
        if (!selectedGroup) return;
        pagesList.innerHTML = '';
        // Get page codes already mapped to this group
        var takenPageCodes = existingGroupPages
            .filter(gp => gp.groupCode == selectedGroup)
            .map(gp => gp.pageCode);

        var availablePages = allPages.filter(p => !takenPageCodes.includes(p.pageCode));
        if (availablePages.length === 0) {
            pagesList.innerHTML = '<li class="list-group-item text-muted">No pages available for this group.</li>';
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

    // Show permission form when addSelectedPagesBtn is clicked
    if (addSelectedPagesBtn) {
        addSelectedPagesBtn.addEventListener('click', function () {
            if (!selectedGroup) return;
            selectedPages = Array.from(pagesList.querySelectorAll('.page-checkbox:checked')).map(cb => ({
                pageCode: parseInt(cb.value),
                pageName: cb.getAttribute('data-name')
            }));
            if (selectedPages.length === 0) {
                alert("Please select at least one page.");
                return;
            }
            permGroupName.textContent = selectedGroupName;
            permPageName.textContent = selectedPages.map(p => p.pageName).join(', ');
            canInsert.checked = false;
            canUpdate.checked = false;
            canDelete.checked = false;
            permissionForm.style.display = "";
        });
    }

    // Load the existing GroupPage records for the selected group
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
                    existingTableBody.innerHTML = '<tr><td colspan="5" class="text-muted">No group-page permissions for this group.</td></tr>';
                } else {
                    records.forEach(function (row) {
                        var tr = document.createElement('tr');
                        tr.innerHTML =
                            `<td>${row.pageName}</td>
                            <td>${row.insertFlag ? "✔️" : ""}</td>
                            <td>${row.updateFlag ? "✔️" : ""}</td>
                            <td>${row.deleteFlag ? "✔️" : ""}</td>
                            <td><button class="btn btn-danger btn-xs delete-gp-btn" data-pagecode="${row.pageCode}" title="Delete">🗑️</button></td>`;
                        existingTableBody.appendChild(tr);
                    });
                }
                existingTableSection.style.display = "";
            });
    }

    // Event delegation for delete buttons
    existingTableBody.addEventListener('click', function (e) {
        if (e.target.classList.contains('delete-gp-btn')) {
            var pageCode = e.target.getAttribute('data-pagecode');
            if (confirm("Are you sure you want to delete this permission?")) {
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
                            // Also refresh the available pages list
                            if (currentRootId) rootSelect.dispatchEvent(new Event('change'));
                        } else {
                            alert("Delete failed.");
                        }
                    })
                    .catch(() => {
                        alert("Delete failed.");
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
                        alert("Permissions saved!");
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
                        alert("Save failed.");
                    }
                })
                .catch(() => {
                    alert("Save failed.");
                })
                .finally(() => {
                    if (saveBtn) saveBtn.disabled = false;
                });
        });
    }
})();