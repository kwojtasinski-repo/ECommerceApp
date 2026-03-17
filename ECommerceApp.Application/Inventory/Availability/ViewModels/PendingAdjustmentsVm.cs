using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Inventory.Availability.ViewModels
{
    public sealed class PendingAdjustmentsVm
    {
        public IReadOnlyList<PendingAdjustmentRowVm> Items { get; init; } = new List<PendingAdjustmentRowVm>();
    }

    public sealed class PendingAdjustmentRowVm
    {
        public int ProductId { get; init; }
        public string ProductName { get; init; } = "";
        public int NewQuantity { get; init; }
        public DateTime SubmittedAt { get; init; }
        public Guid Version { get; init; }
    }
}
