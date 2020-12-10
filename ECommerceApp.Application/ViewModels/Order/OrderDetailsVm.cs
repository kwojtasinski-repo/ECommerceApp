using AutoMapper;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderDetailsVm : IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public int Id { get; set; }
        public int Number { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public virtual CouponUsedDetailsVm CouponUsed { get; set; } // 1:1 Order CouponUsed on one order can be discount
        public int CustomerId { get; set; }
        public CustomerDetailsVm Customer { get; set; }
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public virtual PaymentDetailsVm Payment { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund
        public virtual RefundDetailsVm Refund { get; set; }

        public ICollection<OrderItemDetailsVm> OrderItems { get; set; } // 1:Many relation

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Order, OrderDetailsVm>().ReverseMap();
        }
    }
}
