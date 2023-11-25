using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class BrandDto : IMapFrom<Brand>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<BrandDto, Brand>()
               .ReverseMap();
        }
    }

    public class BrandDtoValidator : AbstractValidator<BrandDto>
    {
        public BrandDtoValidator()
        {
            RuleFor(b => b.Name).NotEmpty()
                    .MinimumLength(2)
                    .MaximumLength(100);
        }
    }
}
