// --- Localization Helper ---
// Use .attr() instead of .data() to support keys like data-session-1, not camelCase
function getJsString(key) {
    // Convert underscores to dashes for attribute lookup
    return $('#js-localization').attr('data-' + key.replace(/_/g, '-'));
}

$(document).ready(function () {
    let selectedBranch = null;
    let selectedDate = new Date().toISOString().slice(0, 10);
    let periods = [];
    let hallsCache = [];

    // --- Set static labels ---
    function setLabels() {
        $('#reservationsTitle').text(getJsString('reservations-title'));
        $('#branchLabel').text(getJsString('branch-label'));
        $('#dateLabel').text(getJsString('date-label'));
        $('#branchSelect').html(`<option value="">${getJsString('select-branch-option')}</option>`);
        $('#addReservationModalLabel').text(getJsString('add-reservation-title'));
        $('#addTeacherLabel').text(getJsString('teacher-label'));
        $('#firstTimeTeacherBtn').text(getJsString('first-time-btn'));
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

    // --- Helper to load branches filtered by root code (from controller/session) ---
    function loadBranches() {
        $.getJSON('/Reservation/GetBranchCodes', function (branches) {
            let options = `<option value="">${getJsString('select-branch-option')}</option>`;
            branches.forEach(b => options += `<option value="${b.branchCode}">${b.branchName}</option>`);
            $('#branchSelect').html(options);
        });
    }

    function loadHallsForBranch(branchCode) {
        $.getJSON('/Reservation/GetHalls?branchCode=' + branchCode, function (halls) {
            hallsCache = halls;
        });
    }

    // --- Teachers: load (isStaff==false) for dropdowns ---
    function loadTeachers(selectId, selectedVal) {
        $.getJSON('/Reservation/GetTeachers', function (teachers) {
            let options = `<option value="">${getJsString('select-teacher-option')}</option>`;
            teachers.forEach(t => {
                options += `<option value="${t.teacherCode}" ${(selectedVal == t.teacherCode ? "selected" : "")}>${t.teacherName}</option>`;
            });
            $(selectId).html(options);
        });
    }

    // --- Add "First time?" link/teacher modal support ---
    // Opens the add teacher modal
    $(document).on('click', '#firstTimeTeacherBtn', function () {
        $('#addTeacherForm')[0].reset();
        $('#teacherAddMsg').addClass('d-none').text('');
        $('#addTeacherModal').modal('show');
    });

    // Submits the add teacher modal
    $('#addTeacherForm').on('submit', function (e) {
        e.preventDefault();
        $.post('/Reservation/AddTeacher', $(this).serialize(), function (data) {
            $('#teacherAddMsg').removeClass('d-none').addClass('text-success').removeClass('text-danger').text(getJsString('success-add-teacher'));
            loadTeachers('#addTeacherSelect', data.teacherCode);
            setTimeout(() => {
                $('#addTeacherModal').modal('hide');
            }, 1000);
        }).fail(function () {
            $('#teacherAddMsg').removeClass('d-none').removeClass('text-success').addClass('text-danger').text(getJsString('failed-add-teacher'));
        });
    });

    // When add reservation modal opens, reload teachers dropdown
    $('#addReservationModal').on('show.bs.modal', function () {
        loadTeachers('#addTeacherSelect');
    });

    // --- Load reservation grid (with session headers) ---
    function loadReservationGrid() {
        if (!selectedBranch) {
            $('#reservationGridTable thead').html('');
            $('#reservationGridTable tbody').html(`<tr><td colspan="12" class="text-center text-muted">${getJsString('no-branch-selected')}</td></tr>`);
            return;
        }
        $.ajax({
            url: '/Reservation/GetReservationGrid',
            data: { reservationDate: selectedDate, branchCode: selectedBranch },
            type: 'GET',
            success: function (data) {
                periods = data.periods;
                // Table head, with sticky first column
                let thead = `<tr><th class="sticky-col bg-white">${getJsString('hall-header')}</th>`;
                if (periods && periods.length)
                    periods.forEach((p, idx) => {
                        // Use session_1 ... session_10 keys for localization
                        thead += `<th>${getJsString('session_' + (idx + 1))}</th>`;
                    });
                thead += '</tr>';
                $('#reservationGridTable thead').html(thead);

                // Table body, modern card layout
                let tbody = '';
                if (data.grid && data.grid.length > 0) {
                    data.grid.forEach((row, hallIdx) => {
                        let hallCode = hallsCache[hallIdx] ? hallsCache[hallIdx].hallCode : '';
                        tbody += '<tr>';
                        row.forEach((cell, idx) => {
                            if (idx === 0) {
                                // Sticky first column for hall name, no add button
                                tbody += `<th class="sticky-col bg-white align-middle">${cell}</th>`;
                            } else {
                                tbody += `<td>`;
                                // --- UPDATED LOGIC: cell is either reservation object OR null ---
                                if (cell && typeof cell === "object" && !Array.isArray(cell)) {
                                    // Single reservation object
                                    tbody += `
                                        <div class="reservation-slot bg-light rounded-3 shadow-sm p-2 mb-2">
                                            <div class="fw-bold text-primary fs-6">${cell.teacherName}</div>
                                            <div class="small text-muted mb-1">${cell.description}</div>
                                            <span class="badge bg-primary mb-1">${cell.start} - ${cell.end}</span>
                                            <div class="d-flex justify-content-center gap-2 mt-2">
                                                <button class="btn btn-outline-warning btn-sm edit-res-btn" title="${getJsString('edit-btn')}" data-res='${JSON.stringify(cell)}'>
                                                    <i class="bi bi-pencil"></i>
                                                </button>
                                                <button class="btn btn-outline-danger btn-sm delete-res-btn" title="${getJsString('delete-btn')}" data-res-code="${cell.reservationCode}">
                                                    <i class="bi bi-trash"></i>
                                                </button>
                                            </div>
                                        </div>
                                    `;
                                } else if (cell && Array.isArray(cell) && cell.length) {
                                    // Array of reservations (legacy/multiple per cell)
                                    cell.forEach(res => {
                                        tbody += `
                                            <div class="reservation-slot bg-light rounded-3 shadow-sm p-2 mb-2">
                                                <div class="fw-bold text-primary fs-6">${res.teacherName}</div>
                                                <div class="small text-muted mb-1">${res.description}</div>
                                                <span class="badge bg-primary mb-1">${res.start} - ${res.end}</span>
                                                <div class="d-flex justify-content-center gap-2 mt-2">
                                                    <button class="btn btn-outline-warning btn-sm edit-res-btn" title="${getJsString('edit-btn')}" data-res='${JSON.stringify(res)}'>
                                                        <i class="bi bi-pencil"></i>
                                                    </button>
                                                    <button class="btn btn-outline-danger btn-sm delete-res-btn" title="${getJsString('delete-btn')}" data-res-code="${res.reservationCode}">
                                                        <i class="bi bi-trash"></i>
                                                    </button>
                                                </div>
                                            </div>
                                        `;
                                    });
                                } else {
                                    tbody += `
                                    <button class="btn btn-outline-success btn-sm w-100 add-res-btn mt-1" data-hall-idx="${hallIdx}" data-hall-code="${hallCode}" data-period-idx="${idx}" title="${getJsString('add-reservation-btn')}">
                                        <i class="bi bi-plus"></i>
                                    </button>`;
                                }
                                tbody += `</td>`;
                            }
                        });
                        tbody += '</tr>';
                    });
                } else {
                    tbody = `<tr><td colspan="${(periods && periods.length ? periods.length + 1 : 12)}" class="text-center text-muted">${getJsString('no-data-found')}</td></tr>`;
                }
                $('#reservationGridTable tbody').html(tbody);
            },
            error: function () {
                $('#reservationGridTable thead').html('');
                $('#reservationGridTable tbody').html(`<tr><td colspan="12" class="text-danger text-center">${getJsString('failed-to-load-grid')}</td></tr>`);
            }
        });
    }

    // --- Reservation CRUD logic ---

    $(document).on('click', '.add-res-btn', function () {
        let hallCode = $(this).data('hall-code');
        let periodIdx = $(this).data('period-idx') || 1;
        if (!hallCode) {
            alert("Could not find hall code for this hall. Please reload the page or contact admin.");
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

    $('#addStartTime, #addEndTime').on('change', function () {
        let start = $('#addStartTime').val();
        let end = $('#addEndTime').val();
        if (start && end) {
            let startHour = parseInt(start.split(':')[0]);
            let startMin = parseInt(start.split(':')[1]);
            let endHour = parseInt(end.split(':')[0]);
            let endMin = parseInt(end.split(':')[1]);
            let duration = (endHour + endMin / 60) - (startHour + startMin / 60);
            $('#addPeriod').val(duration > 0 ? duration : '');
        }
    });

    $('#addReservationForm').on('submit', function (e) {
        e.preventDefault();
        if (!$('#addHallCode').val()) {
            alert('Hall code not set. Please try again.');
            return;
        }
        $.ajax({
            url: '/Reservation/AddReservation',
            type: 'POST',
            data: $(this).serialize(),
            success: function () {
                $('#addReservationModal').modal('hide');
                loadReservationGrid();
            },
            error: function (xhr) {
                alert(getJsString('failed-add-reservation') + (xhr.responseText || 'Unknown error'));
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

    $('#editReservationForm').on('submit', function (e) {
        e.preventDefault();
        $.ajax({
            url: '/Reservation/EditReservation',
            type: 'POST',
            data: $(this).serialize(),
            success: function () {
                $('#editReservationModal').modal('hide');
                loadReservationGrid();
            },
            error: function (xhr) {
                alert(getJsString('failed-edit-reservation') + (xhr.responseText || 'Unknown error'));
            }
        });
    });

    $(document).on('click', '.delete-res-btn', function () {
        if (!confirm(getJsString('delete-confirm'))) return;
        let code = $(this).data('res-code');
        $.ajax({
            url: '/Reservation/DeleteReservation',
            type: 'POST',
            data: { reservationCode: code },
            success: function () {
                loadReservationGrid();
            },
            error: function () {
                alert(getJsString('failed-delete-reservation'));
            }
        });
    });

    $('#branchSelect').on('change', function () {
        selectedBranch = $(this).val();
        loadHallsForBranch(selectedBranch);
        loadReservationGrid();
    });

    $('#dateSelect').val(selectedDate);
    $('#dateSelect').on('change', function () {
        selectedDate = $(this).val();
        loadReservationGrid();
    });

    // --- Add "First time?" link after teacher dropdown in add reservation modal ---
    // This ensures the button is only added once
    function ensureFirstTimeTeacherBtn() {
        if ($('#firstTimeTeacherBtn').length === 0) {
            let $teacherDiv = $('#addTeacherSelect').parent();
            if ($teacherDiv.find('#firstTimeTeacherBtn').length === 0) {
                $('<button class="btn btn-link" type="button" id="firstTimeTeacherBtn" tabindex="-1">' + getJsString('first-time-btn') + '</button>')
                    .insertAfter('#addTeacherSelect');
            }
        }
    }
    $('#addReservationModal').on('shown.bs.modal', ensureFirstTimeTeacherBtn);

    // --- Initial load ---
    loadBranches();
    setTimeout(() => {
        if (selectedBranch) loadHallsForBranch(selectedBranch);
        loadReservationGrid();
    }, 300);
});