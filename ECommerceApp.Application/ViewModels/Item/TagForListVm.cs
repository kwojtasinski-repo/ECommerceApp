using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class TagForListVm : IMapFrom<ECommerceApp.Domain.Model.Tag>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Tag, TagForListVm>();
        }
    }

    public class TagForListValidation : AbstractValidator<TagForListVm>
    {
        public TagForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
