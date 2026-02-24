using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Products.DTOs
{
    public record CreateProductDto(
        string Name,
        decimal Cost,
        int Quantity,
        string Description,
        int CategoryId,
        IEnumerable<int> TagIds);

    public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
    {
        public CreateProductDtoValidator()
        {
            RuleFor(x => x.Name).NotNull().NotEmpty().MinimumLength(3).MaximumLength(150);
            RuleFor(x => x.Cost).GreaterThan(0);
            RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Description).MaximumLength(300);
            RuleFor(x => x.CategoryId).GreaterThan(0);
        }
    }
}
