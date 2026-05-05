
namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class OrderItemDetailsVm : BaseVm
    {
        public int ItemId { get; set; }
        public int ItemOrderQuantity { get; set; }
        public string UserId { get; set; }
        public int? OrderId { get; set; }
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; }
        public string ItemName { get; set; }
        public decimal ItemCost { get; set; }
    }
}
