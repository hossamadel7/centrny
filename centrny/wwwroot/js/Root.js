$(document).ready(function () {
    let currentPage = 1;
    const pageSize = 10;
    let totalRecords = 0;
    let centerFilter = "";

    // Check Bootstrap availability immediately
    if (typeof bootstrap === 'undefined') {
        console.error('Bootstrap 5 is not loaded!');
        alert('Error: Bootstrap 5 is required but not loaded. Please check your includes.');
        return;
    }

    console.log('Bootstrap version detected:', bootstrap);

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
                                   <button class="btn-table edit editBtn" type="button">
    <i class="fas fa-edit me-1"></i>Edit
</button>
<button class="btn-table delete deleteBtn" type="button">
    <i class="fas fa-trash me-1"></i>Delete
</button>
<button class="btn-table modules assign-modules-btn" type="button"
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

    // Enhanced Root Modal Sizing Function
    function adjustRootModalSize() {
        const modal = document.getElementById('rootModal');
        const modalDialog = modal?.querySelector('.modal-dialog');
        const modalContent = modal?.querySelector('.modal-content');
        const modalBody = modal?.querySelector('.modal-body');

        if (modal && modalDialog && modalContent && modalBody) {
            // Get viewport dimensions
            const viewportWidth = window.innerWidth;
            const viewportHeight = window.innerHeight;

            // Adjust based on screen size
            if (viewportWidth >= 1400) {
                modalDialog.style.maxWidth = '95vw';
                modalDialog.style.width = '95vw';
                modalContent.style.minWidth = '800px';
                modalBody.style.minWidth = '750px';
            } else if (viewportWidth >= 1200) {
                modalDialog.style.maxWidth = '90vw';
                modalDialog.style.width = '90vw';
                modalContent.style.minWidth = '700px';
                modalBody.style.minWidth = '650px';
            } else if (viewportWidth >= 992) {
                modalDialog.style.maxWidth = '95vw';
                modalDialog.style.width = '95vw';
                modalContent.style.minWidth = '600px';
                modalBody.style.minWidth = '550px';
            } else if (viewportWidth >= 768) {
                modalDialog.style.maxWidth = '98vw';
                modalDialog.style.width = '98vw';
                modalContent.style.minWidth = '500px';
                modalBody.style.minWidth = '450px';
            } else {
                modalDialog.style.maxWidth = '100vw';
                modalDialog.style.width = '100vw';
                modalContent.style.minWidth = '350px';
                modalBody.style.minWidth = '320px';
            }

            // Ensure modal doesn't exceed viewport height
            const maxHeight = Math.min(viewportHeight * 0.95, 900);
            modalDialog.style.maxHeight = maxHeight + 'px';

            // Center the modal
            modalDialog.style.margin = '1vh auto';

            console.log('Root modal size adjusted for viewport:', viewportWidth + 'x' + viewportHeight);
        }
    }

    // Enhanced modal show function for root modal
    function showRootModalSafely(mode = 'add', data = null) {
        const modalElement = document.getElementById('rootModal');

        if (!modalElement) {
            console.error('Root modal not found');
            showToast('Modal not found', 'error');
            return;
        }

        try {
            // Set up modal content based on mode
            if (mode === 'add') {
                document.getElementById('rootModalLabel').innerHTML = '<i class="fas fa-plus me-2"></i>Add Root';
                document.getElementById('rootCodeContainer').style.display = 'none';
                document.getElementById('addOnlyFields').style.display = 'block';
                document.getElementById('rootForm').reset();
                document.getElementById('rowIndex').value = '';
            } else if (mode === 'edit' && data) {
                document.getElementById('rootModalLabel').innerHTML = '<i class="fas fa-edit me-2"></i>Edit Root';
                document.getElementById('rootCodeContainer').style.display = 'block';
                document.getElementById('addOnlyFields').style.display = 'none';

                // Populate form with data
                Object.keys(data).forEach(key => {
                    const field = document.getElementById(key);
                    if (field) {
                        if (field.type === 'checkbox') {
                            field.checked = data[key];
                        } else {
                            field.value = data[key];
                        }
                    }
                });
            }

            // Adjust modal size before showing
            setTimeout(adjustRootModalSize, 100);

            // Show modal using Bootstrap API
            const modal = new bootstrap.Modal(modalElement, {
                backdrop: 'static',
                keyboard: true,
                focus: true
            });

            modal.show();

            // Adjust size again after modal is visible
            setTimeout(adjustRootModalSize, 300);

        } catch (error) {
            console.error('Error showing root modal:', error);

            // Fallback: direct DOM manipulation
            modalElement.style.display = 'block';
            modalElement.classList.add('show');
            modalElement.setAttribute('aria-hidden', 'false');
            document.body.classList.add('modal-open');

            // Create backdrop if it doesn't exist
            if (!document.querySelector('.modal-backdrop')) {
                const backdrop = document.createElement('div');
                backdrop.className = 'modal-backdrop fade show';
                document.body.appendChild(backdrop);
            }

            setTimeout(adjustRootModalSize, 200);
        }
    }

    // ENHANCED: Show modal for Add with better sizing
    $('#addRootBtn').click(function (e) {
        e.preventDefault();
        e.stopPropagation();

        console.log('Add Root button clicked');

        // Reset form
        $('#rootForm')[0].reset();
        $('#rowIndex').val('');
        $('#rootModalLabel').html('<i class="fas fa-plus me-2"></i>Add Root');
        $('#rootCodeContainer').hide();
        $('#addOnlyFields').show();

        // Use enhanced modal show function
        showRootModalSafely('add');
    });

    // ENHANCED: Show modal for Edit with better sizing
    $('#rootTable').on('click', '.editBtn', function (e) {
        e.preventDefault();
        e.stopPropagation();

        const row = $(this).closest('tr');
        const rootCode = row.data('id');

        console.log('Edit button clicked for root:', rootCode);

        $.get(`/Root/GetRoot?rootCode=${rootCode}`, function (data) {
            console.log('Root data for edit:', data);

            // Populate form fields
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

            // Set up modal for edit mode
            $('#rootModalLabel').html('<i class="fas fa-edit me-2"></i>Edit Root');
            $('#rootCodeContainer').show();
            $('#addOnlyFields').hide();

            // Use enhanced modal show function
            showRootModalSafely('edit', data);

        }).fail(function (xhr, status, error) {
            console.error('Error getting root data:', { xhr, status, error });
            showToast('Failed to load root data for editing', 'error');
        });
    });

    // ENHANCED: Module assignment button with better modal handling
    $(document).on('click', '.assign-modules-btn', function (e) {
        e.preventDefault();
        e.stopPropagation();

        const rootCode = $(this).data('rootcode');
        const rootName = $(this).data('rootname');

        console.log('Module assignment button clicked for:', { rootCode, rootName });

        // Validate data
        if (!rootCode || !rootName) {
            showToast('Invalid root data. Please try again.', 'error');
            return;
        }

        // Set modal data
        $('#displayRootCode').text(rootCode);
        $('#displayRootName').text(rootName);

        // Clear previous data
        $('#assignedModules, #availableModules').empty();

        // Show modal safely with enhanced handling
        showModuleModalSafely(rootCode);
    });

    // Enhanced Module Modal Show Function
    function showModuleModalSafely(rootCode) {
        const modalElement = document.getElementById('moduleAssignmentModal');

        if (!modalElement) {
            console.error('Module assignment modal not found');
            showToast('Modal not found', 'error');
            return;
        }

        try {
            // Show modal using Bootstrap API
            const modal = new bootstrap.Modal(modalElement, {
                backdrop: 'static',
                keyboard: true,
                focus: true
            });

            modal.show();

            // Load data after modal is shown
            modalElement.addEventListener('shown.bs.modal', function () {
                loadModuleAssignment(rootCode);
            }, { once: true });

        } catch (error) {
            console.error('Error showing module modal:', error);

            // Fallback: direct DOM manipulation
            modalElement.style.display = 'block';
            modalElement.classList.add('show');
            modalElement.setAttribute('aria-hidden', 'false');
            document.body.classList.add('modal-open');

            // Create backdrop if it doesn't exist
            if (!document.querySelector('.modal-backdrop')) {
                const backdrop = document.createElement('div');
                backdrop.className = 'modal-backdrop fade show';
                document.body.appendChild(backdrop);
            }

            // Load data after a delay
            setTimeout(() => {
                loadModuleAssignment(rootCode);
            }, 500);
        }
    }

    // UNIVERSAL MODAL SHOW FUNCTION WITH MULTIPLE FALLBACKS (Kept for compatibility)
    function showModalSafely(modalId) {
        console.log('Attempting to show modal:', modalId);

        const modalElement = document.getElementById(modalId);
        if (!modalElement) {
            console.error('Modal element not found:', modalId);
            showToast('Modal not found', 'error');
            return;
        }

        // For root modal, use the enhanced function
        if (modalId === 'rootModal') {
            showRootModalSafely('add');
            return;
        }

        // Method 1: Bootstrap 5 Modal API
        try {
            console.log('Trying Bootstrap 5 Modal API...');

            // Dispose any existing modal instance
            const existingModal = bootstrap.Modal.getInstance(modalElement);
            if (existingModal) {
                existingModal.dispose();
            }

            const modal = new bootstrap.Modal(modalElement, {
                backdrop: 'static',
                keyboard: true,
                focus: true
            });
            modal.show();
            console.log('Bootstrap 5 Modal API worked');
            return;
        } catch (error) {
            console.error('Bootstrap 5 Modal API failed:', error);
        }

        // Method 2: jQuery Bootstrap (if available)
        try {
            console.log('Trying jQuery Bootstrap modal...');
            $(`#${modalId}`).modal('show');
            console.log('jQuery Bootstrap modal worked');
            return;
        } catch (error) {
            console.error('jQuery Bootstrap modal failed:', error);
        }

        // Method 3: Direct DOM manipulation
        try {
            console.log('Trying direct DOM manipulation...');

            // Remove any existing backdrop
            $('.modal-backdrop').remove();

            // Add backdrop
            const backdrop = document.createElement('div');
            backdrop.className = 'modal-backdrop fade show';
            backdrop.style.zIndex = '1050';
            document.body.appendChild(backdrop);

            // Show modal
            modalElement.style.display = 'block';
            modalElement.style.zIndex = '1055';
            modalElement.classList.add('show');
            modalElement.setAttribute('aria-hidden', 'false');

            // Add body class
            document.body.classList.add('modal-open');

            console.log('Direct DOM manipulation worked');

            // Add close functionality
            $(modalElement).find('[data-bs-dismiss="modal"], .btn-secondary').off('click.modalClose').on('click.modalClose', function () {
                hideModalSafely(modalId);
            });

            // Add backdrop click to close
            $(backdrop).off('click.modalClose').on('click.modalClose', function () {
                hideModalSafely(modalId);
            });

            return;
        } catch (error) {
            console.error('Direct DOM manipulation failed:', error);
        }

        // Method 4: Emergency fallback
        console.log('All modal methods failed, using emergency fallback');
        showToast('Modal system error. Refreshing page...', 'error');
        setTimeout(() => {
            location.reload();
        }, 2000);
    }

    // ENHANCED MODAL HIDE FUNCTION
    function hideModalSafely(modalId) {
        console.log('Hiding modal:', modalId);

        const modalElement = document.getElementById(modalId);
        if (!modalElement) return;

        try {
            // Try Bootstrap 5 API first
            const modal = bootstrap.Modal.getInstance(modalElement);
            if (modal) {
                modal.hide();
                return;
            }
        } catch (error) {
            console.error('Bootstrap hide failed:', error);
        }

        try {
            // Try jQuery
            $(`#${modalId}`).modal('hide');
            return;
        } catch (error) {
            console.error('jQuery hide failed:', error);
        }

        // Direct DOM manipulation
        modalElement.style.display = 'none';
        modalElement.classList.remove('show');
        modalElement.setAttribute('aria-hidden', 'true');
        $('.modal-backdrop').remove();
        document.body.classList.remove('modal-open');
    }

    // Delete (Soft Delete)
    $('#rootTable').on('click', '.deleteBtn', function (e) {
        e.preventDefault();
        e.stopPropagation();

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

    // Enhanced Save (Add or Edit) with better form validation
    $('#rootForm').submit(function (e) {
        e.preventDefault();

        const isEdit = $('#rowIndex').val() !== '';

        // Enhanced form validation
        const requiredFields = ['rootOwner', 'rootName', 'rootPhone', 'rootEmail', 'rootFees', 'rootAddress', 'numCenters', 'numUsers'];
        let isValid = true;
        let firstInvalidField = null;

        requiredFields.forEach(fieldId => {
            const field = $(`#${fieldId}`);
            const value = field.val().trim();

            if (!value) {
                field.addClass('is-invalid');
                if (!firstInvalidField) {
                    firstInvalidField = field;
                }
                isValid = false;
            } else {
                field.removeClass('is-invalid');
            }
        });

        // Email validation
        const email = $('#rootEmail').val().trim();
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (email && !emailRegex.test(email)) {
            $('#rootEmail').addClass('is-invalid');
            if (!firstInvalidField) {
                firstInvalidField = $('#rootEmail');
            }
            isValid = false;
        }

        // Phone validation (basic)
        const phone = $('#rootPhone').val().trim();
        if (phone && phone.length < 10) {
            $('#rootPhone').addClass('is-invalid');
            if (!firstInvalidField) {
                firstInvalidField = $('#rootPhone');
            }
            isValid = false;
        }

        if (!isValid) {
            showToast('Please fill in all required fields correctly', 'error');
            if (firstInvalidField) {
                firstInvalidField.focus();
            }
            return;
        }

        const rootData = {
            rootOwner: $('#rootOwner').val().trim(),
            rootName: $('#rootName').val().trim(),
            rootPhone: $('#rootPhone').val().trim(),
            rootEmail: $('#rootEmail').val().trim(),
            rootFees: parseFloat($('#rootFees').val()) || 0,
            rootAddress: $('#rootAddress').val().trim(),
            noOfCenter: parseInt($('#numCenters').val()) || 0,
            noOfUser: parseInt($('#numUsers').val()) || 0,
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

        // Show loading state
        const $submitBtn = $('#rootForm button[type="submit"]');
        const originalText = $submitBtn.html();
        $submitBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-2"></i>Saving...');

        $.ajax({
            url: url,
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(rootData),
            success: function (response) {
                console.log('Save response:', response);
                hideModalSafely('rootModal');
                loadRootData(currentPage, centerFilter);
                showToast(`Root ${isEdit ? 'updated' : 'added'} successfully`, 'success');
            },
            error: function (xhr, status, error) {
                console.error("Save error:", { xhr, status, error, responseText: xhr.responseText });
                showToast('Failed to save root: ' + (xhr.responseText || error), 'error');
            },
            complete: function () {
                // Reset button state
                $submitBtn.prop('disabled', false).html(originalText);
            }
        });
    });

    // Enhanced Module Assignment Loading
    function loadModuleAssignment(rootCode) {
        console.log('Loading module assignment for rootCode:', rootCode);

        if (!rootCode) {
            showToast('No root code provided for module assignment', 'error');
            return;
        }

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
            timeout: 15000, // Increased timeout
            success: function (modules) {
                console.log('Assigned modules received:', modules);
                $('#assignedModules').empty();

                if (!modules || modules.length === 0) {
                    $('#assignedModules').html('<div class="text-center text-muted py-4"><i class="fas fa-info-circle"></i><br>No assigned modules</div>');
                } else {
                    modules.forEach(m => {
                        const moduleElement = createModuleElement(m);
                        if (moduleElement) {
                            $('#assignedModules').append(moduleElement);
                        }
                    });
                }
                setupDragAndDrop();
            },
            error: function (xhr, status, error) {
                console.error('Error loading assigned modules:', { xhr, status, error, responseText: xhr.responseText });
                $('#assignedModules').html(`<div class="text-danger text-center py-4"><i class="fas fa-exclamation-triangle"></i><br>Error loading assigned modules<br><small>Please try again</small></div>`);
            }
        });

        // Load available modules
        $.ajax({
            url: '/Root/GetAvailableModules',
            method: 'GET',
            data: { rootCode: rootCode },
            dataType: 'json',
            timeout: 15000, // Increased timeout
            success: function (modules) {
                console.log('Available modules received:', modules);
                $('#availableModules').empty();

                if (!modules || modules.length === 0) {
                    $('#availableModules').html('<div class="text-center text-muted py-4"><i class="fas fa-info-circle"></i><br>No available modules</div>');
                } else {
                    modules.forEach(m => {
                        const moduleElement = createModuleElement(m);
                        if (moduleElement) {
                            $('#availableModules').append(moduleElement);
                        }
                    });
                }
                setupDragAndDrop();
            },
            error: function (xhr, status, error) {
                console.error('Error loading available modules:', { xhr, status, error, responseText: xhr.responseText });
                $('#availableModules').html(`<div class="text-danger text-center py-4"><i class="fas fa-exclamation-triangle"></i><br>Error loading available modules<br><small>Please try again</small></div>`);
            }
        });
    }

    // Enhanced Module Element Creation
    function createModuleElement(module) {
        if (!module) return null;

        const moduleCode = module.moduleCode || module.ModuleCode;
        const moduleName = module.moduleName || module.ModuleName;

        if (!moduleCode || !moduleName) return null;

        return $(`
            <div class="module-item" draggable="true" data-modulecode="${moduleCode}" title="Drag to move between lists">
                <div class="d-flex align-items-center w-100">
                    <i class="fas fa-grip-vertical me-2 text-muted"></i>
                    <div class="flex-grow-1">
                        <div class="fw-semibold">${moduleName}</div>
                        <small class="text-muted">Code: ${moduleCode}</small>
                    </div>
                </div>
            </div>
        `);
    }

    // Enhanced Drag and Drop with better visual feedback
    function setupDragAndDrop() {
        console.log('Setting up enhanced drag and drop...');

        $('.module-item').off('dragstart dragend');
        $('.modules-container').off('dragover dragenter dragleave drop');

        $('.module-item').on('dragstart', function (e) {
            const moduleCode = $(this).data('modulecode');
            e.originalEvent.dataTransfer.setData('text/plain', moduleCode);
            $(this).addClass('dragging').css('opacity', '0.5');
            console.log('Drag started for module:', moduleCode);
        });

        $('.module-item').on('dragend', function () {
            $(this).removeClass('dragging').css('opacity', '1');
            $('.modules-container').removeClass('drag-over');
        });

        $('.modules-container').on('dragover', function (e) {
            e.preventDefault();
            e.originalEvent.dataTransfer.dropEffect = 'move';
            $(this).addClass('drag-over');
        });

        $('.modules-container').on('dragenter', function (e) {
            e.preventDefault();
            $(this).addClass('drag-over');
        });

        $('.modules-container').on('dragleave', function (e) {
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

            if ($draggedElement.length && !targetContainer.has($draggedElement).length) {
                // Add visual feedback
                $draggedElement.fadeOut(200, function () {
                    $(this).appendTo(targetContainer).fadeIn(200);
                    setupDragAndDrop();
                });

                const targetType = targetContainerId === 'assignedModules' ? 'assigned' : 'available';
                showToast(`Module moved to ${targetType} list`, 'info');
                console.log('Module moved successfully');
            }
        });

        console.log('Enhanced drag and drop setup complete. Found', $('.module-item').length, 'module items');
    }

    // Enhanced Module Assignment Save
    $('#saveModuleAssignments').click(function () {
        const rootCode = parseInt($('#displayRootCode').text(), 10);

        if (isNaN(rootCode)) {
            showToast('No root selected', 'error');
            return;
        }

        const assignments = [];
        $('#assignedModules .module-item').each(function () {
            const moduleCode = parseInt($(this).data('modulecode'), 10);
            if (!isNaN(moduleCode)) {
                assignments.push(moduleCode);
            }
        });

        console.log('Saving module assignments:', { rootCode, assignments });

        const $saveBtn = $(this);
        const originalHtml = $saveBtn.html();
        $saveBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-2"></i>Saving...');

        $.ajax({
            url: '/Root/SaveModuleAssignments',
            type: 'POST',
            contentType: "application/json",
            data: JSON.stringify({
                rootCode: rootCode,
                moduleCodes: assignments
            }),
            timeout: 15000,
            success: function (response) {
                console.log('Module assignments saved successfully:', response);
                hideModalSafely('moduleAssignmentModal');
                showToast('Module assignments saved successfully', 'success');
            },
            error: function (xhr, status, error) {
                console.error('Error saving module assignments:', { xhr, status, error });
                showToast('Failed to save module assignments: ' + (xhr.responseText || error), 'error');
            },
            complete: function () {
                $saveBtn.prop('disabled', false).html(originalHtml);
            }
        });
    });

    // Enhanced Toast Function
    function showToast(message, type = 'info') {
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
            <div class="toast align-items-center text-bg-${bgMap[type] || 'primary'} border-0" role="alert" id="${toastId}" style="z-index: 9999;">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="fas fa-${iconMap[type] || 'info-circle'} me-2"></i>
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>
        `;

        if (!$('#toast-container').length) {
            $('body').append('<div id="toast-container" class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 9999;"></div>');
        }

        $('#toast-container').append(toastHtml);

        try {
            const toast = new bootstrap.Toast(document.getElementById(toastId), {
                delay: type === 'error' ? 6000 : 4000
            });
            toast.show();
            $(`#${toastId}`).on('hidden.bs.toast', function () { $(this).remove(); });
        } catch (error) {
            console.error('Toast error:', error);
            // Fallback to alert
            alert(message);
        }
    }

    // Window resize handler for modal sizing
    $(window).on('resize', function () {
        if ($('#rootModal').hasClass('show')) {
            adjustRootModalSize();
        }
    });

    // Form field validation on blur
    $('#rootForm input, #rootForm select').on('blur', function () {
        $(this).removeClass('is-invalid');

        const value = $(this).val().trim();
        if ($(this).prop('required') && !value) {
            $(this).addClass('is-invalid');
        }

        // Specific validations
        if ($(this).attr('type') === 'email' && value) {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            if (!emailRegex.test(value)) {
                $(this).addClass('is-invalid');
            }
        }
    });

    // Emergency escape hatch
    $(document).keydown(function (e) {
        if (e.key === 'Escape') {
            console.log('Escape pressed, hiding all modals');
            hideModalSafely('rootModal');
            hideModalSafely('moduleAssignmentModal');
        }
    });

    // Global error handler for AJAX requests
    $(document).ajaxError(function (event, xhr, settings, thrownError) {
        if (xhr.status === 404) {
            console.error('AJAX 404 Error - URL not found:', settings.url);
            showToast('The requested resource was not found', 'error');
        } else if (xhr.status === 500) {
            console.error('AJAX 500 Error - Server error:', settings.url);
            showToast('A server error occurred. Please try again.', 'error');
        } else if (xhr.status === 0 && thrownError !== 'abort') {
            console.error('AJAX Network Error:', settings.url);
            showToast('Network error. Please check your connection.', 'error');
        }
    });

    console.log('Enhanced Root management system initialized successfully');
});