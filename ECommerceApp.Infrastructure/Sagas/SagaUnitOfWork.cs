using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sagas;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sagas
{
    internal sealed class SagaUnitOfWork : ISagaUnitOfWork
    {
        private readonly SagasDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public SagaUnitOfWork(SagasDbContext context, IServiceProvider serviceProvider)
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