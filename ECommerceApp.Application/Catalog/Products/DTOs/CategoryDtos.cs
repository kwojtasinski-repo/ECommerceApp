using FluentValidation;

namespace ECommerceApp.Application.Catalog.Products.DTOs
{
    public record CreateCategoryDto(string Name);

    public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
    {
        public CreateCategoryDtoValidator()
        {
            RuleFor(x => x.Name).NotNull().NotEmpty().MaximumLength(100);
        }
    }

    public record UpdateCategoryDto(int Id, string Name);

    public class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
    {
        public UpdateCategoryDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotNull().NotEmpty().MaximumLength(100);
        }
    }
}
