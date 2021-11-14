using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class OrderItemDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }   // 1:Many Item OrderItem  
        public int ItemOrderQuantity { get; set; }

        public class OrderItemVmValidation : AbstractValidator<OrderItemDto>
        {
            public OrderItemVmValidation()
            {
                RuleFor(x => x.Id).NotNull();
                RuleFor(x => x.ItemId).NotNull();
                RuleFor(x => x.ItemOrderQuantity).NotNull();
            }
        }
    }
}
