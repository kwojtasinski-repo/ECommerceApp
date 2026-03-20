using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Coupons.Contracts;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Adapters
{
    internal sealed class StockAvailabilityCheckerAdapter : IStockAvailabilityChecker
    {
        private readonly IStockService _stockService;

        public StockAvailabilityCheckerAdapter(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task<bool> IsInStockAsync(int productId, int quantity, CancellationToken ct = default)
        {
            var stock = await _stockService.GetByProductIdAsync(productId, ct);
            if (stock is null)
            {
                return false;
            }

            return stock.AvailableQuantity >= quantity;
        }
    }
}
