define(['dialogTemplate', 'buttonTemplate'],
    function (templateDialog, templateButton) {
        "use strict";

        const dialogTemplate = templateDialog.dialogTemplate;
        const buttonTemplate = templateButton.buttonTemplate;
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
                const modalDialog = dialogTemplate.createDialog();
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
                return dialogTemplate.createDialogHeader([titleText, closeButton]);
            }

            /**
             * Creates modal body html
             * @param bodyText Text inside modal body.
             *
             */
            function createModalBody(bodyText) {
                const textElement = document.createElement("p");
                textElement.textContent = bodyText;
                return dialogTemplate.createDialogBody(textElement);
            }

            /**
             * Creates modal footer html
             * @param buttons Html buttons used in footer modal.
             *
             */
            function createModalFooter(buttons) {
                const modalFooter = dialogTemplate.createDialogFooter();

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
                    const confirmButton = buttonTemplate.createButton("Tak", "btn btn-danger", () => { closeModal(); invokeActionAfterButtonClick(confirmAction); }, "type");
                    const cancelButton = buttonTemplate.createButton("Nie", "btn btn-secondary", this.close, "type");
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
                },
                createHeader: function (title) {
                    return createModalHeader(title);
                }
            }
        })();
        return {
            modalService
        };
    }
);
