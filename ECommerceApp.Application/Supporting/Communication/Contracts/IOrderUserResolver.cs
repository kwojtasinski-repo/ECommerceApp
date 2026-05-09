using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Contracts
{
    /// <summary>
    /// Port allowing the Communication BC to resolve the owner (userId) of an order
    /// without directly coupling to the Orders BC's application services.
    /// Implemented by Infrastructure adapters that query the Orders data store read-only.
    /// </summary>
    public interface IOrderUserResolver
    {
        Task<string> GetUserIdForOrderAsync(int orderId, CancellationToken ct = default);
    }
}
