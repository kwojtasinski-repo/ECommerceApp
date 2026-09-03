using ECommerceApp.Application.Sales.Orders.Messages;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Contracts
{
    /// <summary>
    /// Read-only pre-flight check against Inventory's real stock, called synchronously before an
    /// order is created. It does not reserve anything — the actual reservation still happens
    /// asynchronously via Inventory's own <c>OrderPlaced</c> handler, unchanged. This only narrows the
    /// race window and rejects the obviously-doomed case up front, instead of confirming an order the
    /// warehouse has no chance of fulfilling.
    /// </summary>
    public interface IStockAvailabilityChecker
    {
        /// <returns>The product ids among <paramref name="items"/> whose requested quantity exceeds
        /// what Inventory currently reports as available. A product absent from Inventory's stock
        /// tracking (e.g. digital goods) is never reported as insufficient.</returns>
        Task<IReadOnlyList<int>> FindInsufficientAsync(
            IReadOnlyList<OrderPlacedItem> items, CancellationToken ct = default);
    }
}
