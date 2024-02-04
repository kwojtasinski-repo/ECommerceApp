using ECommerceApp.Application.DTO;
using Microsoft.AspNetCore.Http;
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
                OrderItems = dto.OrderItems?
                                .Select(oi => new OrderItemDto { Id = oi.Id })?
                                .ToList() ?? new List<OrderItemDto>()
            };

            return order;
        }

        public static string GetUserId(this IHttpContextAccessor httpContextAccessor)
        {
            return httpContextAccessor.HttpContext?.User?.GetUserId();
        }

        public static string GetUserId(this ClaimsPrincipal claimsPrincipal)
        {
            return claimsPrincipal.Claims?
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }

        public static string GetUserRole(this IHttpContextAccessor httpContextAccessor)
        {
            return httpContextAccessor.HttpContext?.User?.Claims?
                        .FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        }
    }
}
