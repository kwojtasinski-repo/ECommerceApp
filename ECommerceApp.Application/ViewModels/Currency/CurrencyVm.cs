using ECommerceApp.Application.DTO;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Currency
{
    public class CurrencyVm
    {
        public CurrencyDto Currency { get; set; }
    }

    public class CurrencyVmValidator : AbstractValidator<CurrencyVm>
    {
        public CurrencyVmValidator()
        {
            RuleFor(c => c.Currency).SetValidator(new CurrencyDtoValidator());
        }
    }
}
