using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class ListForCouponVm
    {
        public List<CouponVm> Coupons { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
