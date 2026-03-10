using ECommerceApp.Domain.Sales.Orders;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Contracts
{
    public interface IOrderProductResolver
    {
        Task<OrderProductSnapshot?> ResolveAsync(int productId, CancellationToken ct = default);
    }
}
