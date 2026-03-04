namespace ECommerceApp.Application.Inventory.Availability.ViewModels
{
    public class StockItemVm
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
