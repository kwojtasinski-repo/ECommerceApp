using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class ListForRefundVm
    {
        public List<RefundForListVm> Refunds { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}