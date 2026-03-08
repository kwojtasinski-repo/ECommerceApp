using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Adapters
{
    internal sealed class StockClientAdapter : IStockClient
    {
        private readonly IStockService _stockService;

        public StockClientAdapter(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task<bool> TryReserveAsync(int productId, int quantity, CancellationToken ct = default)
        {
            var stock = await _stockService.GetByProductIdAsync(productId, ct);
            if (stock is null)
                return false;

            return stock.AvailableQuantity >= quantity;
        }

        public Task ReleaseAsync(int productId, int quantity, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
