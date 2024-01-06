using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Orders
{
    public interface IOrderItemService
    {
        int AddOrderItem(OrderItemDto model);
        OrderItemDto GetOrderItemDetails(int id);
        void UpdateOrderItem(OrderItemDto model);
        IEnumerable<OrderItemDto> GetOrderItems(Expression<Func<OrderItem, bool>> expression);
        IEnumerable<OrderItemDto> GetOrderItemsByItemId(int itemId);
        ListForOrderItemVm GetOrderItems(int pageSize, int pageNo, string searchString);
        bool OrderItemExists(int id);
        void DeleteOrderItem(int id);
        void UpdateOrderItems(IEnumerable<OrderItemDto> orderItems);
        IEnumerable<OrderItemDto> GetOrderItemsForRealization(string userId);
        IEnumerable<int> GetOrderItemsIdsForRealization(string userId);
        int OrderItemCount(string userId);
        int AddOrderItem(int id, string userId);
        ListForOrderItemVm GetAllItemsOrderedByItemId(int id, int pageSize, int pageNo);
        ListForOrderItemVm GetOrderItemsNotOrderedByUserId(string userId, int pageSize, int pageNo);
        IQueryable<OrderItem> GetOrderItems();
        List<OrderItemDto> GetOrderItemsNotOrdered(IEnumerable<int> ids);
    }
}
