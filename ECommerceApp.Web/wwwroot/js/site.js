config.setup();
config.init();

// initialize, enable tooltips
$(document).on('DOMInitialized', async function () {
    if (typeof beforePageFullyLoaded === "function") {
        await beforePageFullyLoaded();
    }

    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
        new bootstrap.Tooltip(el);
    });
});
