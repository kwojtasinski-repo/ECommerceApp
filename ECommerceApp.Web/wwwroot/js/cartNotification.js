define(['modalService'], function (modalServiceModule) {
    "use strict";

    const cartNotification = (function () {
        const _modalService = modalServiceModule.modalService;

        function showCartError(message) {
            _modalService.show(
                _modalService.ModalType.Error,
                'Nie mo\u017cna doda\u0107 do koszyka',
                message || 'Osi\u0105gni\u0119to maksymaln\u0105 dozwolon\u0105 ilo\u015b\u0107 produktu w koszyku.'
            );
        }

        function bindCartForms(limitMessage) {
            document.querySelectorAll('.js-add-to-cart-form').forEach(function (form) {
                form.addEventListener('submit', async function (e) {
                    e.preventDefault();
                    const response = await fetch(form.action, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: new URLSearchParams(new FormData(form))
                    });
                    if (response.ok) {
                        window.location.href = response.url;
                    } else {
                        showCartError(limitMessage);
                    }
                });
            });
        }

        return {
            showCartError: showCartError,
            bindCartForms: bindCartForms
        };
    })();

    return { cartNotification: cartNotification };
});
