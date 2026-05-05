using FluentValidation;
using System;

namespace ECommerceApp.Application.DTO
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }
        public decimal Cost { get; set; }
        public string State { get; set; }
    }

    public class PaymentDtoValidation : AbstractValidator<PaymentDto>
    {
        public PaymentDtoValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull().NotEmpty();
            RuleFor(x => x.DateOfOrderPayment).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}