$(document).ready(function () {
    let itemTypeMap = {};
    let currentPage = 1;
    const pageSize = 10;
    let totalRecords = 0;

    function fetchItemTypesMap(callback) {
        $.ajax({
            url: '/Item/GetItemTypes',
            method: 'GET',
            success: function (types) {
                itemTypeMap = {};
                types.forEach(function (type) {
                    itemTypeMap[type.code] = type.name;
                });
                if (callback) callback();
            },
            error: function () {
                alert('Failed to load item types for mapping.');
                if (callback) callback();
            }
        });
    }

    // No longer used: free count is now global, remove this function if no longer needed
    // function countFreeItems(items) {
    //     return items.filter(item => {
    //         const studentName = (item.studentName ?? '').toString().trim();
    //         return studentName === '';
    //     }).length;
    // }

    function loadItems(page) {
        $.ajax({
            url: `/Item/GetAllItems?page=${page}&pageSize=${pageSize}`,
            method: 'GET',
            success: function (result) {
                let items = result.data;
                totalRecords = result.totalCount;
                let rows = '';

                items.forEach(item => {
                    const studentName = item.studentName ?? '';
                    const itemTypeName = itemTypeMap[item.itemTypeKey] || item.itemTypeKey;
                    // Unique ID for QR codes
                    const qrId = `qr-${item.itemCode}`;
                    rows += `<tr>
                        <td>${item.itemCode}</td>
                        <td>${studentName}</td>
                        <td>${itemTypeName}</td>
                        <td>
                            <div id="${qrId}" class="qr-code-cell"></div>
                        </td>
                        <td>
                          <button class="btn btn-sm btn-primary edit-btn" data-itemcode="${item.itemCode}">Edit</button>
                          <button class="btn btn-sm btn-danger delete-btn" data-itemcode="${item.itemCode}">Delete</button>
                          <button class="btn btn-sm btn-success download-qr-btn" data-qrid="${qrId}" data-itemkey="${item.itemKey}">Download QR</button>
                        </td>
                    </tr>`;
                });

                $('#itemsTable tbody').html(rows);

                // Generate QR codes
                items.forEach(item => {
                    const qrId = `qr-${item.itemCode}`;
                    $(`#${qrId}`).empty();
                    new QRCode(document.getElementById(qrId), {
                        text: item.itemKey,
                        width: 80,
                        height: 80
                    });
                });

                // Global free item count
                loadGlobalFreeItemCount();

                renderPagination();
            },
            error: function () {
                alert('Failed to retrieve items data.');
            }
        });
    }

    // Global free item count function
    function loadGlobalFreeItemCount() {
        $.ajax({
            url: '/Item/GetFreeItemCount',
            method: 'GET',
            success: function (data) {
                $('#freeItemCount').text(data.freeCount);
            },
            error: function () {
                $('#freeItemCount').text('Error');
            }
        });
    }

    function renderPagination() {
        const totalPages = Math.ceil(totalRecords / pageSize);
        let paginationHtml = '';

        if (totalPages <= 1) {
            $('#pagination').html('');
            return;
        }

        paginationHtml += `<li class="page-item${currentPage === 1 ? ' disabled' : ''}">
            <a class="page-link" href="#" data-page="${currentPage - 1}">Previous</a>
        </li>`;

        let startPage = Math.max(1, currentPage - 2);
        let endPage = Math.min(totalPages, currentPage + 2);
        if (currentPage <= 3) endPage = Math.min(5, totalPages);
        if (currentPage > totalPages - 3) startPage = Math.max(1, totalPages - 4);

        for (let i = startPage; i <= endPage; i++) {
            paginationHtml += `<li class="page-item${i === currentPage ? ' active' : ''}">
                <a class="page-link" href="#" data-page="${i}">${i}</a>
            </li>`;
        }

        paginationHtml += `<li class="page-item${currentPage === totalPages ? ' disabled' : ''}">
            <a class="page-link" href="#" data-page="${currentPage + 1}">Next</a>
        </li>`;

        $('#pagination').html(`<ul class="pagination">${paginationHtml}</ul>`);
    }

    $('#pagination').on('click', '.page-link', function (e) {
        e.preventDefault();
        const page = parseInt($(this).data('page'), 10);
        const totalPages = Math.ceil(totalRecords / pageSize);
        if (page > 0 && page <= totalPages && page !== currentPage) {
            currentPage = page;
            loadItems(currentPage);
        }
    });

    function loadItemTypes() {
        $.ajax({
            url: '/Item/GetItemTypes',
            method: 'GET',
            success: function (types) {
                const $select = $('#itemTypeCode');
                $select.empty();
                $select.append('<option value="">Select Item Type...</option>');
                types.forEach(type => {
                    $select.append(`<option value="${type.code}">${type.name}</option>`);
                });
            },
            error: function (xhr) {
                console.error('Failed to load item types.', xhr);
                alert('Failed to load item types.');
            }
        });
    }

    $('#addItemModal').on('show.bs.modal', function () {
        loadItemTypes();
    });

    $('#insertItemsForm').submit(function (e) {
        e.preventDefault();

        const dataToSend = {
            rootCode: parseInt($('#rootCode').val()),
            insertUserCode: parseInt($('#InsertUser').val()),
            itemTypeCode: parseInt($('#itemTypeCode').val()),
            recordCount: parseInt($('#RecordCount').val())
        };

        $.ajax({
            url: '/Item/InsertItems',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(dataToSend),
            success: function (response) {
                alert(response.message);
                fetchItemTypesMap(function () {
                    loadItems(currentPage);
                });
                loadGlobalFreeItemCount();
                $('#insertItemsForm')[0].reset();
                const modal = bootstrap.Modal.getInstance(document.getElementById('addItemModal'));
                if (modal) modal.hide();
            },
            error: function (xhr) {
                alert('Error: ' + (xhr.responseJSON?.error || xhr.statusText));
            }
        });
    });

    $('#itemsTable').on('click', '.edit-btn', function () {
        const itemCode = parseInt($(this).data('itemcode'));
        $('#editItemCode').val(itemCode);

        const modal = new bootstrap.Modal(document.getElementById('editItemModal'));
        modal.show();
    });

    $('#editItemForm').submit(function (e) {
        e.preventDefault();

        const dataToSend = {
            itemCode: parseInt($('#editItemCode').val()),
            studentCode: $('#editStudentCode').val()
        };

        $.ajax({
            url: '/Item/UpdateItem',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(dataToSend),
            success: function () {
                const modalEl = document.getElementById('editItemModal');
                const modal = bootstrap.Modal.getInstance(modalEl);
                modal.hide();

                loadItems(currentPage);
                loadGlobalFreeItemCount();
            },
            error: function (xhr, status, error) {
                alert('Error updating item: ' + error);
            }
        });
    });

    $('#itemsTable').on('click', '.delete-btn', function () {
        if (!confirm('Are you sure you want to delete this item?')) return;

        const itemCode = $(this).data('itemcode');

        $.ajax({
            url: '/Item/SoftDeleteItem',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(itemCode),
            success: function (response) {
                alert(response.message);
                loadItems(currentPage);
                loadGlobalFreeItemCount();
            },
            error: function (xhr) {
                alert('Error deleting item: ' + (xhr.responseJSON?.error || xhr.statusText));
            }
        });
    });

    // Download QR Code as image for the corresponding row
    $('#itemsTable').on('click', '.download-qr-btn', function () {
        const qrId = $(this).data('qrid');
        const itemKey = $(this).data('itemkey');
        let canvas = $(`#${qrId} canvas`)[0];
        let img = $(`#${qrId} img`)[0];

        if (canvas) {
            let url = canvas.toDataURL("image/png");
            let link = document.createElement('a');
            link.href = url;
            link.download = (itemKey ? itemKey : 'qr') + '.png';
            link.click();
        } else if (img) {
            let link = document.createElement('a');
            link.href = img.src;
            link.download = (itemKey ? itemKey : 'qr') + '.png';
            link.click();
        } else {
            alert('No QR code found to download!');
        }
    });

    // Initial load
    fetchItemTypesMap(function () {
        loadItems(currentPage);
        loadGlobalFreeItemCount();
    });
});