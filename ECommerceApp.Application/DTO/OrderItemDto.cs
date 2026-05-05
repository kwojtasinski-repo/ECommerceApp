using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class OrderItemDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal ItemCost { get; set; }
        public int ItemOrderQuantity { get; set; }
        public string UserId { get; set; }
        public int? OrderId { get; set; }
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; }

        public class OrderItemDtoValidation : AbstractValidator<OrderItemDto>
        {
            public OrderItemDtoValidation()
            {
                RuleFor(x => x.Id).NotNull();
                RuleFor(x => x.ItemId).NotNull().GreaterThan(0);
                RuleFor(x => x.ItemOrderQuantity).NotNull().GreaterThan(0);
                RuleFor(x => x.UserId).NotNull().NotEmpty();
            }
        }
    }
}
