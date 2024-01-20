/**
* Contains operations with creating buttons
*
*/
const buttonTemplate = (function () {
    return {
        /**
         * Creates button template
         * @param elementName Text of button.
         * @param className Class button.
         * @param onclick Method invoked on click.
         * @param type Type of button.
         * @param attributes Attributes of button. Template [{ key: value }].
         *
         */
        createButton: function (text, className, onclick, type, attributes) {
            const button = document.createElement("button");
            button.textContent = text ? text : "";
            button.className = className ? className : "";
            button.onclick = onclick ? onclick : () => { };
            button.type = type ? type : "button";

            if (attributes) {
                for (const attribute of attributes) {
                    if (!attribute || !attribute.key || !attribute.value) {
                        continue;
                    }

                    button.setAttribute(attribute.key, attribute.value);
                }
            }

            return button;
        }
    }
})();

/**
* Contains operation with creating dialogs
*
*/
const dialogTeplate = (function () {
    return {
        /**
         * Creates dialog html
         * @param header Html Header of modal.
         * @param body Html Body of modal.
         * @param footer Html Footer of modal.
         *
         */
        createDialog(header, body, footer) {
            const dialog = document.createElement("div");
            dialog.className = "modal-dialog";
            dialog.role = "document";

            if (header) {
                dialog.appendChild(header);
            }

            if (body) {
                dialog.appendChild(body);
            }

            if (footer) {
                dialog.appendChild(footer);
            }

            return dialog;
        },
        /**
         * Creates dialog header html
         * @param innerHtml Html Header of modal.
         *
         */
        createDialogHeader(innerHtml) {
            const header = document.createElement("div");
            header.className = "modal-header";

            if (!innerHtml) {
                return header;
            }

            if (innerHtml instanceof Array) {
                for (const element of innerHtml) {
                    if (!element) {
                        continue;
                    }
                    header.appendChild(element);
                }
                return header;
            }

            header.appendChild(innerHtml);
            return header;
        },
        /**
         * Creates dialog body html
         * @param innerHtml Html Body of modal.
         *
         */
        createDialogBody(innerHtml) {
            const body = document.createElement("div");
            body.className = "modal-body";

            if (!innerHtml) {
                return body;
            }

            if (innerHtml instanceof Array) {
                for (const element of innerHtml) {
                    if (!element) {
                        continue;
                    }
                    body.appendChild(element);
                }
                return body;
            } 

            body.appendChild(innerHtml);
            return body;
        },
        /**
         * Creates dialog footer html
         * @param innerHtml Html Footer of modal.
         *
         */
        createDialogFooter(innerHtml) {
            const footer = document.createElement("div");
            footer.className = "modal-footer";

            if (!innerHtml) {
                return footer;
            }

            if (innerHtml instanceof Array) {
                for (const element of innerHtml) {
                    if (!element) {
                        continue;
                    }
                    footer.appendChild(element);
                }
                return footer;
            }

            footer.appendChild(innerHtml);
            return footer;
        }
    }
})();

