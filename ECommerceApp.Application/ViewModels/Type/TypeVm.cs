using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Type
{
    public class TypeVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Type>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<TypeVm, ECommerceApp.Domain.Model.Type>().ReverseMap();
        }
    }

    public class TypeVmValidation : AbstractValidator<TypeVm>
    {
        public TypeVmValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
