using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Infrastructure.Inventory.Handlers
{
    internal sealed class StockAvailableQueryHandler : IQueryHandler<StockAvailableQuery, bool>
    {
        private readonly IStockService _stockService;

        public StockAvailableQueryHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task<bool> HandleAsync(StockAvailableQuery query, CancellationToken ct = default)
        {
            var stock = await _stockService.GetByProductIdAsync(query.ProductId, ct);
            return stock is not null && stock.AvailableQuantity >= query.Quantity;
        }
    }
}
