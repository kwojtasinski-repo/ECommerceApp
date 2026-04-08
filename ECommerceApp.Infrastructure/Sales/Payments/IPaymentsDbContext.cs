using ECommerceApp.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Payments
{
    internal interface IPaymentsDbContext
    {
        DbSet<Payment> Payments { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
