$(document).ready(function () {
    Build_Dropdown("gym");
    Build_Dropdown("column");

    $('#gym').on("change", function (e) { loadContent(); });
    $('#column').on("change", function (e) { loadContent(); });

    loadContent();

    $("#save").on("click", function (e) {
        e.preventDefault();
        prepareForm();

        var gymVal = $('#gym').val();
        var columnVal = $('#column').val();
        var contentVal = $('#content').val();
        if (gymVal === "0" || columnVal === "0") {
            Swal.fire("Error", "Please select both gym and column before saving.", "error");
            return;
        }

        $.ajax({
            type: 'POST',
            url: "/Content/save",
            contentType: "application/json; charset=utf-8",
            dataType: 'json',
            data: JSON.stringify({ gym: gymVal, column: columnVal, content: contentVal }),
            success: function (data) {
                // If you expect "1" for success and other values for failure
                if (data === "1" || data === 1 || data.success === true) {
                    Swal.fire("Success", "Changes saved successfully.", "success");
                    // Optionally refresh data
                    // loadContent();
                } else {
                    Swal.fire("Error", "Saving failed. Check your input.", "error");
                }
                // If you use modal, uncomment below
                // sub_id = 0;
                // $("#subform").modal("hide");
            },
            error: function () {
                Swal.fire("Error", "Connection lost; try again later.", "error");
            }
        });
    });
});

function loadContent() {
    var gymVal = $('#gym').val();
    var columnVal = $('#column').val();
    if (gymVal === "0" || columnVal === "0") {
        editor.setValue("");
        return;
    }
    $.ajax({
        type: 'POST',
        url: "/Content/getContent?gym=" + gymVal + "&column=" + columnVal,
        contentType: "text/plain",
        dataType: 'json',
        success: function (data) {
            if (!data || data.length === 0 || !data[0])
                editor.setValue("");
            else
                editor.setValue(data[0]);
            prepareForm();
        },
        error: function () {
            editor.setValue("");
        }
    });
}

function Build_Dropdown(id) {
    $.ajax({
        url: '/Content/get_' + id,
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        type: "POST",
        success: function (data) {
            var parentElement = $("#" + id);
            parentElement.empty();
            parentElement.append('<option value="0"> - Select - </option>');
            for (var i = 0; i < data.length; i++) {
                parentElement.append('<option value="' + data[i].CODE + '">' + data[i].NAME + '</option>');
            }
            parentElement.trigger("change");
        },
        error: function () {
            Swal.fire("Error", "Error binding list!", "error");
        }
    });
}