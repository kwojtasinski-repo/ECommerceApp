using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderVm : BaseVm
    {
        public DateTime? Delivered { get; set; } = null;
        public bool IsDelivered { get; set; } = false;
        public int? CouponUsedId { get; set; } = null;
        public int CustomerId { get; set; }
        public int? PaymentId { get; set; } = null; // 1:1 Order Payment
        public bool IsPaid { get; set; } = false;
        public int? RefundId { get; set; } = null; // 1:1 Order Refund

        public ICollection<OrderItemsIdsVm> OrderItems { get; set; } // 1:Many relation

        public NewOrderVm MapToNewOrderVm()
        {
            var order = new NewOrderVm()
            {
                Id = this.Id,
                Cost = 0,
                Delivered = this.Delivered,
                IsDelivered = this.IsDelivered,
                CouponUsedId = this.CouponUsedId,
                CustomerId = this.CustomerId,
                PaymentId = this.PaymentId,
                IsPaid = this.IsPaid,
                RefundId = this.RefundId,
                OrderItems = new List<NewOrderItemVm>()
            };

            if (OrderItems != null && OrderItems.Count > 0)
            {
                var orderItems = new List<NewOrderItemVm>();
                foreach (var orderItem in OrderItems)
                {
                    var item = new NewOrderItemVm
                    {
                        Id = orderItem.Id
                    };
                    orderItems.Add(item);
                }

                order.OrderItems = orderItems;
            }

            return order;
        }

        public class OrderVmValidation : AbstractValidator<OrderVm>
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
