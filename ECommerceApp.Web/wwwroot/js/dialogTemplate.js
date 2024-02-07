define([],
    function () {
        "use strict";

        /**
        * Contains operation with creating dialogs
        *
        */
        const dialogTemplate = (function () {
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
        return {
            dialogTemplate
        }
    }
);
