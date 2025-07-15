// --- Localization Helper ---
function getJsString(key) {
    // The data attribute will be in kebab-case, so convert key to lower case and replace underscores with hyphens
    return $('#js-localization').data(key.toLowerCase());
}

// --- Set all static and header labels on page load ---
function setItemLabels() {
    $('#filterRootLabel').text(getJsString('filter-by-root-label'));
    $('#filterRootCode').html(`<option value="">${getJsString('all-roots-option')}</option>`);
    $('#freeItemsLabel').text(getJsString('free-items-label'));
    $('#downloadLastGeneratedBtn span').text(getJsString('download-last-generated-btn'));
    $('#addItemBtn span').text(getJsString('add-item-btn'));
    $('#itemCodeHeader').text(getJsString('table-item-code-header'));
    $('#studentNameHeader').text(getJsString('table-student-name-header'));
    $('#itemTypeHeader').text(getJsString('table-item-type-header'));
    $('#itemKeyHeader').text(getJsString('table-item-key-header'));
    $('#actionsHeader').text(getJsString('table-actions-header'));
    $('#addItemModalLabel').text(getJsString('add-items-title'));
    $('#rootLabel').text(getJsString('root-label'));
    $('#itemTypeLabelAdd').text(getJsString('item-type-label'));
    $('#numberOfItemsLabel').text(getJsString('record-count-label'));
    $('#RecordCount').attr('placeholder', getJsString('record-count-placeholder'));
    $('#addItemsBtn span').text(getJsString('add-btn'));
    $('#cancelBtn span').text(getJsString('cancel-btn'));
    $('#editItemModalLabel').text(getJsString('edit-item-title'));
    $('#studentCodeLabel').text(getJsString('student-code-label'));
    $('#saveChangesBtn span').text(getJsString('save-changes-btn'));
    $('#cancelBtn2 span').text(getJsString('cancel-btn'));
    $('#qrModalLabel').text(getJsString('qr-modal-title'));
    $('#downloadQRBtn span').text(getJsString('download-qr-btn'));
}

