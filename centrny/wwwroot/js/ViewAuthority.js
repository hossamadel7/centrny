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

    var selectedGroup = null, selectedGroupName = "";
    var selectedPage = null, selectedPageName = "";

    if (!rootSelect) return;

    rootSelect.addEventListener('change', function () {
        var rootId = rootSelect.value;
        groupsList.innerHTML = '';
        pagesList.innerHTML = '';
        selectedGroup = selectedPage = null;
        permissionForm.style.display = "none";
        if (!rootId) {
            infoForms.style.display = "none";
            return;
        }

        // Load groups
        fetch('/ViewAuthority/GetGroupsForRoot?rootId=' + encodeURIComponent(rootId))
            .then(res => res.json())
            .then(groups => {
                if (groups.length === 0) {
                    groupsList.innerHTML = '<li class="list-group-item text-muted">No groups found.</li>';
                } else {
                    groups.forEach(g => {
                        var li = document.createElement('li');
                        li.className = "list-group-item";
                        li.innerHTML = `<span>${g.groupName}</span>
                            <button type="button" class="btn btn-xs btn-primary choose-btn" data-code="${g.groupCode}" data-name="${g.groupName}">+</button>`;
                        groupsList.appendChild(li);
                    });
                }
            });

        // Load pages
        fetch('/ViewAuthority/GetPagesForRoot?rootId=' + encodeURIComponent(rootId))
            .then(res => res.json())
            .then(pages => {
                if (pages.length === 0) {
                    pagesList.innerHTML = '<li class="list-group-item text-muted">No pages found.</li>';
                } else {
                    pages.forEach(p => {
                        var li = document.createElement('li');
                        li.className = "list-group-item";
                        li.innerHTML = `<span>${p.pageName}</span>
                            <button type="button" class="btn btn-xs btn-primary choose-btn" data-code="${p.pageCode}" data-name="${p.pageName}">+</button>`;
                        pagesList.appendChild(li);
                    });
                }
            });

        infoForms.style.display = "";
    });

    // Delegate click for group +
    groupsList.addEventListener('click', function (e) {
        if (e.target.classList.contains('choose-btn')) {
            Array.from(groupsList.querySelectorAll('.choose-btn')).forEach(btn => btn.classList.remove('selected'));
            e.target.classList.add('selected');
            selectedGroup = e.target.getAttribute('data-code');
            selectedGroupName = e.target.getAttribute('data-name');
            showPermissionFormIfReady();
        }
    });

    // Delegate click for page +
    pagesList.addEventListener('click', function (e) {
        if (e.target.classList.contains('choose-btn')) {
            Array.from(pagesList.querySelectorAll('.choose-btn')).forEach(btn => btn.classList.remove('selected'));
            e.target.classList.add('selected');
            selectedPage = e.target.getAttribute('data-code');
            selectedPageName = e.target.getAttribute('data-name');
            showPermissionFormIfReady();
        }
    });

    function showPermissionFormIfReady() {
        if (selectedGroup && selectedPage) {
            permGroupName.textContent = selectedGroupName;
            permPageName.textContent = selectedPageName;
            canInsert.checked = false;
            canUpdate.checked = false;
            canDelete.checked = false;
            permissionForm.style.display = "";
        }
    }

    if (permForm) {
        permForm.addEventListener('submit', function (e) {
            e.preventDefault();
            if (!selectedGroup || !selectedPage) return;
            var payload = {
                groupCode: parseInt(selectedGroup),
                pageCode: parseInt(selectedPage),
                canInsert: canInsert.checked,
                canUpdate: canUpdate.checked,
                canDelete: canDelete.checked
            };

            fetch('/ViewAuthority/SaveGroupPagePermission', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        alert("Permission saved!");
                        permissionForm.style.display = "none";
                        // Optionally reset selection
                        Array.from(groupsList.querySelectorAll('.choose-btn')).forEach(btn => btn.classList.remove('selected'));
                        Array.from(pagesList.querySelectorAll('.choose-btn')).forEach(btn => btn.classList.remove('selected'));
                        selectedGroup = selectedPage = null;
                    } else {
                        alert("Save failed.");
                    }
                });
        });
    }
})();