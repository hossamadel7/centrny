// --- Localization Helper ---
function getJsString(key) {
    return $('#js-localization').attr('data-' + key.replace(/_/g, '-'));
}

$(document).ready(function () {
    // Detect which mode we're in
    var isCenter = typeof window.isCenter !== 'undefined'
        ? window.isCenter
        : ($("#rootAddReservationBtn").length === 0);

    // ================= NON-CENTER FLOW =================
    if (!isCenter) {
        // Set labels
        $('#rootDateLabel').text(getJsString('date-label'));
        $('#rootTeacherLabel').text(getJsString('teacher-label'));
        $('#rootDescriptionLabel').text(getJsString('description-label'));
        $('#rootCapacityLabel').text(getJsString('capacity-label'));
        $('#rootCostLabel').text(getJsString('cost-label'));
        $('#rootStartTimeLabel').text(getJsString('start-time-label'));
        $('#rootEndTimeLabel').text(getJsString('end-time-label'));
        $('#rootPeriodLabel').text(getJsString('period-label'));
        $('#rootDepositLabel').text(getJsString('deposit-label'));
        $('#rootFinalCostLabel').text(getJsString('final-cost-label'));
        $('#rootReservationSaveBtn').text(getJsString('add-reservation-btn'));

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
                    alert(data.message || 'Teacher not found.');
                }
            });
        }

        function loadRootReservations() {
            $('#rootReservationsList').html('<div class="text-muted text-center py-4">Loading...</div>');
            $.get('/Reservation/GetRootReservations', { reservationDate: selectedDate }, function (data) {
                if (!data.success) {
                    $('#rootReservationsList').html(`<div class="text-danger">${data.message || 'Failed to load reservations.'}</div>`);
                    return;
                }
                if (!data.reservations.length) {
                    $('#rootReservationsList').html(`<div class="text-muted text-center py-4">${getJsString('no-data-found')}</div>`);
                    return;
                }
                let cards = '';
                data.reservations.forEach(function (r) {
                    // --- Horizontal Card Design ---
                    cards += `
                        <div class="reservation-horizontal-card mb-3">
                            <div class="card-horizontal-content d-flex align-items-center py-3 px-4">
                                <div class="card-info flex-grow-1">
                                    <div class="card-title-row d-flex align-items-center mb-2">
                                        <span class="teacher-name fw-bold me-3">
                                            <i class="bi bi-person-circle text-primary"></i>
                                            ${r.teacherName}
                                        </span>
                                        <span class="description text-muted ms-2">${r.description || ''}</span>
                                    </div>
                                    <div class="card-details-row d-flex align-items-center flex-wrap gap-3 mb-1">
                                        <span class="price badge bg-success px-2 py-1">
                                            <i class="bi bi-currency-dollar"></i> ${r.cost || '0'}
                                        </span>
                                        <span class="time badge bg-primary px-2 py-1">
                                            <i class="bi bi-clock"></i> ${r.reservationStartTime || ''} - ${r.reservationEndTime || ''}
                                        </span>
                                    </div>
                                </div>
                                <div class="card-actions ms-3 d-flex flex-column gap-2 align-items-end">
                                    <button class="modern-btn edit-btn edit-root-btn" title="${getJsString('edit-btn')}" data-res='${JSON.stringify(r)}'>
                                        <i class="bi bi-pencil"></i>
                                    </button>
                                    <button class="modern-btn delete-btn delete-root-btn" title="${getJsString('delete-btn')}" data-res-code="${r.reservationCode}">
                                        <i class="bi bi-trash"></i>
                                    </button>
                                </div>
                            </div>
                        </div>
                    `;
                });
                $('#rootReservationsList').html(cards);
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
                $('#rootPeriod').val(diff > 0 ? diff : '');
            } else {
                $('#rootPeriod').val('');
            }
        }

        $('#rootAddReservationBtn').on('click', function () {
            fetchSingleTeacher(function (t) {
                $('#rootReservationModalLabel').text(getJsString('add-reservation-title'));
                $('#rootReservationSaveBtn').text(getJsString('add-reservation-btn'));
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
                $('#rootReservationSaveBtn').text(getJsString('save-changes-btn'));
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
            $.ajax({
                url: url,
                type: 'POST',
                data: $(this).serialize(),
                success: function () {
                    $('#rootReservationModal').modal('hide');
                    loadRootReservations();
                },
                error: function (xhr) {
                    alert((isEdit ? getJsString('failed-edit-reservation') : getJsString('failed-add-reservation')) + (xhr.responseText || ''));
                }
            });
        });

        $(document).on('click', '.delete-root-btn', function () {
            if (!confirm(getJsString('delete-confirm'))) return;
            var code = $(this).data('res-code');
            $.ajax({
                url: '/Reservation/DeleteRootReservation',
                type: 'POST',
                data: { reservationCode: code },
                success: function () {
                    loadRootReservations();
                },
                error: function () {
                    alert(getJsString('failed-delete-reservation'));
                }
            });
        });

        $('#rootDateSelect').val(selectedDate);
        $('#rootDateSelect').on('change', function () {
            selectedDate = $(this).val();
            loadRootReservations();
        });

        $('#rootStartTime, #rootEndTime').on('change', calculatePeriod);

        fetchSingleTeacher(function () {
            loadRootReservations();
        });
    }

    // ================ CENTER FLOW: Existing logic ================
    if (isCenter) {
        let selectedBranch = null;
        let selectedDate = new Date().toISOString().slice(0, 10);
        let periods = [];
        let hallsCache = [];

        function setLabels() {
            $('#reservationsTitle').text(getJsString('reservations-title'));
            $('#branchLabel').text(getJsString('branch-label'));
            $('#dateLabel').text(getJsString('date-label'));
            $('#branchSelect').html(`<option value="">${getJsString('select-branch-option')}</option>`);
            $('#addReservationModalLabel').text(getJsString('add-reservation-title'));
            $('#addTeacherLabel').text(getJsString('teacher-label'));
            $('#firstTimeTeacherBtn').text(getJsString('first-time-btn')).addClass('modern-btn btn-cancel');
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
            $('#addTeacherBtn').text(getJsString('add-teacher-btn')).addClass('modern-btn');
            $('#closeTeacherBtn').text(getJsString('close-btn')).addClass('modern-btn btn-cancel');
            $('#editReservationModalLabel').text(getJsString('edit-reservation-title'));
            $('#editTeacherLabel').text(getJsString('teacher-label'));
            $('#editDescriptionLabel').text(getJsString('description-label'));
            $('#editStartTimeLabel').text(getJsString('start-time-label'));
            $('#editEndTimeLabel').text(getJsString('end-time-label'));
            $('#saveChangesBtn').text(getJsString('save-changes-btn')).addClass('modern-btn');
            $('#addReservationBtn').addClass('modern-btn');
        }
        setLabels();

        $('select').addClass('modern-input styled-select');
        $('input[type="text"], input[type="number"], input[type="date"], input[type="time"]').addClass('modern-input');

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

        function loadTeachers(selectId, selectedVal) {
            $.getJSON('/Reservation/GetTeachers', function (teachers) {
                let options = `<option value="">${getJsString('select-teacher-option')}</option>`;
                teachers.forEach(t => {
                    options += `<option value="${t.teacherCode}" ${(selectedVal == t.teacherCode ? "selected" : "")}>${t.teacherName}</option>`;
                });
                $(selectId).html(options);
            });
        }

        $(document).on('click', '#firstTimeTeacherBtn', function () {
            $('#addTeacherForm')[0].reset();
            $('#teacherAddMsg').addClass('d-none').text('');
            $('#addTeacherModal').modal('show');
        });

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

        $('#addReservationModal').on('show.bs.modal', function () {
            loadTeachers('#addTeacherSelect');
        });

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
                    let thead = `<tr><th class="sticky-col bg-white text-dark bold-rendered">${getJsString('hall-header')}</th>`;
                    if (periods && periods.length)
                        periods.forEach((p, idx) => {
                            thead += `<th>${getJsString('session_' + (idx + 1))}</th>`;
                        });
                    thead += '</tr>';
                    $('#reservationGridTable thead').html(thead);

                    let tbody = '';
                    if (data.grid && data.grid.length > 0) {
                        data.grid.forEach((row, hallIdx) => {
                            let hallCode = hallsCache[hallIdx] ? hallsCache[hallIdx].hallCode : '';
                            tbody += '<tr>';
                            row.forEach((cell, idx) => {
                                if (idx === 0) {
                                    tbody += `<th class="sticky-col bg-white align-middle bold-rendered text-dark">${cell}</th>`;
                                } else {
                                    tbody += `<td>`;
                                    if (cell && typeof cell === "object" && !Array.isArray(cell)) {
                                        tbody += `
                                            <div class="reservation-slot bg-light rounded-3 shadow-sm p-2 mb-2">
                                                <div class="fw-bold text-primary fs-6">${cell.teacherName}</div>
                                                <div class="small text-muted mb-1">${cell.description}</div>
                                                <span class="badge bg-primary mb-1">${cell.start} - ${cell.end}</span>
                                                <div class="d-flex justify-content-center gap-2 mt-2">
                                                    <button class="modern-btn edit-btn edit-res-btn" title="${getJsString('edit-btn')}" data-res='${JSON.stringify(cell)}'>
                                                        <i class="bi bi-pencil"></i>
                                                    </button>
                                                    <button class="modern-btn delete-btn delete-res-btn" title="${getJsString('delete-btn')}" data-res-code="${cell.reservationCode}">
                                                        <i class="bi bi-trash"></i>
                                                    </button>
                                                </div>
                                            </div>
                                        `;
                                    } else if (cell && Array.isArray(cell) && cell.length) {
                                        cell.forEach(res => {
                                            tbody += `
                                                <div class="reservation-slot bg-light rounded-3 shadow-sm p-2 mb-2">
                                                    <div class="fw-bold text-primary fs-6">${res.teacherName}</div>
                                                    <div class="small text-muted mb-1">${res.description}</div>
                                                    <span class="badge bg-primary mb-1">${res.start} - ${res.end}</span>
                                                    <div class="d-flex justify-content-center gap-2 mt-2">
                                                        <button class="modern-btn edit-btn edit-res-btn" title="${getJsString('edit-btn')}" data-res='${JSON.stringify(res)}'>
                                                            <i class="bi bi-pencil"></i>
                                                        </button>
                                                        <button class="modern-btn delete-btn delete-res-btn" title="${getJsString('delete-btn')}" data-res-code="${res.reservationCode}">
                                                            <i class="bi bi-trash"></i>
                                                        </button>
                                                    </div>
                                                </div>
                                            `;
                                        });
                                    } else {
                                        tbody += `
                                        <button class="modern-btn btn-table add-res-btn w-100 mt-1" data-hall-idx="${hallIdx}" data-hall-code="${hallCode}" data-period-idx="${idx}" title="${getJsString('add-reservation-btn')}">
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

        $(document).on('change', '#addStartTime, #addEndTime', function () {
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

        $(document).on('submit', '#addReservationForm', function (e) {
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

        $(document).on('submit', '#editReservationForm', function (e) {
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

        $(document).on('change', '#branchSelect', function () {
            selectedBranch = $(this).val();
            loadHallsForBranch(selectedBranch);
            loadReservationGrid();
        });

        $('#dateSelect').val(selectedDate);
        $(document).on('change', '#dateSelect', function () {
            selectedDate = $(this).val();
            loadReservationGrid();
        });

        function ensureFirstTimeTeacherBtn() {
            if ($('#firstTimeTeacherBtn').length === 0) {
                let $teacherDiv = $('#addTeacherSelect').parent();
                if ($teacherDiv.find('#firstTimeTeacherBtn').length === 0) {
                    $('<button class="modern-btn btn-cancel" type="button" id="firstTimeTeacherBtn" tabindex="-1">' + getJsString('first-time-btn') + '</button>')
                        .insertAfter('#addTeacherSelect');
                }
            }
        }
        $('#addReservationModal').on('shown.bs.modal', ensureFirstTimeTeacherBtn);

        loadBranches();
        setTimeout(() => {
            if (selectedBranch) loadHallsForBranch(selectedBranch);
            loadReservationGrid();
        }, 300);
    }
});