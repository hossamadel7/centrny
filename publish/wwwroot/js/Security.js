let selectedRootCode = null;
let isUsernameTaken = false;
let loggedInUserCode = window.loggedInUserCode || 1; // Set this globally on login for group/user creation

function loadRoots(userType) {
    if (!userType) return;
    var isCenter = (userType === 'center');
    $.ajax({
        url: '/Security/GetRoots',
        type: 'GET',
        data: { isCenter: isCenter },
        success: function (data) {
            var $menu = $('#rootDropdownMenu');
            $menu.empty();
            if (!Array.isArray(data) && typeof data === 'object' && data !== null) {
                data = data.roots || [];
            }
            if (!data || data.length === 0) {
                $menu.append('<li><span class="dropdown-item disabled">No roots found</span></li>');
                $('#rootDropdownButton').text('No roots found').attr('disabled', true);
                $('#groupsActionsContainer').empty();
            } else {
                $.each(data, function (i, root) {
                    $menu.append(
                        `<li>
                            <a class="dropdown-item root-item" href="#" data-root-code="${root.rootCode}">
                                ${root.rootCode} - ${root.rootName}
                            </a>
                        </li>`
                    );
                });
                $('#rootDropdownButton').removeAttr('disabled').text('Select a root');
                $('#rootDropdownButton').attr('aria-expanded', 'false');
            }
        },
        error: function (xhr, status, error) {
            var $menu = $('#rootDropdownMenu');
            $menu.empty().append('<li><span class="dropdown-item disabled">Error loading roots</span></li>');
            $('#rootDropdownButton').text('Error loading roots').attr('disabled', true);
            $('#groupsActionsContainer').empty();
            console.error('Error loading roots:', status, error, xhr.responseText);
        }
    });
}

function loadBranchesForRoot(rootCode, $select) {
    $select.html('<option value="">Loading branches...</option>');
    $.ajax({
        url: '/Security/GetBranchesByRoot',
        type: 'GET',
        data: { rootCode: rootCode },
        success: function (branches) {
            if (!Array.isArray(branches)) branches = [];
            if (branches.length === 0) {
                $select.html('<option value="">No branches found for this root</option>');
            } else {
                $select.html('<option value="">Select branch</option>');
                $.each(branches, function (i, branch) {
                    $select.append(`<option value="${branch.branchCode}">${branch.branchName}</option>`);
                });
            }
        },
        error: function () {
            $select.html('<option value="">Error loading branches</option>');
        }
    });
}

function loadGroups(rootCode) {
    $.ajax({
        url: '/Security/GetGroupsByRoot',
        type: 'GET',
        data: { rootCode: rootCode },
        success: function (response) {
            var $container = $('#groupsContainer');
            $container.empty();
            if (Array.isArray(response.groups)) {
                renderGroupsList(response.groups, $container);
            } else if (response.success === false || !response.groups || response.groups.length === 0) {
                $container.html('<div class="alert alert-warning">No groups found for this root.</div>');
            } else {
                renderGroupsList(response.groups, $container);
            }
        },
        error: function (xhr, status, error) {
            $('#groupsContainer').html('<div class="alert alert-danger">Error loading groups.</div>');
            console.error('Error loading groups:', status, error, xhr.responseText);
        }
    });
}

function renderGroupsList(groups, $container) {
    var html = '<ul class="list-group">';
    $.each(groups, function (i, group) {
        html += `
            <li class="list-group-item" data-group-code="${group.groupCode}">
                <div class="d-flex align-items-center mb-2">
                    <span class="fw-bold me-auto">${group.groupName}</span>
                    <button class="modern-btn edit-btn edit-group-btn ms-2" data-group-code="${group.groupCode}" data-group-name="${group.groupName}" data-group-desc="${group.groupDesc || ''}" data-branch-code="${group.branchCode || ''}">
                        <i class="bi bi-pencil"></i> Edit
                    </button>
                    <button class="modern-btn delete-btn delete-group-btn ms-2" data-group-code="${group.groupCode}">
                        <i class="bi bi-trash"></i> Delete
                    </button>
                    <button class="modern-btn success-btn create-user-btn ms-2" data-group-code="${group.groupCode}" data-group-name="${group.groupName}">
                        <i class="bi bi-person-plus"></i> + Create User
                    </button>
                    <button class="modern-btn secondary-btn toggle-users ms-2" data-group-code="${group.groupCode}">
                        <span class="plus-minus">▼</span>
                    </button>
                </div>
                <div class="users-list mt-2" style="display:none"></div>
            </li>`;
    });
    html += '</ul>';
    $container.html(html);
}

