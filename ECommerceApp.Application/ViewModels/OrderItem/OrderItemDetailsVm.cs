using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class OrderItemDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.OrderItem>
    {
        public int ItemId { get; set; }   // 1:Many Item OrderItem  
        public int ItemOrderQuantity { get; set; }
        public string UserId { get; set; }
        public int? OrderId { get; set; }  // Many : 1 OrderItem Order
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; } // 1:Many Refund OrderItem
        public string ItemName { get; set; }
        public decimal ItemCost { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.OrderItem, OrderItemDetailsVm>()
                .ForMember(i => i.ItemName, opt => opt.MapFrom(o => o.Item.Name))
                .ForMember(i => i.ItemCost, opt => opt.MapFrom(o => o.Item.Cost));
        }
    }
}