$(document).ready(function () {
    setItemLabels();

    let itemTypeMap = {};
    let rootMap = {};
    let currentPage = 1;
    const pageSize = 10;
    let totalRecords = 0;
    let loggedInUserCode = null;
    let selectedRootCode = "";
    let lastGeneratedItems = [];

    function fixBodyScrolling() {
        setTimeout(function () {
            $('.modal-backdrop').remove();
            document.body.style.overflow = 'auto';
            $('html').css('overflow', 'auto');
        }, 400);
    }

    function showAlert(msg) {
        alert(msg);
    }

    // Fetch logged-in user code on load
    function fetchLoggedInUserCode(callback) {
        $.ajax({
            url: '/Item/GetLoggedInUserCode',
            method: 'GET',
            success: function (data) {
                loggedInUserCode = data.userCode;
                if (callback) callback();
            },
            error: function () {
                showAlert(getJsString('failed-to-get-logged-in-user'));
                if (callback) callback();
            }
        });
    }

    function loadRootFilterDropdown() {
        $.ajax({
            url: '/Item/GetRootCodes',
            method: 'GET',
            success: function (roots) {
                const $select = $('#filterRootCode');
                $select.empty().append(`<option value="">${getJsString('all-roots-option')}</option>`);
                if (roots && roots.length > 0) {
                    roots.forEach(function (root) {
                        $select.append(
                            `<option value="${root.code}">${root.name} (${root.code})</option>`
                        );
                    });
                }
            }
        });
    }

    function fetchRootCodes(callback) {
        $.ajax({
            url: '/Item/GetRootCodes',
            method: 'GET',
            success: function (roots) {
                rootMap = {};
                const $select = $('#rootCode');
                $select.empty();
                $select.append(`<option value="">${getJsString('select-root-option')}</option>`);
                if (roots && roots.length > 0) {
                    roots.forEach(function (root) {
                        rootMap[root.code] = root.name;
                        $select.append(
                            `<option value="${root.code}">${root.name} (${root.code})</option>`
                        );
                    });
                }
                $select.closest('.mb-3').show();
                if (callback) callback();
            },
            error: function (xhr) {
                showAlert(getJsString('failed-to-load-roots'));
                $('#rootCode').empty().append(`<option value="">${getJsString('no-roots-found')}</option>`);
                $('#rootCode').closest('.mb-3').show();
                if (callback) callback();
            }
        });
    }

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
                showAlert(getJsString('failed-to-load-item-types-for-mapping'));
                if (callback) callback();
            }
        });
    }

    function loadItems(page) {
        let url = `/Item/GetAllItems?page=${page}&pageSize=${pageSize}`;
        if (selectedRootCode) url += `&rootCode=${selectedRootCode}`;
        $.ajax({
            url: url,
            method: 'GET',
            success: function (result) {
                let items = result.data;
                totalRecords = result.totalCount;
                let rows = '';

                items.forEach(item => {
                    const studentName = item.studentName ?? '';
                    const itemTypeName = itemTypeMap[item.itemTypeKey] || item.itemTypeKey;
                    const qrId = `qr-${item.itemCode}`;
                    rows += `<tr>
                        <td>${item.itemCode}</td>
                        <td>${studentName}</td>
                        <td>${itemTypeName}</td>
                        <td>
                            <div id="${qrId}" class="qr-code-cell"></div>
                        </td>
                        <td>
                          <button class="btn-table edit edit-btn" data-itemcode="${item.itemCode}"><i class="fas fa-pencil"></i> ${getJsString('edit-btn')}</button>
                          <button class="btn-table delete delete-btn" data-itemcode="${item.itemCode}"><i class="fas fa-trash"></i> ${getJsString('delete-btn')}</button>
                          <button class="btn-table add download-qr-btn" data-qrid="${qrId}" data-itemkey="${item.itemKey}"><i class="fas fa-download"></i> ${getJsString('download-qr-action-btn')}</button>
                        </td>
                    </tr>`;
                });

                $('#itemsTable tbody').html(rows);

                items.forEach(item => {
                    const qrId = `qr-${item.itemCode}`;
                    $(`#${qrId}`).empty();
                    new QRCode(document.getElementById(qrId), {
                        text: item.itemKey,
                        width: 80,
                        height: 80
                    });
                });

                loadGlobalFreeItemCount();
                renderPagination();
                fixBodyScrolling();
            },
            error: function () {
                showAlert(getJsString('failed-to-retrieve-items'));
            }
        });
    }

    function loadGlobalFreeItemCount() {
        let url = '/Item/GetFreeItemCount';
        if (selectedRootCode) url += `?rootCode=${selectedRootCode}`;
        $.ajax({
            url: url,
            method: 'GET',
            success: function (data) {
                $('#freeItemCount').text(data.freeCount);
            },
            error: function () {
                $('#freeItemCount').text(getJsString('error'));
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
            <a class="page-link" href="#" data-page="${currentPage - 1}">${getJsString('pagination-previous')}</a>
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
            <a class="page-link" href="#" data-page="${currentPage + 1}">${getJsString('pagination-next')}</a>
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
                $select.append(`<option value="">${getJsString('select-item-type-option')}</option>`);
                types.forEach(type => {
                    $select.append(`<option value="${type.code}">${type.name}</option>`);
                });
            },
            error: function (xhr) {
                showAlert(getJsString('failed-to-load-item-types'));
            }
        });
    }

    $('#addItemModal').on('show.bs.modal', function () {
        fetchRootCodes();
        loadItemTypes();
        $('#rootCode').closest('.mb-3').show();
        var $btn = $('#addItemsBtn');
        $btn.prop('disabled', false);
        $btn.find('span').text(getJsString('add-btn'));
    });

    $('#insertItemsForm').submit(function (e) {
        e.preventDefault();

        var $btn = $('#addItemsBtn');
        var originalText = $btn.find('span').text();
        $btn.prop('disabled', true);
        $btn.find('span').text(getJsString('processing'));

        const dataToSend = {
            rootCode: parseInt($('#rootCode').val()),
            insertUserCode: loggedInUserCode,
            itemTypeCode: parseInt($('#itemTypeCode').val()),
            recordCount: parseInt($('#RecordCount').val())
        };

        if (!dataToSend.rootCode || !dataToSend.insertUserCode || !dataToSend.itemTypeCode || !dataToSend.recordCount) {
            showAlert(getJsString('please-fill-all-fields'));
            $btn.prop('disabled', false);
            $btn.find('span').text(originalText);
            return;
        }

        $.ajax({
            url: '/Item/InsertItems',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(dataToSend),
            success: function (response) {
                showAlert(response.message);
                if (response.lastInsertedItems && response.lastInsertedItems.length > 0) {
                    lastGeneratedItems = response.lastInsertedItems;
                    $('#downloadLastGeneratedBtn').prop('disabled', false);
                } else {
                    lastGeneratedItems = [];
                    $('#downloadLastGeneratedBtn').prop('disabled', true);
                }
                fetchItemTypesMap(function () {
                    loadItems(currentPage);
                });
                loadGlobalFreeItemCount();
                $('#insertItemsForm')[0].reset();
                $btn.prop('disabled', false);
                $btn.find('span').text(originalText);
                const modal = bootstrap.Modal.getInstance(document.getElementById('addItemModal'));
                if (modal) modal.hide();
                fixBodyScrolling();
            },
            error: function (xhr) {
                showAlert(getJsString('error') + ': ' + (xhr.responseJSON?.error || xhr.statusText));
                lastGeneratedItems = [];
                $('#downloadLastGeneratedBtn').prop('disabled', true);
                $btn.prop('disabled', false);
                $btn.find('span').text(originalText);
                fixBodyScrolling();
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
                fixBodyScrolling();
            },
            error: function (xhr, status, error) {
                showAlert(getJsString('error-updating-item') + error);
                fixBodyScrolling();
            }
        });
    });

    $('#itemsTable').on('click', '.delete-btn', function () {
        if (!confirm(getJsString('delete-confirm'))) return;
        const itemCode = $(this).data('itemcode');
        $.ajax({
            url: '/Item/SoftDeleteItem',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(itemCode),
            success: function (response) {
                showAlert(response.message);
                loadItems(currentPage);
                loadGlobalFreeItemCount();
                fixBodyScrolling();
            },
            error: function (xhr) {
                showAlert(getJsString('error-deleting-item') + (xhr.responseJSON?.error || xhr.statusText));
                fixBodyScrolling();
            }
        });
    });

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
            showAlert(getJsString('no-qrcode-found'));
        }
    });

    $('#downloadLastGeneratedBtn').on('click', function () {
        if (!lastGeneratedItems || lastGeneratedItems.length === 0) {
            showAlert(getJsString('no-generated-items-found'));
            return;
        }

        let zip = new JSZip();
        let count = 0;
        let total = lastGeneratedItems.length;
        let $hiddenDiv = $('<div id="hidden-qrs" style="position:absolute;left:-9999px;top:-9999px;"></div>').appendTo('body');

        lastGeneratedItems.forEach(function (item) {
            let qrDivId = 'hidden-qr-' + item.itemCode;
            let $qrDiv = $('<div></div>').attr('id', qrDivId);
            $hiddenDiv.append($qrDiv);

            let qr = new QRCode(document.getElementById(qrDivId), {
                text: item.itemKey,
                width: 256,
                height: 256
            });

            setTimeout(function () {
                try {
                    let canvas = $qrDiv.find('canvas')[0];
                    let imgData;
                    if (canvas) {
                        imgData = canvas.toDataURL("image/png").split(',')[1];
                    } else {
                        let img = $qrDiv.find('img')[0];
                        if (img) {
                            imgData = img.src.split(',')[1];
                        }
                    }
                    if (imgData) {
                        zip.file(item.itemKey + '.png', imgData, { base64: true });
                    }
                } catch (e) { }
                count++;
                if (count === total) {
                    zip.generateAsync({ type: "blob" }).then(function (content) {
                        saveAs(content, "LastGeneratedQRCodes.zip");
                        $hiddenDiv.remove();
                        $('#downloadLastGeneratedBtn').prop('disabled', true);
                        lastGeneratedItems = [];
                        fixBodyScrolling();
                    });
                }
            }, 300);
        });
    });

    $('#filterRootCode').on('change', function () {
        selectedRootCode = $(this).val();
        currentPage = 1;
        loadItems(currentPage);
        loadGlobalFreeItemCount();
        fixBodyScrolling();
    });

    fetchLoggedInUserCode(function () {
        loadRootFilterDropdown();
        fetchItemTypesMap(function () {
            loadItems(currentPage);
            loadGlobalFreeItemCount();
            fixBodyScrolling();
        });
    });
});