function loadUsers(groupCode, $usersList, $plusMinus) {
    $usersList.html('<em>Loading users...</em>').slideDown();
    $.ajax({
        url: '/Security/GetUsersByGroup',
        type: 'GET',
        data: { groupCode: groupCode },
        success: function (users) {
            if (!Array.isArray(users)) users = [];
            if (users.length === 0) {
                $usersList.html('<span class="text-muted">No users found for this group.</span>');
            } else {
                var ul = $('<ul class="list-group ms-4"></ul>');
                $.each(users, function (i, user) {
                    ul.append(`
                        <li class="list-group-item py-1 d-flex align-items-center justify-content-between">
                            <span>${user.userCode} - ${user.name}</span>
                            <span>
                                <button class="modern-btn edit-btn edit-user-btn"
                                    data-user-code="${user.userCode}"
                                    data-group-code="${groupCode}"
                                    data-name="${user.name || ''}"
                                    data-username="${user.username || ''}"
                                    data-is-active="${user.isActive ? 'true' : 'false'}">
                                    <i class="bi bi-pencil"></i> Edit
                                </button>
                                <button class="modern-btn delete-btn delete-user-btn"
                                    data-user-code="${user.userCode}"
                                    data-group-code="${groupCode}">
                                    <i class="bi bi-trash"></i> Delete
                                </button>
                            </span>
                        </li>
                    `);
                });
                $usersList.html(ul);
            }
            $usersList.data('loaded', true);
            if ($plusMinus) $plusMinus.text('▲');
        },
        error: function (xhr, status, error) {
            $usersList.html('<span class="text-danger">Error loading users.</span>');
            console.error('Error loading users:', status, error, xhr.responseText);
        }
    });
}

function checkUsernameAvailable(usernameBase, $input, $msg) {
    if (!usernameBase) {
        $msg.text('');
        $input.removeClass('is-invalid is-valid');
        isUsernameTaken = false;
        return;
    }
    $.ajax({
        url: '/Security/IsUsernameTaken',
        type: 'GET',
        data: { username: usernameBase },
        success: function (result) {
            if (result.taken) {
                $msg.text('This username is already taken.')
                    .removeClass('text-success')
                    .addClass('text-danger')
                    .css({ 'color': '#dc3545', 'background': '#f8d7da', 'border-radius': '4px', 'padding': '2px 8px' });
                $input.removeClass('is-valid').addClass('is-invalid');
                isUsernameTaken = true;
            } else {
                $msg.text('Username is available.')
                    .removeClass('text-danger')
                    .addClass('text-success')
                    .css({ 'color': '#28a745', 'background': '#d4edda', 'border-radius': '4px', 'padding': '2px 8px' });
                $input.removeClass('is-invalid').addClass('is-valid');
                isUsernameTaken = false;
            }
        },
        error: function (xhr, status, error) {
            $msg.text('Error checking username.')
                .removeClass('text-success')
                .addClass('text-danger')
                .css({ 'color': '#dc3545', 'background': '#f8d7da', 'border-radius': '4px', 'padding': '2px 8px' });
            $input.removeClass('is-valid').addClass('is-invalid');
            isUsernameTaken = false;
            console.error('Error checking username:', status, error, xhr.responseText);
        }
    });
}

