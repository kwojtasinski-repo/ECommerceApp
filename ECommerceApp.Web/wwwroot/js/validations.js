define([],
    function () {
        "use strict";

        return {
            emailRegex: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
            passwordRegex: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\x21-\x2F\x3A-\x40\x5B-\x60\x7B-\x7E])[A-Za-z\d\x21-\x2F\x3A-\x40\x5B-\x60\x7B-\x7E]{8,}$/,
            maxCountImages: 5,
            allowedExtensions: ['.jpg', '.png']
        };
    }
);
