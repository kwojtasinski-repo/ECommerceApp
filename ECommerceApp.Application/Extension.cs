using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ECommerceApp.Application
{
    public static class Extension
    {
        public static OrderItemDto AsOrderItemDto(this AddOrderItemDto dto)
        {
            var orderItem = new OrderItemDto()
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
                CurrencyId = dto.CurrencyId,
                OrderItems = dto.OrderItems.Select(oi => new OrderItemVm { Id = oi.Id }).ToList()
            };

            return order;
        }

        public static OrderItemVm AsOrderVm(this NewOrderItemVm newOrderItemVm)
        {
            var orderItem = new OrderItemVm
            {
                Id = newOrderItemVm.Id,
                CouponUsedId = newOrderItemVm.CouponUsedId,
                ItemId = newOrderItemVm.ItemId,
                OrderId = newOrderItemVm.OrderId,
                ItemOrderQuantity = newOrderItemVm.ItemOrderQuantity,
                RefundId = newOrderItemVm.RefundId,
                UserId = newOrderItemVm.UserId
            };

            return orderItem;
        }

        public static NewOrderItemVm AsNewOrderItemVm(this OrderItemVm orderItemVm)
        {
            var orderItem = new NewOrderItemVm
            {
                Id = orderItemVm.Id,
                CouponUsedId = orderItemVm.CouponUsedId,
                ItemId = orderItemVm.ItemId,
                OrderId = orderItemVm.OrderId,
                ItemOrderQuantity = orderItemVm.ItemOrderQuantity,
                RefundId = orderItemVm.RefundId,
                UserId = orderItemVm.UserId,
            };

            return orderItem;
        }

        public static OrderVm AsOrderVm(this NewOrderVm newOrderVm)
        {
            var order = new OrderVm
            {
                Id = newOrderVm.Id,
                Cost = newOrderVm.Cost,
                CouponUsedId = newOrderVm.CouponUsedId,
                CustomerId = newOrderVm.CustomerId,
                PaymentId = newOrderVm.PaymentId,
                CurrencyId = newOrderVm.CurrencyId,
                Delivered = newOrderVm.Delivered,
                RefundId = newOrderVm.RefundId,
                IsDelivered = newOrderVm.IsDelivered,
                UserId = newOrderVm.UserId,
                IsPaid = newOrderVm.IsPaid,
                Number = newOrderVm.Number,
                Ordered = newOrderVm.Ordered,
                OrderItems = newOrderVm.OrderItems.Select(oi => oi.AsOrderVm()).ToList()
            };

            return order;
        }

        public static NewOrderVm AsNewOrderVm(this OrderVm orderVm)
        {
            var order = new NewOrderVm
            {
                Id = orderVm.Id,
                Cost = orderVm.Cost,
                CouponUsedId = orderVm.CouponUsedId,
                CustomerId = orderVm.CustomerId,
                PaymentId = orderVm.PaymentId,
                Delivered = orderVm.Delivered,
                RefundId = orderVm.RefundId,
                IsDelivered = orderVm.IsDelivered,
                UserId = orderVm.UserId,
                IsPaid = orderVm.IsPaid,
                Number = orderVm.Number,
                Ordered = orderVm.Ordered,
                CurrencyId = orderVm.CurrencyId,
                OrderItems = orderVm.OrderItems.Select(oi => oi.AsNewOrderItemVm()).ToList()
            };

            return order;
        }

        public static OrderItemVm AsOrderItemVm(this NewOrderItemVm newOrderItemVm)
        {
            var orderItem = new OrderItemVm
            {
                Id = newOrderItemVm.Id,
                CouponUsedId = newOrderItemVm.CouponUsedId,
                ItemId = newOrderItemVm.ItemId,
                ItemOrderQuantity = newOrderItemVm.ItemOrderQuantity,
                OrderId = newOrderItemVm.OrderId,
                RefundId = newOrderItemVm.RefundId,
                UserId = newOrderItemVm.UserId
            };

            return orderItem;
        }

        public static Domain.Model.Item MapToItem(this ItemVm itemVm)
        {
            var item = new Domain.Model.Item()
            {
                Id = itemVm.Id,
                Name = itemVm.Name,
                Cost = itemVm.Cost,
                Description = itemVm.Description,
                Warranty = itemVm.Warranty,
                Quantity = itemVm.Quantity,
                BrandId = itemVm.BrandId,
                TypeId = itemVm.TypeId,
                CurrencyId = itemVm.CurrencyId
            };

            var itemTags = new List<Domain.Model.ItemTag>();

            if (itemVm.ItemTags != null)
            {
                foreach (var tag in itemVm.ItemTags)
                {
                    var itemTag = new Domain.Model.ItemTag
                    {
                        ItemId = itemVm.Id,
                        TagId = tag.TagId
                    };

                    itemTags.Add(itemTag);
                }
            }

            item.ItemTags = itemTags;

            return item;
        }

        public static ItemVm MapToItemVm(this Domain.Model.Item item)
        {
            if (item != null)
            {
                var itemVm = new ItemVm()
                {
                    Id = item.Id,
                    Name = item.Name,
                    Cost = item.Cost,
                    Description = item.Description,
                    Warranty = item.Warranty,
                    Quantity = item.Quantity,
                    BrandId = item.BrandId,
                    TypeId = item.TypeId,
                    CurrencyId = item.CurrencyId
                };

                var itemTags = new List<ItemTagVm>();

                if (item.ItemTags != null)
                {
                    foreach (var tag in item.ItemTags)
                    {
                        var itemTag = new ItemTagVm
                        {
                            TagId = tag.TagId
                        };

                        itemTags.Add(itemTag);
                    }
                }

                itemVm.ItemTags = itemTags;

                return itemVm;
            }
            else
            {
                return null;
            }
        }

        public static string GetUserId(this IHttpContextAccessor httpContextAccessor)
        {
            return httpContextAccessor.HttpContext?.User?.Claims?
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
