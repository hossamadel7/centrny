$(document).ready(function () {
    let selectedBranch = null;
    let selectedDate = new Date().toISOString().slice(0, 10);
    let periods = [];
    let hallsCache = [];

    function loadBranches() {
        $.getJSON('/Reservation/GetBranchCodes', function (branches) {
            let options = '<option value="">Select Branch</option>';
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
            let options = '';
            teachers.forEach(t => {
                options += `<option value="${t.teacherCode}" ${(selectedVal == t.teacherCode ? "selected" : "")}>${t.teacherName}</option>`;
            });
            $(selectId).html(options);
        });
    }

    function loadReservationGrid() {
        if (!selectedBranch) {
            $('#reservationGridTable thead').html('');
            $('#reservationGridTable tbody').html('<tr><td colspan="12" class="text-center text-muted">Please select a branch.</td></tr>');
            return;
        }
        $.ajax({
            url: '/Reservation/GetReservationGrid',
            data: { reservationDate: selectedDate, branchCode: selectedBranch },
            type: 'GET',
            success: function (data) {
                periods = data.periods;
                // Table head, with sticky first column
                let thead = '<tr><th class="sticky-col bg-white">Hall</th>';
                if (periods && periods.length)
                    periods.forEach(p => thead += `<th>${p}</th>`);
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
                                if (cell && Array.isArray(cell) && cell.length) {
                                    cell.forEach(res => {
                                        tbody += `
                                        <div class="reservation-slot bg-light rounded-3 shadow-sm p-2 mb-2">
                                            <div class="fw-bold text-primary fs-6">${res.teacherName}</div>
                                            <div class="small text-muted mb-1">${res.description}</div>
                                            <span class="badge bg-primary mb-1">${res.start} - ${res.end}</span>
                                            <div class="d-flex justify-content-center gap-2 mt-2">
                                                <button class="btn btn-outline-warning btn-sm edit-res-btn" title="Edit" data-res='${JSON.stringify(res)}'>
                                                    <i class="bi bi-pencil"></i>
                                                </button>
                                                <button class="btn btn-outline-danger btn-sm delete-res-btn" title="Delete" data-res-code="${res.reservationCode}">
                                                    <i class="bi bi-trash"></i>
                                                </button>
                                            </div>
                                        </div>
                                        `;
                                    });
                                } else {
                                    tbody += `
                                    <button class="btn btn-outline-success btn-sm w-100 add-res-btn mt-1" data-hall-idx="${hallIdx}" data-hall-code="${hallCode}" data-period-idx="${idx}" title="Add">
                                        <i class="bi bi-plus"></i>
                                    </button>`;
                                }
                                tbody += `</td>`;
                            }
                        });
                        tbody += '</tr>';
                    });
                } else {
                    tbody = `<tr><td colspan="${(periods && periods.length ? periods.length + 1 : 12)}" class="text-center text-muted">No data found for this branch and date.</td></tr>`;
                }
                $('#reservationGridTable tbody').html(tbody);
            },
            error: function () {
                $('#reservationGridTable thead').html('');
                $('#reservationGridTable tbody').html('<tr><td colspan="12" class="text-danger text-center">Failed to load grid.</td></tr>');
            }
        });
    }

    // All other logic is unchanged
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
                alert('Failed to add reservation: ' + (xhr.responseText || 'Unknown error'));
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
                alert('Failed to edit reservation: ' + (xhr.responseText || 'Unknown error'));
            }
        });
    });

    $(document).on('click', '.delete-res-btn', function () {
        if (!confirm('Are you sure you want to delete this reservation?')) return;
        let code = $(this).data('res-code');
        $.ajax({
            url: '/Reservation/DeleteReservation',
            type: 'POST',
            data: { reservationCode: code },
            success: function () {
                loadReservationGrid();
            },
            error: function () {
                alert('Failed to delete reservation.');
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

    loadBranches();
    setTimeout(() => {
        if (selectedBranch) loadHallsForBranch(selectedBranch);
        loadReservationGrid();
    }, 300);
});