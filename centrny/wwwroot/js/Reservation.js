// === RESERVATION SYSTEM - MODERN UI/UX ===
// --- Localization Helper ---
function getJsString(key) {
    return $('#js-localization').attr('data-' + key.replace(/_/g, '-'));
}

$(document).ready(function () {
    // Detect which mode we're in
    var isCenter = typeof window.isCenter !== 'undefined'
        ? window.isCenter
        : ($("#rootAddReservationBtn").length === 0);

    console.log('Reservation system mode:', isCenter ? 'Center' : 'Non-Center');

    // ================= NON-CENTER FLOW =================
    if (!isCenter) {
        // Set labels with modern styling
        $('#rootDateLabel').text(getJsString('date-label'));
        $('#rootAddBtnText').text(getJsString('add-reservation-btn'));

        let singleTeacher = null;
        let selectedDate = new Date().toISOString().slice(0, 10);

        function fetchSingleTeacher(cb) {
            if (singleTeacher) {
                if (cb) cb(singleTeacher);
                return;
            }
            $.getJSON('/Reservation/GetSingleTeacherForRoot', function (data) {
                if (data.success) {
                    singleTeacher = { teacherCode: data.teacherCode, teacherName: data.teacherName };
                    if (cb) cb(singleTeacher);
                } else {
                    showReservationAlert(data.message || 'Teacher not found.', 'danger');
                }
            });
        }

        function loadRootReservations() {
            $('#rootReservationsList').html(`
                <div class="reservation-loading">
                    <div class="reservation-spinner"></div>
                    Loading reservations...
                </div>
            `);

            $.get('/Reservation/GetRootReservations', { reservationDate: selectedDate }, function (data) {
                if (!data.success) {
                    $('#rootReservationsList').html(`
                        <div class="reservation-empty">
                            <div class="reservation-empty-icon">⚠️</div>
                            <div class="reservation-empty-title">Error Loading Reservations</div>
                            <div class="reservation-empty-text">${data.message || 'Failed to load reservations.'}</div>
                        </div>
                    `);
                    return;
                }

                if (!data.reservations.length) {
                    $('#rootReservationsList').html(`
                        <div class="reservation-empty">
                            <div class="reservation-empty-icon">📅</div>
                            <div class="reservation-empty-title">No Reservations Found</div>
                            <div class="reservation-empty-text">No reservations scheduled for this date.</div>
                            <button class="reservation-btn reservation-btn-primary" onclick="$('#rootAddReservationBtn').click()">
                                <i class="bi bi-plus-circle"></i>
                                Add First Reservation
                            </button>
                        </div>
                    `);
                    return;
                }

                let cards = '';
                data.reservations.forEach(function (r) {
                    cards += `
                        <div class="reservation-horizontal-card">
                            <div class="reservation-card-content">
                                <div class="reservation-card-info">
                                    <div class="reservation-card-title">
                                        <div class="reservation-teacher-name">
                                            <i class="bi bi-person-circle"></i>
                                            ${r.teacherName}
                                        </div>
                                        <div class="reservation-description">${r.description || ''}</div>
                                    </div>
                                    <div class="reservation-card-details">
                                        <div class="reservation-badge reservation-badge-price">
                                            <i class="bi bi-currency-dollar"></i>
                                            ${r.cost || '0'}
                                        </div>
                                        <div class="reservation-badge reservation-badge-time">
                                            <i class="bi bi-clock"></i>
                                            ${r.reservationStartTime || ''} - ${r.reservationEndTime || ''}
                                        </div>
                                        ${r.capacity ? `<div class="reservation-badge" style="background: #e0f2fe; color: #0277bd;"><i class="bi bi-people"></i> ${r.capacity}</div>` : ''}
                                    </div>
                                </div>
                                <div class="reservation-card-actions">
                                    <button class="reservation-btn reservation-btn-icon reservation-btn-edit edit-root-btn" 
                                            title="${getJsString('edit-btn')}" 
                                            data-res='${JSON.stringify(r)}'>
                                        <i class="bi bi-pencil"></i>
                                    </button>
                                    <button class="reservation-btn reservation-btn-icon reservation-btn-delete delete-root-btn" 
                                            title="${getJsString('delete-btn')}" 
                                            data-res-code="${r.reservationCode}">
                                        <i class="bi bi-trash"></i>
                                    </button>
                                </div>
                            </div>
                        </div>
                    `;
                });
                $('#rootReservationsList').html(cards);
            }).fail(function () {
                $('#rootReservationsList').html(`
                    <div class="reservation-empty">
                        <div class="reservation-empty-icon">❌</div>
                        <div class="reservation-empty-title">Connection Error</div>
                        <div class="reservation-empty-text">Unable to connect to server. Please try again.</div>
                    </div>
                `);
            });
        }

        function calculatePeriod() {
            let start = $('#rootStartTime').val();
            let end = $('#rootEndTime').val();
            if (start && end) {
                let s = start.split(":");
                let e = end.split(":");
                let startMin = parseInt(s[0]) * 60 + parseInt(s[1]);
                let endMin = parseInt(e[0]) * 60 + parseInt(e[1]);
                let diff = (endMin - startMin) / 60;
                $('#rootPeriod').val(diff > 0 ? diff.toFixed(2) : '');
            } else {
                $('#rootPeriod').val('');
            }
        }

        // Event Handlers
        $('#rootAddReservationBtn').on('click', function () {
            fetchSingleTeacher(function (t) {
                $('#rootReservationModalLabel').text(getJsString('add-reservation-title'));
                $('#rootReservationSaveBtn').html('<i class="bi bi-plus-circle"></i> ' + getJsString('add-reservation-btn'));
                $('#rootReservationForm')[0].reset();
                $('#rootReservationCode').val('');
                $('#rootTeacherCode').val(t.teacherCode);
                $('#rootTeacherName').val(t.teacherName);
                $('#rootRTime').val(selectedDate);
                $('#rootStartTime, #rootEndTime').val('');
                $('#rootPeriod').val('');
                $('#rootReservationModal').modal('show');
            });
        });

        $(document).on('click', '.edit-root-btn', function () {
            let r = $(this).data('res');
            fetchSingleTeacher(function (t) {
                $('#rootReservationModalLabel').text(getJsString('edit-reservation-title'));
                $('#rootReservationSaveBtn').html('<i class="bi bi-check-circle"></i> ' + getJsString('save-changes-btn'));
                $('#rootReservationForm')[0].reset();
                $('#rootReservationCode').val(r.reservationCode);
                $('#rootTeacherCode').val(t.teacherCode);
                $('#rootTeacherName').val(t.teacherName);
                $('#rootDescription').val(r.description);
                $('#rootCapacity').val(r.capacity);
                $('#rootCost').val(r.cost);
                $('#rootDeposit').val(r.deposit);
                $('#rootFinalCost').val(r.finalCost || '');
                $('#rootRTime').val(r.rTime);
                $('#rootStartTime').val(r.reservationStartTime || '');
                $('#rootEndTime').val(r.reservationEndTime || '');
                $('#rootPeriod').val(r.period || '');
                $('#rootReservationModal').modal('show');
            });
        });

        $('#rootReservationForm').on('submit', function (e) {
            e.preventDefault();
            var isEdit = !!$('#rootReservationCode').val();
            var url = isEdit ? '/Reservation/EditRootReservation' : '/Reservation/AddRootReservation';

            // Show loading state
            var $submitBtn = $('#rootReservationSaveBtn');
            var originalText = $submitBtn.html();
            $submitBtn.html('<i class="reservation-spinner" style="width: 16px; height: 16px; margin-right: 8px;"></i> Saving...');
            $submitBtn.prop('disabled', true);

            $.ajax({
                url: url,
                type: 'POST',
                data: $(this).serialize(),
                success: function () {
                    $('#rootReservationModal').modal('hide');
                    loadRootReservations();
                    showReservationAlert(isEdit ? 'Reservation updated successfully!' : 'Reservation added successfully!', 'success');
                },
                error: function (xhr) {
                    var errorMsg = isEdit ? getJsString('failed-edit-reservation') : getJsString('failed-add-reservation');
                    showReservationAlert(errorMsg + (xhr.responseText || ''), 'danger');
                },
                complete: function () {
                    $submitBtn.html(originalText);
                    $submitBtn.prop('disabled', false);
                }
            });
        });

        $(document).on('click', '.delete-root-btn', function () {
            if (!confirm(getJsString('delete-confirm'))) return;
            var code = $(this).data('res-code');
            var $btn = $(this);
            $btn.html('<i class="reservation-spinner" style="width: 12px; height: 12px;"></i>');

            $.ajax({
                url: '/Reservation/DeleteRootReservation',
                type: 'POST',
                data: { reservationCode: code },
                success: function () {
                    loadRootReservations();
                    showReservationAlert('Reservation deleted successfully!', 'success');
                },
                error: function () {
                    showReservationAlert(getJsString('failed-delete-reservation'), 'danger');
                    $btn.html('<i class="bi bi-trash"></i>');
                }
            });
        });

        $('#rootDateSelect').val(selectedDate);
        $('#rootDateSelect').on('change', function () {
            selectedDate = $(this).val();
            loadRootReservations();
        });

        $('#rootStartTime, #rootEndTime').on('change', calculatePeriod);

        // Initialize
        fetchSingleTeacher(function () {
            loadRootReservations();
        });
    }

    // ================ CENTER FLOW ================
    if (isCenter) {
        let selectedBranch = null;
        let selectedDate = new Date().toISOString().slice(0, 10);
        let periods = [];
        let hallsCache = [];

        function setLabels() {
            $('#branchLabel').text(getJsString('branch-label'));
            $('#dateLabel').text(getJsString('date-label'));
            $('#branchSelect').html(`<option value="">${getJsString('select-branch-option')}</option>`);
            $('#addReservationModalLabel').text(getJsString('add-reservation-title'));
            $('#addTeacherLabel').text(getJsString('teacher-label'));
            $('#firstTimeText').text(getJsString('first-time-btn'));
            $('#addDescriptionLabel').text(getJsString('description-label'));
            $('#addCapacityLabel').text(getJsString('capacity-label'));
            $('#addCostLabel').text(getJsString('cost-label'));
            $('#addStartTimeLabel').text(getJsString('start-time-label'));
            $('#addEndTimeLabel').text(getJsString('end-time-label'));
            $('#addPeriodLabel').text(getJsString('period-label'));
            $('#addDepositLabel').text(getJsString('deposit-label'));
            $('#addFinalCostLabel').text(getJsString('final-cost-label'));
            $('#addTeacherModalLabel').text(getJsString('add-teacher-title'));
            $('#teacherNameLabel').text(getJsString('teacher-name-label'));
            $('#teacherPhoneLabel').text(getJsString('teacher-phone-label'));
            $('#teacherAddressLabel').text(getJsString('teacher-address-label'));
            $('#addTeacherBtn').text(getJsString('add-teacher-btn'));
            $('#closeTeacherBtn').text(getJsString('close-btn'));
            $('#editReservationModalLabel').text(getJsString('edit-reservation-title'));
            $('#editTeacherLabel').text(getJsString('teacher-label'));
            $('#editDescriptionLabel').text(getJsString('description-label'));
            $('#editStartTimeLabel').text(getJsString('start-time-label'));
            $('#editEndTimeLabel').text(getJsString('end-time-label'));
            $('#saveChangesBtn').text(getJsString('save-changes-btn'));
        }
        setLabels();

        function loadBranches() {
            $.getJSON('/Reservation/GetBranchCodes', function (branches) {
                let options = `<option value="">${getJsString('select-branch-option')}</option>`;
                branches.forEach(b => options += `<option value="${b.branchCode}">${b.branchName}</option>`);
                $('#branchSelect').html(options);
            }).fail(function () {
                showReservationAlert('Failed to load branches', 'danger');
            });
        }

        function loadHallsForBranch(branchCode) {
            $.getJSON('/Reservation/GetHalls?branchCode=' + branchCode, function (halls) {
                hallsCache = halls.sort((a, b) => a.hallCode - b.hallCode);
                console.log('Halls loaded for branch:', branchCode, hallsCache);
            }).fail(function () {
                console.error('Failed to load halls for branch:', branchCode);
                hallsCache = [];
                showReservationAlert('Failed to load halls for selected branch', 'warning');
            });
        }

        function loadTeachers(selectId, selectedVal) {
            $.getJSON('/Reservation/GetTeachers', function (teachers) {
                let options = `<option value="">${getJsString('select-teacher-option')}</option>`;
                teachers.forEach(t => {
                    options += `<option value="${t.teacherCode}" ${(selectedVal == t.teacherCode ? "selected" : "")}>${t.teacherName}</option>`;
                });
                $(selectId).html(options);
            }).fail(function () {
                showReservationAlert('Failed to load teachers', 'warning');
            });
        }

        function loadReservationGrid() {
            if (!selectedBranch) {
                $('#reservationGridTable thead').html('');
                $('#reservationGridTable tbody').html(`
                    <tr>
                        <td colspan="12" class="reservation-empty">
                            <div class="reservation-empty-icon">🏢</div>
                            <div class="reservation-empty-title">Select a Branch</div>
                            <div class="reservation-empty-text">Please select a branch to view reservations</div>
                        </td>
                    </tr>
                `);
                return;
            }

            // Show loading state
            $('#reservationGridTable tbody').html(`
                <tr>
                    <td colspan="12" class="reservation-loading">
                        <div class="reservation-spinner"></div>
                        Loading reservation grid...
                    </td>
                </tr>
            `);

            $.ajax({
                url: '/Reservation/GetReservationGrid',
                data: { reservationDate: selectedDate, branchCode: selectedBranch },
                type: 'GET',
                success: function (data) {
                    periods = data.periods;

                    console.log('Grid Response:', data);
                    console.log('Halls from response:', data.halls);

                    if (data.halls) {
                        hallsCache = data.halls;
                        console.log('Updated hallsCache:', hallsCache);
                    } else {
                        console.error('No halls data in response!');
                    }

                    let thead = `<tr><th class="sticky-col">${getJsString('hall-header')}</th>`;
                    if (periods && periods.length) {
                        periods.forEach((p, idx) => {
                            thead += `<th>${getJsString('session_' + (idx + 1))}</th>`;
                        });
                    }
                    thead += '</tr>';
                    $('#reservationGridTable thead').html(thead);

                    let tbody = '';
                    if (data.grid && data.grid.length > 0) {
                        data.grid.forEach((row, hallIdx) => {
                            let hallCode = (data.halls && data.halls[hallIdx]) ? data.halls[hallIdx].hallCode : '';

                            console.log(`Hall Index: ${hallIdx}, Hall Code: ${hallCode}, Hall Name: ${row[0]}, Available halls:`, data.halls);

                            tbody += '<tr>';
                            row.forEach((cell, idx) => {
                                if (idx === 0) {
                                    tbody += `<th class="sticky-col">${cell}</th>`;
                                } else {
                                    tbody += `<td>`;
                                    if (cell && typeof cell === "object" && !Array.isArray(cell)) {
                                        tbody += `
                                            <div class="reservation-slot">
                                                <div class="reservation-slot-teacher">
                                                    <i class="bi bi-person-circle"></i>
                                                    ${cell.teacherName}
                                                </div>
                                                <div class="reservation-slot-description">${cell.description}</div>
                                                <div class="reservation-slot-time">${cell.start} - ${cell.end}</div>
                                                <div class="reservation-slot-actions">
                                                    <button class="reservation-btn reservation-btn-icon reservation-btn-edit edit-res-btn" 
                                                            title="${getJsString('edit-btn')}" 
                                                            data-res='${JSON.stringify(cell)}'>
                                                        <i class="bi bi-pencil"></i>
                                                    </button>
                                                    <button class="reservation-btn reservation-btn-icon reservation-btn-delete delete-res-btn" 
                                                            title="${getJsString('delete-btn')}" 
                                                            data-res-code="${cell.reservationCode}">
                                                        <i class="bi bi-trash"></i>
                                                    </button>
                                                </div>
                                            </div>
                                        `;
                                    } else if (cell && Array.isArray(cell) && cell.length) {
                                        cell.forEach(res => {
                                            tbody += `
                                                <div class="reservation-slot">
                                                    <div class="reservation-slot-teacher">
                                                        <i class="bi bi-person-circle"></i>
                                                        ${res.teacherName}
                                                    </div>
                                                    <div class="reservation-slot-description">${res.description}</div>
                                                    <div class="reservation-slot-time">${res.start} - ${res.end}</div>
                                                    <div class="reservation-slot-actions">
                                                        <button class="reservation-btn reservation-btn-icon reservation-btn-edit edit-res-btn" 
                                                                title="${getJsString('edit-btn')}" 
                                                                data-res='${JSON.stringify(res)}'>
                                                            <i class="bi bi-pencil"></i>
                                                        </button>
                                                        <button class="reservation-btn reservation-btn-icon reservation-btn-delete delete-res-btn" 
                                                                title="${getJsString('delete-btn')}" 
                                                                data-res-code="${res.reservationCode}">
                                                            <i class="bi bi-trash"></i>
                                                        </button>
                                                    </div>
                                                </div>
                                            `;
                                        });
                                    } else {
                                        if (!hallCode) {
                                            console.error(`Missing hall code for hall index ${hallIdx}`);
                                            tbody += `<div class="reservation-alert reservation-alert-danger">Hall code missing</div>`;
                                        } else {
                                            tbody += `
                                                <button class="reservation-add-cell-btn add-res-btn" 
                                                        data-hall-idx="${hallIdx}" 
                                                        data-hall-code="${hallCode}" 
                                                        data-period-idx="${idx}" 
                                                        title="${getJsString('add-reservation-btn')}">
                                                    <i class="bi bi-plus-lg"></i>
                                                </button>
                                            `;
                                        }
                                    }
                                    tbody += `</td>`;
                                }
                            });
                            tbody += '</tr>';
                        });
                    } else {
                        tbody = `
                            <tr>
                                <td colspan="${(periods && periods.length ? periods.length + 1 : 12)}" class="reservation-empty">
                                    <div class="reservation-empty-icon">📅</div>
                                    <div class="reservation-empty-title">No Data Found</div>
                                    <div class="reservation-empty-text">No reservations for this date and branch</div>
                                </td>
                            </tr>
                        `;
                    }
                    $('#reservationGridTable tbody').html(tbody);
                },
                error: function () {
                    $('#reservationGridTable thead').html('');
                    $('#reservationGridTable tbody').html(`
                        <tr>
                            <td colspan="12" class="reservation-empty">
                                <div class="reservation-empty-icon">❌</div>
                                <div class="reservation-empty-title">Error Loading Grid</div>
                                <div class="reservation-empty-text">Failed to load reservation data</div>
                            </td>
                        </tr>
                    `);
                    showReservationAlert('Failed to load reservation grid', 'danger');
                }
            });
        }

        // Event Handlers
        $(document).on('click', '#firstTimeTeacherBtn', function () {
            $('#addTeacherForm')[0].reset();
            $('#teacherAddMsg').addClass('d-none').text('');
            $('#addTeacherModal').modal('show');
        });

        $('#addTeacherForm').on('submit', function (e) {
            e.preventDefault();
            var $submitBtn = $('#addTeacherBtn');
            var originalText = $submitBtn.html();
            $submitBtn.html('<i class="reservation-spinner" style="width: 16px; height: 16px; margin-right: 8px;"></i> Adding...');
            $submitBtn.prop('disabled', true);

            $.post('/Reservation/AddTeacher', $(this).serialize(), function (data) {
                $('#teacherAddMsg').removeClass('d-none').addClass('reservation-alert-success').removeClass('reservation-alert-danger').text(getJsString('success-add-teacher'));
                loadTeachers('#addTeacherSelect', data.teacherCode);
                setTimeout(() => {
                    $('#addTeacherModal').modal('hide');
                }, 1500);
            }).fail(function () {
                $('#teacherAddMsg').removeClass('d-none').removeClass('reservation-alert-success').addClass('reservation-alert-danger').text(getJsString('failed-add-teacher'));
            }).always(function () {
                $submitBtn.html(originalText);
                $submitBtn.prop('disabled', false);
            });
        });

        $('#addReservationModal').on('show.bs.modal', function () {
            loadTeachers('#addTeacherSelect');
        });

        $(document).on('click', '.add-res-btn', function () {
            let hallCode = $(this).data('hall-code');
            let periodIdx = $(this).data('period-idx') || 1;
            if (!hallCode) {
                showReservationAlert("Could not find hall code for this hall. Please reload the page or contact admin.", 'danger');
                return;
            }
            $('#addHallCode').val(hallCode);
            $('#addBranchCode').val(selectedBranch);
            $('#addReservationDate').val(selectedDate);
            $('#addStartTime').val(periods[periodIdx - 1] || '');
            $('#addEndTime').val('');
            $('#addDescription').val('');
            $('#addCapacity').val('');
            $('#addCost').val('');
            $('#addDeposit').val('');
            $('#addFinalCost').val('');
            $('#addPeriod').val('');
            loadTeachers('#addTeacherSelect');
            $('#addReservationModal').modal('show');
        });

        $(document).on('change', '#addStartTime, #addEndTime', function () {
            let start = $('#addStartTime').val();
            let end = $('#addEndTime').val();
            if (start && end) {
                let startHour = parseInt(start.split(':')[0]);
                let startMin = parseInt(start.split(':')[1]);
                let endHour = parseInt(end.split(':')[0]);
                let endMin = parseInt(end.split(':')[1]);
                let duration = (endHour + endMin / 60) - (startHour + startMin / 60);
                $('#addPeriod').val(duration > 0 ? duration.toFixed(2) : '');
            }
        });

        $(document).on('submit', '#addReservationForm', function (e) {
            e.preventDefault();
            if (!$('#addHallCode').val()) {
                showReservationAlert('Hall code not set. Please try again.', 'danger');
                return;
            }

            var $submitBtn = $('#addReservationBtn');
            var originalText = $submitBtn.html();
            $submitBtn.html('<i class="reservation-spinner" style="width: 16px; height: 16px; margin-right: 8px;"></i> Adding...');
            $submitBtn.prop('disabled', true);

            $.ajax({
                url: '/Reservation/AddReservation',
                type: 'POST',
                data: $(this).serialize(),
                success: function (response) {
                    if (response && response.alert && response.message) {
                        showReservationAlert(response.message, 'warning');
                        return;
                    }
                    $('#addReservationModal').modal('hide');
                    loadReservationGrid();
                    showReservationAlert('Reservation added successfully!', 'success');
                },
                error: function (xhr) {
                    try {
                        var errorResponse = JSON.parse(xhr.responseText);
                        if (errorResponse && errorResponse.alert && errorResponse.message) {
                            showReservationAlert(errorResponse.message, 'warning');
                            return;
                        }
                    } catch (e) {
                        // If not JSON or no alert property, show default error
                    }
                    showReservationAlert(getJsString('failed-add-reservation') + (xhr.responseText || 'Unknown error'), 'danger');
                },
                complete: function () {
                    $submitBtn.html(originalText);
                    $submitBtn.prop('disabled', false);
                }
            });
        });

        $(document).on('click', '.edit-res-btn', function () {
            let res = $(this).data('res');
            $('#editReservationCode').val(res.reservationCode);
            $('#editDescription').val(res.description);
            $('#editStartTime').val(res.start);
            $('#editEndTime').val(res.end);
            loadTeachers('#editTeacherSelect', res.teacherCode);
            $('#editReservationModal').modal('show');
        });

        $(document).on('submit', '#editReservationForm', function (e) {
            e.preventDefault();
            var $submitBtn = $('#saveChangesBtn');
            var originalText = $submitBtn.html();
            $submitBtn.html('<i class="reservation-spinner" style="width: 16px; height: 16px; margin-right: 8px;"></i> Saving...');
            $submitBtn.prop('disabled', true);

            $.ajax({
                url: '/Reservation/EditReservation',
                type: 'POST',
                data: $(this).serialize(),
                success: function (response) {
                    if (response && response.alert && response.message) {
                        showReservationAlert(response.message, 'warning');
                        return;
                    }
                    $('#editReservationModal').modal('hide');
                    loadReservationGrid();
                    showReservationAlert('Reservation updated successfully!', 'success');
                },
                error: function (xhr) {
                    try {
                        var errorResponse = JSON.parse(xhr.responseText);
                        if (errorResponse && errorResponse.alert && errorResponse.message) {
                            showReservationAlert(errorResponse.message, 'warning');
                            return;
                        }
                    } catch (e) {
                        // If not JSON or no alert property, show default error
                    }
                    showReservationAlert(getJsString('failed-edit-reservation') + (xhr.responseText || 'Unknown error'), 'danger');
                },
                complete: function () {
                    $submitBtn.html(originalText);
                    $submitBtn.prop('disabled', false);
                }
            });
        });

        $(document).on('click', '.delete-res-btn', function () {
            if (!confirm(getJsString('delete-confirm'))) return;
            let code = $(this).data('res-code');
            var $btn = $(this);
            $btn.html('<i class="reservation-spinner" style="width: 12px; height: 12px;"></i>');

            $.ajax({
                url: '/Reservation/DeleteReservation',
                type: 'POST',
                data: { reservationCode: code },
                success: function () {
                    loadReservationGrid();
                    showReservationAlert('Reservation deleted successfully!', 'success');
                },
                error: function () {
                    showReservationAlert(getJsString('failed-delete-reservation'), 'danger');
                    $btn.html('<i class="bi bi-trash"></i>');
                }
            });
        });

        $(document).on('change', '#branchSelect', function () {
            selectedBranch = $(this).val();
            if (selectedBranch) {
                loadHallsForBranch(selectedBranch);
            }
            loadReservationGrid();
        });

        $('#dateSelect').val(selectedDate);
        $(document).on('change', '#dateSelect', function () {
            selectedDate = $(this).val();
            loadReservationGrid();
        });

        // Initialize
        loadBranches();
        setTimeout(() => {
            if (selectedBranch) loadHallsForBranch(selectedBranch);
            loadReservationGrid();
        }, 300);
    }

    // ================ SHARED UTILITIES ================
    function showReservationAlert(message, type = 'info', duration = 5000) {
        const alertId = 'reservation-alert-' + Date.now();
        const alertHtml = `
            <div id="${alertId}" class="reservation-alert reservation-alert-${type}" style="position: fixed; top: 20px; right: 20px; z-index: 9999; max-width: 400px; animation: slideInRight 0.3s;">
                <i class="bi bi-${type === 'success' ? 'check-circle' : type === 'danger' ? 'x-circle' : type === 'warning' ? 'exclamation-triangle' : 'info-circle'}"></i>
                ${message}
                <button type="button" class="btn-close" style="float: right; background: none; border: none; font-size: 1.2rem; opacity: 0.7;" onclick="$('#${alertId}').remove()">×</button>
            </div>
        `;

        $('body').append(alertHtml);

        // Auto remove after duration
        if (duration > 0) {
            setTimeout(() => {
                $(`#${alertId}`).fadeOut(300, function () {
                    $(this).remove();
                });
            }, duration);
        }
    }

    // Add CSS for slide animation
    if (!$('#reservation-animations').length) {
        $('<style id="reservation-animations">').appendTo('head').text(`
            @keyframes slideInRight {
                from { transform: translateX(100%); opacity: 0; }
                to { transform: translateX(0); opacity: 1; }
            }
            .sticky-col {
                position: sticky;
                left: 0;
                background: white !important;
                z-index: 5;
            }
        `);
    }
});