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

const modalService = (function () {
    // private variables, functions
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
        const modalDialog = document.createElement("div");
        modalDialog.className = "modal-dialog modal-dialog-centered";
        modalDialog.role = "document";
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
        const modalHeader = document.createElement("div");
        modalHeader.className = "modal-header";

        if (title) {
            const titleText = document.createElement("h5");
            titleText.className = "modal-title";
            titleText.textContent = title;
            modalHeader.appendChild(titleText);
        }

        const closeButton = buttonTemplate.createButton(undefined, "close", closeModal, "button", [{ key: "data-dismiss", value: "modal" }, { key: "aria-label", value: "Close" }]);

        const spanCloseButton = document.createElement("span");
        spanCloseButton.setAttribute("aria-hidden", "true");
        spanCloseButton.innerHTML = "&times;";

        closeButton.appendChild(spanCloseButton);
        modalHeader.appendChild(closeButton);

        return modalHeader;
    }

    function createModalBody(bodyText) {
        const modalBody = document.createElement("div");
        modalBody.className = "modal-body";

        const textElement = document.createElement("p");
        textElement.textContent = bodyText;
        modalBody.appendChild(textElement);

        return modalBody;
    }

    function createModalFooter(buttons) {
        const modalFooter = document.createElement("div");
        modalFooter.className = "modal-footer";

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
            debugger
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
        close: function () {
            closeModal();
        }
    }
})();

const ajaxRequest = (function () {
    function asyncAjax(url, type, data, contentType, dataType) {
        return new Promise(function (resolve, reject) {
            debugger
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
            debugger
            return asyncAjax(url, type, data, contentType, dataType);
        }
    }
})();
