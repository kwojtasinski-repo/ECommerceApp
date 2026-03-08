using System.Collections.Generic;

namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public sealed record CartVm(string UserId, IReadOnlyList<CartLineVm> Lines);
}
