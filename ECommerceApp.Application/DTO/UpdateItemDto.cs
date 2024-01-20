using FluentValidation;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.DTO
{
    public class UpdateItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }

        public IEnumerable<int> TagsId { get; set; } = new List<int>();
        public IEnumerable<UpdateItemImageDto> Images { get; set; } = new List<UpdateItemImageDto>();
    }

    public record UpdateItemImageDto(int ImageId, string ImageName, string ImageSource);


    public class UpdateItemDtoValidtor : AbstractValidator<UpdateItemDto>
    {
        public UpdateItemDtoValidtor()
        {
            RuleFor(u => u.Id).GreaterThan(0);
            RuleFor(u => u.Name).NotNull().NotEmpty().MinimumLength(3).MaximumLength(100);
            RuleFor(u => u.Description).NotNull().NotEmpty().MinimumLength(3).MaximumLength(255);
            RuleFor(u => u.Cost).GreaterThan(0);
            RuleFor(u => u.Quantity).GreaterThan(0);
            RuleFor(u => u.Warranty).NotNull().NotEmpty().Must((warranty) =>
            {
                return int.TryParse(warranty, out var _);
            });

            When(u => u.TagsId is not null && u.TagsId.Any(), () => {
                    RuleForEach(u => u.TagsId).ChildRules(t =>
                    {
                        t.RuleFor(id => id).GreaterThan(0);
                    });
                });
            When(u => u.TagsId is not null && u.Images.Any(), () =>
            {
                RuleForEach(i => i.Images).SetValidator(new UpdateItemImageDtoValidator());
            });
        }
    }

    public class UpdateItemImageDtoValidator : AbstractValidator<UpdateItemImageDto>
    {
        public UpdateItemImageDtoValidator()
        {
            When(i => i.ImageId == default, () =>
            {
                RuleFor(im => im.ImageName).NotNull();
                RuleFor(im => im.ImageSource).NotNull();
            });
            When(i => i.ImageId > 0, () =>
            {
                RuleFor(im => im.ImageName).Null();
                RuleFor(im => im.ImageSource).Null();
            });
        }
    }
}
