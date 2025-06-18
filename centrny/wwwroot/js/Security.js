let selectedRootCode = null;
let isUsernameTaken = false;

// Load roots based on userType
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
            if (!data || data.length === 0) {
                $menu.append('<li><span class="dropdown-item disabled">No roots found</span></li>');
                $('#rootDropdownButton').text('No roots found').prop('disabled', true);
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
                $('#rootDropdownButton').text('Select a root').prop('disabled', false);
            }
        },
        error: function () {
            var $menu = $('#rootDropdownMenu');
            $menu.empty().append('<li><span class="dropdown-item disabled">Error loading roots</span></li>');
            $('#rootDropdownButton').text('Error loading roots').prop('disabled', true);
        }
    });
}

$(document).ready(function () {
    // Initialize dropdown as disabled
    $('#rootDropdownButton').prop('disabled', true).text('Select root type first');
    $('#rootDropdownMenu').empty().append('<li><span class="dropdown-item disabled">Select root type first</span></li>');

    $('input[name="userType"]').on('change', function () {
        var userType = $('input[name="userType"]:checked').val();
        if (userType === "center" || userType === "teacher") {
            $('#rootDropdownButton').prop('disabled', false).text('Loading...');
            $('#rootDropdownMenu').empty().append('<li><span class="dropdown-item disabled">Loading...</span></li>');
            loadRoots(userType);
        } else {
            $('#rootDropdownButton').prop('disabled', true).text('Select root type first');
            $('#rootDropdownMenu').empty().append('<li><span class="dropdown-item disabled">Select root type first</span></li>');
        }
        $('#groupsContainer').empty();
        selectedRootCode = null;
    });

    // Handle root selection from Bootstrap dropdown
    $('#rootDropdownMenu').on('click', '.root-item', function (e) {
        e.preventDefault();
        var rootCode = $(this).data('root-code');
        var rootText = $(this).text();
        $('#rootDropdownButton').text(rootText);
        selectedRootCode = rootCode;

        // Load groups for this root
        if (rootCode) {
            $.ajax({
                url: '/Security/GetGroupsByRoot',
                type: 'GET',
                data: { rootCode: rootCode },
                success: function (response) {
                    var $container = $('#groupsContainer');
                    $container.empty();
                    if (!response.success || response.groups.length === 0) {
                        $container.html('<div class="alert alert-warning">No groups found for this root.</div>');
                    } else {
                        var html = '<ul class="list-group">';
                        $.each(response.groups, function (i, group) {
                            html += `
                                <li class="list-group-item" data-group-code="${group.groupCode}">
                                    <div class="d-flex align-items-center mb-2">
                                        <span class="fw-bold me-auto">${group.groupName}</span>
                                        <button class="btn btn-sm btn-success ms-2 create-user-btn" data-group-code="${group.groupCode}" data-group-name="${group.groupName}">
                                            + Create User
                                        </button>
                                        <button class="btn btn-sm btn-outline-primary ms-2 toggle-users" data-group-code="${group.groupCode}">
                                            <span class="plus-minus">+</span>
                                        </button>
                                    </div>
                                    <div class="users-list mt-2" style="display:none"></div>
                                </li>`;
                        });
                        html += '</ul>';
                        $container.html(html);
                    }
                },
                error: function () {
                    $('#groupsContainer').html('<div class="alert alert-danger">Error loading groups.</div>');
                }
            });
        }
    });

    // Username check and styling
    $('#createUserUsername').on('blur', function () {
        var usernameBase = $(this).val().trim();
        var $input = $(this);
        var $msg = $('#usernameCheckMsg');
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
                    $msg
                        .text('This username is already taken.')
                        .removeClass('text-success')
                        .addClass('text-danger')
                        .css({ 'color': '#dc3545', 'background': '#f8d7da', 'border-radius': '4px', 'padding': '2px 8px' });
                    $input.removeClass('is-valid').addClass('is-invalid');
                    isUsernameTaken = true;
                } else {
                    $msg
                        .text('Username is available.')
                        .removeClass('text-danger')
                        .addClass('text-success')
                        .css({ 'color': '#28a745', 'background': '#d4edda', 'border-radius': '4px', 'padding': '2px 8px' });
                    $input.removeClass('is-invalid').addClass('is-valid');
                    isUsernameTaken = false;
                }
            },
            error: function () {
                $msg
                    .text('Error checking username.')
                    .removeClass('text-success')
                    .addClass('text-danger')
                    .css({ 'color': '#dc3545', 'background': '#f8d7da', 'border-radius': '4px', 'padding': '2px 8px' });
                $input.removeClass('is-valid').addClass('is-invalid');
                isUsernameTaken = false;
            }
        });
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

        $.ajax({
            url: '/Security/CreateUser',
            type: 'POST',
            data: {
                name: name,
                username: username,
                password: '',
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
            error: function () {
                $('#createUserSuccess').removeClass('d-none').removeClass('alert-success').addClass('alert-danger').text('Failed to create user.');
            }
        });
    });

    // Toggle users in group
    $('#groupsContainer').on('click', '.toggle-users', function () {
        var $btn = $(this);
        var $usersList = $btn.closest('li').find('.users-list');
        var groupCode = $btn.data('group-code');
        var $plusMinus = $btn.find('.plus-minus');

        if ($usersList.is(':visible')) {
            $usersList.slideUp();
            $plusMinus.text('+');
        } else {
            if ($usersList.data('loaded')) {
                $usersList.slideDown();
                $plusMinus.text('-');
                return;
            }
            $usersList.html('<em>Loading users...</em>').slideDown();
            $.ajax({
                url: '/Security/GetUsersByGroup',
                type: 'GET',
                data: { groupCode: groupCode },
                success: function (users) {
                    if (users.length === 0) {
                        $usersList.html('<span class="text-muted">No users found for this group.</span>');
                    } else {
                        var ul = $('<ul class="list-group ms-4"></ul>');
                        $.each(users, function (i, user) {
                            ul.append(`
                                <li class="list-group-item py-1 d-flex align-items-center justify-content-between">
                                    <span>${user.userCode} - ${user.name}</span>
                                    <span>
                                        <button class="btn btn-sm btn-outline-secondary edit-user-btn"
                                            data-user-code="${user.userCode}"
                                            data-group-code="${groupCode}"
                                            data-name="${user.name || ''}"
                                            data-username="${user.username || ''}"
                                            data-is-active="${user.isActive ? 'true' : 'false'}">
                                            Edit
                                        </button>
                                        <button class="btn btn-sm btn-outline-danger delete-user-btn"
                                            data-user-code="${user.userCode}"
                                            data-group-code="${groupCode}">
                                            Delete
                                        </button>
                                    </span>
                                </li>
                            `);
                        });
                        $usersList.html(ul);
                    }
                    $usersList.data('loaded', true);
                    $plusMinus.text('-');
                },
                error: function () {
                    $usersList.html('<span class="text-danger">Error loading users.</span>');
                }
            });
        }
    });

    // Show create user modal
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
        var createUserModal = new bootstrap.Modal(document.getElementById('createUserModal'));
        createUserModal.show();
    });

    // Edit user modal show
    $('body').on('click', '.edit-user-btn', function () {
        $('#editUserCode').val($(this).data('user-code'));
        $('#editUserName').val($(this).data('name'));
        $('#editUserIsActive').prop('checked', $(this).data('is-active') === true || $(this).data('is-active') === 'true');
        $('#resetPasswordBtn').data('user-code', $(this).data('user-code'));
        var editUserModal = new bootstrap.Modal(document.getElementById('editUserModal'));
        editUserModal.show();
    });

    // Edit form submit
    $('#editUserForm').on('submit', function (e) {
        e.preventDefault();
        var userCode = $('#editUserCode').val();
        var name = $('#editUserName').val();
        var isActive = $('#editUserIsActive').is(':checked');
        $.ajax({
            url: '/Security/EditUser',
            type: 'POST',
            data: {
                userCode: userCode,
                name: name,
                isActive: isActive
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
            error: function () {
                alert('Failed to edit user.');
            }
        });
    });

    // Reset password
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
                error: function () {
                    alert('Failed to reset password.');
                }
            });
        }
    });

    // Delete user (deactivate)
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
                error: function () {
                    alert('Failed to delete user.');
                }
            });
        }
    });
});