using FluentValidation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceApp.Application.DTO
{
    public class AddOrderDto
    {
        public int Id { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public string PromoCode { get; set; }
        public int CustomerId { get; set; }

        public ICollection<OrderItemsIdsDto> OrderItems { get; set; }
    }

    public class OrderItemsIdsDto
    {
        public int Id { get; set; }
    }

    public class AddOrderDtoValidation : AbstractValidator<AddOrderDto>
    {
        public AddOrderDtoValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.CustomerId).NotNull();

            When(x => x.OrderItems != null && x.OrderItems.Count > 0, () =>
            {
                RuleForEach(oi => oi.OrderItems).SetValidator(new OrderItemsIdsDtoValidation());
            });
        }
    }

    public class OrderItemsIdsDtoValidation : AbstractValidator<OrderItemsIdsDto>
    {
        public OrderItemsIdsDtoValidation()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }
}
