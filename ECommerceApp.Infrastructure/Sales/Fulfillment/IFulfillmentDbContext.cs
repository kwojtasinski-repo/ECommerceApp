using ECommerceApp.Domain.Sales.Fulfillment;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment
{
    internal interface IFulfillmentDbContext
    {
        DbSet<Refund> Refunds { get; }
        DbSet<Shipment> Shipments { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
