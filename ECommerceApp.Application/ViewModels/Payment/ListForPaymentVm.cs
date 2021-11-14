using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Payment
{ 
    public class ListForPaymentVm
    {
        public List<PaymentVm> Payments { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForPaymentValidation : AbstractValidator<ListForPaymentVm>
    {
        public ListForPaymentValidation()
        {
            RuleFor(x => x.Payments).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}