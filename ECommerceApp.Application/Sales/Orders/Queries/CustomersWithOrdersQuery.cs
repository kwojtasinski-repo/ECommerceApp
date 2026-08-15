using System.Collections.Generic;

namespace ECommerceApp.Application.Messaging
{
    /// <summary>
    /// Cross-BC query: of the given set of AccountProfile CustomerIds, which ones have at least one
    /// order? Batched (single query for N candidates) to avoid N+1 lookups.
    /// Used by AccountProfile's unclaimed-guest-profile cleanup job (ADR-0030 Phase 4) via
    /// IModuleClient.SendAsync.
    /// </summary>
    public sealed record CustomersWithOrdersQuery(IReadOnlyCollection<int> CustomerIds) : IQuery<IReadOnlySet<int>>;
}
