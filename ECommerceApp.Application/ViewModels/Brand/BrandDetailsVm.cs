using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Item;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Brand
{
    public class BrandDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Brand>
    {
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
