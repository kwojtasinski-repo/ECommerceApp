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

        public static OrderDto AsDto(this AddOrderDto dto)
        {
            var order = new OrderDto
            {
                Id = dto.Id,
                CustomerId = dto.CustomerId,
                OrderItems = dto.OrderItems.Select(oi => new OrderItemDto { Id = oi.Id }).ToList()
            };

            return order;
        }

        public static OrderDto AsOrderDto(this NewOrderVm newOrderVm)
        {
            var order = new OrderDto
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
                OrderItems = newOrderVm.OrderItems.ToList()
            };

            return order;
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
