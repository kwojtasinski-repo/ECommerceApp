using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application
{
    public static class Extension
    {
        public static OrderItemVm AsVm(this OrderItemDto dto)
        {
            var orderItem = new OrderItemVm()
            {
                Id = dto.Id,
                ItemId = dto.ItemId,
                ItemOrderQuantity = dto.ItemOrderQuantity,
                OrderId = null,
                UserId = ""
            };

            return orderItem;
        }

        public static OrderVm AsVm(this OrderDto dto)
        {
            var order = new OrderVm
            {
                Id = dto.Id,
                CustomerId = dto.CustomerId,
                OrderItems = dto.OrderItems.Select(oi => new OrderItemVm { Id = oi.Id }).ToList()
            };

            return order;
        }
    }
}
