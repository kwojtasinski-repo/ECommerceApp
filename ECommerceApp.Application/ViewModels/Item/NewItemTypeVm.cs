using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class NewItemTypeVm : IMapFrom<ECommerceApp.Domain.Model.Type>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewItemTypeVm, ECommerceApp.Domain.Model.Type>().ReverseMap();
        }
    }

    public class NewItemTypeValidation : AbstractValidator<NewItemTypeVm>
    {
        public NewItemTypeValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
