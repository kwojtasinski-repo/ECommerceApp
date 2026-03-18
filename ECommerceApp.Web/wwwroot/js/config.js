const config = (function () {
    function setup() {
        require.config({
            baseUrl: '/js',
            paths: {
                he: '../lib/he'
            }
        });
    }

    function init() {
        require(["validations", "common", "buttonTemplate", "dialogTemplate", "errors", "forms", "ajaxRequest", "modalService", "he"],
            function (validations, common, buttonTemplate, dialogTemplate, errors, forms, ajaxRequest, modalService, he) {
                window.PagerClick = common.PagerClick;
                $(document).trigger('DOMInitialized', [{
                    emailRegex: validations.emailRegex,
                    passwordRegex: validations.passwordRegex,
                    maxCountImages: validations.maxCountImages,
                    allowedExtensions: validations.allowedExtensions,
                    PagerClick: common.PagerClick,
                    statusCodes: common.statusCodes,
                    formatPln: common.formatPln,
                    buttonTemplate: buttonTemplate.buttonTemplate,
                    dialogTemplate: dialogTemplate.dialogTemplate,
                    errors: errors.errors,
                    showErrorFromResponse: errors.showErrorFromResponse,
                    showError: errors.showError,
                    forms: forms.forms,
                    ajaxRequest: ajaxRequest.ajaxRequest,
                    modalService: modalService.modalService,
                    he
                }]);
            }
        );
    }

    return {
        setup,
        init
    };
})();
