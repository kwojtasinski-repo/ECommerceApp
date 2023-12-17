using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class TypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
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
