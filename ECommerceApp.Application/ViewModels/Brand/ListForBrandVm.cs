using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Brand
{
    public class ListForBrandVm
    {
        public List<BrandVm> Brands { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForBrandVmValidation : AbstractValidator<ListForBrandVm>
    {
        public ListForBrandVmValidation()
        {
            RuleFor(x => x.Brands).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}