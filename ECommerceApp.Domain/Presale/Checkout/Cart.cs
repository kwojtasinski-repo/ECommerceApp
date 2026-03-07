using ECommerceApp.Domain.Shared;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public class Cart
    {
        public CartId Id { get; private set; } = new CartId(0);
        public string UserId { get; private set; } = default!;

        private readonly List<CartItem> _items = new();
        public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

        private Cart() { }

        public static Cart Create(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new DomainException("UserId is required.");

            return new Cart { UserId = userId };
        }

        public void AddItem(int productId, int quantity, decimal unitPrice)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive.");
            if (unitPrice <= 0)
                throw new DomainException("Unit price must be positive.");

            var existing = _items.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                existing.UpdateQuantity(existing.Quantity + quantity);
                return;
            }

            _items.Add(CartItem.Create(Id, productId, quantity, unitPrice));
        }

        public bool UpdateQuantity(int productId, int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive.");

            var item = _items.FirstOrDefault(i => i.ProductId == productId);
            if (item is null)
                return false;

            item.UpdateQuantity(quantity);
            return true;
        }

        public bool RemoveItem(int productId)
        {
            var item = _items.FirstOrDefault(i => i.ProductId == productId);
            if (item is null)
                return false;

            _items.Remove(item);
            return true;
        }

        public void Clear() => _items.Clear();
    }
}
