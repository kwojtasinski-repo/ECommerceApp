using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Options;
using FluentValidation;

namespace ECommerceApp.Application.Presale.Checkout.Validators
{
    public sealed class AddToCartDtoValidator : AbstractValidator<AddToCartDto>
    {
        public AddToCartDtoValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be positive.")
                .LessThanOrEqualTo(CheckoutOptions.MaxWebQuantityPerOrderLine)
                .WithMessage($"Quantity cannot exceed {CheckoutOptions.MaxWebQuantityPerOrderLine} per order line.");
        }
    }
}
