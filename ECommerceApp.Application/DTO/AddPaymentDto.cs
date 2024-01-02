using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class AddPaymentDto
    {
        public int OrderId { get; set; }
        public int CurrencyId { get; set; }
    }

    public class AddPaymentDtoValidator : AbstractValidator<AddPaymentDto>
    {
        public AddPaymentDtoValidator()
        {
            RuleFor(x => x.CurrencyId).NotNull().GreaterThan(0);
            RuleFor(x => x.OrderId).NotNull().GreaterThan(0);
        }
    }
}
