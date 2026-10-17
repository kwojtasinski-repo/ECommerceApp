// Client-side counterpart of ECommerceApp.Web/Validation/ProfileEmailAttribute.cs (data-val-profileemail).
// DOC-SYNC: the pattern below must stay identical to the one in
// ECommerceApp.Domain/AccountProfile/ValueObjects/Email.cs (^[^@\s]+@[^@\s]+\.[^@\s]+$, case-insensitive).
// Requires jquery.validate and jquery.validate.unobtrusive to be loaded first.
(function ($) {
    "use strict";

    if (!$ || !$.validator || !$.validator.unobtrusive) {
        return;
    }

    var profileEmailPattern = /^[^@\s]+@[^@\s]+\.[^@\s]+$/i;

    // Empty values are left to [Required], like the server-side attribute's companion usage.
    $.validator.addMethod("profileemail", function (value, element) {
        return this.optional(element) || profileEmailPattern.test(value);
    });

    $.validator.unobtrusive.adapters.addBool("profileemail");
}(window.jQuery));
