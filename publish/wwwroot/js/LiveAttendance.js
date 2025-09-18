// LiveAttendance.js
// Robust camera start with permission checks, iOS-friendly constraints path, and deviceId fallback.
// Ensures <video id="zxing-video"> is always present before starting scan.
// Requires Bootstrap JS (Modal, Toast) loaded by the layout.

(function () {
    'use strict';

    let students = [];
    let currentPage = 1;
    let totalPages = 1;
    const pageSize = 10;

    // ZXing dynamic loader with multiple fallbacks
    function loadZXingLibrary() {
        return new Promise((resolve, reject) => {
            if (typeof ZXing !== 'undefined') {
                console.log('[ZXING] Already loaded');
                resolve();
                return;
            }

            const zxingSources = [
                'https://unpkg.com/@zxing/library@latest/umd/index.min.js',
                'https://cdn.jsdelivr.net/npm/@zxing/library@latest/umd/index.min.js',
                'https://cdnjs.cloudflare.com/ajax/libs/zxing-library/0.20.0/index.min.js',
                'https://cdn.skypack.dev/@zxing/library@latest',
                'https://esm.sh/@zxing/library@latest'
            ];

            let currentIndex = 0;

            function tryLoad() {
                if (currentIndex >= zxingSources.length) {
                    console.error('[ZXING] All ZXing sources failed to load');
                    reject(new Error('All ZXing sources failed to load'));
                    return;
                }
                const src = zxingSources[currentIndex];
                console.log('[ZXING] Loading:', src);
                const script = document.createElement('script');
                script.src = src;
                script.async = true;

                script.onload = function () {
                    setTimeout(() => {
                        if (typeof ZXing !== 'undefined') {
                            console.log('[ZXING] Loaded:', src);
                            resolve();
                        } else {
                            currentIndex++;
                            tryLoad();
                        }
                    }, 500);
                };
                script.onerror = function () {
                    console.warn('[ZXING] Failed to load:', src);
                    script.remove();
                    currentIndex++;
                    tryLoad();
                };

                document.head.appendChild(script);
            }

            tryLoad();
        });
    }

    // Global state
    let isScanning = false;
    let initTimeout = null;
    let codeReader = null;
    let zxingLoaded = false;
    let configRef = null;

    // DOM helpers
    const $ = (sel) => document.querySelector(sel);

    function getConfig() {
        const root = $('#liveAttendanceApp');
        if (!root) {
            console.error('[LiveAttendance] #liveAttendanceApp not found');
            return null;
        }
        return {
            classCode: parseInt(root.dataset.classCode, 10),
            scanUrl: root.dataset.scanUrl,
            studentsUrl: root.dataset.studentsUrl
        };
    }

    function updateScanStatus(message, className) {
        const statusElement = $('#scanStatus');
        if (statusElement) {
            statusElement.innerHTML = '<div class="' + className + '"><small>' + message + '</small></div>';
        }
        console.log('[LiveAttendance] ScanStatus:', message);
    }

    function updatePermissionBadge(state, message) {
        const badge = $('#cameraPermissionStatus');
        if (!badge) return;
        let cls = 'bg-secondary';
        let text = 'Unknown';

        switch (state) {
            case 'checking': cls = 'bg-info'; text = 'Checking...'; break;
            case 'granted': cls = 'bg-success'; text = 'Permission: Granted'; break;
            case 'prompt': cls = 'bg-warning'; text = 'Permission: Prompt'; break;
            case 'denied': cls = 'bg-danger'; text = 'Permission: Denied'; break;
            case 'insecure': cls = 'bg-danger'; text = 'HTTPS required'; break;
            case 'unsupported': cls = 'bg-danger'; text = 'Camera not supported'; break;
            default: cls = 'bg-secondary'; text = 'Unknown';
        }

        badge.className = 'badge ' + cls + ' ms-2';
        badge.textContent = text;

        if (message) {
            updateScanStatus(message,
                state === 'granted' ? 'text-success' :
                    state === 'denied' || state === 'insecure' ? 'text-danger' :
                        'text-info'
            );
        }
        console.log('[LiveAttendance] PermissionBadge:', text, message || '');
    }

    function isSecureContext() {
        return location.protocol === 'https:' ||
            location.hostname === 'localhost' ||
            location.hostname === '127.0.0.1';
    }

    // Ensure camera permission: check + request (prompts user)
    async function ensureCameraPermission() {
        updatePermissionBadge('checking');
        console.log('[LiveAttendance] Checking camera permission...');

        if (!isSecureContext()) {
            updatePermissionBadge('insecure', 'Camera requires HTTPS (or localhost).');
            alert('Camera access requires a secure context (HTTPS) or running on localhost.');
            return false;
        }

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            updatePermissionBadge('unsupported', 'This browser does not support camera APIs.');
            alert('This browser does not support camera APIs (getUserMedia). Try a modern browser.');
            return false;
        }

        // Try Permissions API if available
        try {
            if (navigator.permissions && navigator.permissions.query) {
                const res = await navigator.permissions.query({ name: 'camera' });
                if (res && res.state === 'granted') {
                    updatePermissionBadge('granted', 'Camera permission already granted.');
                    console.log('[LiveAttendance] Permission API: granted');
                    return true;
                } else if (res && res.state === 'denied') {
                    updatePermissionBadge('denied', 'Camera permission is blocked. Allow it in browser site settings.');
                    alert('Camera permission is blocked. Please open your browser site settings and allow camera access for this site, then try again.');
                    console.log('[LiveAttendance] Permission API: denied');
                    return false;
                } else {
                    updatePermissionBadge('prompt', 'Requesting camera permission...');
                    console.log('[LiveAttendance] Permission API: prompt');
                }
            }
        } catch {
            updatePermissionBadge('prompt', 'Requesting camera permission...');
            console.log('[LiveAttendance] Permission API: fallback to getUserMedia');
        }

        // Trigger native prompt by requesting a stream
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
            stream.getTracks().forEach(t => t.stop());
            updatePermissionBadge('granted', 'Camera permission granted.');
            console.log('[LiveAttendance] getUserMedia: permission granted');
            return true;
        } catch (err) {
            let msg = 'Camera permission denied or unavailable.';
            if (err && err.name === 'NotAllowedError') msg = 'Permission denied by browser.';
            else if (err && err.name === 'NotFoundError') msg = 'No camera device found.';
            else if (err && err.name === 'NotReadableError') msg = 'Camera is in use by another app.';
            else if (err && err.name === 'SecurityError') msg = 'HTTPS required to access camera.';
            updatePermissionBadge('denied', msg);
            alert(msg + '\n\nTip: Check site permissions and ensure no other app is using the camera.');
            console.error('[LiveAttendance] getUserMedia error:', err);
            return false;
        }
    }

    function showManualOnlyInterface() {
        const readerDiv = $('#reader');
        if (readerDiv) {
            readerDiv.innerHTML = `
                <div class="alert alert-info text-center">
                  <i class="mdi mdi-information-outline me-2"></i>
                  <strong>Camera Not Available</strong><br>
                  Please use the manual input section below to mark attendance.
                </div>
            `;
        }
        const switchBtn = $('#switchCamera');
        if (switchBtn) switchBtn.style.display = 'none';
        console.log('[LiveAttendance] Showing manual-only interface.');
    }

    function handleZXingScannerError(error) {
        const errStr = (error && error.toString ? error.toString() : '').toLowerCase();
        let msg = 'Camera initialization failed.';
        if (errStr.includes('permission') || errStr.includes('notallowed')) msg = 'Camera permission denied. Please allow camera access.';
        else if (errStr.includes('notfound') || errStr.includes('device')) msg = 'No camera found on this device.';
        else if (errStr.includes('notsupported')) msg = 'Camera not supported in this browser.';
        else if (errStr.includes('notreadable')) msg = 'Camera is being used by another application.';
        updateScanStatus(msg + ' Using manual input instead.', 'text-warning');
        showManualOnlyInterface();
        console.error('[LiveAttendance] ZXing Scanner Error:', error);
    }

    function stopZXingScanner() {
        isScanning = false;

        if (initTimeout) {
            clearTimeout(initTimeout);
            initTimeout = null;
        }

        if (codeReader) {
            try { codeReader.reset(); } catch { /* noop */ }
            codeReader = null;
        }

        const switchBtn = $('#switchCamera');
        if (switchBtn) switchBtn.style.display = 'none';

        updateScanStatus('Scanner stopped', 'text-muted');
        console.log('[LiveAttendance] Scanner stopped');
    }

    // Always ensure the <video id="zxing-video"> exists before scan
    function prepareVideoElement() {
        const readerDiv = $('#reader');
        if (!readerDiv) {
            console.error('[LiveAttendance] #reader not found!');
            return null;
        }
        readerDiv.innerHTML = ''; // Always clear so video is not duplicated
        const video = document.createElement('video');
        video.id = 'zxing-video';
        video.setAttribute('playsinline', true);
        video.style.width = '100%';
        video.style.maxHeight = '420px';
        video.style.borderRadius = '10px';
        readerDiv.appendChild(video);
        console.log('[LiveAttendance] Injected <video id="zxing-video"> into #reader');
        return video;
    }

    // iOS/Safari friendly path: use constraints with facingMode
    async function tryStartWithConstraints() {
        prepareVideoElement();
        const constraints = {
            video: {
                facingMode: { ideal: 'environment' }
            },
            audio: false
        };
        console.log('[LiveAttendance] Trying ZXing decodeFromConstraints...');
        return codeReader.decodeFromConstraints(constraints, 'zxing-video', (result, err) => {
            if (result && isScanning) {
                console.log('[LiveAttendance] QR Scan result:', result.text);
                handleZXingQRScan(configRef, result.text);
            }
        });
    }

    // DeviceId path: enumerate devices and pick back camera if possible
    async function tryStartWithDeviceId() {
        prepareVideoElement();

        const videoInputDevices = await codeReader.listVideoInputDevices();
        console.log('[LiveAttendance] Video devices:', videoInputDevices);
        if (!videoInputDevices || videoInputDevices.length === 0) {
            throw new Error('No camera devices found');
        }

        let selectedDeviceId = videoInputDevices[0].deviceId;
        for (const dev of videoInputDevices) {
            const label = (dev.label || '').toLowerCase();
            if (label.includes('back') || label.includes('rear') || label.includes('environment')) {
                selectedDeviceId = dev.deviceId;
                break;
            }
        }
        console.log('[LiveAttendance] Using deviceId:', selectedDeviceId);
        return codeReader.decodeFromVideoDevice(selectedDeviceId, 'zxing-video', (result, err) => {
            if (result && isScanning) {
                console.log('[LiveAttendance] QR Scan result:', result.text);
                handleZXingQRScan(configRef, result.text);
            }
        });
    }

    async function initializeZXingScanner() {
        console.log('[LiveAttendance] initializeZXingScanner called');
        const readerDiv = $('#reader');
        if (!readerDiv) {
            updateScanStatus('Scanner container not found', 'text-danger');
            console.error('[LiveAttendance] #reader not found during scanner init');
            return;
        }
        console.log('[LiveAttendance] #reader:', readerDiv, 'innerHTML:', readerDiv.innerHTML);

        // Ensure permission before starting
        const permissionOk = await ensureCameraPermission();
        console.log('[LiveAttendance] Camera permission result:', permissionOk);
        if (!permissionOk) {
            showManualOnlyInterface();
            return;
        }

        if (!zxingLoaded || typeof ZXing === 'undefined') {
            updateScanStatus('QR Scanner library not available. Using manual input only.', 'text-warning');
            showManualOnlyInterface();
            console.error('[LiveAttendance] ZXing NOT loaded!');
            return;
        }

        updateScanStatus('Starting scanner...', 'text-info');

        if (initTimeout) clearTimeout(initTimeout);
        initTimeout = setTimeout(() => {
            showManualOnlyInterface();
            updateScanStatus('Scanner startup timeout. Using manual input.', 'text-warning');
            console.warn('[LiveAttendance] Scanner startup timeout');
        }, 15000);

        try {
            codeReader = new ZXing.BrowserQRCodeReader();
            console.log('[LiveAttendance] Created ZXing.BrowserQRCodeReader');

            // Always create video before attempting scan
            let scanStarted = false;
            try {
                await tryStartWithConstraints();
                scanStarted = true;
            } catch (e1) {
                console.warn('[LiveAttendance] decodeFromConstraints failed:', e1);
                try {
                    await tryStartWithDeviceId();
                    scanStarted = true;
                } catch (e2) {
                    console.error('[LiveAttendance] decodeFromDeviceId failed:', e2);
                    // Both failed
                    throw e2;
                }
            }

            clearTimeout(initTimeout);

            if (scanStarted) {
                updateScanStatus('Scanner ready! Position QR code in the frame', 'text-success');
                isScanning = true;

                // Show switch camera if multiple devices
                try {
                    const devices = await codeReader.listVideoInputDevices();
                    if (devices && devices.length > 1) {
                        const switchBtn = $('#switchCamera');
                        if (switchBtn) switchBtn.style.display = 'inline-block';
                        console.log('[LiveAttendance] Multiple cameras found, showing switch button');
                    }
                } catch (devErr) {
                    console.warn('[LiveAttendance] Error listing cameras:', devErr);
                }
            } else {
                showManualOnlyInterface();
                console.warn('[LiveAttendance] scanStarted false, showing manual interface');
            }

        } catch (error) {
            clearTimeout(initTimeout);
            handleZXingScannerError(error);
        }
    }

    function extractItemKey(scannedText) {
        let itemKey = '';

        if (scannedText.includes('/student/')) {
            const parts = scannedText.split('/student/');
            if (parts.length > 1) {
                itemKey = parts[1].split('?')[0].split('#')[0].split('/')[0];
            }
        } else if (/^[A-Za-z0-9]{10,50}$/.test(scannedText.trim())) {
            itemKey = scannedText.trim();
        } else {
            const matches = scannedText.match(/[A-Za-z0-9]{15,}/g);
            if (matches && matches.length > 0) {
                itemKey = matches[0];
            }
        }
        console.log('[LiveAttendance] Extracted itemKey:', itemKey, 'from', scannedText);
        return itemKey;
    }

    function showSuccessToast(message, studentName, attendanceTime) {
        const toastMessage = $('#toastMessage');
        const toastElement = $('#attendanceSuccessToast');
        if (toastMessage && toastElement) {
            toastMessage.innerHTML =
                '<strong>' + (studentName || 'Student') + '</strong> has been marked as present.<br>' +
                '<small class="text-muted">Time: ' + (attendanceTime || 'Now') + '</small>';

            const toast = new bootstrap.Toast(toastElement, { autohide: true, delay: 4000 });
            toast.show();
            console.log('[LiveAttendance] Showing success toast:', message, studentName, attendanceTime);
        }
    }

    // Update stats using new backend numbers
    function updateStats(data) {
        $('#totalStudents').textContent = data.totalEnrolled;
        $('#attendedStudents').textContent = data.attendedOnSchedule;
        $('#attendedOutOfSchedule').textContent = data.attendedOutOfSchedule;
        $('#absentStudents').textContent = data.totalAbsent;
        $('#attendancePercentage').textContent = data.totalEnrolled > 0
            ? Math.round((data.attendedOnSchedule / data.totalEnrolled) * 100) + '% Attendance'
            : '0% Attendance';

        const progressBar = $('#attendanceProgress');
        if (progressBar) {
            const pct = data.totalEnrolled > 0
                ? (data.attendedOnSchedule / data.totalEnrolled) * 100
                : 0;
            progressBar.style.width = pct + '%';
            progressBar.setAttribute('aria-valuenow', pct);
        }
        console.log('[LiveAttendance] Stats updated:', {
            totalEnrolled: data.totalEnrolled,
            attendedOnSchedule: data.attendedOnSchedule,
            attendedOutOfSchedule: data.attendedOutOfSchedule,
            absent: data.totalAbsent
        });
    }

    function updateStudentsTable(studentsPage) {
        const tbody = $('#studentsTableBody');
        if (!tbody) return;

        tbody.innerHTML = '';

        studentsPage.forEach(student => {
            const rowClass = student.isAttended ? 'table-success' : '';
            const icon = student.isAttended ? 'mdi-check-circle text-success' : 'mdi-clock-outline text-muted';
            const statusBadge = student.isAttended
                ? '<span class="badge bg-success">Present</span>'
                : '<span class="badge bg-secondary">Absent</span>';
            const attendanceTime = student.attendanceTime || student.AttendanceTime || '-';
            const outOfScheduleBadge = (student.isHisSchedule === false)
                ? '<span class="badge bg-warning ms-2">Out of Schedule</span>' : '';

            const row =
                '<tr id="student-' + student.studentCode + '" class="' + rowClass + '">' +
                '  <td>' +
                '    <div class="d-flex align-items-center">' +
                '      <i class="mdi ' + icon + ' me-2"></i>' +
                '      <strong>' + (student.studentName || '-') + '</strong> ' + outOfScheduleBadge +
                '    </div>' +
                '  </td>' +
                '  <td>' + (student.studentPhone || '-') + '</td>' +
                '  <td>' + (student.studentParentPhone || '-') + '</td>' +
                '  <td>' + (student.branchName || '-') + '</td>' +
                '  <td>' + (student.yearName || '-') + '</td>' +
                '  <td>' + statusBadge + '</td>' +
                '  <td><span class="attendance-time">' + attendanceTime + '</span></td>' +
                '</tr>';

            tbody.insertAdjacentHTML('beforeend', row);
        });
        console.log('[LiveAttendance] Students table updated');
    }

    function renderPaginationControls() {
        const container = $('#paginationControls');
        if (!container) return;
        container.innerHTML = '';
        for (let i = 1; i <= totalPages; i++) {
            container.innerHTML += `<button class="btn btn-sm btn-outline-primary mx-1" ${i === currentPage ? "disabled" : ""} onclick="window.gotoPage(${i})">${i}</button>`;
        }
    }
    window.gotoPage = function (page) {
        fetchStudentsPage(page);
    };

    function fetchStudentsPage(page = 1) {
        const config = getConfig();
        fetch(`${config.studentsUrl}?classCode=${encodeURIComponent(config.classCode)}&page=${page}&pageSize=${pageSize}`)
            .then(res => {
                if (!res.ok) throw new Error('Failed to fetch students');
                return res.json();
            })
            .then(data => {
                if (data.success) {
                    students = data.students;
                    totalPages = data.totalPages;
                    currentPage = data.page;
                    updateStudentsTable(students);
                    updateStats(data);
                    renderPaginationControls();
                }
            })
            .catch((err) => {
                console.warn('[LiveAttendance] Error refreshing students:', err);
            });
    }

    function refreshStudentsList() {
        fetchStudentsPage(currentPage);
    }

    function processAttendance(config, itemKey, forceAttendance = false) {
        updateScanStatus('Sending to server...', 'text-warning');
        console.log('[LiveAttendance] Processing attendance:', itemKey, 'force:', forceAttendance);

        const requestData = {
            itemKey: itemKey,
            classCode: config.classCode,
            attendanceType: 1,
            isForcedAttendance: forceAttendance
        };

        fetch(config.scanUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestData)
        })
            .then(response => {
                if (!response.ok) throw new Error('Network response was not ok: ' + response.status);
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    updateScanStatus('Success: ' + (data.studentName || 'Student') + ' marked present!', 'text-success');
                    showSuccessToast(data.message || 'Attendance marked successfully', data.studentName, data.attendanceTime);

                    setTimeout(() => {
                        fetchStudentsPage(currentPage);
                        setTimeout(() => {
                            if (codeReader) {
                                isScanning = true;
                                updateScanStatus('Ready for next scan', 'text-success');
                            }
                        }, 1200);
                    }, 600);
                } else if (data.canForce) {
                    if (typeof Swal !== 'undefined') {
                        Swal.fire({
                            title: 'Student not in schedule',
                            text: data.error + ' Do you want to mark attendance anyway?',
                            icon: 'warning',
                            showCancelButton: true,
                            confirmButtonText: 'Yes, mark attendance anyway',
                            cancelButtonText: 'No, cancel'
                        }).then((result) => {
                            if (result.isConfirmed) {
                                updateScanStatus('Marking attendance as out of schedule...', 'text-warning');
                                processAttendance(config, itemKey, true);
                            } else {
                                updateScanStatus('Attendance cancelled.', 'text-info');
                                setTimeout(() => {
                                    isScanning = true;
                                    updateScanStatus('Ready to scan again', 'text-info');
                                }, 1400);
                            }
                        });
                    } else {
                        if (confirm(data.error + '\nDo you want to mark attendance anyway?')) {
                            processAttendance(config, itemKey, true);
                        } else {
                            updateScanStatus('Attendance cancelled.', 'text-info');
                            setTimeout(() => {
                                isScanning = true;
                                updateScanStatus('Ready to scan again', 'text-info');
                            }, 1400);
                        }
                    }
                } else {
                    const errorMsg = data.error || data.message || 'Unknown error occurred';
                    updateScanStatus('Error: ' + errorMsg, 'text-danger');
                    setTimeout(() => {
                        if (codeReader) {
                            isScanning = true;
                            updateScanStatus('Ready to scan again', 'text-info');
                        }
                    }, 1800);
                }
            })
            .catch(error => {
                updateScanStatus('Network error: ' + error.message, 'text-danger');
                setTimeout(() => {
                    if (codeReader) {
                        isScanning = true;
                        updateScanStatus('Ready to try again', 'text-info');
                    }
                }, 1800);
                console.error('[LiveAttendance] Attendance network error:', error);
            });
    }

    function handleZXingQRScan(config, scannedText) {
        if (!isScanning) return;

        isScanning = false;
        updateScanStatus('QR detected! Processing...', 'text-warning');
        console.log('[LiveAttendance] handleZXingQRScan:', scannedText);

        const itemKey = extractItemKey(scannedText);
        if (!itemKey) {
            updateScanStatus('Invalid QR code format. Expected student profile QR or item key.', 'text-danger');
            setTimeout(() => {
                isScanning = true;
                updateScanStatus('Ready to scan again', 'text-info');
            }, 1500);
            return;
        }

        processAttendance(config, itemKey);
    }

    function switchZXingCamera() {
        if (!codeReader || !isScanning) {
            updateScanStatus('Scanner not active', 'text-warning');
            return;
        }

        updateScanStatus('Switching camera...', 'text-info');
        stopZXingScanner();

        setTimeout(() => {
            initializeZXingScanner();
            setTimeout(() => { isScanning = true; }, 800);
        }, 350);
        console.log('[LiveAttendance] Switching camera');
    }

    function handleManualInput(config) {
        const input = $('#manualItemKey');
        if (!input) return;
        const itemKey = input.value.trim();
        if (!itemKey) {
            alert('Please enter a valid item key');
            return;
        }
        processAttendance(config, itemKey);
        input.value = '';
        console.log('[LiveAttendance] Manual input processed:', itemKey);
    }

    function initializeEventListeners(config) {
        configRef = config;

        // Modal events
        const qrModal = $('#qrScannerModal');
        if (qrModal) {
            qrModal.addEventListener('shown.bs.modal', async function () {
                console.log('[LiveAttendance] Modal shown event fired. aria-hidden:', qrModal.getAttribute('aria-hidden'), 'display:', getComputedStyle(qrModal).display);
                const ok = await ensureCameraPermission();
                if (!ok) {
                    showManualOnlyInterface();
                    return;
                }
                prepareVideoElement();
                setTimeout(() => {
                    console.log('[LiveAttendance] Calling initializeZXingScanner after modal shown');
                    initializeZXingScanner();
                }, 150);
            });
            qrModal.addEventListener('hidden.bs.modal', function () {
                stopZXingScanner();
                console.log('[LiveAttendance] Modal hidden event fired.');
            });
        } else {
            console.error('[LiveAttendance] #qrScannerModal not found for event binding!');
        }

        // Open modal
        const liveAttendBtn = $('#liveAttendBtn');
        if (liveAttendBtn) {
            liveAttendBtn.addEventListener('click', function () {
                const modal = new bootstrap.Modal($('#qrScannerModal'));
                modal.show();
                console.log('[LiveAttendance] LiveAttendBtn clicked, modal.show() called');
            });
        } else {
            console.warn('[LiveAttendance] #liveAttendBtn not found!');
        }

        // Check/Enable Camera Access
        const checkPermBtn = $('#checkCameraPermissionBtn');
        if (checkPermBtn) {
            checkPermBtn.addEventListener('click', ensureCameraPermission);
        }

        // Manual input actions
        const manualBtn = $('#manualAttendBtn');
        if (manualBtn) {
            manualBtn.addEventListener('click', () => handleManualInput(config));
        }
        const manualInput = $('#manualItemKey');
        if (manualInput) {
            manualInput.addEventListener('keypress', function (e) {
                if (e.key === 'Enter') handleManualInput(config);
            });
        }

        // Switch camera
        const switchCameraBtn = $('#switchCamera');
        if (switchCameraBtn) {
            switchCameraBtn.addEventListener('click', switchZXingCamera);
        }

        // Refresh list
        const refreshBtn = $('#refreshStudents');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => refreshStudentsList());
        }

        // Auto-refresh every 30 seconds
        setInterval(() => refreshStudentsList(), 30000);

        window.handleZXingQRScan = (text) => handleZXingQRScan(config, text);

        console.log('[LiveAttendance] Event listeners initialized');
    }

    document.addEventListener('DOMContentLoaded', function () {
        const config = getConfig();
        if (!config) {
            console.error('[LiveAttendance] No config found, aborting boot');
            return;
        }

        updatePermissionBadge('checking', 'Ready to check permission.');
        updatePermissionBadge('');

        loadZXingLibrary()
            .then(() => {
                zxingLoaded = true;
                console.log('[LiveAttendance] ZXing library loaded');
            })
            .catch(() => {
                zxingLoaded = false;
                console.error('[LiveAttendance] ZXing library failed to load');
            })
            .finally(() => {
                initializeEventListeners(config);
                fetchStudentsPage(1);
                console.log('[LiveAttendance] Boot complete');
            });
    });

})();