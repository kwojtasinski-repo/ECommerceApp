using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class ListForOrderVm
    {
        public List<OrderForListVm> Orders { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForOrderValidation : AbstractValidator<ListForOrderVm>
    {
        public ListForOrderValidation()
        {
            RuleFor(x => x.Orders).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}