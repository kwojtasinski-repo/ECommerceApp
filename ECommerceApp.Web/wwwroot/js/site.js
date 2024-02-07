config.setup();
config.init();

// initialize, enable tooltips
$(document).on('DOMInitialized', async function () {
    if (typeof beforePageFullyLoaded === "function") {
        await beforePageFullyLoaded();
    }

    $('[data-toggle="tooltip"]').tooltip();
    $('.data-search').attr('data-live-search', true);
    $('.data-search').selectpicker({
        width: '100%',
        title: '- [Wybierz] -',
        style: 'select-search-style',
        size: 6
    })
});
