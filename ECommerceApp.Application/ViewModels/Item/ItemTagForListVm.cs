using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemTagForListVm : IMapFrom<ECommerceApp.Domain.Model.ItemTag>
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public ItemDetailsVm Item { get; set; }
        public int TagId { get; set; }
        public string TagName { get; set; }
        public TagDto Tag { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ItemTagForListVm, ECommerceApp.Domain.Model.ItemTag>().ReverseMap()
                .ForMember(i => i.ItemName, opt => opt.MapFrom(m => m.Item.Name))
                .ForMember(i => i.TagName, opt => opt.MapFrom(m => m.Tag.Name));
        }
    }
}
