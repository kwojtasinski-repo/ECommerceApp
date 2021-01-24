using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class TypeForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Type>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Type, TypeForListVm>();
        }
    }

    public class TypeForListValidation : AbstractValidator<TypeForListVm>
    {
        public TypeForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
