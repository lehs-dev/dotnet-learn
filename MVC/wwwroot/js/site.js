// Tăng cường các form HTML; dữ liệu vẫn được xử lý bởi controller và API.
for (const dialog of document.querySelectorAll('dialog[data-auto-open="true"]'))
    if (!dialog.open) dialog.showModal();
document.addEventListener('submit', event => {
    const form = event.target;
    if (form instanceof HTMLFormElement && form.dataset.confirm && !window.confirm(form
            .dataset.confirm)) event.preventDefault();
    if (form instanceof HTMLFormElement && form.action.endsWith('/auth/register')) {
        const password = form.elements.namedItem('Password');
        const confirm = form.elements.namedItem('ConfirmPassword');
        confirm.setCustomValidity(password.value === confirm.value ? '' :
            'Mật khẩu xác nhận không khớp.');
        if (!form.reportValidity()) event.preventDefault();
    }
});
document.addEventListener('input', event => {
    if (event.target.name === 'ConfirmPassword') event.target.setCustomValidity('');
});
