using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class TypeDto : IMapFrom<Domain.Model.Type>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<TypeDto, Domain.Model.Type>()
              .ReverseMap();
        }
    }

    public class TypeDtoValidator : AbstractValidator<TypeDto>
    {
        public TypeDtoValidator()
        {
            RuleFor(b => b.Name).NotEmpty()
                    .MinimumLength(2)
                    .MaximumLength(100);
        }
    }
}
