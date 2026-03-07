using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Contracts
{
    public interface ICatalogClient
    {
        Task<decimal?> GetUnitPriceAsync(int productId, CancellationToken ct = default);
    }
}
