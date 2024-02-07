const config = (function () {
    function setup() {
        require.config({
            baseUrl: '/js'
        });
    }

    function init() {
        require(["validations", "common", "buttonTemplate", "dialogTemplate", "errors", "forms", "ajaxRequest", "modalService"],
            function (validations, common, buttonTemplate, dialogTemplate, errors, forms, ajaxRequest, modalService) {
                addObjectPropertiesToGlobal(validations);
                addObjectPropertiesToGlobal(common);
                addObjectPropertiesToGlobal(buttonTemplate);
                addObjectPropertiesToGlobal(dialogTemplate);
                addObjectPropertiesToGlobal(errors);
                addObjectPropertiesToGlobal(forms);
                addObjectPropertiesToGlobal(ajaxRequest);
                addObjectPropertiesToGlobal(modalService);

                $(document).trigger('DOMInitialized');

                function addObjectPropertiesToGlobal(obj) {
                    for (const prop in obj) {
                        window[prop] = obj[prop];
                    }
                }
            }
        );
    }

    return {
        setup,
        init
    };
})();
