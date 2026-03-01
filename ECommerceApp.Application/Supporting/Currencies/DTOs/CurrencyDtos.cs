using ECommerceApp.Domain.Supporting.Currencies.ValueObjects;
using FluentValidation;

namespace ECommerceApp.Application.Supporting.Currencies.DTOs
{
    public record CreateCurrencyDto(string Code, string Description);

    public class CreateCurrencyDtoValidator : AbstractValidator<CreateCurrencyDto>
    {
        public CreateCurrencyDtoValidator()
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotNull().NotEmpty()
                .Length(3)
                .Must(c => CurrencyCode.IsKnownIso4217Code(c))
                .WithMessage("Currency code must be a valid ISO 4217 code (e.g. EUR, USD, PLN).");
            RuleFor(x => x.Description).NotNull().NotEmpty().MinimumLength(3).MaximumLength(300);
        }
    }

    public record UpdateCurrencyDto(int Id, string Code, string Description);

    public class UpdateCurrencyDtoValidator : AbstractValidator<UpdateCurrencyDto>
    {
        public UpdateCurrencyDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotNull().NotEmpty()
                .Length(3)
                .Must(c => CurrencyCode.IsKnownIso4217Code(c))
                .WithMessage("Currency code must be a valid ISO 4217 code (e.g. EUR, USD, PLN).");
            RuleFor(x => x.Description).NotNull().NotEmpty().MinimumLength(3).MaximumLength(300);
        }
    }
}
