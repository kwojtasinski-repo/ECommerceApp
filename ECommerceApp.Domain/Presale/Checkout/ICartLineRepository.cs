using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public interface ICartLineRepository
    {
        Task<IReadOnlyList<CartLine>> GetByUserIdAsync(PresaleUserId userId, CancellationToken ct = default);
        Task UpsertAsync(CartLine cartLine, CancellationToken ct = default);
        Task DeleteAsync(PresaleUserId userId, PresaleProductId productId, CancellationToken ct = default);
        Task DeleteRangeAsync(PresaleUserId userId, IReadOnlyList<PresaleProductId> productIds, CancellationToken ct = default);
        Task DeleteAllForUserAsync(PresaleUserId userId, CancellationToken ct = default);
    }
}
