using ECommerceApp.Domain.Model;
using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class TagDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
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