/**
* Services responsible for operations with modal like creating, showing, closing
*
*/
const modalService = (function () {
    const modalIdentifyClass = "modalClass";

    /**
     * Creates modal template html
     * @param headerTemplate Html Header of modal.
     * @param bodyTemplate Html Body of modal.
     * @param footerTemplate Html Footer of modal.
     *
     */
    function createModalTemplate(headerTemplate, bodyTemplate, footerTemplate) {
        const modalDiv = document.createElement("div");
        modalDiv.className = "modal fade " + modalIdentifyClass;
        modalDiv.tabIndex = "-1";
        modalDiv.role = "dialog";
        modalDiv.setAttribute('data-keyboard', 'false');
        modalDiv.setAttribute('data-backdrop', 'static')
        modalDiv.appendChild(createDialog(headerTemplate, bodyTemplate, footerTemplate));
        return modalDiv;
    }

    /**
     * Creates dialog html
     * @param headerTemplate Html Header of modal.
     * @param bodyTemplate Html Body of modal.
     * @param footerTemplate Html Footer of modal.
     *
     */
    function createDialog(headerTemplate, bodyTemplate, footerTemplate) {
        const modalDialog = dialogTeplate.createDialog();
        modalDialog.className += " modal-dialog-centered";
        modalDialog.appendChild(createModalContent(headerTemplate, bodyTemplate, footerTemplate));
        return modalDialog;
    }

    /**
     * Creates modal content html
     * @param headerTemplate Html Header of modal.
     * @param bodyTemplate Html Body of modal.
     * @param footerTemplate Html Footer of modal.
     *
     */
    function createModalContent(headerTemplate, bodyTemplate, footerTemplate) {
        const modalContent = document.createElement("div");
        modalContent.className = "modal-content";

        modalContent.appendChild(headerTemplate);
        modalContent.appendChild(bodyTemplate);
        modalContent.appendChild(footerTemplate);

        return modalContent;
    }

    /**
     * Creates modal header html
     * @param title Title of modal
     *
     */
    function createModalHeader(title) {
        const titleText = document.createElement("h5");
        titleText.className = "modal-title";
        titleText.textContent = title ? title : "";
        const closeButton = buttonTemplate.createButton(undefined, "close", closeButtonHandler, "button", [{ key: "data-dismiss", value: "modal" }, { key: "aria-label", value: "Close" }]);
        const spanCloseButton = document.createElement("span");
        spanCloseButton.setAttribute("aria-hidden", "true");
        spanCloseButton.innerHTML = "&times;";
        closeButton.appendChild(spanCloseButton);
        return dialogTeplate.createDialogHeader([titleText, closeButton]);
    }

    /**
     * Creates modal body html
     * @param bodyText Text inside modal body.
     *
     */
    function createModalBody(bodyText) {
        const textElement = document.createElement("p");
        textElement.textContent = bodyText;
        return dialogTeplate.createDialogBody(textElement);
    }

    /**
     * Creates modal footer html
     * @param buttons Html buttons used in footer modal.
     *
     */
    function createModalFooter(buttons) {
        const modalFooter = dialogTeplate.createDialogFooter();

        if (buttons) {
            for (const button of buttons) {
                modalFooter.appendChild(button);
            }
            return modalFooter;
        }

        const closeButton = buttonTemplate.createButton("Close", "btn btn-secondary", closeButtonHandler, "button", [{ key: "data-dismiss", value: "modal" }]);
        modalFooter.appendChild(closeButton);

        return modalFooter;
    }

    /**
     * Close modal
     *
     */
    function closeModal() {
        const modal = $('.' + modalIdentifyClass);
        if (!modal) {
            return;
        }

        modal.modal('hide');
        // cleanup 150ms for animation
        setTimeout(() => modal.remove(), 150);
    }

    /** actions which defines what should be used on subscribe, structure { actionName: '', func: () => {} }
    */
    const actions = [];
    const confirmAction = 'confirm';
    const denyAction = 'deny';

    /**
     * Method is invoking assigned action after click on button
     * For example it is used for confirmation dialog to confirm or deny choice
     * @param actionName Action name of button.
     *
     */
    function invokeActionAfterButtonClick(actionName) {
        const actionToInvoke = actions.find(a => a.actionName == actionName);
        actionToInvoke?.func();
        if (actions.length > 0) {
            actions.splice(0, actions.length);
        }
    }

    /**
     * Handler for closing modal, it closes modal and invoke method which was assigned as denyAction
     *
     */
    function closeButtonHandler() {
        closeModal();
        invokeActionAfterButtonClick(denyAction);
    }

    // public variables functions
    return {
        /**
         * Shows information modal
         * @param headerText Title of modal.
         * @param bodyText Text inside of body modal.
         *
         */
        showInformationModal: function (headerText, bodyText) {
            const headerTemplate = createModalHeader(headerText);
            const bodyTemplate = createModalBody(bodyText);
            const footerTemplate = createModalFooter();
            const modalTemplate = createModalTemplate(headerTemplate, bodyTemplate, footerTemplate);
            document.body.appendChild(modalTemplate);
            $('.' + modalIdentifyClass).modal('show');
        },
        /**
         * Shows confirmation modal
         * @param headerText Title of modal.
         * @param bodyText Text inside of body modal.
         *
         */
        showConfirmationModal: function (headerText, bodyText) {
            const headerTemplate = createModalHeader(headerText);
            const bodyTemplate = createModalBody(bodyText);
            const confirmButton = buttonTemplate.createButton("Yes", "btn btn-danger", () => { closeModal(); invokeActionAfterButtonClick(confirmAction); }, "type");
            const cancelButton = buttonTemplate.createButton("No", "btn btn-secondary", this.close, "type");
            const footerTemplate = createModalFooter([confirmButton, cancelButton]);
            const modalTemplate = createModalTemplate(headerTemplate, bodyTemplate, footerTemplate);
            document.body.appendChild(modalTemplate);
            $('.' + modalIdentifyClass).modal('show');
            return new Promise((resolve, _) => {
                actions.push({ actionName: confirmAction, func: () => resolve(true) });
                actions.push({ actionName: denyAction, func: () => resolve(false) });
            });
        },
        /**
         * Shows custom modal with custom header, body and footer
         * @param header Html Header of modal.
         * @param body Html Body of modal.
         * @param footer Html Footer of modal.
         *
         */
        showCustomModal: function (header, body, footer) {
            const modalTemplate = createModalTemplate(header, body, footer);
            document.body.appendChild(modalTemplate);
            $('.' + modalIdentifyClass).modal('show');
        },

        /**
         * Method closes modal
         *
         */
        close: function () {
            closeButtonHandler();
        }
    }
})();

