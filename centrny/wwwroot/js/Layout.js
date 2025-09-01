// Unified toggle function that supports both LTR (slide from left) and RTL (slide from right)
function toggleSidebar(open) {
    const sidebar = document.getElementById('modernSidebar');
    const overlay = document.getElementById('sidebarOverlay');
    const body = document.body;

    if (!sidebar || !overlay) return;

    if (open) {
        sidebar.classList.add('open');
        overlay.classList.add('active');
        body.classList.add('sidebar-open');
        // Move focus inside sidebar for accessibility
        sidebar.setAttribute('aria-hidden', 'false');
        sidebar.focus({ preventScroll: true });
    } else {
        sidebar.classList.remove('open');
        overlay.classList.remove('active');
        body.classList.remove('sidebar-open');
        sidebar.setAttribute('aria-hidden', 'true');
    }
}

document.addEventListener('DOMContentLoaded', function () {
    // Close sidebar when clicking nav links
    document.querySelectorAll('.modern-sidebar .nav-link').forEach(link => {
        link.addEventListener('click', () => toggleSidebar(false));
    });

    // Open button
    const menuToggleBtn = document.getElementById('menuToggleBtn');
    if (menuToggleBtn) {
        menuToggleBtn.addEventListener('click', function () {
            const isOpen = document.getElementById('modernSidebar').classList.contains('open');
            toggleSidebar(!isOpen);
        });
    }

    // Close button
    const sidebarCloseBtn = document.getElementById('sidebarCloseBtn');
    if (sidebarCloseBtn) {
        sidebarCloseBtn.addEventListener('click', function () {
            toggleSidebar(false);
        });
    }

    // Overlay click
    const sidebarOverlay = document.getElementById('sidebarOverlay');
    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', function () {
            toggleSidebar(false);
        });
    }

    // Logout
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', function (e) {
            e.preventDefault();
            const form = document.getElementById('logoutForm');
            if (form) form.submit();
        });
    }

    // Close when resizing to desktop (optional safeguard)
    window.addEventListener('resize', () => {
        if (window.innerWidth > 1200) {
            toggleSidebar(false);
        }
    });

    // Resource Language Toggle (for resource localization only)
    const resourceLangForm = document.getElementById('resourceLangForm');
    const resourceLangSwitchBtn = document.getElementById('resourceLangSwitchBtn');
    const resourceLangHiddenInput = document.getElementById('resourceLangHiddenInput');
    const returnUrlInput = document.getElementById('returnUrlInput');
    if (resourceLangSwitchBtn && resourceLangForm && resourceLangHiddenInput && returnUrlInput) {
        resourceLangSwitchBtn.addEventListener('click', function (e) {
            // Set current URL before submitting form so user stays on same page after toggle
            returnUrlInput.value = window.location.pathname + window.location.search + window.location.hash;
            // Button submits the form, so nothing else needed
        });
    }
});

// ESC key support & focus trap minimal
document.addEventListener('keydown', function (e) {
    const sidebar = document.getElementById('modernSidebar');
    if (!sidebar) return;

    if (e.key === "Escape" && sidebar.classList.contains('open')) {
        toggleSidebar(false);
    }

    // Basic focus trap inside sidebar when open
    if (sidebar.classList.contains('open') && e.key === 'Tab') {
        const focusable = sidebar.querySelectorAll('a,button,[tabindex]:not([tabindex="-1"])');
        if (!focusable.length) return;
        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (e.shiftKey && document.activeElement === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    }
});