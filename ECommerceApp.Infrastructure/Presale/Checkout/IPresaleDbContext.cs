using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout
{
    internal interface IPresaleDbContext
    {
        DbSet<CartLine> CartLines { get; }
        DbSet<SoftReservation> SoftReservations { get; }
        DbSet<StockSnapshot> StockSnapshots { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
