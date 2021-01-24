using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class NewItemBrandVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Brand>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewItemBrandVm, ECommerceApp.Domain.Model.Brand>().ReverseMap();
        }
    }

    public class NewItemBrandValidation : AbstractValidator<NewItemBrandVm>
    {
        public NewItemBrandValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
