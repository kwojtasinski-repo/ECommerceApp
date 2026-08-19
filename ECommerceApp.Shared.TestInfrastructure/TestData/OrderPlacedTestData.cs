using ECommerceApp.Application.Sales.Orders.Messages;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class OrderPlacedTestData
    {
        public static OrderPlaced Create(
            int orderId = 1,
            decimal totalAmount = 150m,
            int currencyId = 1,
            int productId = 10,
            int quantity = 2,
            string userId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
            int expirationHours = 24,
            bool includeItem = true)
        {
            var occurredAt = DateTime.UtcNow;

            return new OrderPlaced(
                OrderId: orderId,
                Items: includeItem
                    ? new List<OrderPlacedItem> { new(ProductId: productId, Quantity: quantity) }
                    : new List<OrderPlacedItem>(),
                UserId: userId,
                ExpiresAt: occurredAt.AddHours(expirationHours),
                OccurredAt: occurredAt,
                TotalAmount: totalAmount,
                CurrencyId: currencyId);
        }
    }
}