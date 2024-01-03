using FluentValidation;

namespace ECommerceApp.Application.DTO
{
    public class AddOrderItemDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int ItemOrderQuantity { get; set; }
    }

    public class AddOrderItemDtoValidation : AbstractValidator<AddOrderItemDto>
    {
        public AddOrderItemDtoValidation()
        {
            RuleFor(x => x.ItemId).NotNull().GreaterThan(0);
            RuleFor(x => x.ItemOrderQuantity).NotNull().GreaterThan(0);
        }
    }
}
