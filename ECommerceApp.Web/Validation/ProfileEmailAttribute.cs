using System.ComponentModel.DataAnnotations;
using ECommerceApp.Domain.AccountProfile.ValueObjects;

namespace ECommerceApp.Web.Validation
{
    /// <summary>
    /// Validates an e-mail address with the same rule the AccountProfile <see cref="Email"/> value object enforces,
    /// so an address accepted by a form can always be turned into a profile e-mail.
    /// Use <c>[Required]</c> alongside it to control the message for a missing value.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field | System.AttributeTargets.Parameter)]
    public sealed class ProfileEmailAttribute : ValidationAttribute
    {
        public ProfileEmailAttribute()
            : base("The {0} field is not a valid e-mail address.")
        {
        }

        public override bool IsValid(object value)
        {
            return value is string text && Email.IsValid(text);
        }
    }
}
