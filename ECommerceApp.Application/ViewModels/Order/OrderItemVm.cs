using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderItemVm : BaseVm
    {
        public int ItemId { get; set; }   // 1:Many Item OrderItem  
        public int ItemOrderQuantity { get; set; }

        public NewOrderItemVm MapToNewOrderItemVm()
        {
            var orderItem = new NewOrderItemVm()
            {
                Id = this.Id,
                ItemId = this.ItemId,
                ItemOrderQuantity = this.ItemOrderQuantity,
                OrderId = null,
                UserId = ""
            };

            return orderItem;
        }

        public OrderItemForListVm MapToOrderItemForList()
        {
            var orderItem = new OrderItemForListVm()
            {
                Id = this.Id,
                ItemId = this.ItemId,
                ItemOrderQuantity = this.ItemOrderQuantity,
                OrderId = null,
                UserId = ""
            };

            return orderItem;
        }

        public class OrderItemVmValidation : AbstractValidator<OrderItemVm>
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
