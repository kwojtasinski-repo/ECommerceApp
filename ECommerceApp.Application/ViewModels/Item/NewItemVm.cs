using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Application.DTO;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Item
{
    public class NewItemVm : BaseVm
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Description { get; set; }
        public string Warranty { get; set; }
        public int Quantity { get; set; }
        public int BrandId { get; set; }
        public int TypeId { get; set; }
        public int CurrencyId { get; set; }

        public List<TypeDto> Types { get; set; } = new List<TypeDto>();
        public List<TagDto> Tags { get; set; } = new List<TagDto>();
        public List<int> ItemTags { get; set; } = new List<int>();
        public List<GetImageVm> Images { get; set; } = new List<GetImageVm>();
    }

    public class NewItemValidation : AbstractValidator<NewItemVm>
    {
        public NewItemValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull().MinimumLength(2).MaximumLength(100);
            RuleFor(x => x.Cost).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.Description).NotNull().MinimumLength(2).MaximumLength(255);
            RuleFor(x => x.Warranty).NotNull();
            RuleFor(x => x.Quantity).NotNull().GreaterThan(0);
            RuleFor(x => x.BrandId).NotNull();
            RuleFor(x => x.TypeId).NotNull();
        }
    }
}
