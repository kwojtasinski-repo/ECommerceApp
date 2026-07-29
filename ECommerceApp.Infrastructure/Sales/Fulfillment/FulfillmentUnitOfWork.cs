using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment
{
    internal sealed class FulfillmentUnitOfWork : IFulfillmentUnitOfWork
    {
        private readonly FulfillmentDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public FulfillmentUnitOfWork(FulfillmentDbContext context, IServiceProvider serviceProvider)
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
