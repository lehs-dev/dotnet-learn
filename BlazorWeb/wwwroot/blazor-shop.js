window.electroBlazor = {
    open: function(id) { const el = document.getElementById(id); if (el && !el.open) el.showModal(); },
    close: function(id) { document.getElementById(id)?.close(); }
};
document.addEventListener('click', function(event) {
    const dialog = event.target;
    if (dialog instanceof HTMLDialogElement) {
        const box = dialog.getBoundingClientRect();
        if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) dialog.close();
    }
});
