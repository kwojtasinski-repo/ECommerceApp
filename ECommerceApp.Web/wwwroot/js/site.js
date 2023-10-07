﻿const buttonTemplate = (function () {
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
* Creates button template
* @param index Page to choose in form.
* @param id Form id.
* @param pageNo The page number element identifier - default pageNo.
*
*/
function PagerClick(index, id, pageNo) {
    document.getElementById(pageNo ? pageNo : "pageNo").value = index;
    debugger
    var form = $('#'+id)[0];
    form.submit();
}
