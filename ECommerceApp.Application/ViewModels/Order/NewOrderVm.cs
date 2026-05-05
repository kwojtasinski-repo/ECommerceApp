using ECommerceApp.Application.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class NewOrderVm : BaseVm
    {
        public NewOrderVm()
        {
            OrderItems = new List<OrderItemDto>();
            Items = new List<ItemDto>();
        }

        public string Number { get; set; }
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public int CustomerId { get; set; }
        public string UserId { get; set; }
        public int? PaymentId { get; set; }
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; }
        public int CouponId { get; set; }
        public int Discount { get; set; }
        public string PromoCodeUsed { get; set; }
        public string ReasonRefund { get; set; }
        public bool AcceptedRefund { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public bool ChangedCode { get; set; }
        public bool ChangedRefund { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }


        public List<OrderItemDto> OrderItems { get; set; } // 1:Many relation
        public List<ItemDto> Items { get; set; }
        public CustomerDetailsDto NewCustomer { get; set; }
        public bool CustomerData { get; set; }
        public string CustomerInformation { get; set; }
        public string PaymentNumber { get; set; }
        public string PromoCode { get; set; }
    }

    public class NewOrderValidation : AbstractValidator<NewOrderVm>
    {
        public NewOrderValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull();
            RuleFor(x => x.Cost).NotNull();
            RuleFor(x => x.Ordered).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
