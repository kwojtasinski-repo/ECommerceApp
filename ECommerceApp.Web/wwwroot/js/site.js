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

const dialogTeplate = (function () {
    return {
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

const modalService = (function () {
    const modalIdentifyClass = "modalClass";

    function createModalTemplate(headerTemplate, bodyTemplate, footerTemplate) {
        const modalDiv = document.createElement("div");
        modalDiv.className = "modal fade " + modalIdentifyClass;
        modalDiv.tabIndex = "-1";
        modalDiv.role = "dialog";
        modalDiv.appendChild(createDialog(headerTemplate, bodyTemplate, footerTemplate));
        return modalDiv;
    }

    function createDialog(headerTemplate, bodyTemplate, footerTemplate) {
        const modalDialog = dialogTeplate.createDialog();
        modalDialog.className += " modal-dialog-centered";
        modalDialog.appendChild(createModalContent(headerTemplate, bodyTemplate, footerTemplate));
        return modalDialog;
    }

    function createModalContent(headerTemplate, bodyTemplate, footerTemplate) {
        const modalContent = document.createElement("div");
        modalContent.className = "modal-content";

        modalContent.appendChild(headerTemplate);
        modalContent.appendChild(bodyTemplate);
        modalContent.appendChild(footerTemplate);

        return modalContent;
    }

    function createModalHeader(title) {
        const titleText = document.createElement("h5");
        titleText.className = "modal-title";
        titleText.textContent = title ? title : "";
        const closeButton = buttonTemplate.createButton(undefined, "close", closeModal, "button", [{ key: "data-dismiss", value: "modal" }, { key: "aria-label", value: "Close" }]);
        const spanCloseButton = document.createElement("span");
        spanCloseButton.setAttribute("aria-hidden", "true");
        spanCloseButton.innerHTML = "&times;";
        closeButton.appendChild(spanCloseButton);
        return dialogTeplate.createDialogHeader([titleText, closeButton]);
    }

    function createModalBody(bodyText) {
        const textElement = document.createElement("p");
        textElement.textContent = bodyText;
        return dialogTeplate.createDialogBody(textElement);
    }

    function createModalFooter(buttons) {
        const modalFooter = dialogTeplate.createDialogFooter();

        if (buttons) {
            for (const button of buttons) {
                modalFooter.appendChild(button);
            }
            return modalFooter;
        }

        const closeButton = buttonTemplate.createButton("Close", "btn btn-secondary", closeModal, "button", [{ key: "data-dismiss", value: "modal" }]);
        modalFooter.appendChild(closeButton);

        return modalFooter;
    }

    function closeModal() {
        const modal = $('.' + modalIdentifyClass);
        if (!modal) {
            return;
        }

        modal.modal('hide');
        // cleanup 150ms for animation
        setTimeout(() => modal.remove(), 150);
    }

    // public variables functions
    return {
        showInformationModal: function (headerText, bodyText) {
            const headerTemplate = createModalHeader(headerText);
            const bodyTemplate = createModalBody(bodyText);
            const footerTemplate = createModalFooter();
            const modalTemplate = createModalTemplate(headerTemplate, bodyTemplate, footerTemplate);
            document.body.appendChild(modalTemplate);
            $('.' + modalIdentifyClass).modal('show');
        },
        showConfirmationModal: function (headerText, bodyText, result) {
            const headerTemplate = createModalHeader(headerText);
            const bodyTemplate = createModalBody(bodyText);
            const confirmButton = buttonTemplate.createButton("Yes", "btn btn-danger", () => { this.close();result(); }, "type");
            const cancelButton = buttonTemplate.createButton("No", "btn btn-secondary", this.close, "type");
            const footerTemplate = createModalFooter([confirmButton, cancelButton]);
            const modalTemplate = createModalTemplate(headerTemplate, bodyTemplate, footerTemplate);
            document.body.appendChild(modalTemplate);
            $('.' + modalIdentifyClass).modal('show');
        },
        showCustomModal: function (header, body, footer) {
            const modalTemplate = createModalTemplate(header, body, footer);
            document.body.appendChild(modalTemplate);
            $('.' + modalIdentifyClass).modal('show');
        },
        close: function () {
            closeModal();
        }
    }
})();

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

const forms = (function () {
    function iterateThroughObjectAndSetRulesListeners(obj) {
        iterateThroughObjectAndRunCallbackOnRules(obj, setRulesListeners);
    }

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

    function createErrorSpanInner(text) {
        const span = document.createElement('span');
        span.className = "text-danger field-validation-valid";
        span.textContent = text;
        return span;
    }

    function showError(context, text) {
        const siblingSpan = $(context).siblings('span');
        if (siblingSpan[0]) {
            siblingSpan.text(text);
        } else {
            $('#' + context.attributes.id.value)[0].parentElement.appendChild(createErrorSpanInner(text));
        }        
    }

    function validateForm(validator) {
        if (typeof validator.validate === 'function') {
            return validator.validate();
        }
        const value = validAllFields(validator);
        return value;
    }

    function validAllFields(obj) {
        return iterateThroughObjectAndRunCallbackOnRules(obj, validField);
    }

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

    function clearValidationMessagesForAllFields(obj) {
        iterateThroughObjectAndRunCallbackOnRules(obj, clearValidationMessageForField);
    }

    function clearValidationMessageForField(field) {
        field.valid = true;
        forms.showValidationError(field.controlId, '');
    }

    function iterateThroughObjectAndRunCallbackOnRules(obj, callback) {
        let value;
        for (const field in obj) {
            if (typeof obj[field] !== 'object' || obj[field] === null) {
                continue;
            }

            if (Array.isArray(obj[field].rules) && (typeof obj[field].controlId === 'string' || obj[field].controlId instanceof String)) {
                value = callback(obj[field]);
                continue;
            }

            value = iterateThroughObjectAndRunCallbackOnRules(obj[field], callback);
        }
        return value;
    }

    return {
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
                if (typeof formValidator.preSubmit === 'function') {
                    formValidator.beforeSubmit();
                }
                $(this).unbind('submit').submit(); // continue the submit unbind preventDefault
            });
        },
        showValidationError: function (controlId, text) {
            const siblingSpan = $('#' + controlId).siblings('span');
            if (siblingSpan[0]) {
                siblingSpan.text(text);
            } else {
                $('#' + controlId)[0].parentElement.appendChild(forms.createErrorSpan(text));
            }
        },
        createErrorSpan: function (text) {
            return createErrorSpanInner(text);
        }
    }
})();

$(document).ready(function () {
    $('[data-toggle="tooltip"]').tooltip();
});
