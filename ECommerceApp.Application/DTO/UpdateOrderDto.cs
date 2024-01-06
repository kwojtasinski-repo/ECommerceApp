using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.DTO
{
    public class UpdateOrderDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int? CouponUsedId { get; set; }
        public PaymentInfoDto Payment { get; set; }
        public bool IsDelivered { get; set; }

        public ICollection<AddOrderItemDto> OrderItems { get; set; }

        public string OrderNumber { get; set; }
        public string PromoCode { get; set; }
        public DateTime? Ordered { get; set; }
    }
    
    public class PaymentInfoDto
    {
        public int Id { get; set; }
        public int CurrencyId { get; set; }
    }

    public class UpdateOrderDtoValidation : AbstractValidator<UpdateOrderDto>
    {
        public UpdateOrderDtoValidation()
        {
            RuleFor(x => x.Id).NotNull().GreaterThan(0);
            RuleFor(x => x.CustomerId).NotNull().GreaterThan(0);
            RuleFor(x => x.CouponUsedId).NotNull().GreaterThan(0);

            When(x => x.OrderItems != null && x.OrderItems.Any(), () =>
            {
                RuleForEach(oi => oi.OrderItems).SetValidator(new UpdateOrderItemDtoValidation());
            });
            When(x => x.Ordered is not null, () =>
            {
                RuleFor(o => o.Ordered).GreaterThan(new DateTime());
            });
        }
    }

    public class UpdateOrderItemDtoValidation : AbstractValidator<AddOrderItemDto>
    {
        public UpdateOrderItemDtoValidation()
        {
            RuleFor(x => x.Id).NotNull();

            When(x => x.Id == 0, () =>
            {
                RuleFor(oi => oi.ItemId).NotNull().GreaterThan(0);
                RuleFor(oi => oi.ItemOrderQuantity).NotNull().GreaterThan(0);
            });
        }
    }
}
