using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class TagDto : IMapFrom<Tag>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<TagDto, Tag>()
               .ReverseMap();
        }
    }

    public class TagDtoValidator : AbstractValidator<TagDto>
    {
        public TagDtoValidator()
        {
            RuleFor(b => b.Name).NotEmpty()
                    .MinimumLength(2)
                    .MaximumLength(100);
        }
    }
}
