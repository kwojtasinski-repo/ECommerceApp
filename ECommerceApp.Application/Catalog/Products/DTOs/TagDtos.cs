using FluentValidation;

namespace ECommerceApp.Application.Catalog.Products.DTOs
{
    public record ProductTagDto(int Id, string Name, string Slug, string Color, bool IsVisible);

    public record CreateTagDto(string Name, string Color, bool IsVisible);

    public class CreateTagDtoValidator : AbstractValidator<CreateTagDto>
    {
        public CreateTagDtoValidator()
        {
            RuleFor(x => x.Name).NotNull().NotEmpty().MaximumLength(100);
            RuleFor(x => x.Color).MaximumLength(30);
        }
    }

    public record UpdateTagDto(int Id, string Name, string Color, bool IsVisible);

    public class UpdateTagDtoValidator : AbstractValidator<UpdateTagDto>
    {
        public UpdateTagDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotNull().NotEmpty().MaximumLength(100);
            RuleFor(x => x.Color).MaximumLength(30);
        }
    }
}
