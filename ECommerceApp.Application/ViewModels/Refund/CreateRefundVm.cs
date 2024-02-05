using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Refund
{
    public class CreateRefundVm : BaseVm
    {
        public string Reason { get; set; }
        public int OrderId { get; set; }

        public RefundVm MapToNewRefund()
        {
            var refund = new RefundVm()
            {
                Id = this.Id,
                Reason = this.Reason,
                OrderId = this.OrderId
            };

            return refund;
        }
    }

    public class CreateRefundVmValidation : AbstractValidator<CreateRefundVm>
    {
        public CreateRefundVmValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Reason).MaximumLength(255);
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}
