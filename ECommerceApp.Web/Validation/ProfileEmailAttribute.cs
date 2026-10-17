using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ECommerceApp.Domain.AccountProfile.ValueObjects;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ECommerceApp.Web.Validation
{
    /// <summary>
    /// Validates an e-mail address with the same rule the AccountProfile <see cref="Email"/> value object enforces,
    /// so an address accepted by a form can always be turned into a profile e-mail.
    /// Use <c>[Required]</c> alongside it to control the message for a missing value.
    /// Client side: emits <c>data-val-profileemail</c>, handled by wwwroot/js/profile-email-validation.js.
    /// </summary>
    // DOC-SYNC: the format rule lives in ECommerceApp.Domain/AccountProfile/ValueObjects/Email.cs and is mirrored in
    // ECommerceApp.Web/wwwroot/js/profile-email-validation.js. Change all three together.
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field | System.AttributeTargets.Parameter)]
    public sealed class ProfileEmailAttribute : ValidationAttribute, IClientModelValidator
    {
        public ProfileEmailAttribute()
            : base("The {0} field is not a valid e-mail address.")
        {
        }

        public override bool IsValid(object value)
        {
            return value is string text && Email.IsValid(text);
        }

        public void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-profileemail", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        }

        private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
        {
            if (!attributes.ContainsKey(key))
            {
                attributes.Add(key, value);
            }
        }
    }
}
