using FluentValidation;
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

    public class ListForItemOrderValidation : AbstractValidator<ListForOrderItemVm>
    {
        public ListForItemOrderValidation()
        {
            RuleFor(x => x.ItemOrders).NotNull();
            RuleFor(x => x.CurrentPage).NotNull();
            RuleFor(x => x.PageSize).NotNull();
            RuleFor(x => x.SearchString).NotNull();
            RuleFor(x => x.Count).NotNull();
        }
    }
}