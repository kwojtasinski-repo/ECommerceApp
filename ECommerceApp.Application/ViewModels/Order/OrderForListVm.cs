using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.OrderItem;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public string Number { get; set; }
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public string UserId { get; set; }
        public int? CouponUsedId { get; set; }
        public int CustomerId { get; set; }
        public int? PaymentId { get; set; }
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }

        public ICollection<OrderItemForListVm> OrderItems { get; set; }

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
}
