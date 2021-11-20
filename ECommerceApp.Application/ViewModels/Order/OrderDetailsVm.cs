using AutoMapper;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.OrderItem;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public int Number { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public int CustomerId { get; set; }
        public string UserId { get; set; }
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund
        public int CurrencyId { get; set; }

        public List<OrderItemDetailsVm> OrderItems { get; set; } // 1:Many relation

        public void Mapping(Profile profile)
        {
            profile.CreateMap<OrderDetailsVm, ECommerceApp.Domain.Model.Order>().ReverseMap();
        }
    }
}
