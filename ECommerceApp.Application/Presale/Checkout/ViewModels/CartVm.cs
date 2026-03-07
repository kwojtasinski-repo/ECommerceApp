using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public sealed class CartVm
    {
        public int CartId { get; set; }
        public string UserId { get; set; } = default!;
        public IReadOnlyList<CartItemVm> Items { get; set; } = Array.Empty<CartItemVm>();
        public decimal TotalPrice => Items.Sum(i => i.LineTotal);
    }
}
