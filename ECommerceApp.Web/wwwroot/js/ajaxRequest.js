define([],
    function () {
        "use strict";

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
        return {
            ajaxRequest
        }
    }
);