using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Sales.Coupons;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Handlers
{
    internal sealed class ProductNameChangedHandler : IMessageHandler<ProductNameChanged>
    {
        private readonly IScopeTargetRepository _scopeTargets;

        public ProductNameChangedHandler(IScopeTargetRepository scopeTargets)
        {
            _scopeTargets = scopeTargets;
        }

        public async Task HandleAsync(ProductNameChanged message, CancellationToken ct = default)
        {
            var targets = await _scopeTargets.GetByScopeTypeAndTargetIdAsync(
                CouponRuleNames.PerProduct, message.ProductId, ct);

            if (targets.Count == 0) return;

            foreach (var target in targets)
            {
                target.UpdateTargetName(message.NewName);
            }

            await _scopeTargets.UpdateRangeAsync(targets, ct);
        }
    }
}
