using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class ListForCouponVm
    {
        public List<CouponForListVm> Coupons { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForCouponValidation : AbstractValidator<ListForCouponVm>
    {
        public ListForCouponValidation()
        {
            RuleFor(x => x.Coupons).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}
