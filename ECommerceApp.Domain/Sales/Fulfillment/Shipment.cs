using ECommerceApp.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public class Shipment
    {
        public ShipmentId Id { get; private set; } = default!;
        public int OrderId { get; private set; }
        public string? TrackingNumber { get; private set; }
        public ShipmentStatus Status { get; private set; }
        public DateTime? ShippedAt { get; private set; }
        public DateTime? DeliveredAt { get; private set; }

        private readonly List<ShipmentLine> _lines = new();
        public IReadOnlyList<ShipmentLine> Lines => _lines.AsReadOnly();

        private Shipment() { }

        public static Shipment Create(int orderId, IEnumerable<ShipmentLine> lines)
        {
            if (orderId <= 0)
                throw new DomainException("OrderId must be positive.");

            var lineList = lines?.ToList() ?? throw new DomainException("Lines are required.");
            if (!lineList.Any())
                throw new DomainException("At least one line is required.");

            var shipment = new Shipment
            {
                OrderId = orderId,
                Status = ShipmentStatus.Pending
            };
            shipment._lines.AddRange(lineList);
            return shipment;
        }

        public void MarkAsInTransit(string trackingNumber)
        {
            if (Status != ShipmentStatus.Pending)
                throw new DomainException($"Shipment '{Id?.Value}' is not in Pending status.");

            if (string.IsNullOrWhiteSpace(trackingNumber))
                throw new DomainException("Tracking number is required.");

            TrackingNumber = trackingNumber;
            Status = ShipmentStatus.InTransit;
            ShippedAt = DateTime.UtcNow;
        }

        public void MarkAsDelivered()
        {
            if (Status != ShipmentStatus.InTransit)
                throw new DomainException($"Shipment '{Id?.Value}' is not in transit.");

            Status = ShipmentStatus.Delivered;
            DeliveredAt = DateTime.UtcNow;
        }

        public void MarkAsFailed()
        {
            if (Status is not (ShipmentStatus.Pending or ShipmentStatus.InTransit))
                throw new DomainException($"Shipment '{Id?.Value}' cannot fail from status '{Status}'.");

            Status = ShipmentStatus.Failed;
        }

        public void MarkAsPartiallyDelivered(IReadOnlyList<int> deliveredProductIds)
        {
            if (Status != ShipmentStatus.InTransit)
                throw new DomainException($"Shipment '{Id?.Value}' is not in transit.");

            if (deliveredProductIds is null || !deliveredProductIds.Any())
                throw new DomainException("At least one delivered product is required.");

            Status = ShipmentStatus.PartiallyDelivered;
            DeliveredAt = DateTime.UtcNow;
        }
    }
}
