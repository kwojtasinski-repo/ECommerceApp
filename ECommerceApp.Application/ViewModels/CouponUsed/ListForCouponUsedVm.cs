using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.CouponUsed
{
    public class ListForCouponUsedVm
    {
        public List<CouponUsedVm> CouponsUsed { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}
