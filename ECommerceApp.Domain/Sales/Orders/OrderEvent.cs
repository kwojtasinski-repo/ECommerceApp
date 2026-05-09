using System;

namespace ECommerceApp.Domain.Sales.Orders
{
    public class OrderEvent
    {
        public OrderEventId Id { get; private set; }
        public OrderId OrderId { get; private set; }
        public OrderEventType EventType { get; private set; }
        public string Payload { get; private set; }
        public DateTime OccurredAt { get; private set; }

        private OrderEvent() { }

        internal OrderEvent(OrderId orderId, OrderEventType eventType, string payload = null)
        {
            OrderId = orderId;
            EventType = eventType;
            Payload = payload;
            OccurredAt = DateTime.UtcNow;
        }
    }
}