$(document).ready(function () {
    $('#rootDropdownButton').attr('disabled', true).text('Select root type first');
    $('#rootDropdownMenu').empty().append('<li><span class="dropdown-item disabled">Select root type first</span></li>');
    $('#groupsActionsContainer').empty();

    $('input[name="userType"]').on('change', function () {
        var userType = $('input[name="userType"]:checked').val();
        if (userType === "center" || userType === "teacher") {
            $('#rootDropdownButton').removeAttr('disabled').text('Loading...');
            $('#rootDropdownMenu').empty().append('<li><span class="dropdown-item disabled">Loading...</span></li>');
            loadRoots(userType);
        } else {
            $('#rootDropdownButton').attr('disabled', true).text('Select root type first');
            $('#rootDropdownMenu').empty().append('<li><span class="dropdown-item disabled">Select root type first</span></li>');
        }
        $('#groupsContainer').empty();
        $('#groupsActionsContainer').empty();
        selectedRootCode = null;
    });

    $('#rootDropdownMenu').on('click', '.root-item', function (e) {
        e.preventDefault();
        var rootCode = $(this).data('root-code');
        var rootText = $(this).text();
        $('#rootDropdownButton').text(rootText);
        selectedRootCode = rootCode;

        $('#groupsActionsContainer').html(
            `<button class="modern-btn success-btn" id="createGroupBtn"><i class="bi bi-plus-circle"></i> + Create Group</button>`
        );

        loadGroups(rootCode);
    });

    // Create Group Modal open
    $('#groupsActionsContainer').on('click', '#createGroupBtn', function () {
        if (!selectedRootCode) return;
        $('#createGroupRootCode').val(selectedRootCode);
        $('#createGroupName').val('');
        $('#createGroupDesc').val('');
        let $branchSelect = $('#createGroupBranch');
        loadBranchesForRoot(selectedRootCode, $branchSelect);
        var groupModal = new bootstrap.Modal(document.getElementById('createGroupModal'));
        groupModal.show();
    });

    // Create Group submit
    $('#createGroupForm').on('submit', function (e) {
        e.preventDefault();
        var groupName = $('#createGroupName').val();
        var groupDesc = $('#createGroupDesc').val();
        var rootCode = $('#createGroupRootCode').val();
        var branchCode = $('#createGroupBranch').val();
        var insertUser = window.loggedInUserCode || 1;
        $.ajax({
            url: '/Security/CreateGroup',
            type: 'POST',
            data: {
                groupName: groupName,
                groupDesc: groupDesc,
                rootCode: rootCode,
                branchCode: branchCode,
                insertUser: insertUser
            },
            success: function (res) {
                if (res.success) {
                    var modal = bootstrap.Modal.getInstance(document.getElementById('createGroupModal'));
                    if (modal) modal.hide();
                    loadGroups(rootCode);
                } else {
                    alert(res.message || "Failed to create group.");
                }
            },
            error: function (xhr, status, error) {
                alert('Failed to create group.');
                console.error('Error:', status, error, xhr.responseText);
            }
        });
    });

    // Edit Group Modal open
    $('#groupsContainer').on('click', '.edit-group-btn', function () {
        $('#editGroupCode').val($(this).data('group-code'));
        $('#editGroupName').val($(this).data('group-name'));
        $('#editGroupDesc').val($(this).data('group-desc'));
        let branchCode = $(this).data('branch-code');
        let $branchSelect = $('#editGroupBranch');
        loadBranchesForRoot(selectedRootCode, $branchSelect);
        setTimeout(function () {
            $branchSelect.val(branchCode || '');
        }, 400);
        var editGroupModal = new bootstrap.Modal(document.getElementById('editGroupModal'));
        editGroupModal.show();
    });

    // Edit Group submit
    $('#editGroupForm').on('submit', function (e) {
        e.preventDefault();
        var groupCode = $('#editGroupCode').val();
        var groupName = $('#editGroupName').val();
        var groupDesc = $('#editGroupDesc').val();
        var branchCode = $('#editGroupBranch').val();
        $.ajax({
            url: '/Security/EditGroup',
            type: 'POST',
            data: {
                groupCode: groupCode,
                groupName: groupName,
                groupDesc: groupDesc,
                branchCode: branchCode
            },
            success: function (res) {
                if (res.success) {
                    var modal = bootstrap.Modal.getInstance(document.getElementById('editGroupModal'));
                    if (modal) modal.hide();
                    loadGroups(selectedRootCode);
                } else {
                    alert(res.message || "Failed to edit group.");
                }
            },
            error: function (xhr, status, error) {
                alert('Failed to edit group.');
                console.error('Error:', status, error, xhr.responseText);
            }
        });
    });

    // Delete Group
    $('#groupsContainer').on('click', '.delete-group-btn', function () {
        var groupCode = $(this).data('group-code');
        if (confirm('Deleting the group means deleting all users in this group. Do you want to continue?')) {
            $.ajax({
                url: '/Security/DeleteGroup',
                type: 'POST',
                data: { groupCode: groupCode },
                success: function (res) {
                    if (res.success) {
                        loadGroups(selectedRootCode);
                    } else {
                        alert(res.message || "Failed to delete group.");
                    }
                },
                error: function (xhr, status, error) {
                    alert('Failed to delete group.');
                    console.error('Error:', status, error, xhr.responseText);
                }
            });
        }
    });

    $('#groupsContainer').on('click', '.toggle-users', function () {
        var $btn = $(this);
        var $usersList = $btn.closest('li').find('.users-list');
        var groupCode = $btn.data('group-code');
        var $plusMinus = $btn.find('.plus-minus');

        if ($usersList.is(':visible')) {
            $usersList.slideUp();
            $plusMinus.text('▼');
        } else {
            if ($usersList.data('loaded')) {
                $usersList.slideDown();
                $plusMinus.text('▲');
                return;
            }
            loadUsers(groupCode, $usersList, $plusMinus);
        }
    });

    // Add a password field for user creation (optional, send as plain text)
    // If you want default password only, no change needed in modal or here.
    // For custom password:
    // <input type="password" id="createUserPassword" class="form-control" placeholder="Password (leave blank for default)">
    // Add the field in your HTML form if desired.

    $('#groupsContainer').on('click', '.create-user-btn', function () {
        var groupCode = $(this).data('group-code');
        $('#createUserGroupCode').val(groupCode);
        $('#createUserName').val('');
        $('#createUserUsername').val('');
        $('#usernameCheckMsg').text('').removeClass('text-danger text-success').css({ 'color': '', 'background': '', 'border-radius': '', 'padding': '' });
        isUsernameTaken = false;
        $('#createUserUsername').removeClass('is-valid is-invalid');
        $('#createUserIsActive').prop('checked', true);
        $('#createUserSuccess').addClass('d-none').removeClass('alert-danger').addClass('alert-success').text('');
        $('#createInsertUserCode').val('');
        // If you add password field, also reset its value here:
        if ($('#createUserPassword').length) $('#createUserPassword').val('');
        var createUserModal = new bootstrap.Modal(document.getElementById('createUserModal'));
        createUserModal.show();
    });

    $('#createUserUsername').on('blur', function () {
        var usernameBase = $(this).val().trim();
        var $input = $(this);
        var $msg = $('#usernameCheckMsg');
        checkUsernameAvailable(usernameBase, $input, $msg);
    });

    $('#createUserForm').on('submit', function (e) {
        if (isUsernameTaken) {
            $('#usernameCheckMsg')
                .text('This username is already taken.')
                .removeClass('text-success')
                .addClass('text-danger')
                .css({ 'color': '#dc3545', 'background': '#f8d7da', 'border-radius': '4px', 'padding': '2px 8px' });
            $('#createUserUsername').removeClass('is-valid').addClass('is-invalid').focus();
            e.preventDefault();
            return;
        }
        var name = $('#createUserName').val();
        var username = $('#createUserUsername').val();
        var groupCode = $('#createUserGroupCode').val();
        var isActive = $('#createUserIsActive').is(':checked');
        var insertUserCode = $('#createInsertUserCode').val();
        // If you add a password field:
        var password = $('#createUserPassword').length ? $('#createUserPassword').val() : '';
        // Send plain text (empty will use default on backend)
        $.ajax({
            url: '/Security/CreateUser',
            type: 'POST',
            data: {
                name: name,
                username: username,
                password: password,
                groupCode: groupCode,
                isActive: isActive,
                insertUserCode: insertUserCode
            },
            success: function (result) {
                if (result.success) {
                    $('#createUserSuccess').removeClass('d-none').addClass('alert-success').removeClass('alert-danger').text('User created! Username: ' + result.username + '. Default password: 123456789');
                    var $toggleBtn = $(`.toggle-users[data-group-code='${groupCode}']`);
                    $toggleBtn.closest('li').find('.users-list').removeData('loaded');
                    $toggleBtn.click();
                    setTimeout(function () {
                        $toggleBtn.click();
                    }, 300);
                    setTimeout(function () {
                        var modal = bootstrap.Modal.getInstance(document.getElementById('createUserModal'));
                        if (modal) modal.hide();
                    }, 1200);
                } else {
                    $('#createUserSuccess').removeClass('d-none').removeClass('alert-success').addClass('alert-danger').text(result.message || 'Failed to create user.');
                }
            },
            error: function (xhr, status, error) {
                $('#createUserSuccess').removeClass('d-none').removeClass('alert-success').addClass('alert-danger').text('Failed to create user.');
                console.error('Error creating user:', status, error, xhr.responseText);
            }
        });
    });

    $('body').on('click', '.edit-user-btn', function () {
        $('#editUserCode').val($(this).data('user-code'));
        $('#editUserName').val($(this).data('name'));
        $('#editUserPassword').val('');
        $('#editUserIsActive').prop('checked', $(this).data('is-active') === true || $(this).data('is-active') === 'true');
        $('#resetPasswordBtn').data('user-code', $(this).data('user-code'));
        var editUserModal = bootstrap.Modal.getInstance(document.getElementById('editUserModal'))
            || new bootstrap.Modal(document.getElementById('editUserModal'));
        editUserModal.show();
    });

    $('#editUserForm').on('submit', function (e) {
        e.preventDefault();
        var userCode = $('#editUserCode').val();
        var name = $('#editUserName').val();
        var password = $('#editUserPassword').val();
        var isActive = $('#editUserIsActive').is(':checked');
        $.ajax({
            url: '/Security/EditUser',
            type: 'POST',
            data: {
                userCode: userCode,
                name: name,
                isActive: isActive,
                password: password // pass password for change (plain text)
            },
            success: function (result) {
                var $toggleBtn = $(`.toggle-users[data-group-code]`).filter(function () {
                    return $(this).closest('li').find(`.edit-user-btn[data-user-code='${userCode}']`).length > 0;
                });
                $toggleBtn.closest('li').find('.users-list').removeData('loaded');
                $toggleBtn.click();
                setTimeout(function () {
                    $toggleBtn.click();
                }, 300);
                var modal = bootstrap.Modal.getInstance(document.getElementById('editUserModal'));
                if (modal) modal.hide();
            },
            error: function (xhr, status, error) {
                alert('Failed to edit user.');
                console.error('Error editing user:', status, error, xhr.responseText);
            }
        });
    });

    $('body').on('click', '.reset-password-btn', function () {
        var userCode = $(this).data('user-code');
        if (confirm('Are you sure you want to reset the password for this user?')) {
            $.ajax({
                url: '/Security/ResetUserPassword',
                type: 'POST',
                data: { userCode: userCode },
                success: function (result) {
                    alert(result.message || 'Password reset.');
                },
                error: function (xhr, status, error) {
                    alert('Failed to reset password.');
                    console.error('Error resetting password:', status, error, xhr.responseText);
                }
            });
        }
    });

    $('body').on('click', '.delete-user-btn', function () {
        var userCode = $(this).data('user-code');
        var groupCode = $(this).data('group-code');
        if (confirm('Are you sure you want to delete this user?')) {
            $.ajax({
                url: '/Security/DeleteUser',
                type: 'POST',
                data: { userCode: userCode },
                success: function (result) {
                    var $toggleBtn = $(`.toggle-users[data-group-code='${groupCode}']`);
                    $toggleBtn.closest('li').find('.users-list').removeData('loaded');
                    $toggleBtn.click();
                    setTimeout(function () {
                        $toggleBtn.click();
                    }, 300);
                },
                error: function (xhr, status, error) {
                    alert('Failed to delete user.');
                    console.error('Error deleting user:', status, error, xhr.responseText);
                }
            });
        }
    });
});