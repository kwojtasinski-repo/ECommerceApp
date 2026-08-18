using System;
using System.Collections.Concurrent;
using ECommerceApp.Application.Catalog.Products.Messages;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using FulfillmentRefundApproved = ECommerceApp.Application.Sales.Fulfillment.Messages.RefundApproved;
using FulfillmentRefundRejected = ECommerceApp.Application.Sales.Fulfillment.Messages.RefundRejected;

namespace ECommerceApp.Application.Messaging
{
    /// <summary>
    /// Explicit, reviewable registry mapping outbox message types to short, stable string keys
    /// used as <c>OutboxMessage.MessageTypeKey</c>. Deliberately NOT reflection/assembly-scan based.
    /// </summary>
    public static class MessageTypeRegistry
    {
        // ConcurrentDictionary: tests call Register(...) at run time (see MessageTypeRegistryTests,
        // OutboxDispatcherTests) while xUnit may run other test classes' KeyFor/TypeFor lookups
        // concurrently on other threads. A plain Dictionary is not thread-safe for concurrent
        // read/write and would risk corruption or spurious exceptions under parallel test execution.
        private static readonly ConcurrentDictionary<Type, string> KeysByType = new();
        private static readonly ConcurrentDictionary<string, Type> TypesByKey = new();

        static MessageTypeRegistry()
        {
            // Register message types as retrofits are applied in Phase 3.
            // Keys are short, stable strings used in OutboxMessage.MessageTypeKey.
            Register(typeof(ProductUpdated), "catalog.product.updated");
            Register(typeof(ProductPublished), "catalog.product.published");
            Register(typeof(ProductUnpublished), "catalog.product.unpublished");
            Register(typeof(PaymentConfirmed), "payments.payment.confirmed");
            Register(typeof(PaymentExpired), "payments.payment.expired");
            Register(typeof(CouponApplied), "coupons.coupon.applied");
            Register(typeof(OrderPriceAdjusted), "coupons.order-price.adjusted");
            Register(typeof(CouponRemovedFromOrder), "coupons.coupon.removed-from-order");
            Register(typeof(FulfillmentRefundApproved), "fulfillment.refund.approved");
            Register(typeof(FulfillmentRefundRejected), "fulfillment.refund.rejected");
            Register(typeof(RefundStockReturned), "fulfillment.refund.stock-returned");
            Register(typeof(RefundCustomerNotified), "fulfillment.refund.customer-notified");
            Register(typeof(ShipmentDispatched), "fulfillment.shipment.dispatched");
            Register(typeof(ShipmentDelivered), "fulfillment.shipment.delivered");
            Register(typeof(ShipmentFailed), "fulfillment.shipment.failed");
            Register(typeof(ShipmentPartiallyDelivered), "fulfillment.shipment.partially-delivered");
            Register(typeof(StockAvailabilityChanged), "inventory.stock.availability-changed");
            Register(typeof(StockReconciliationRequired), "inventory.stock.reconciliation-required");
            Register(typeof(OrderPlaced), "orders.order.placed");
            Register(typeof(OrderPlacementFailed), "orders.order.placement-failed");
            Register(typeof(OrderShipped), "orders.order.shipped");
            Register(typeof(OrderCancelled), "orders.order.cancelled");
        }

        internal static void Register(Type messageType, string key)
        {
            if (!KeysByType.TryAdd(messageType, key))
            {
                throw new ArgumentException(
                    $"Message type '{messageType.FullName}' is already registered in {nameof(MessageTypeRegistry)}.",
                    nameof(messageType));
            }

            if (!TypesByKey.TryAdd(key, messageType))
            {
                throw new ArgumentException(
                    $"Outbox message type key '{key}' is already registered in {nameof(MessageTypeRegistry)}.",
                    nameof(key));
            }
        }

        public static string KeyFor(Type messageType)
        {
            if (KeysByType.TryGetValue(messageType, out var key))
                return key;

            throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' is not registered in {nameof(MessageTypeRegistry)}. " +
                "Add an explicit Register(...) entry before publishing this type via the outbox.");
        }

        public static Type TypeFor(string key)
        {
            if (TypesByKey.TryGetValue(key, out var type))
                return type;

            throw new InvalidOperationException(
                $"Outbox message type key '{key}' is not registered in {nameof(MessageTypeRegistry)}. " +
                "This indicates a deploy-order mismatch or a removed message type — investigate, do not swallow.");
        }
    }
}
