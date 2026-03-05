using System;

namespace ECommerceApp.Application.Inventory.Availability
{
    public sealed class InventoryOptions
    {
        public const string SectionName = "Inventory";

        public TimeSpan SoftHoldTtl { get; set; } = TimeSpan.FromMinutes(15);
    }
}
