using ECommerceApp.Application.Inventory.Availability;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability
{
    internal sealed class InventoryUnitOfWork : IInventoryUnitOfWork
    {
        private readonly AvailabilityDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public InventoryUnitOfWork(AvailabilityDbContext context, IServiceProvider serviceProvider)
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
