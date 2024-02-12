using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class OrderItemForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.OrderItem>
    {
        public int ItemId { get; set; }
        public int ItemOrderQuantity { get; set; }
        public string UserId { get; set; }
        public int? OrderId { get; set; }
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; }
        public string ItemName { get; set; }
        public int ItemQuantityAvailable { get; set; }
        public string ItemBrand { get; set; }
        public string ItemType { get; set; }
        public decimal ItemCost { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<OrderItemForListVm, ECommerceApp.Domain.Model.OrderItem>().ReverseMap()
                .ForMember(i => i.ItemName, opt => opt.MapFrom(i => i.Item.Name))
                .ForMember(i => i.ItemQuantityAvailable, opt => opt.MapFrom(i => i.Item.Quantity))
                .ForMember(i => i.ItemBrand, opt => opt.MapFrom(i => i.Item.Brand.Name))
                .ForMember(i => i.ItemType, opt => opt.MapFrom(i => i.Item.Type.Name))
                .ForMember(i => i.ItemCost, opt => opt.MapFrom(i => i.Item.Cost));
        }
    }
}
