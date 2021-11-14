using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Refund
{
    public class ListForRefundVm
    {
        public List<RefundVm> Refunds { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForRefundValidation : AbstractValidator<ListForRefundVm>
    {
        public ListForRefundValidation()
        {
            RuleFor(x => x.Refunds).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}