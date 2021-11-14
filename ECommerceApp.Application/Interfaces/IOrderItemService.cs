using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IOrderItemService : IAbstractService<OrderItemVm, IOrderItemRepository, OrderItem>
    {
        int AddOrderItem(OrderItemVm model);
        OrderItemDetailsVm GetOrderItemDetails(int id);
        OrderItemVm GetOrderItemById(int id);
        void UpdateOrderItem(OrderItemVm model);
        IEnumerable<OrderItemVm> GetOrderItems(Expression<Func<OrderItem, bool>> expression);
        ListForItemOrderVm GetOrderItems(int pageSize, int pageNo, string searchString);
        bool OrderItemExists(int id);
        void DeleteOrderItem(int id);
    }
}
