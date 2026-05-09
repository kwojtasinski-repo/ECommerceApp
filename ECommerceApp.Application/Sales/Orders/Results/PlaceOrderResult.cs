namespace ECommerceApp.Application.Sales.Orders.Results
{
    public sealed record PlaceOrderResult
    {
        public bool IsSuccess { get; }
        public int? OrderId { get; }
        public int? CustomerId { get; }
        public string FailureReason { get; }

        private PlaceOrderResult(bool isSuccess, int? orderId, int? customerId, string failureReason)
        {
            IsSuccess = isSuccess;
            OrderId = orderId;
            CustomerId = customerId;
            FailureReason = failureReason;
        }

        public static PlaceOrderResult Success(int orderId)
            => new(true, orderId, null, null);

        public static PlaceOrderResult CustomerNotFound(int customerId)
            => new(false, null, customerId, $"Customer '{customerId}' not found.");

        public static PlaceOrderResult CartItemsNotFound()
            => new(false, null, null, "None of the provided cart items were found.");

        public static PlaceOrderResult CartItemsNotOwnedByUser()
            => new(false, null, null, "One or more cart items belong to a different user.");

        public static PlaceOrderResult PlacementFailed(int orderId)
            => new(false, orderId, null, "Order placement failed during handler fan-out.");
    }
}
