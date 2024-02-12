const config = (function () {
    function setup() {
        require.config({
            baseUrl: '/js',
            'paths': {
                'he': '../lib/he'
            }
        });
    }

    function init() {
        require(["validations", "common", "buttonTemplate", "dialogTemplate", "errors", "forms", "ajaxRequest", "modalService", "he"],
            function (validations, common, buttonTemplate, dialogTemplate, errors, forms, ajaxRequest, modalService, he) {
                addObjectPropertiesToGlobal(validations);
                addObjectPropertiesToGlobal(common);
                addObjectPropertiesToGlobal(buttonTemplate);
                addObjectPropertiesToGlobal(dialogTemplate);
                addObjectPropertiesToGlobal(errors);
                addObjectPropertiesToGlobal(forms);
                addObjectPropertiesToGlobal(ajaxRequest);
                addObjectPropertiesToGlobal(modalService);
                window.he = he;
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
