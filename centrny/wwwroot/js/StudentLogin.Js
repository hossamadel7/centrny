/* studentLogin.js
   Redirects to /OnlineStudent after successful login.
   Requirements:
   - Form id: studentLoginForm
   - Inputs: name="username", name="password"
   - Anti-forgery hidden input: __RequestVerificationToken
   - Button id: btnStudentLogin
   - Status div id: studentLoginStatus
   - Optional toggle password button id: toggleStudentPwd
*/
(function () {
    const form = document.getElementById('studentLoginForm');
    if (!form) return;

    const btnSubmit = document.getElementById('btnStudentLogin');
    const statusBox = document.getElementById('studentLoginStatus');
    const togglePwdBtn = document.getElementById('toggleStudentPwd');
    const pwdInput = form.querySelector('input[name="password"]');
    const userInput = form.querySelector('input[name="username"]');

    // Toggle password visibility
    togglePwdBtn?.addEventListener('click', function () {
        if (!pwdInput) return;
        if (pwdInput.type === 'password') {
            pwdInput.type = 'text';
            togglePwdBtn.textContent = 'Hide';
        } else {
            pwdInput.type = 'password';
            togglePwdBtn.textContent = 'Show';
        }
    });

    // AJAX submit
    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        clearStatus();

        const username = (userInput?.value || '').trim();
        const password = pwdInput?.value || '';

        if (!username || !password) {
            showError('Username and password are required.');
            return;
        }

        setBusy(true);

        try {
            const resp = await fetch('/StudentLogin/Login', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: buildFormData({ username, password })
            });

            if (!resp.ok) {
                showError('Server error. Please try again.');
                setBusy(false);
                return;
            }

            let data;
            try {
                data = await resp.json();
            } catch {
                showError('Unexpected response.');
                setBusy(false);
                return;
            }

            if (data.success) {
                window.location.href = data.redirectUrl; // /OnlineStudent
            } else {
                showError(data.error || 'Login failed.');
                setBusy(false);
            }
        } catch {
            showError('Network error. Check connection.');
            setBusy(false);
        }
    });

    function getAntiForgeryToken() {
        const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function buildFormData(values) {
        const fd = new FormData();
        for (const k in values) {
            fd.append(k, values[k] ?? '');
        }
        const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        if (tokenInput) fd.append('__RequestVerificationToken', tokenInput.value);
        return fd;
    }

    function showError(message) {
        if (!statusBox) return;
        statusBox.textContent = message;
        statusBox.classList.remove('text-success');
        statusBox.classList.add('text-danger');
        statusBox.style.display = 'block';
    }

    function clearStatus() {
        if (!statusBox) return;
        statusBox.textContent = '';
        statusBox.style.display = 'none';
    }

    function setBusy(isBusy) {
        if (btnSubmit) {
            btnSubmit.disabled = isBusy;
            btnSubmit.textContent = isBusy ? 'Processing...' : 'Login';
        }
        if (userInput) userInput.readOnly = isBusy;
        if (pwdInput) pwdInput.readOnly = isBusy;
        if (togglePwdBtn) togglePwdBtn.disabled = isBusy;
    }

    // Optional logout helper
    window.StudentLogout = async function () {
        try {
            const resp = await fetch('/StudentLogin/Logout', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: buildFormData({})
            });
            if (resp.ok) {
                const data = await resp.json();
                if (data.success) {
                    window.location.href = data.redirectUrl;
                } else {
                    alert(data.error || 'Logout failed.');
                }
            } else {
                alert('Logout request failed.');
            }
        } catch {
            alert('Network error during logout.');
        }
    };
})();