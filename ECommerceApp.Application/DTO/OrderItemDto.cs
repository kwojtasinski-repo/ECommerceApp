using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class OrderItemDto : IMapFrom<OrderItemDto>
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal ItemCost { get; set; }
        public int ItemOrderQuantity { get; set; }
        public string UserId { get; set; }
        public int? OrderId { get; set; }
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Model.OrderItem, OrderItemDto>()
                .ForMember(oi => oi.Id, opt => opt.MapFrom(oi => oi.Id))
                .ForMember(oi => oi.ItemId, opt => opt.MapFrom(oi => oi.ItemId))
                .ForMember(oi => oi.ItemName, opt => opt.MapFrom(oi => oi.Item.Name))
                .ForMember(oi => oi.ItemCost, opt => opt.MapFrom(oi => oi.Item.Cost))
                .ForMember(oi => oi.ItemOrderQuantity, opt => opt.MapFrom(oi => oi.ItemOrderQuantity))
                .ForMember(oi => oi.UserId, opt => opt.MapFrom(oi => oi.UserId))
                .ForMember(oi => oi.OrderId, opt => opt.MapFrom(oi => oi.OrderId))
                .ForMember(oi => oi.CouponUsedId, opt => opt.MapFrom(oi => oi.CouponUsedId))
                .ForMember(oi => oi.RefundId, opt => opt.MapFrom(oi => oi.RefundId))
                .ReverseMap()
                .ForMember(oi => oi.Item, opt => opt.Ignore());
        }

        public class OrderItemDtoValidation : AbstractValidator<OrderItemDto>
        {
            public OrderItemDtoValidation()
            {
                RuleFor(x => x.Id).NotNull();
                RuleFor(x => x.ItemId).NotNull().GreaterThan(0);
                RuleFor(x => x.ItemOrderQuantity).NotNull().GreaterThan(0);
                RuleFor(x => x.UserId).NotNull().NotEmpty();
            }
        }
    }
}
