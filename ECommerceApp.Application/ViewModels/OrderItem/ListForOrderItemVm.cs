using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class ListForOrderItemVm
    {
        public List<OrderItemForListVm> ItemOrders { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string SearchString { get; set; }
        public int Count { get; set; }
    }
}