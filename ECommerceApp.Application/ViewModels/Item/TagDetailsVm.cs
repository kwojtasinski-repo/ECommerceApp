using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class TagDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Tag>
    {
        public string Name { get; set; }

        List<ItemsWithTagsVm> ItemTags { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<TagDetailsVm, ECommerceApp.Domain.Model.Tag>().ReverseMap();
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
