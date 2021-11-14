using ECommerceApp.Application.ViewModels.OrderItem;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderDto
    {
        public int Id { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public string PromoCode { get; set; }
        public int CustomerId { get; set; }

        public ICollection<OrderItemsIdsVm> OrderItems { get; set; } // 1:Many relation

        public class OrderVmValidation : AbstractValidator<OrderDto>
        {
            public OrderVmValidation()
            {
                RuleFor(x => x.Id).NotNull();
                RuleFor(x => x.CustomerId).NotNull();

                When(x => x.OrderItems != null && x.OrderItems.Count > 0, () =>
                {
                    RuleForEach(oi => oi.OrderItems).SetValidator(new OrderItemsIdsVmValidation());
                });
            }
        }

        public class OrderItemsIdsVmValidation : AbstractValidator<OrderItemsIdsVm>
        {
            public OrderItemsIdsVmValidation()
            {
                RuleFor(x => x.Id).GreaterThan(0);
            }
        }
    }
}
