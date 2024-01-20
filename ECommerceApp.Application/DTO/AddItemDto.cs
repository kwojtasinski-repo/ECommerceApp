using FluentValidation;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.DTO
{
    public class AddItemDto
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }

        public IEnumerable<int> TagsId { get; set; } = new List<int>();
        public IEnumerable<AddItemImageDto> Images { get; set; } = new List<AddItemImageDto>();
    }

    public record AddItemImageDto(string ImageName, string ImageSource);

    public class AddItemDtoValidtor : AbstractValidator<AddItemDto>
    {
        public AddItemDtoValidtor()
        {
            RuleFor(a => a.Name).NotNull().NotEmpty().MinimumLength(3).MaximumLength(100);
            RuleFor(a => a.Description).NotNull().NotEmpty().MinimumLength(3).MaximumLength(255);
            RuleFor(a => a.Cost).GreaterThan(0);
            RuleFor(a => a.Quantity).GreaterThan(0);
            RuleFor(a => a.Warranty).NotNull().NotEmpty().Must((warranty) =>
            {
                return int.TryParse(warranty, out var _);
            });

            When(a => a.TagsId is not null && a.TagsId.Any(),
                () =>
                {
                    RuleForEach(a => a.TagsId).ChildRules(t =>
                    {
                        t.RuleFor(id => id).GreaterThan(0);
                    });
                });
            When(a => a.TagsId is not null && a.Images.Any(), () =>
            {
                RuleForEach(i => i.Images).ChildRules(i =>
                {
                    i.RuleFor(im => im.ImageName).NotNull();
                    i.RuleFor(im => im.ImageSource).NotNull();
                });
            });
        }
    }
}