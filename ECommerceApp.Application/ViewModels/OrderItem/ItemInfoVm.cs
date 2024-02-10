using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class ItemInfoVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Item>
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ItemInfoVm, ECommerceApp.Domain.Model.Item>().ReverseMap();
        }
    }
}
