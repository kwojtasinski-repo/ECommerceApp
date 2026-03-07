using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Contracts
{
    public interface IStockClient
    {
        Task<bool> TryHoldAsync(int productId, int quantity, CancellationToken ct = default);
        Task ReleaseAsync(int productId, int quantity, CancellationToken ct = default);
    }
}
