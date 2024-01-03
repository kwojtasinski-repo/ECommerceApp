using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Orders
{
    public interface IOrderItemService : IAbstractService<OrderItemVm, IOrderItemRepository, OrderItem>
    {
        int AddOrderItem(OrderItemDto model);
        OrderItemDto GetOrderItemDetails(int id);
        void UpdateOrderItem(OrderItemDto model);
        IEnumerable<OrderItemVm> GetOrderItems(Expression<Func<OrderItem, bool>> expression);
        IEnumerable<OrderItemVm> GetOrderItemsNotOrderedByUserId(string userId);
        IEnumerable<OrderItemVm> GetOrderItemsByItemId(int itemId);
        ListForOrderItemVm GetOrderItems(int pageSize, int pageNo, string searchString);
        bool OrderItemExists(int id);
        void DeleteOrderItem(int id);
        void UpdateOrderItems(IEnumerable<OrderItemVm> orderItems);
        IEnumerable<NewOrderItemVm> GetOrderItemsForRealization(Expression<Func<OrderItem, bool>> expression);
        int OrderItemCount(string userId);
        int AddOrderItem(int id, string userId);
        ListForOrderItemVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo);
        ListForOrderItemVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo);
        IQueryable<OrderItem> GetOrderItems();
    }
}
