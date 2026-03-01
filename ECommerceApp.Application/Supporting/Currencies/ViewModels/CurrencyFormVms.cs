using FluentValidation;

namespace ECommerceApp.Application.Supporting.Currencies.ViewModels
{
    public class CreateCurrencyFormVm
    {
        public string Code { get; set; }
        public string Description { get; set; }
    }

    public class CreateCurrencyFormVmValidator : AbstractValidator<CreateCurrencyFormVm>
    {
        public CreateCurrencyFormVmValidator()
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotNull().NotEmpty()
                .Length(3)
                .Matches(@"^[A-Z]{3}$")
                .WithMessage("Currency code must be exactly 3 uppercase letters (e.g. EUR, USD, PLN).");
            RuleFor(x => x.Description)
                .NotNull().NotEmpty()
                .MinimumLength(3).MaximumLength(300);
        }
    }

    public class UpdateCurrencyFormVm
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
    }

    public class UpdateCurrencyFormVmValidator : AbstractValidator<UpdateCurrencyFormVm>
    {
        public UpdateCurrencyFormVmValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotNull().NotEmpty()
                .Length(3)
                .Matches(@"^[A-Z]{3}$")
                .WithMessage("Currency code must be exactly 3 uppercase letters (e.g. EUR, USD, PLN).");
            RuleFor(x => x.Description)
                .NotNull().NotEmpty()
                .MinimumLength(3).MaximumLength(300);
        }
    }
}
