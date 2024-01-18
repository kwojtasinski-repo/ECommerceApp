using ECommerceApp.Application.DTO;
using FluentValidation;
using System.Collections.Generic;
using static ECommerceApp.Application.DTO.OrderItemDto;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class NewOrderItemVm
    {
        public OrderItemDto OrderItem { get; set; } = new ();

        public List<ItemInfoVm> Items { get; set; } = new ();
    }

    public class NewOrderItemValidation : AbstractValidator<NewOrderItemVm>
    {
        public NewOrderItemValidation()
        {
            RuleFor(x => x.OrderItem).NotNull().SetValidator(new OrderItemDtoValidation());
        }
    }
}
