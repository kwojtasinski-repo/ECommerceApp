using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Brand
{
    public class BrandVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Brand>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<BrandVm, ECommerceApp.Domain.Model.Brand>().ReverseMap();
        }
    }

    public class BrandVmValidation : AbstractValidator<BrandVm>
    {
        public BrandVmValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
