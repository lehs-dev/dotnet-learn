// Delegated handlers also work after Blazor navigation.
function openShopDialog(id) {
    const dialog = document.getElementById(id);
    if (!(dialog instanceof HTMLDialogElement) || dialog.open) return;
    document.querySelectorAll('dialog[open]').forEach(item => item.close());
    dialog.showModal();
}

document.addEventListener('click', event => {
    if (!(event.target instanceof Element)) return;
    const open = event.target.closest('[data-open-dialog]');
    if (open) {
        event.preventDefault();
        openShopDialog(open.dataset.openDialog);
    }
    const close = event.target.closest('[data-close-dialog]');
    if (close) {
        event.preventDefault();
        close.closest('dialog')?.close();
    }
    if (event.target instanceof HTMLDialogElement) {
        const box = event.target.getBoundingClientRect();
        if (event.clientX < box.left || event.clientX > box.right || event.clientY < box
            .top || event.clientY > box.bottom) event.target.close();
    }
});

function electroShopOpenAuth() {
    const query = new URLSearchParams(location.search);
    const auto = document.querySelector('dialog[data-auto-open="true"]');
    if (auto) openShopDialog(auto.id);
    else if (query.has('register') || query.get('auth') === 'register') openShopDialog(
        'register-dialog');
    else if (query.has('login') || query.has('authError') || query.has('auth')) openShopDialog(
        'login-dialog');
}

function checkPasswordConfirmation(form) {
    const password = form.querySelector('input[name="Password"], input[name$=".Password"]');
    const confirmation = form.querySelector(
        'input[name="ConfirmPassword"], input[name$=".ConfirmPassword"]');
    if (password && confirmation) confirmation.setCustomValidity(password.value === confirmation
        .value ? '' : 'Mật khẩu xác nhận không khớp.');
}

document.addEventListener('input', event => {
    if (event.target.form) checkPasswordConfirmation(event.target.form);
});

document.addEventListener('submit', event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;
    checkPasswordConfirmation(form);
    if (!form.checkValidity()) {
        event.preventDefault();
        form.reportValidity();
        return;
    }
    if (form.dataset.confirm && !window.confirm(form.dataset.confirm)) {
        event.preventDefault();
        return;
    }
    // Preserve submitter name/value (e.g. payment outcome) and antiforgery fields.
    // aria-disabled blocks duplicate submits without changing submitted form data.
    if (!event.defaultPrevented && form.method.toLowerCase() === 'post') {
        if (form.dataset.submitting === 'true') {
            event.preventDefault();
            return;
        }
        form.dataset.submitting = 'true';
        const button = event.submitter;
        button?.setAttribute('aria-busy', 'true');
        button?.setAttribute('aria-disabled', 'true');
        setTimeout(() => {
            delete form.dataset.submitting;
            button?.removeAttribute('aria-busy');
            button?.removeAttribute('aria-disabled');
        }, 30000);
    }
});

window.addEventListener('pageshow', () => {
    document.querySelectorAll('form[data-submitting]').forEach(form => delete form.dataset
        .submitting);
    document.querySelectorAll('[aria-busy="true"]').forEach(button => {
        button.removeAttribute('aria-busy');
        button.removeAttribute('aria-disabled');
    });
});
document.addEventListener('DOMContentLoaded', electroShopOpenAuth);
document.addEventListener('enhancedload', electroShopOpenAuth);
