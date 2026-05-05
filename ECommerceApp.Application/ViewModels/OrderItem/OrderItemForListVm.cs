
namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class OrderItemForListVm : BaseVm
    {
        public int ItemId { get; set; }
        public int ItemOrderQuantity { get; set; }
        public string UserId { get; set; }
        public int? OrderId { get; set; }
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; }
        public string ItemName { get; set; }
        public int ItemQuantityAvailable { get; set; }
        public string ItemBrand { get; set; }
        public string ItemType { get; set; }
        public decimal ItemCost { get; set; }
    }
}
