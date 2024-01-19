using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.DTO
{
    public class ItemDetailsDto : ItemDto, IMapFrom<Item>
    {
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
        public IEnumerable<ImageDto> Images { get; set; }

        public override void Mapping(Profile profile)
        {
            profile.CreateMap<Item, ItemDetailsDto>()
                .ForMember(i => i.Tags, src => src.MapFrom(i => i.ItemTags.Select(it => it.Tag).ToList()))
                .ForMember(i => i.Images, src => src.Ignore());
        }
    }
}
