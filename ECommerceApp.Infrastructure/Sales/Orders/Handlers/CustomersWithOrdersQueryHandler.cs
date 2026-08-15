using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Services;

namespace ECommerceApp.Infrastructure.Sales.Orders.Handlers
{
    internal sealed class CustomersWithOrdersQueryHandler : IQueryHandler<CustomersWithOrdersQuery, IReadOnlySet<int>>
    {
        private readonly IOrderService _orders;

        public CustomersWithOrdersQueryHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public Task<IReadOnlySet<int>> HandleAsync(CustomersWithOrdersQuery query, CancellationToken ct = default)
            => _orders.GetCustomerIdsWithOrdersAsync(query.CustomerIds, ct);
    }
}
