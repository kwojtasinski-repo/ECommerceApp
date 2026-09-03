namespace ECommerceApp.Application.Presale.Checkout.Results
{
    public abstract class AddToCartResult
    {
        private AddToCartResult() { }

        public sealed class Success : AddToCartResult
        {
            internal Success() { }
        }

        public sealed class QuantityExceeded : AddToCartResult
        {
            public int MaxAllowed { get; }
            internal QuantityExceeded(int maxAllowed) { MaxAllowed = maxAllowed; }
        }

        public sealed class Unavailable : AddToCartResult
        {
            public int AvailableQuantity { get; }
            internal Unavailable(int availableQuantity) { AvailableQuantity = availableQuantity; }
        }

        public static Success Added() => new();
        public static QuantityExceeded LimitExceeded(int maxAllowed) => new(maxAllowed);
        public static Unavailable OutOfStock(int availableQuantity) => new(availableQuantity);
    }
}
