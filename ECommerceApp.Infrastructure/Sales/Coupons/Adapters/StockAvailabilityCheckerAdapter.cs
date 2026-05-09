using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Adapters
{
    internal sealed class StockAvailabilityCheckerAdapter : IStockAvailabilityChecker
    {
        private readonly IModuleClient _moduleClient;

        public StockAvailabilityCheckerAdapter(IModuleClient moduleClient)
        {
            _moduleClient = moduleClient;
        }

        public Task<bool> IsInStockAsync(int productId, int quantity, CancellationToken ct = default)
            => _moduleClient.SendAsync(new StockAvailableQuery(productId, quantity), ct);
    }
}
