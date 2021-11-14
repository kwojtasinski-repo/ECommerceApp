using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
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
        ListForOrderItemVm GetOrderItems(int pageSize, int pageNo, string searchString);
        bool OrderItemExists(int id);
        void DeleteOrderItem(int id);
        void UpdateOrderItems(IEnumerable<OrderItemVm> orderItems);
        IEnumerable<NewOrderItemVm> GetOrderItemsForRealization(Expression<Func<OrderItem, bool>> expression);
        int OrderItemCount(string userId);
        int AddOrderItem(int id, string userId);
    }
}
