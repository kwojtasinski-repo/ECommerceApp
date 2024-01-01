using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class CreatePayment : BaseVm
    {
        public int OrderId { get; set; }
        public int CurrencyId { get; set; }

        public class CreatePaymentValidation : AbstractValidator<CreatePayment>
        {
            public CreatePaymentValidation()
            {
                RuleFor(x => x.Id).NotNull();
            }
        }
    }
}
