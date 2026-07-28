using ECommerceApp.Application.Catalog.Products;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal sealed class CatalogUnitOfWork : ICatalogUnitOfWork
    {
        private readonly CatalogDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public CatalogUnitOfWork(CatalogDbContext context, IServiceProvider serviceProvider)
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
