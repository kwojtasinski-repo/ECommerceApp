define([],
    function () {
        "use strict";

        /**
        * PagerClick method to submit pagination form with data
        * @param index Page to choose in form.
        * @param id Form id.
        * @param pageNo The page number element identifier - default pageNo.
        *
        */
        function PagerClick(index, id, pageNo) {
            document.getElementById(pageNo ? pageNo : "pageNo").value = index;
            var form = $('#' + id)[0];
            form.submit();
        }

        /**
        * Contains status codes used in requests response 
        *
        */
        const statusCodes = (function () {
            return {
                OK: 200,
                Created: 201,
                BadRequest: 400,
                Unauthorized: 401,
                Forbidden: 403,
                NotFound: 404,
                MethodNotAllowed: 405,
                InternalErrorServer: 500
            }
        })();

        /**
         * Format number to currency local PL
         * @param {number} num
         * @param {number} minimumFractionDigits default 2
         * @param {number} maximumFractionDigits default 2
         * @returns
         */
        const formatPln = (num, minimumFractionDigits = 2, maximumFractionDigits = 2) => num.toLocaleString('pl-PL', {
            minimumFractionDigits, maximumFractionDigits
        });

        return {
            PagerClick,
            statusCodes,
            formatPln
        }
    }
);
