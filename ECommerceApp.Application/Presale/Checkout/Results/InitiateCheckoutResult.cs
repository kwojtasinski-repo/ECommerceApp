using System.Collections.Generic;

namespace ECommerceApp.Application.Presale.Checkout.Results
{
    public abstract class InitiateCheckoutResult
    {
        private InitiateCheckoutResult() { }

        public sealed class Completed : InitiateCheckoutResult
        {
            public int ReservedCount { get; }
            public IReadOnlyList<int> UnavailableProductIds { get; }

            internal Completed(int reservedCount, IReadOnlyList<int> unavailable)
            {
                ReservedCount = reservedCount;
                UnavailableProductIds = unavailable;
            }
        }

        public sealed class CartEmpty : InitiateCheckoutResult
        {
            internal CartEmpty() { }
        }

        public sealed class NothingReserved : InitiateCheckoutResult
        {
            public IReadOnlyList<int> UnavailableProductIds { get; }
            internal NothingReserved(IReadOnlyList<int> unavailable) => UnavailableProductIds = unavailable;
        }

        public static Completed Reserved(int count, IReadOnlyList<int> unavailable) => new(count, unavailable);
        public static CartEmpty EmptyCart() => new();
        public static NothingReserved AllUnavailable(IReadOnlyList<int> unavailable) => new(unavailable);
    }
}