/**
* Operations with Promise Ajax Request
*
*/
const ajaxRequest = (function () {
    function asyncAjax(url, type, data, contentType, dataType) {
        return new Promise(function (resolve, reject) {
            $.ajax({
                url,
                type: type ? type : 'GET',
                data: data,
                contentType: contentType,
                dataType: dataType,
                beforeSend: function () {
                },
                success: function (data) {
                    resolve(data);
                },
                error: function (err) {
                    reject(err);
                }
            });
        });
    }

    return {
        /**
         * Sends Ajax Promise Request
         * @param url Url to send request.
         * @param type Type of Request GET, POST, etc. if not specified GET Request will be send
         * @param data Data to send.
         * @param contentType Content Type for example application-json.
         * @param dataType Data Type.
         *
         */
        send: function (url, type, data, contentType, dataType) {
            return asyncAjax(url, type, data, contentType, dataType);
        }
    }
})();

/**
* PagerClick method to submit pagination form with data
* @param index Page to choose in form.
* @param id Form id.
* @param pageNo The page number element identifier - default pageNo.
*
*/
function PagerClick(index, id, pageNo) {
    document.getElementById(pageNo ? pageNo : "pageNo").value = index;
    var form = $('#'+id)[0];
    form.submit();
}

/**
* Contains status codes used in requests response 
*
*/
const statusCodes = (function () {
    return {
        OK: 200,
        Created: 201,
        Unauthorized: 401,
        Forbidden: 403,
        NotFound: 404,
        MethodNotAllowed: 405,
        InternalErrorServer: 500
    }
})();

