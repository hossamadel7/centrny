// Content.js with VS-style Find-in-Editor for current loaded column

var editor;

const rootColorColumns = [
    { CODE: "RootBodyColor", NAME: "Body color" },
    { CODE: "RootButtonColor", NAME: "Button color" },
    { CODE: "RootBodyFont", NAME: "Body font color" },
    { CODE: "RootButtonFontColor", NAME: "Button font color" },
    { CODE: "RootButtonFontColor2", NAME: "Button font color 2" },
    { CODE: "RootBackgroundColor", NAME: "Background color" }
];

let originalColors = {};

var lastEditorSearch = { cursor: null, query: "" };
function setupEditorSearch() {
    $('#editorSearchBtn').on('click', function () {
        var query = $('#editorSearch').val();
        if (!query) return;
        if (!lastEditorSearch.cursor || lastEditorSearch.query !== query) {
            lastEditorSearch.cursor = editor.getSearchCursor(query, null, { caseFold: true, multiline: true });
            lastEditorSearch.query = query;
        }
        if (!lastEditorSearch.cursor.findNext()) {
            lastEditorSearch.cursor = editor.getSearchCursor(query, null, { caseFold: true, multiline: true });
            lastEditorSearch.query = query;
            if (!lastEditorSearch.cursor.findNext()) {
                Swal.fire("No matches found.");
                return;
            }
        }
        editor.focus();
        editor.setSelection(lastEditorSearch.cursor.from(), lastEditorSearch.cursor.to());
        editor.scrollIntoView({ from: lastEditorSearch.cursor.from(), to: lastEditorSearch.cursor.to() });
    });
    $('#editorSearch').on('keydown', function (e) {
        if (e.key === 'Enter') $('#editorSearchBtn').click();
    });
}
function resetEditorSearch() {
    lastEditorSearch = { cursor: null, query: "" };
}

$(document).ready(function () {
    editor = CodeMirror.fromTextArea(document.getElementById('content'), {
        mode: 'htmlmixed',
        lineNumbers: true,
        theme: 'default',
        height: '300px'
    });
    setupEditorSearch();
    editor.on('change', resetEditorSearch);

    loadGyms();
    loadColumns();
    $('#gymFilter').on('change', loadGyms);
    $('#gym').on("change", function () {
        loadRootColors();
        loadContent();
        updateActionBarState();
    });
    $('#column').on("change", function () {
        loadContent();
        updateActionBarState();
    });
    $("#save").on("click", function (e) {
        e.preventDefault();
        saveAll();
    });
    loadRootColors();
    loadContent();
    setTimeout(updateActionBarState, 700);
});

/* Dropdown Loading, Colors, Content Loading, Save, Action Bar... unchanged from previous versions */

