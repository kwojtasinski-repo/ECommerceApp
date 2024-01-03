using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.OrderItem;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public int Number { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public string UserId { get; set; }
        public int? CouponUsedId { get; set; }
        public int CustomerId { get; set; }
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }

        public ICollection<OrderItemForListVm> OrderItems { get; set; } // 1:Many relation

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Order, OrderForListVm>()
                .ForMember(oi => oi.OrderItems, opt => opt.MapFrom(i => i.OrderItems))
                .ForMember(o => o.CurrencyCode, opt => opt.MapFrom(or => or.Currency.Code));
        }

        public NewOrderVm MapToNewOrderVm()
        {
            var order = new NewOrderVm()
            {
                Id = this.Id,
                Number = this.Number,
                Cost = this.Cost,
                Ordered = this.Ordered,
                Delivered = this.Delivered,
                IsDelivered = this.IsDelivered,
                UserId = this.UserId,
                CouponUsedId = this.CouponUsedId,
                CustomerId = this.CustomerId,
                PaymentId = this.PaymentId,
                IsPaid = this.IsPaid,
                RefundId = this.RefundId,
                OrderItems = new List<OrderItemDto>()
            };

            if (OrderItems != null && OrderItems.Count > 0)
            {
                var orderItems = new List<OrderItemDto>();
                foreach(var orderItem in OrderItems)
                {
                    var item = new OrderItemDto
                    {
                        Id = orderItem.Id
                    };
                    orderItems.Add(item);
                }

                order.OrderItems = orderItems;
            }

            return order;
        }
    }

    public class OrderForListValidation : AbstractValidator<OrderForListVm>
    {
        public OrderForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull();
            RuleFor(x => x.Cost).NotNull();
            RuleFor(x => x.Ordered).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
