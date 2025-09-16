document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("changePasswordForm");
    if (!form) return;

    form.addEventListener("submit", function (e) {
        const newPassword = document.getElementById("newPassword").value.trim();
        const confirmPassword = document.getElementById("confirmPassword").value.trim();

        if (newPassword.length < 6) {
            alert("Password must be at least 6 characters.");
            e.preventDefault();
            return;
        }
        if (newPassword !== confirmPassword) {
            alert("Passwords do not match.");
            e.preventDefault();
            return;
        }
        if (newPassword === "123456789") {
            alert("You cannot use the default password as your new password.");
            e.preventDefault();
            return;
        }
    });
});