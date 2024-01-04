using AutoMapper;
using ECommerceApp.Application.Mapping;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace ECommerceApp.Application.DTO
{
    public class OrderDto : IMapFrom<Domain.Model.Order>
    {
        public int Id { get; set; }
        public int Number { get; set; }
        [Column(TypeName = "decimal(18,2)")]
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
        public int CurrencyId { get; set; }

        public List<OrderItemDto> OrderItems { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<OrderDto, ECommerceApp.Domain.Model.Order>().ReverseMap();
        }
    }
}
