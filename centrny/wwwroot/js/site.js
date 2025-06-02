$(document).ready(function () {
    $.ajax({
        url: '/Home/GetRootModules',
        type: 'GET',
        dataType: 'json',
        success: function (data) {
            // Clear table body
            $('#rootModulesTable tbody').empty();

            // Loop through data and append rows
            $.each(data, function (index, item) {
                $('#rootModulesTable tbody').append(
                    '<tr><td>' + item.module_code + '</td><td>' + item.root_code + '</td></tr>'
                );
            });
        },
        error: function (xhr, status, error) {
            alert("Error loading root modules: " + error);
        }
    });
});
