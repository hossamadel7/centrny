// Subscription.js
// Handles UI interactions for Subscription plans and purchase workflow.
// Localized using window.SubLoc (injected from server). Fallbacks are provided if a key is missing.

(function ($) {

    const L = (typeof window !== 'undefined' && window.SubLoc) ? window.SubLoc : {};

    function t(key, fallback) {
        return (L && L[key]) ? L[key] : (fallback || key);
    }

    const endpoints = {
        list: '/Subscription/GetSubscriptions',
        get: id => `/Subscription/GetSubscription/${id}`,
        create: '/Subscription/Create',
        edit: '/Subscription/Edit',
        delete: '/Subscription/Delete',
        years: '/Subscription/GetYears',
        subjects: yearCode => `/Subscription/GetSubjects?yearCode=${encodeURIComponent(yearCode)}`,
        searchStudents: phone => `/Subscription/SearchStudentsByPhone?phone=${encodeURIComponent(phone)}`,
        buy: '/Subscription/BuyStudentPlan'
    };

    let plansTable = null;
    let yearsCache = null;
    let planModal, deleteModal, buyModal;
    let isEditMode = false;

    // Helpers
    function getToken() {
        return $('input[name="__RequestVerificationToken"]').val();
    }

    function notify(type, message, timeout = 6000) {
        const id = 'alert-' + Date.now();
        const html = `<div id="${id}" class="alert alert-${type} alert-dismissible fade show" role="alert">
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="${t('Close', 'Close')}"></button>
    </div>`;
        $('#alertHost').append(html);

        if (timeout) {
            setTimeout(() => {
                const el = document.getElementById(id);
                if (el) {
                    // Bootstrap 5 native API (no jQuery plugin)
                    const bsAlert = bootstrap.Alert.getOrCreateInstance(el);
                    bsAlert.close();
                }
            }, timeout);
        }
    }

    function escapeHtml(str) {
        return (str ?? '').toString()
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function formatSubjectsCell(subjects) {
        if (!subjects || !subjects.length)
            return `<span class="text-muted">${t('None', 'None')}</span>`;
        return subjects.map(s =>
            `<span class="badge bg-info text-dark table-subjects-badge" title="${t('Count', 'Count')}: ${s.count}">
                ${escapeHtml(s.subjectName)} <small>(${s.count})</small>
             </span>`
        ).join('');
    }

    // DataTable initialization
    function initTable() {
        plansTable = $('#plansTable').DataTable({
            ajax: {
                url: endpoints.list,
                dataSrc: function (json) {
                    if (json && json.success === false) {
                        notify('danger', escapeHtml(json.message || t('ErrorLoadingPlans', 'Error fetching subscription plans.')));
                        return [];
                    }
                    return json;
                },
                error: function () {
                    notify('danger', t('ErrorLoadingPlans', 'Error fetching subscription plans.'));
                }
            },
            responsive: true,
            processing: true,
            deferRender: true,
            columns: [
                {
                    data: 'subPlanCode',
                    render: (d, t, r, meta) => meta.row + 1
                },
                { data: 'subPlanName' },
                {
                    data: 'price',
                    render: d => `<span class="text-nowrap">${d?.toLocaleString?.() ?? d}</span>`
                },
                { data: 'totalCount' },
                { data: 'expiryMonths' },
                {
                    data: 'description',
                    render: d => d ? escapeHtml(d) : '<span class="text-muted">-</span>',
                    className: 'wrap-any'
                },
                {
                    data: 'subjects',
                    orderable: false,
                    render: formatSubjectsCell
                },
                {
                    data: null,
                    orderable: false,
                    render: function (data, type, row) {
                        return `
                            <div class="btn-group btn-group-sm" role="group">
                                <button class="btn btn-outline-primary btn-edit" data-id="${row.subPlanCode}" title="${t('EditSubscriptionPlan', 'Edit Subscription Plan')}">
                                    <i class="bi bi-pencil-square"></i>
                                </button>
                                <button class="btn btn-outline-danger btn-delete" data-id="${row.subPlanCode}" title="${t('DeleteSubscriptionPlan', 'Delete Subscription Plan')}">
                                    <i class="bi bi-trash"></i>
                                </button>
                                <button class="btn btn-outline-success btn-buy" data-id="${row.subPlanCode}" data-name="${escapeHtml(row.subPlanName)}" title="${t('BuyPlanForStudent', 'Buy Plan for Student')}">
                                    <i class="bi bi-cart-plus"></i>
                                </button>
                            </div>`;
                    }
                }
            ],
            order: [[0, 'asc']],
            language: {
                emptyTable: t('NoPlans', 'No subscription plans found.'),
                loadingRecords: t('LoadingPlans', 'Loading plans...')
            }
        });
    }

    function reloadPlans() {
        if (plansTable) plansTable.ajax.reload(null, false);
    }

    // Years & Subjects
    function loadYears() {
        if (yearsCache) return Promise.resolve(yearsCache);
        return $.getJSON(endpoints.years)
            .then(data => {
                yearsCache = data;
                return data;
            })
            .catch(() => {
                notify('danger', t('FailedToLoadYears', 'Failed to load years.'));
                return [];
            });
    }

    function loadSubjects(yearCode, $subjectSelect, preselect) {
        $subjectSelect.prop('disabled', true).html(`<option value="">${t('Loading', 'Loading...')}</option>`);
        $.getJSON(endpoints.subjects(yearCode))
            .done(list => {
                $subjectSelect.empty().append(`<option value="">${t('SelectSubject', '-- Select Subject --')}</option>`);
                list.forEach(s => {
                    $subjectSelect.append(`<option value="${s.subjectCode}">${escapeHtml(s.subjectName)}</option>`);
                });
                if (preselect) {
                    $subjectSelect.val(preselect);
                }
                $subjectSelect.prop('disabled', false);
            })
            .fail(() => {
                $subjectSelect.html(`<option value="">${t('FailedToLoadSubjects', 'Error loading subjects')}</option>`);
                notify('danger', t('FailedToLoadSubjects', 'Failed to load subjects for selected year.'));
            });
    }

    // Subject rows
    function addSubjectRow(data) {
        const rowId = 'row-' + Date.now() + '-' + Math.floor(Math.random() * 1000);
        const $tr = $(`
            <tr class="subject-row" id="${rowId}">
                <td>
                    <select class="form-select year-select" required>
                        <option value="">${t('SelectYear', '-- Select Year --')}</option>
                    </select>
                </td>
                <td>
                    <select class="form-select subject-select" required disabled>
                        <option value="">${t('SelectSubject', '-- Select Subject --')}</option>
                    </select>
                </td>
                <td>
                    <input type="number" class="form-control count-input" min="1" value="${data?.Count ?? 1}" required />
                </td>
                <td class="text-center">
                    <button type="button" class="btn btn-sm btn-outline-danger btn-remove-row" title="${t('Remove', 'Remove')}">
                        <i class="bi bi-x-lg"></i>
                    </button>
                </td>
            </tr>
        `);

        $('#subjectsTbody').append($tr);

        loadYears().then(years => {
            const $yearSelect = $tr.find('.year-select');
            years.forEach(y => {
                $yearSelect.append(`<option value="${y.yearCode}">${escapeHtml(y.yearName)}</option>`);
            });
            if (data?.YearCode) {
                $yearSelect.val(data.YearCode);
                loadSubjects(data.YearCode, $tr.find('.subject-select'), data.SubjectCode);
            }
        });

        return $tr;
    }

    function recalcTotalCount() {
        let total = 0;
        $('#subjectsTbody .count-input').each(function () {
            const v = parseInt($(this).val(), 10);
            if (!isNaN(v)) total += v;
        });
        $('#totalCountDisplay').text(total);
    }

    // Plan Modal
    function openCreateModal() {
        isEditMode = false;
        $('#planModalTitle').text(t('CreateSubscriptionPlan', 'Create Subscription Plan'));
        $('#SubPlanCode').val('');
        $('#SubPlanName').val('');
        $('#Price').val('');
        $('#ExpiryMonths').val('');
        $('#Description').val('');
        $('#subjectsTbody').empty();
        addSubjectRow();
        recalcTotalCount();
        $('#btnSavePlan .save-text').text(t('SavePlan', 'Save Plan'));
        planModal.show();
    }

    function openEditModal(id) {
        isEditMode = true;
        $('#planModalTitle').text(t('EditSubscriptionPlan', 'Edit Subscription Plan'));
        $('#subjectsTbody').empty();
        $('#btnSavePlan .save-text').text(t('UpdatePlan', 'Update Plan'));

        $.getJSON(endpoints.get(id))
            .done(res => {
                $('#SubPlanCode').val(res.subPlanCode);
                $('#SubPlanName').val(res.subPlanName);
                $('#Price').val(res.price);
                $('#ExpiryMonths').val(res.expiryMonths);
                $('#Description').val(res.description || '');

                if (res.subjects && res.subjects.length) {
                    res.subjects.forEach(s => {
                        addSubjectRow({
                            YearCode: s.yearCode,
                            SubjectCode: s.subjectCode,
                            Count: s.count
                        });
                    });
                } else {
                    addSubjectRow();
                }
                recalcTotalCount();
                planModal.show();
            })
            .fail(() => {
                notify('danger', t('FailedToLoadPlan', 'Failed to load subscription plan.'));
            });
    }

    function collectPlanFormData() {
        const subjects = [];
        let valid = true;
        $('#subjectsTbody tr').each(function () {
            const yearCode = parseInt($(this).find('.year-select').val(), 10);
            const subjectCode = parseInt($(this).find('.subject-select').val(), 10);
            const count = parseInt($(this).find('.count-input').val(), 10);

            if (!yearCode || !subjectCode || !count) {
                valid = false;
                return;
            }

            subjects.push({
                YearCode: yearCode,
                SubjectCode: subjectCode,
                Count: count
            });
        });

        if (!valid || subjects.length === 0) {
            notify('warning', t('InvalidSubjectsWarning', 'Please complete all subject rows.'));
            return null;
        }

        return {
            SubPlanCode: parseInt($('#SubPlanCode').val(), 10) || 0,
            SubPlanName: $('#SubPlanName').val().trim(),
            Price: parseInt($('#Price').val(), 10) || 0,
            ExpiryMonths: parseFloat($('#ExpiryMonths').val()) || 0,
            Description: $('#Description').val().trim(),
            Subjects: subjects
        };
    }

    function setSavingState(isSaving) {
        const $btn = $('#btnSavePlan');
        $btn.prop('disabled', isSaving);
        $btn.find('.spinner-border')[isSaving ? 'removeClass' : 'addClass']('d-none');
    }

    function setDeletingState(isDeleting) {
        const $btn = $('#btnConfirmDelete');
        $btn.prop('disabled', isDeleting);
        $btn.find('.spinner-border')[isDeleting ? 'removeClass' : 'addClass']('d-none');
    }

    function setBuyingState(isBuying) {
        const $btn = $('#btnConfirmBuy');
        $btn.prop('disabled', isBuying);
        $btn.find('.spinner-border')[isBuying ? 'removeClass' : 'addClass']('d-none');
    }

    function submitPlanForm(e) {
        e.preventDefault();
        const data = collectPlanFormData();
        if (!data) return;

        if (!data.SubPlanName) {
            notify('warning', t('PlanNameRequired', 'Plan name is required.'));
            return;
        }
        if (data.Subjects.length === 0) {
            notify('warning', t('AtLeastOneSubjectRequired', 'At least one subject row is required.'));
            return;
        }

        const url = isEditMode ? endpoints.edit : endpoints.create;
        const payload = JSON.stringify(data);

        setSavingState(true);

        $.ajax({
            url,
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            data: payload
        })
            .done(res => {
                if (res.success) {
                    notify('success', isEditMode ? t('PlanUpdated', 'Plan updated successfully.') : t('PlanCreated', 'Plan created successfully.'));
                    planModal.hide();
                    reloadPlans();
                } else {
                    notify('danger', escapeHtml(res.message || t('OperationFailed', 'Operation failed.')));
                }
            })
            .fail(() => {
                notify('danger', t('ErrorSavingPlan', 'Error saving subscription plan.'));
            })
            .always(() => setSavingState(false));
    }

    // Delete
    function openDeleteModal(id) {
        $('#deleteSubPlanCode').val(id);
        deleteModal.show();
    }

    function confirmDelete() {
        const id = parseInt($('#deleteSubPlanCode').val(), 10);
        if (!id) return;
        setDeletingState(true);
        $.ajax({
            url: endpoints.delete,
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            data: JSON.stringify({ SubPlanCode: id })
        })
            .done(res => {
                if (res.success) {
                    notify('success', t('PlanDeleted', 'Plan deleted successfully.'));
                    deleteModal.hide();
                    reloadPlans();
                } else {
                    notify('danger', escapeHtml(res.message || t('PlanDeleteFailed', 'Delete failed.')));
                }
            })
            .fail(() => notify('danger', t('ErrorDeletingPlan', 'Error deleting plan.')))
            .always(() => setDeletingState(false));
    }

    // Buy Plan
    function openBuyModal(subPlanCode, planName) {
        $('#buy_SubPlanCode').val(subPlanCode);
        // Pattern text - keep English tokens if localization for pattern not provided
        $('#buyPlanInfo').text(`${t('Title', 'Plan')}: ${planName} (Code: ${subPlanCode})`);
        $('#studentsResultTable tbody').empty();
        $('#studentPhone').val('');
        $('#selectedStudentCode').val('');
        $('#btnConfirmBuy').prop('disabled', true);
        buyModal.show();
    }

    function searchStudents() {
        const phone = $('#studentPhone').val().trim();
        if (!phone) {
            notify('warning', t('EnterPhoneWarning', 'Enter a phone number to search.'));
            return;
        }
        const $btn = $('#btnSearchStudent');
        const original = $btn.html();
        $btn.prop('disabled', true).html(`<span class="spinner-border spinner-border-sm"></span> ${t('SearchStudent', 'Searching')}`);

        $.getJSON(endpoints.searchStudents(phone))
            .done(res => {
                if (!res.success) {
                    notify('danger', escapeHtml(res.message || t('SearchFailed', 'Search failed.')));
                    return;
                }
                const students = res.students || [];
                const $tbody = $('#studentsResultTable tbody').empty();
                if (!students.length) {
                    $tbody.append(`<tr><td colspan="3" class="text-center text-muted">${t('NoStudentsFound', 'No students found.')}</td></tr>`);
                } else {
                    students.forEach(st => {
                        $tbody.append(`
                            <tr class="student-row pointer" data-id="${st.studentCode}">
                                <td><input type="radio" name="studentSelect" value="${st.studentCode}"></td>
                                <td>${escapeHtml(st.name)}</td>
                                <td>${escapeHtml(st.yearName || '')}</td>
                            </tr>
                        `);
                    });
                }
            })
            .fail(() => {
                notify('danger', t('ErrorSearchingStudents', 'Error searching students.'));
            })
            .always(() => {
                $btn.prop('disabled', false).html(original);
            });
    }

    function selectStudent(studentCode) {
        $('#selectedStudentCode').val(studentCode);
        $('#btnConfirmBuy').prop('disabled', false);
    }

    function submitBuy(e) {
        e.preventDefault();
        const planCode = parseInt($('#buy_SubPlanCode').val(), 10);
        const studentCode = parseInt($('#selectedStudentCode').val(), 10);
        if (!planCode || !studentCode) {
            notify('warning', t('SelectStudent', 'Select a student.'));
            return;
        }
        setBuyingState(true);
        $.ajax({
            url: endpoints.buy,
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            data: JSON.stringify({
                SubscriptionPlanCode: planCode,
                StudentCode: studentCode
            })
        })
            .done(res => {
                if (res.success) {
                    notify('success', t('PlanPurchased', 'Plan purchased successfully.'));
                    buyModal.hide();
                } else {
                    notify('danger', escapeHtml(res.message || t('PurchaseFailed', 'Purchase failed.')));
                }
            })
            .fail(() => notify('danger', t('ErrorPurchasingPlan', 'Error purchasing plan.')))
            .always(() => setBuyingState(false));
    }

    // Event bindings
    function bindEvents() {
        $('#btnAddPlan').on('click', openCreateModal);
        $('#btnRefresh').on('click', () => reloadPlans());
        $('#planForm').on('submit', submitPlanForm);
        $('#btnConfirmDelete').on('click', confirmDelete);
        $('#btnAddSubjectRow').on('click', () => {
            addSubjectRow();
            recalcTotalCount();
        });

        $('#subjectsTbody')
            .on('change', '.year-select', function () {
                const yearCode = $(this).val();
                const $row = $(this).closest('tr');
                const $subjectSelect = $row.find('.subject-select');
                $subjectSelect.html(`<option value="">${t('SelectSubject', '-- Select Subject --')}</option>`);
                if (yearCode) {
                    loadSubjects(yearCode, $subjectSelect);
                } else {
                    $subjectSelect.prop('disabled', true);
                }
            })
            .on('change input', '.subject-select, .year-select, .count-input', recalcTotalCount)
            .on('click', '.btn-remove-row', function () {
                $(this).closest('tr').remove();
                recalcTotalCount();
            });

        $('#plansTable tbody')
            .on('click', '.btn-edit', function () {
                const id = $(this).data('id');
                openEditModal(id);
            })
            .on('click', '.btn-delete', function () {
                const id = $(this).data('id');
                openDeleteModal(id);
            })
            .on('click', '.btn-buy', function () {
                const id = $(this).data('id');
                const name = $(this).data('name');
                openBuyModal(id, name);
            });

        $('#btnSearchStudent').on('click', searchStudents);

        $('#studentsResultTable tbody').on('click', 'tr.student-row', function () {
            const id = $(this).data('id');
            $(this).find('input[type=radio]').prop('checked', true);
            $('#studentsResultTable tbody tr').removeClass('table-active');
            $(this).addClass('table-active');
            selectStudent(id);
        });

        $('#buyForm').on('submit', submitBuy);
    }

    function initModals() {
        planModal = new bootstrap.Modal(document.getElementById('planModal'));
        deleteModal = new bootstrap.Modal(document.getElementById('deleteModal'));
        buyModal = new bootstrap.Modal(document.getElementById('buyModal'));
    }

    // Init
    $(document).ready(function () {
        initModals();
        initTable();
        bindEvents();
    });

})(jQuery);