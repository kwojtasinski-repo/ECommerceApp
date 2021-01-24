using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class BrandForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Brand>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Brand, BrandForListVm>();
        }
    }

    public class BrandForListDetailsValidation : AbstractValidator<BrandForListVm>
    {
        public BrandForListDetailsValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
