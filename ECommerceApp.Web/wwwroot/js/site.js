// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
const modalService = (function () {
    // private variables, functions
    const modalIdentifyClass = "modalClass";

    function createModalTemplate(headerText, bodyText) {
        const modalDiv = document.createElement("div");
        modalDiv.className = "modal fade " + modalIdentifyClass;
        modalDiv.tabIndex = "-1";
        modalDiv.role = "dialog";
        modalDiv.appendChild(createDialog(headerText, bodyText));
        return modalDiv;
    }

    function createDialog(headerText, bodyText) {
        const modalDialog = document.createElement("div");
        modalDialog.className = "modal-dialog modal-dialog-centered";
        modalDialog.role = "document";
        modalDialog.appendChild(createModalContent(headerText, bodyText));
        return modalDialog;
    }

    function createModalContent(headerText, bodyText) {
        const modalContent = document.createElement("div");
        modalContent.className = "modal-content";

        modalContent.appendChild(createModalHeader(headerText));
        modalContent.appendChild(createModalBody(bodyText));
        modalContent.appendChild(createModalFooter());

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

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "close";
        closeButton.setAttribute("data-dismiss", "modal");
        closeButton.setAttribute("aria-label", "Close");
        closeButton.onclick = closeModal;

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

    function createModalFooter() {
        const modalFooter = document.createElement("div");
        modalFooter.className = "modal-footer";
        const closeButton = document.createElement("button");
        closeButton.className = "btn btn-secondary";
        closeButton.type = "button";
        closeButton.setAttribute("data-dismiss", "modal");
        closeButton.textContent = "Close";
        closeButton.onclick = closeModal;
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
        setTimeout(() => $('.' + modalIdentifyClass).remove(), 150);
    }

    // public variables functions
    return {
        show: function (header, body) {
            const modalTemplate = createModalTemplate(header, body);
            document.body.appendChild(modalTemplate);
            $('.' + modalIdentifyClass).modal('show');
        },
        hide: function () {
            closeModal();
        }
    }
})();
