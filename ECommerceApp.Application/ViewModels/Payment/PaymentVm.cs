using FluentValidation;
using System;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class PaymentVm : BaseVm
    {
        public string Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public int CurrencyId { get; set; }
        public string CustomerName { get; set; }
        public decimal Cost { get; set; }
        public string CurrencyName { get; set; }
        public Domain.Model.PaymentState State { get; set; }

        private static DateTime SetFormat(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
        }
    }

    public class NewPaymentValidation : AbstractValidator<PaymentVm>
    {
        public NewPaymentValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull().NotEmpty();
            RuleFor(x => x.DateOfOrderPayment).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
            RuleFor(x => x.Cost).GreaterThan(0);
        }
    }
}