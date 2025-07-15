function toggleSidebar(open) {
    const sidebar = document.getElementById('modernSidebar');
    const overlay = document.getElementById('sidebarOverlay');
    if (open) {
        sidebar.classList.add('open');
        overlay.classList.add('active');
    } else {
        sidebar.classList.remove('open');
        overlay.classList.remove('active');
    }
}

document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.modern-sidebar .nav-link').forEach(link => {
        link.addEventListener('click', () => toggleSidebar(false));
    });

    const menuToggleBtn = document.getElementById('menuToggleBtn');
    if (menuToggleBtn) {
        menuToggleBtn.addEventListener('click', function () {
            toggleSidebar(true);
        });
    }
    const sidebarCloseBtn = document.getElementById('sidebarCloseBtn');
    if (sidebarCloseBtn) {
        sidebarCloseBtn.addEventListener('click', function () {
            toggleSidebar(false);
        });
    }
    const sidebarOverlay = document.getElementById('sidebarOverlay');
    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', function () {
            toggleSidebar(false);
        });
    }
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', function (e) {
            e.preventDefault();
            document.getElementById('logoutForm').submit();
        });
    }
});

document.addEventListener('keydown', function (e) {
    if (e.key === "Escape") {
        toggleSidebar(false);
    }
});