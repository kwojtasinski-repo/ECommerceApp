using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class ItemTagVm : IMapFrom<Domain.Model.ItemTag>
    {
        public int TagId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ItemTagVm, Domain.Model.ItemTag>().ReverseMap()
                .ForMember(t => t.TagId, map => map.MapFrom(src => src.TagId));
        }
    }
}