function loadGyms() {
    const mode = $('#gymFilter').val() || 'all';
    $.ajax({
        url: '/Content/get_gym_filtered',
        type: 'POST',
        contentType: "application/json; charset=utf-8",
        dataType: 'json',
        data: JSON.stringify({ mode: mode }),
        success: function (data) {
            const $gym = $('#gym');
            $gym.empty();
            $gym.append('<option value="0">- Select Root -</option>');
            data.forEach(r => {
                let badge = "";
                if (r.IS_CENTER === true) badge = " (Center)";
                else if (r.IS_CENTER === false) badge = " (Non-center)";
                $gym.append('<option value="' + r.CODE + '">' + r.NAME + badge + '</option>');
            });
            $gym.trigger("change");
        },
        error: function () {
            Swal.fire("Error", "Failed to load roots.", "error");
        }
    });
}
function loadColumns() {
    $.ajax({
        url: '/Content/get_column',
        type: 'POST',
        contentType: "application/json; charset=utf-8",
        dataType: 'json',
        success: function (data) {
            const $col = $('#column');
            $col.empty();
            $col.append('<option value="0">- Select Column -</option>');
            data.forEach(c => {
                $col.append('<option value="' + c.CODE + '">' + c.NAME + '</option>');
            });
            $col.trigger("change");
        },
        error: function () {
            Swal.fire("Error", "Failed to load columns.", "error");
        }
    });
}
function prepareForm() {
    document.getElementById('content').value = editor.getValue();
    return true;
}
function toggleFullScreen() {
    var widgetBox = document.getElementById("widgetBox");
    if (!document.fullscreenElement) {
        (widgetBox.requestFullscreen ||
            widgetBox.mozRequestFullScreen ||
            widgetBox.webkitRequestFullscreen ||
            widgetBox.msRequestFullscreen).call(widgetBox);
        $(".CodeMirror").css("height", "550px");
    } else {
        (document.exitFullscreen ||
            document.mozCancelFullScreen ||
            document.webkitExitFullscreen ||
            document.msExitFullscreen).call(document);
        $(".CodeMirror").css("height", "300px");
    }
}
function loadRootColors() {
    const gymVal = $('#gym').val();
    const $row = $('#rootColorsRow');
    const $inputsDiv = $('#rootColorsInputs');
    originalColors = {};

    if (gymVal === "0" || !gymVal) {
        $row.hide();
        $inputsDiv.html("");
        return;
    }
    $row.show();
    $inputsDiv.html('<div class="col-12 mb-2">Loading...</div>');
    $.ajax({
        type: 'POST',
        url: "/Content/getRootColorsAll",
        contentType: "application/json; charset=utf-8",
        dataType: 'json',
        data: JSON.stringify({ gym: gymVal }),
        success: function (data) {
            let html = "";
            rootColorColumns.forEach(function (col) {
                const rawVal = (data && typeof data[col.CODE] !== "undefined" && data[col.CODE]) ? data[col.CODE] : "";
                originalColors[col.CODE] = rawVal;
                const normalized = normalizeColor(rawVal);
                const statusText = normalized ? "Saved" : "Not set";
                const statusClass = normalized ? "secondary" : "warning";
                const displayVal = normalized || "";
                const previewColor = normalized || "#FFFFFF";
                html += `
                    <div class="col-md-4 mb-3">
                        <label for="${col.CODE}Input" style="display:flex;justify-content:space-between;align-items:center;">
                            <span>${col.NAME}</span>
                            <small id="${col.CODE}Status" class="text-${statusClass}">${statusText}</small>
                        </label>
                        <div class="input-group input-group-sm">
                            <span class="input-group-text" style="width:38px;padding:0;">
                                <span id="${col.CODE}Preview" style="display:inline-block;width:100%;height:20px;background:${previewColor};border:1px solid #ccc;"></span>
                            </span>
                            <input
                                type="text"
                                id="${col.CODE}Input"
                                class="form-control root-color-input"
                                placeholder="#RRGGBB"
                                autocomplete="off"
                                value="${displayVal}"
                                data-original="${rawVal}"
                                data-code="${col.CODE}"
                                data-changed="0"
                                maxlength="7"
                            />
                        </div>
                        <small class="form-text text-muted">#RGB or #RRGGBB</small>
                    </div>
                `;
            });
            $inputsDiv.html(html);
            $('.root-color-input').on('input', function () {
                const code = $(this).data('code');
                validateAndMark(code);
            });
        },
        error: function () {
            $inputsDiv.html('<div class="col-12 mb-2 text-danger">Error loading root colors.</div>');
        }
    });
}
function normalizeColor(v) {
    if (!v) return "";
    v = v.trim();
    if (!v.startsWith("#")) v = "#" + v;
    if (/^#[0-9A-Fa-f]{6}$/.test(v)) return v.toUpperCase();
    if (/^#[0-9A-Fa-f]{3}$/.test(v)) {
        return ("#" + v[1] + v[1] + v[2] + v[2] + v[3] + v[3]).toUpperCase();
    }
    return "";
}
function validateAndMark(code) {
    const $inp = $('#' + code + 'Input');
    const $status = $('#' + code + 'Status');
    const $preview = $('#' + code + 'Preview');
    let val = $inp.val().trim();
    if (val === "") {
        $inp.attr('data-changed', originalColors[code] ? '1' : '0');
        setStatus($status, originalColors[code] ? "Will clear (ignored)" : "Not set", "warning");
        $preview.css('background', '#FFFFFF');
        return;
    }
    if (!val.startsWith("#")) val = "#" + val;
    const full = /^#[0-9A-Fa-f]{6}$/;
    const short = /^#[0-9A-Fa-f]{3}$/;
    if (!(full.test(val) || short.test(val))) {
        setStatus($status, "Invalid", "danger");
        $inp.addClass('is-invalid');
        $preview.css('background', '#FFFFFF');
        return;
    }
    val = normalizeColor(val);
    $inp.removeClass('is-invalid');
    $preview.css('background', val);
    const normOriginal = normalizeColor(originalColors[code]);
    if (normOriginal !== val) {
        $inp.attr('data-changed', '1');
        setStatus($status, normOriginal ? "Changed" : "Will save", normOriginal ? "info" : "success");
    } else {
        $inp.attr('data-changed', '0');
        setStatus($status, "Saved", "secondary");
    }
    $inp.val(val);
}
function setStatus($el, text, tone) {
    $el.text(text);
    $el.removeClass(function (i, c) { return (c.match(/(^|\\s)text-\\S+/g) || []).join(' '); });
    $el.addClass('text-' + tone);
}
function gatherColorChanges(gymVal) {
    const payloads = [];
    rootColorColumns.forEach(function (col) {
        const $inp = $('#' + col.CODE + 'Input');
        if ($inp.length === 0) return;
        const changed = $inp.attr('data-changed') === '1';
        const val = $inp.val().trim();
        if (!changed) return;
        if (val === "") return;
        if (!/^#[0-9A-F]{6}$/.test(val)) return;
        payloads.push({
            gym: gymVal,
            column: col.CODE,
            content: val
        });
    });
    return payloads;
}
function loadContent() {
    const gymVal = $('#gym').val();
    const columnVal = $('#column').val();
    if (gymVal === "0" || columnVal === "0") {
        editor.setValue("");
        resetEditorSearch();
        return;
    }
    $.ajax({
        type: 'POST',
        url: "/Content/getContent?gym=" + gymVal + "&column=" + columnVal,
        contentType: "application/json; charset=utf-8",
        dataType: 'json',
        success: function (data) {
            if (!data || data.length === 0 || !data[0]) {
                editor.setValue("");
            } else {
                editor.setValue(data[0]);
            }
            prepareForm();
            resetEditorSearch();
        },
        error: function () {
            editor.setValue("");
            resetEditorSearch();
        }
    });
}
function saveAll() {
    prepareForm();
    const gymVal = $('#gym').val();
    const columnVal = $('#column').val();
    const contentVal = $('#content').val();
    if (gymVal === "0") {
        Swal.fire("Select Root", "Please select a root first.", "warning");
        return;
    }
    let anyInvalid = false;
    $('.root-color-input').each(function () {
        const val = $(this).val().trim();
        if (val !== "" && !/^#[0-9A-F]{6}$/.test(val)) {
            anyInvalid = true;
            $(this).addClass('is-invalid');
        } else {
            $(this).removeClass('is-invalid');
        }
    });
    if (anyInvalid) {
        Swal.fire("Invalid Colors", "Fix invalid color values before saving.", "error");
        return;
    }
    const ajaxCalls = [];
    const colorUpdates = gatherColorChanges(gymVal);
    colorUpdates.forEach(function (cu) {
        ajaxCalls.push($.ajax({
            type: 'POST',
            url: "/Content/saveRootColumn",
            contentType: "application/json; charset=utf-8",
            dataType: 'json',
            data: JSON.stringify(cu)
        }));
    });
    if (columnVal !== "0") {
        ajaxCalls.push($.ajax({
            type: 'POST',
            url: "/Content/save",
            contentType: "application/json; charset=utf-8",
            dataType: 'json',
            data: JSON.stringify({ gym: gymVal, column: columnVal, content: contentVal })
        }));
    }
    if (ajaxCalls.length === 0) {
        Swal.fire("Nothing to save", "No changes detected.", "info");
        return;
    }
    $.when.apply($, ajaxCalls).done(function () {
        Swal.fire("Success", "Changes saved successfully.", "success");
        loadRootColors();
    }).fail(function () {
        Swal.fire("Error", "Saving failed.", "error");
    });
}
function updateActionBarState() {
    var gymVal = $('#gym').val();
    var colVal = $('#column').val();
    var $bar = $('#actionBar');
    if (gymVal && gymVal !== "0") {
        $bar.removeClass('hidden');
        $('#selectedRootInfo').text('Root: ' + $('#gym option:selected').text());
    } else {
        $bar.addClass('hidden');
        $('#selectedRootInfo').text('No root selected');
    }
    if (colVal && colVal !== "0") {
        $('#selectedColumnInfo').text('Column: ' + $('#column option:selected').text());
    } else {
        $('#selectedColumnInfo').text('No column selected');
    }
}