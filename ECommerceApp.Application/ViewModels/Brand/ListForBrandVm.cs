using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Brand
{
    public class ListForItemBrandVm
    {
        public List<BrandVm> Brands { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForItemBrandValidation : AbstractValidator<ListForItemBrandVm>
    {
        public ListForItemBrandValidation()
        {
            RuleFor(x => x.Brands).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}