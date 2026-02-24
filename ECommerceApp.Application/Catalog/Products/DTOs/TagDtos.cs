using FluentValidation;

namespace ECommerceApp.Application.Catalog.Products.DTOs
{
    public record ProductTagDto(int Id, string Name, string Slug);

    public record CreateTagDto(string Name);

    public class CreateTagDtoValidator : AbstractValidator<CreateTagDto>
    {
        public CreateTagDtoValidator()
        {
            RuleFor(x => x.Name).NotNull().NotEmpty().MaximumLength(50);
        }
    }

    public record UpdateTagDto(int Id, string Name);

    public class UpdateTagDtoValidator : AbstractValidator<UpdateTagDto>
    {
        public UpdateTagDtoValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotNull().NotEmpty().MaximumLength(50);
        }
    }
}
