using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Item;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Tag
{
    public class TagDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Tag>
    {
        public string Name { get; set; }

        public List<ItemVm> ItemTags { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Tag, TagDetailsVm>()
                .ForMember(t => t.Id, map => map.MapFrom(src => src.Id))
                .ForMember(t => t.Name, map => map.MapFrom(src => src.Name))
                .ForMember(t => t.ItemTags, map => map.MapFrom(src => src.ItemTags));
        }
    }

    public class TagDetailsValidation : AbstractValidator<TagDetailsVm>
    {
        public TagDetailsValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
