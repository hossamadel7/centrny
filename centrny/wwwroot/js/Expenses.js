$(document).ready(function () {
    if ($("#rootDropdown").length) {
        $("#rootDropdown").change(function () {
            var rootCode = $(this).val();
            if (rootCode) {
                $.get("/Expenses/GetExpensesByRoot", { rootCode: rootCode }, function (data) {
                    $("#expensesTableContainer").html(data);
                }).fail(function () {
                    $("#expensesTableContainer").html("<div class='alert alert-danger'>Failed to load expenses.</div>");
                });
            } else {
                $("#expensesTableContainer").html("");
            }
        });
    }
});