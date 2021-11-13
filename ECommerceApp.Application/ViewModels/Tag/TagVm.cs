using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Tag
{
    public class TagVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Tag>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Tag, TagVm>().ReverseMap();
        }
    }

    public class TagValidation : AbstractValidator<TagVm>
    {
        public TagValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
