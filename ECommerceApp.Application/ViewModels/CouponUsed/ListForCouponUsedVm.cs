using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class ListForCouponUsedVm
    {
        public List<CouponUsedVm> CouponsUsed { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForCouponUsedValidation : AbstractValidator<ListForCouponUsedVm>
    {
        public ListForCouponUsedValidation()
        {
            RuleFor(x => x.CouponsUsed).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}
