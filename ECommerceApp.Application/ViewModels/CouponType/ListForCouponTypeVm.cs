﻿using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.CouponType
{
    public class ListForCouponTypeVm
    {
        public List<CouponTypeVm> CouponTypes { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }

    public class ListForCouponTypeVmValidation : AbstractValidator<ListForCouponTypeVm>
    {
        public ListForCouponTypeVmValidation()
        {
            RuleFor(x => x.CouponTypes).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}
