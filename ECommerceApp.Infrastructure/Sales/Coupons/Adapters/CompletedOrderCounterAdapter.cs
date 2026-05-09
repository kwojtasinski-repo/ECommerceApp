using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Adapters
{
    internal sealed class CompletedOrderCounterAdapter : ICompletedOrderCounter
    {
        private readonly IModuleClient _moduleClient;

        public CompletedOrderCounterAdapter(IModuleClient moduleClient)
        {
            _moduleClient = moduleClient;
        }

        public Task<int> CountByUserAsync(string userId, CancellationToken ct = default)
            => _moduleClient.SendAsync(new CompletedOrderCountQuery(userId), ct);
    }
}
