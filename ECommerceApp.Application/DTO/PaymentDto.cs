using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels;
using FluentValidation;
using System;

namespace ECommerceApp.Application.DTO
{
    public class PaymentDto : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public int Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public int OrderId { get; set; } // 1:1 Payment Order
        public int OrderNumber { get; set; }
        public int CurrencyId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<PaymentDto, ECommerceApp.Domain.Model.Payment>().ReverseMap();
        }
    }

    public class PaymentDtoValidation : AbstractValidator<PaymentDto>
    {
        public PaymentDtoValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull().GreaterThan(0);
            RuleFor(x => x.DateOfOrderPayment).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}
