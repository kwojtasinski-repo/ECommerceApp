using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.Messages;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class StockAvailabilityChecker : IStockAvailabilityChecker
    {
        private readonly IStockService _stockService;

        public StockAvailabilityChecker(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task<IReadOnlyList<int>> FindInsufficientAsync(
            IReadOnlyList<OrderPlacedItem> items, CancellationToken ct = default)
        {
            var productIds = items.Select(i => i.ProductId).ToList();
            var stockByProductId = new Dictionary<int, StockItemDto>();
            await foreach (var stock in _stockService.GetByProductIdsAsync(productIds, ct))
            {
                stockByProductId[stock.ProductId] = stock;
            }

            var insufficient = new List<int>();
            foreach (var item in items)
            {
                if (stockByProductId.TryGetValue(item.ProductId, out var stock)
                    && item.Quantity > stock.AvailableQuantity)
                {
                    insufficient.Add(item.ProductId);
                }
            }

            return insufficient;
        }
    }
}
