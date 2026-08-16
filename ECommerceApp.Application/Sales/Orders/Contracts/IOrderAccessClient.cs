using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Contracts
{
    public interface IOrderAccessClient
    {
        Task<bool> HasAccessAsync(int orderId, string token, CancellationToken ct = default);
    }
}
