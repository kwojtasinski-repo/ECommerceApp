using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders
{
    internal sealed class OrdersUnitOfWork : IOrdersUnitOfWork
    {
        private readonly OrdersDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public OrdersUnitOfWork(OrdersDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }

        public async Task<IOutboxTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            var scope = await CrossContextTransactionScope.BeginAsync(_context, _serviceProvider, ct);
            return new OutboxTransaction(scope);
        }
    }
}
