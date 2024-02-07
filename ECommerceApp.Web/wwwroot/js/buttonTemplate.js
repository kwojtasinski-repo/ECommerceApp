define([],
    function () {
        "use strict";

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

        return {
            buttonTemplate
        }
    }
);
