define([],
    function () {
        "use strict";

        /**
        * Operations with Promise Ajax Request
        *
        */
        const ajaxRequest = (function () {
            function asyncAjax(url, type, data, contentType, dataType) {
                const isFormData = data instanceof FormData;
                return new Promise(function (resolve, reject) {
                    $.ajax({
                        url,
                        type: type ? type : 'GET',
                        data: data,
                        contentType: isFormData ? false : contentType,
                        dataType: dataType,
                        processData: !isFormData,
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
                },

                /**
                 * Extracts error codes from a rejected jqXHR object.
                 * Handles both response shapes:
                 *   - Web AJAX path: BadRequest returns a plain array  [ { code, parameters }, ... ]
                 *   - API path: ExceptionMiddleware wraps in { codes: [...], ... }
                 * Returns an empty array when no codes are present.
                 * @param {Object} error The jqXHR object from a rejected ajaxRequest.send() call.
                 * @returns {Array} Raw error code objects.
                 */
                getErrorCodes: function (error) {
                    const json = error && error.responseJSON;
                    if (!json) {
                        return [];
                    }
                    if (Array.isArray(json)) {
                        return json;
                    }
                    if (Array.isArray(json.codes)) {
                        return json.codes;
                    }
                    return [];
                }
            }
        })();
        return {
            ajaxRequest
        }
    }
);