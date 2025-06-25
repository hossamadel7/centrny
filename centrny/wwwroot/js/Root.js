$(document).ready(function () {
    let currentPage = 1;
    const pageSize = 10;
    let totalRecords = 0;
    let centerFilter = "";

    // Initial load
    loadRootData(currentPage, centerFilter);

    // Filter radio change
    $('input[name="centerFilter"]').change(function () {
        centerFilter = $(this).val();
        currentPage = 1;
        loadRootData(currentPage, centerFilter);
    });

    // Load active roots with pagination and filter
    function loadRootData(page, centerFilter) {
        let url = `/Root/GetActiveRoots?page=${page}&pageSize=${pageSize}`;
        if (centerFilter !== undefined && centerFilter !== "") {
            url += `&isCenter=${centerFilter}`;
        }

        console.log('Loading root data from URL:', url);

        $.ajax({
            url: url,
            type: "GET",
            dataType: "json",
            success: function (response) {
                console.log('Root data response:', response);
                let data = response.data;
                totalRecords = response.totalCount;
                let tableContent = "";

                data.forEach(function (root) {
                    tableContent += `
                        <tr data-id="${root.rootCode}">
                            <td>${root.rootCode}</td>
                            <td>${root.rootOwner}</td>
                            <td>${root.rootName}</td>
                            <td>${root.rootPhone}</td>
                            <td>${root.rootEmail}</td>
                            <td>$${root.rootFees}</td>
                            <td>${root.rootAddress}</td>
                            <td>${root.noOfCenter}</td>
                            <td>${root.noOfUser}</td>
                            <td>
                                <span class="badge ${root.isCenter ? 'bg-success' : 'bg-info'}">
                                    ${root.isCenter ? 'Center' : 'Teacher'}
                                </span>
                            </td>
                            <td>
                                <div class="d-flex flex-column gap-1">
                                    <button class="btn root-index-btn-edit editBtn">
                                        <i class="fas fa-edit me-1"></i>Edit
                                    </button>
                                    <button class="btn root-index-btn-delete deleteBtn">
                                        <i class="fas fa-trash me-1"></i>Delete
                                    </button>
                                    <button class="btn root-index-btn-questions assign-modules-btn"
                                        data-rootcode="${root.rootCode}" 
                                        data-rootname="${root.rootName}">
                                        <i class="fas fa-puzzle-piece me-1"></i>Modules
                                    </button>
                                </div>
                            </td>
                        </tr>`;
                });

                $("#rootTable tbody").html(tableContent);
                renderPagination();
            },
            error: function (xhr, status, error) {
                console.error("Root data loading error:", { xhr, status, error, responseText: xhr.responseText });
                showToast("Failed to retrieve data: " + (xhr.responseText || error), 'error');
            }
        });
    }

    // Render pagination controls
    function renderPagination() {
        const totalPages = Math.ceil(totalRecords / pageSize);
        let paginationHtml = '';

        if (totalPages <= 1) {
            $('#pagination').html('');
            return;
        }

        // Previous button
        paginationHtml += `<li class="page-item${currentPage === 1 ? ' disabled' : ''}">
            <a class="page-link" href="#" data-page="${currentPage - 1}">Previous</a>
        </li>`;

        // Page numbers (show up to 5 at a time)
        let startPage = Math.max(1, currentPage - 2);
        let endPage = Math.min(totalPages, currentPage + 2);
        if (currentPage <= 3) endPage = Math.min(5, totalPages);
        if (currentPage > totalPages - 3) startPage = Math.max(1, totalPages - 4);

        for (let i = startPage; i <= endPage; i++) {
            paginationHtml += `<li class="page-item${i === currentPage ? ' active' : ''}">
                <a class="page-link" href="#" data-page="${i}">${i}</a>
            </li>`;
        }

        // Next button
        paginationHtml += `<li class="page-item${currentPage === totalPages ? ' disabled' : ''}">
            <a class="page-link" href="#" data-page="${currentPage + 1}">Next</a>
        </li>`;

        $('#pagination').html(`<ul class="pagination">${paginationHtml}</ul>`);
    }

    // Handle pagination click
    $('#pagination').on('click', '.page-link', function (e) {
        e.preventDefault();
        const page = parseInt($(this).data('page'), 10);
        const totalPages = Math.ceil(totalRecords / pageSize);
        if (page > 0 && page <= totalPages && page !== currentPage) {
            currentPage = page;
            loadRootData(currentPage, centerFilter);
        }
    });

    // Show modal for Add
    $('#addRootBtn').click(function () {
        $('#rootForm')[0].reset();
        $('#rowIndex').val('');
        $('#rootModalLabel').html('<i class="fas fa-plus me-2"></i>Add Root');
        $('#rootCodeContainer').hide();
        $('#addOnlyFields').show();
        $('#rootModal').modal('show');
    });

    // Show modal for Edit
    $('#rootTable').on('click', '.editBtn', function () {
        const row = $(this).closest('tr');
        const rootCode = row.data('id');

        console.log('Editing root with code:', rootCode);

        $.get(`/Root/GetRoot?rootCode=${rootCode}`, function (data) {
            console.log('Root data for edit:', data);
            $('#rootCodeDisplay').text(data.rootCode);
            $('#rootOwner').val(data.rootOwner);
            $('#rootName').val(data.rootName);
            $('#rootPhone').val(data.rootPhone);
            $('#rootEmail').val(data.rootEmail);
            $('#rootFees').val(data.rootFees);
            $('#rootAddress').val(data.rootAddress);
            $('#numCenters').val(data.noOfCenter);
            $('#numUsers').val(data.noOfUser);
            $('#isCenter').prop('checked', data.isCenter);

            $('#rowIndex').val(data.rootCode);
            $('#rootModalLabel').html('<i class="fas fa-edit me-2"></i>Edit Root');
            $('#rootCodeContainer').show();
            $('#addOnlyFields').hide();
            const modal = new bootstrap.Modal(document.getElementById('rootModal'));
            modal.show();
        }).fail(function (xhr, status, error) {
            console.error('Error getting root data:', { xhr, status, error });
            showToast('Failed to load root data for editing', 'error');
        });
    });

    // Delete (Soft Delete)
    $('#rootTable').on('click', '.deleteBtn', function () {
        const row = $(this).closest('tr');
        const id = row.data('id');

        if (confirm("Are you sure you want to delete this root?")) {
            $.post("/Root/DeleteRoot", { id: id }, function () {
                loadRootData(currentPage, centerFilter);
                showToast('Root deleted successfully', 'success');
            }).fail(function (xhr, status, error) {
                console.error('Delete error:', { xhr, status, error });
                showToast('Failed to delete root: ' + (xhr.responseText || error), 'error');
            });
        }
    });

    // Save (Add or Edit)
    $('#rootForm').submit(function (e) {
        e.preventDefault();

        const isEdit = $('#rowIndex').val() !== '';

        const rootData = {
            rootOwner: $('#rootOwner').val(),
            rootName: $('#rootName').val(),
            rootPhone: $('#rootPhone').val(),
            rootEmail: $('#rootEmail').val(),
            rootFees: parseFloat($('#rootFees').val()),
            rootAddress: $('#rootAddress').val(),
            noOfCenter: parseInt($('#numCenters').val()),
            noOfUser: parseInt($('#numUsers').val()),
            isCenter: $('#isCenter').is(':checked')
        };

        if (isEdit) {
            rootData.rootCode = parseInt($('#rowIndex').val());
        } else {
            rootData.insertUser = parseInt($('#insertUser').val()) || 1;
            rootData.isActive = $('#isActive').is(':checked');
        }

        const url = isEdit ? "/Root/EditRoot" : "/Root/AddRoot";

        console.log('Saving root data:', { url, rootData });

        $.ajax({
            url: url,
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(rootData),
            success: function (response) {
                console.log('Save response:', response);
                $('#rootModal').modal('hide');
                loadRootData(currentPage, centerFilter);
                showToast(`Root ${isEdit ? 'updated' : 'added'} successfully`, 'success');
            },
            error: function (xhr, status, error) {
                console.error("Save error:", { xhr, status, error, responseText: xhr.responseText });
                showToast('Failed to save root: ' + (xhr.responseText || error), 'error');
            }
        });
    });

    // ========== ENHANCED MODULE ASSIGNMENT FUNCTIONALITY ========== //

    // Open module assignment modal
    $(document).on('click', '.assign-modules-btn', function () {
        const rootCode = $(this).data('rootcode');
        const rootName = $(this).data('rootname');

        console.log('Opening module assignment for:', { rootCode, rootName });

        $('#displayRootCode').text(rootCode);
        $('#displayRootName').text(rootName);
        loadModuleAssignment(rootCode);
        $('#moduleAssignmentModal').modal('show');
    });

    function loadModuleAssignment(rootCode) {
        console.log('Loading module assignment for rootCode:', rootCode);

        // Clear containers first
        $('#assignedModules, #availableModules').empty();

        // Show loading states
        $('#assignedModules').html('<div class="text-center py-4"><i class="fas fa-spinner fa-spin"></i> Loading assigned modules...</div>');
        $('#availableModules').html('<div class="text-center py-4"><i class="fas fa-spinner fa-spin"></i> Loading available modules...</div>');

        // Load assigned modules
        $.ajax({
            url: '/Root/GetAssignedModules',
            method: 'GET',
            data: { rootCode: rootCode },
            dataType: 'json',
            success: function (modules) {
                console.log('Assigned modules received:', modules);
                $('#assignedModules').empty();

                if (!modules || modules.length === 0) {
                    $('#assignedModules').html('<div class="text-center text-muted py-4"><i class="fas fa-info-circle"></i><br>No assigned modules</div>');
                } else {
                    modules.forEach(m => {
                        console.log('Adding assigned module:', m);
                        $('#assignedModules').append(createModuleElement(m));
                    });
                }
                setupDragAndDrop();
            },
            error: function (xhr, status, error) {
                console.error('Error loading assigned modules:', { xhr, status, error, responseText: xhr.responseText });
                $('#assignedModules').html(`<div class="text-danger text-center py-4"><i class="fas fa-exclamation-triangle"></i><br>Error loading assigned modules<br><small>${xhr.responseText || error}</small></div>`);
            }
        });

        // Load available modules
        $.ajax({
            url: '/Root/GetAvailableModules',
            method: 'GET',
            data: { rootCode: rootCode },
            dataType: 'json',
            success: function (modules) {
                console.log('Available modules received:', modules);
                $('#availableModules').empty();

                if (!modules || modules.length === 0) {
                    $('#availableModules').html('<div class="text-center text-muted py-4"><i class="fas fa-info-circle"></i><br>No available modules</div>');
                } else {
                    modules.forEach(m => {
                        console.log('Adding available module:', m);
                        $('#availableModules').append(createModuleElement(m));
                    });
                }
                setupDragAndDrop();
            },
            error: function (xhr, status, error) {
                console.error('Error loading available modules:', { xhr, status, error, responseText: xhr.responseText });
                $('#availableModules').html(`<div class="text-danger text-center py-4"><i class="fas fa-exclamation-triangle"></i><br>Error loading available modules<br><small>${xhr.responseText || error}</small></div>`);
            }
        });
    }

    function createModuleElement(module) {
        console.log('Creating module element for:', module);

        // Validate module object
        if (!module) {
            console.error('Module is null or undefined');
            return $('<div class="text-danger">Invalid module data</div>');
        }

        // Handle different property name cases
        const moduleCode = module.moduleCode || module.ModuleCode;
        const moduleName = module.moduleName || module.ModuleName;

        if (!moduleCode || !moduleName) {
            console.error('Invalid module object - missing required properties:', module);
            return $('<div class="text-danger">Invalid module data - missing properties</div>');
        }

        const element = $(`
            <div class="module-item" draggable="true" data-modulecode="${moduleCode}">
                <i class="fas fa-grip-vertical me-2 text-muted"></i>
                <span>${moduleName}</span>
                <small class="text-muted ms-auto">(${moduleCode})</small>
            </div>
        `);

        console.log('Created module element:', element);
        return element;
    }

    function setupDragAndDrop() {
        console.log('Setting up drag and drop...');

        // Remove existing event listeners to prevent duplicates
        $('.module-item').off('dragstart dragend');
        $('.modules-container').off('dragover dragenter dragleave drop');

        // Drag start
        $('.module-item').on('dragstart', function (e) {
            const moduleCode = $(this).data('modulecode');
            const moduleName = $(this).find('span').text();

            console.log('Drag started for module:', { moduleCode, moduleName });

            e.originalEvent.dataTransfer.setData('text/plain', moduleCode);
            e.originalEvent.dataTransfer.effectAllowed = 'move';
            $(this).addClass('dragging');
        });

        // Drag end
        $('.module-item').on('dragend', function (e) {
            console.log('Drag ended');
            $(this).removeClass('dragging');
            $('.modules-container').removeClass('drag-over');
        });

        // Container events
        $('.modules-container').on('dragover', function (e) {
            e.preventDefault();
            e.originalEvent.dataTransfer.dropEffect = 'move';
        });

        $('.modules-container').on('dragenter', function (e) {
            e.preventDefault();
            $(this).addClass('drag-over');
        });

        $('.modules-container').on('dragleave', function (e) {
            // Only remove if leaving the container entirely
            if (!$(this).has(e.relatedTarget).length) {
                $(this).removeClass('drag-over');
            }
        });

        $('.modules-container').on('drop', function (e) {
            e.preventDefault();
            $(this).removeClass('drag-over');

            const moduleCode = e.originalEvent.dataTransfer.getData('text/plain');
            const $draggedElement = $(`.module-item[data-modulecode="${moduleCode}"]`);
            const targetContainer = $(this);
            const targetContainerId = targetContainer.attr('id');

            console.log('Drop event:', {
                moduleCode,
                targetContainerId,
                elementExists: $draggedElement.length > 0,
                isAlreadyInTarget: targetContainer.has($draggedElement).length > 0
            });

            // Check if the element exists and is not already in this container
            if ($draggedElement.length && !targetContainer.has($draggedElement).length) {
                console.log('Moving module from', $draggedElement.parent().attr('id'), 'to', targetContainerId);

                // Remove the element from its current location
                $draggedElement.remove();

                // Add it to the new container
                targetContainer.append($draggedElement);

                // Re-setup drag and drop for the moved element
                setupDragAndDrop();

                console.log('Module moved successfully');
                showToast(`Module moved to ${targetContainerId === 'assignedModules' ? 'assigned' : 'available'} list`, 'info');
            } else {
                console.log('Drop cancelled - element not found or already in target');
            }
        });

        console.log('Drag and drop setup complete. Found', $('.module-item').length, 'module items');
    }

    $('#saveModuleAssignments').click(function () {
        const rootCode = parseInt($('#displayRootCode').text(), 10);

        console.log('Saving module assignments for rootCode:', rootCode);

        if (isNaN(rootCode)) {
            showToast('No root selected. Please open the module assignment modal from a valid root.', 'error');
            return;
        }

        const assignments = [];
        $('#assignedModules .module-item').each(function () {
            const moduleCode = parseInt($(this).data('modulecode'), 10);
            if (!isNaN(moduleCode)) {
                assignments.push(moduleCode);
            }
        });

        console.log('Assignments to save:', assignments);

        // Show saving state
        const $saveBtn = $(this);
        $saveBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-2"></i>Saving...');

        $.ajax({
            url: '/Root/SaveModuleAssignments',
            type: 'POST',
            contentType: "application/json",
            data: JSON.stringify({
                rootCode: rootCode,
                moduleCodes: assignments
            }),
            success: function (response) {
                console.log('Save response:', response);
                $('#moduleAssignmentModal').modal('hide');
                showToast('Module assignments saved successfully', 'success');
            },
            error: function (xhr, status, error) {
                console.error('Save error:', { xhr, status, error, responseText: xhr.responseText });
                showToast('Saving module assignments failed: ' + (xhr.responseText || error), 'error');
            },
            complete: function () {
                // Reset button state
                $saveBtn.prop('disabled', false).html('<i class="fas fa-save me-2"></i>Save Changes');
            }
        });
    });

    function showToast(message, type = 'info') {
        // Create toast element
        const toastId = 'toast-' + Date.now();
        const iconMap = {
            'success': 'check-circle',
            'error': 'exclamation-triangle',
            'warning': 'exclamation-circle',
            'info': 'info-circle'
        };

        const bgMap = {
            'success': 'success',
            'error': 'danger',
            'warning': 'warning',
            'info': 'info'
        };

        const toastHtml = `
            <div class="toast align-items-center text-bg-${bgMap[type] || 'primary'} border-0" role="alert" aria-live="assertive" aria-atomic="true" id="${toastId}">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="fas fa-${iconMap[type] || 'info-circle'} me-2"></i>
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;

        // Add toast container if it doesn't exist
        if (!$('#toast-container').length) {
            $('body').append('<div id="toast-container" class="toast-container position-fixed top-0 end-0 p-3"></div>');
        }

        $('#toast-container').append(toastHtml);
        const toast = new bootstrap.Toast(document.getElementById(toastId));
        toast.show();

        // Remove toast element after it's hidden
        $(`#${toastId}`).on('hidden.bs.toast', function () {
            $(this).remove();
        });
    }
});