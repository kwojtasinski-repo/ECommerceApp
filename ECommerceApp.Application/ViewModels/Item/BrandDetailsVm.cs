using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class BrandDetailsVm : IMapFrom<ECommerceApp.Domain.Model.Brand>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public List<NewItemVm> Items { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<BrandDetailsVm, ECommerceApp.Domain.Model.Brand>().ReverseMap()
                .ForMember(p => p.Items, opt => opt.MapFrom(ps => ps.Items));
        }
    }

    public class BrandDetailsValidation : AbstractValidator<BrandDetailsVm>
    {
        public BrandDetailsValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
