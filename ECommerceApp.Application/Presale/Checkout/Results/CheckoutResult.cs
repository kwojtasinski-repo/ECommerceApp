namespace ECommerceApp.Application.Presale.Checkout.Results
{
    public abstract class CheckoutResult
    {
        private CheckoutResult() { }

        public sealed class Success : CheckoutResult
        {
            public int OrderId { get; }
            internal Success(int orderId) => OrderId = orderId;
        }

        public sealed class NoSoftReservations : CheckoutResult
        {
            internal NoSoftReservations() { }
        }

        public sealed class StockUnavailable : CheckoutResult
        {
            public int ProductId { get; }
            internal StockUnavailable(int productId) => ProductId = productId;
        }

        public sealed class OrderFailed : CheckoutResult
        {
            public string Reason { get; }
            internal OrderFailed(string reason) => Reason = reason;
        }

        public static Success Succeeded(int orderId) => new(orderId);
        public static NoSoftReservations NoReservations() => new();
        public static StockUnavailable StockNotAvailable(int productId) => new(productId);
        public static OrderFailed Failed(string reason) => new(reason);
    }
}
