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
            let _modalInstance = null;

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
             * @param closeHandler Handler invoked when the header close button is clicked
             *
             */
            function createModalHeader(title, closeHandler) {
                const titleText = document.createElement("h5");
                titleText.className = "modal-title";
                titleText.textContent = title ? title : "";
                const closeButton = buttonTemplate.createButton(undefined, "btn-close", closeHandler, "button", [{ key: "aria-label", value: "Close" }]);
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
             * @param defaultCloseHandler Handler for the default Close button (used when no custom buttons provided).
             *
             */
            function createModalFooter(buttons, defaultCloseHandler) {
                const modalFooter = dialogTemplate.createDialogFooter();

                if (buttons) {
                    for (const button of buttons) {
                        modalFooter.appendChild(button);
                    }
                    return modalFooter;
                }

                const closeButton = buttonTemplate.createButton("Close", "btn btn-secondary", defaultCloseHandler, "button");
                modalFooter.appendChild(closeButton);

                return modalFooter;
            }

            /**
             * Shows a modal element using Bootstrap 5 Modal class
             * @param el The modal DOM element.
             *
             */
            function showModal(el) {
                _modalInstance = new bootstrap.Modal(el, { backdrop: 'static', keyboard: false });
                _modalInstance.show();
            }

            /**
             * Close modal
             *
             */
            function closeModal() {
                const el = document.querySelector('.' + modalIdentifyClass);
                if (!el) return;

                _modalInstance?.hide();
                el.addEventListener('hidden.bs.modal', function () { el.remove(); }, { once: true });
                _modalInstance = null;
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
             * Handler for closing info modals — only closes the modal, does not invoke any action
             *
             */
            function closeOnlyHandler() {
                closeModal();
            }

            /**
             * Handler for closing confirmation modal deny button — closes modal and invokes denyAction
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
                    const headerTemplate = createModalHeader(headerText, closeOnlyHandler);
                    const bodyTemplate = createModalBody(bodyText);
                    const footerTemplate = createModalFooter(undefined, closeOnlyHandler);
                    const modalTemplate = createModalTemplate(headerTemplate, bodyTemplate, footerTemplate);
                    document.body.appendChild(modalTemplate);
                    showModal(modalTemplate);
                },
                /**
                 * Shows confirmation modal
                 * @param headerText Title of modal.
                 * @param bodyText Text inside of body modal.
                 *
                 */
                showConfirmationModal: function (headerText, bodyText) {
                    const headerTemplate = createModalHeader(headerText, closeButtonHandler);
                    const bodyTemplate = createModalBody(bodyText);
                    const confirmButton = buttonTemplate.createButton("Tak", "btn btn-danger", function () { closeModal(); invokeActionAfterButtonClick(confirmAction); }, "button");
                    const cancelButton = buttonTemplate.createButton("Nie", "btn btn-secondary", closeButtonHandler, "button");
                    const footerTemplate = createModalFooter([confirmButton, cancelButton]);
                    const modalTemplate = createModalTemplate(headerTemplate, bodyTemplate, footerTemplate);
                    document.body.appendChild(modalTemplate);
                    showModal(modalTemplate);
                    return new Promise(function (resolve, _) {
                        actions.push({ actionName: confirmAction, func: function () { resolve(true); } });
                        actions.push({ actionName: denyAction, func: function () { resolve(false); } });
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
                    showModal(modalTemplate);
                },

                /**
                 * Method closes modal
                 *
                 */
                close: function () {
                    closeModal();
                },
                createHeader: function (title) {
                    return createModalHeader(title, closeOnlyHandler);
                }
            }
        })();
        return {
            modalService
        };
    }
);