/**
* Contains operations with form validators.
* Every validator should contain formId which is id of form, object to validate
* Optional validator can contain method beforeSubmit which can be used for specific use case, can also contain method validate which is custom validation of validator and is used before submit
* Stucture of validator { controlId: 'idForm', objectToValidate: { ... } }
* Stucture of validator with method beforeSubmit and validate { controlId: 'idForm', objectToValidate: { ... }, beforeSubmit: function() { ... }, validate: function() { ... } }
* Object to validate should contain fields: controlId - id name of html element, rules - array of function to validate, valid - define if field is valid, value - value of field, optional onChange - method invoked insted of default onChange method for specific resons
* Example of object stucture { controlId: 'idObject', rules: [ v => v || 'Value is required' ] , valid: false, value: '' }
* Stucture with optional method onChange { controlId: 'idObject', rules: [ v => v || 'Value is required' ] , valid: false, value: '', onChange: function(event) { ... } }
*
*/
const forms = (function () {
    /**
    * Iterate Through object and set rules listener which can handle operations
    * @param obj Object to invoke.
    *
    */
    function iterateThroughObjectAndSetRulesListeners(obj) {
        iterateThroughObjectAndRunCallbackOnRules(obj, setRulesListeners);
    }

    /**
    * Set Rules Listeners onchange event depends on controlId, controlName
    * @param field Html Header of modal.
    *
    */
    function setRulesListeners(field) {
        document.addEventListener("change", function (event) {
            if (event.target.id === field.controlId || event.target.name === field.controlName) {
                if (typeof field.onChange === 'function') {
                    field.onChange(event.target);
                    return;
                }
                validOnChange(event.target, field);
            }
        });
    }

    /**
    * Invoked when field is changed
    * @param context Context value for example html element.
    * @param field Field of validator.
    *
    */
    function validOnChange(context, field) {
        field.value = context.value;
        for (const rule of field.rules) {
            const message = rule(context.value);
            if (message && message.length > 0) {
                field.valid = false;
                showError(context, message);
                return;
            }
        }

        field.valid = true;
        $(context).siblings('span').text('');
    }

    /**
    * Creates Error span html
    * @param text Text inside error span.
    *
    */
    function createErrorSpanInner(text) {
        const span = document.createElement('span');
        span.className = "text-danger field-validation-invalid";
        span.textContent = text;
        return span;
    }

    /**
    * Shows error siblings to closest context html
    * @param context Html element.
    * @param text Text of error.
    *
    */
    function showError(context, text) {
        const siblingSpan = $(context).siblings('span');
        if (siblingSpan[0]) {
            siblingSpan.text(text);
        } else {
            $('#' + context.attributes.id.value)[0].parentElement.appendChild(createErrorSpanInner(text));
        }        
    }

    /**
    * Validates form
    * @param validator Form Validator to validate.
    *
    */
    function validateForm(validator) {
        if (typeof validator.validate === 'function') {
            return validator.validate();
        }
        return validAllFields(validator);
    }

    /**
    * Validates all validators fields
    * @param obj Validator.
    *
    */
    function validAllFields(obj) {
        iterateThroughObjectAndRunCallbackOnRules(obj, validField);
        try {
            iterateThroughObjectAndRunCallbackOnRules(obj, throwIfFieldIsInvalidAndScrollToControl);
            return true;
        } catch {
            return false;
        }
    }

    /**
    * Method throws error if validation fails and scrolls to closest error
    * @param field Invalid validator's Field.
    *
    */
    function throwIfFieldIsInvalidAndScrollToControl(field) {
        if (!field.valid) {
            $('#' + field.controlId)[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
            throw new Error('Field ' + field.controlId + ' has invalid field');
        }
    }

    /**
    * Method validates field
    * @param field Field of validator.
    *
    */
    function validField(field) {
        for (const rule of field.rules) {
            const val = rule(field.value);
            if (val && val.length > 0) {
                field.valid = false;
                forms.showValidationError(field.controlId, val);
                return false;
            }
        }
        return true;
    }

    /**
    * Clear all validation messages for all fields
    * @param obj Validator.
    *
    */
    function clearValidationMessagesForAllFields(obj) {
        iterateThroughObjectAndRunCallbackOnRules(obj, clearValidationMessageForField);
    }

    /**
    * Clears validation messages for specific field
    * @param field Field of validator.
    *
    */
    function clearValidationMessageForField(field) {
        field.valid = true;
        forms.showValidationError(field.controlId, '');
    }

    /**
    * Iterate through object and run callback if object contains rules field
    * @param obj Validator.
    * @param callback method to invoke with field argument.
    *
    */
    function iterateThroughObjectAndRunCallbackOnRules(obj, callback) {
        for (const field in obj) {
            if (typeof obj[field] !== 'object' || obj[field] === null) {
                continue;
            }

            if (Array.isArray(obj[field].rules) && (typeof obj[field].controlId === 'string' || obj[field].controlId instanceof String)) {
                callback(obj[field]);
                continue;
            }

            iterateThroughObjectAndRunCallbackOnRules(obj[field], callback);
        }
    }

    return {
        /**
        * Intialize form validator, set rules, actions before submit
        * @param formValidator Validator.
        *
        */
        initFormValidator: function (formValidator) {
            if (typeof formValidator !== 'object' || formValidator === null) {
                return;
            }
            iterateThroughObjectAndSetRulesListeners(formValidator);

            $('#' + formValidator.formId).submit(function (event) {
                event.preventDefault();
                clearValidationMessagesForAllFields(formValidator);
                if (!validateForm(formValidator)) {
                    return;
                }
                if (typeof formValidator.beforeSubmit === 'function') {
                    formValidator.beforeSubmit();
                }
                $(this).unbind('submit').submit(); // continue the submit unbind preventDefault
            });
        },
        /**
        * Shows validations errors depends on controlId
        * @param controlId Control id.
        * @param text Text to show.
        *
        */
        showValidationError: function (controlId, text) {
            if (!controlId) {
                return;
            }
            const siblingSpan = $('#' + controlId).siblings('span');
            if (siblingSpan[0]) {
                siblingSpan.text(text);
            } else {
                $('#' + controlId)[0].parentElement.appendChild(forms.createErrorSpan(text));
            }
        },
        /**
        * Creates html error span
        * @param text Text of error.
        *
        */
        createErrorSpan: function (text) {
            return createErrorSpanInner(text);
        },
        /**
        * Validate control
        * @param controlField Validator's field.
        *
        */
        validControl: function(controlField) {
            validField(controlField);
        },
        /**
        * Clear Validation error
        * @param controlId id of control (html id attribute).
        *
        */
        clearValidationError(controlId) {
            forms.showValidationError(controlId, '');
        }
    }
})();

// enable tooltips
$(document).ready(function () {
    $('[data-toggle="tooltip"]').tooltip();
});

const emailRegex = /(?:(?:\r\n)?[ \t])*(?:(?:(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[\t]))*"(?:(?:\r\n)?[ \t])*))*@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*|(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)*\<(?:(?:\r\n)?[ \t])*(?:@(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[\t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*(?:,@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[\t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*)*:(?:(?:\r\n)?[ \t])*)?(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*))*@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*\>(?:(?:\r\n)?[ \t])*)|(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)*:(?:(?:\r\n)?[ \t])*(?:(?:(?:[^()<>@,;:\\".\[\]\000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*))*@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*|(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)*\<(?:(?:\r\n)?[ \t])*(?:@(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*(?:,@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\]\000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*)*:(?:(?:\r\n)?[ \t])*)?(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*))*@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*\>(?:(?:\r\n)?[ \t])*)(?:,\s*(?:(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*))*@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*|(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)*\<(?:(?:\r\n)?[ \t])*(?:@(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*(?:,@(?:(?:\r\n)?[\t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*)*:(?:(?:\r\n)?[ \t])*)?(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|"(?:[^\"\r\\]|\\.|(?:(?:\r\n)?[ \t]))*"(?:(?:\r\n)?[ \t])*))*@(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*)(?:\.(?:(?:\r\n)?[ \t])*(?:[^()<>@,;:\\".\[\] \000-\031]+(?:(?:(?:\r\n)?[ \t])+|\Z|(?=[\["()<>@,;:\\".\[\]]))|\[([^\[\]\r\\]|\\.)*\](?:(?:\r\n)?[ \t])*))*\>(?:(?:\r\n)?[ \t])*))*)?;\s*)/;
const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\x21-\x2F\x3A-\x40\x5B-\x60\x7B-\x7E])[A-Za-z\d\x21-\x2F\x3A-\x40\x5B-\x60\x7B-\x7E]{8,}$/;

const maxCountImages = 5;
const allowedExtensions = ['.jpg', '.png'];
