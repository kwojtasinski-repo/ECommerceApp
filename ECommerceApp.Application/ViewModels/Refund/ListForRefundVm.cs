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
}