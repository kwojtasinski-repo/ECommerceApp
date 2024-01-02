using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;

namespace ECommerceApp.Application.DTO
{
    public class PaymentDto : IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }
        public decimal Cost { get; set; }
        public string State { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Payment, PaymentDto>()
                .ForMember(p => p.CurrencyName, src => src.MapFrom(p => p.Currency.Code))
                .ForMember(p => p.State, src => src.MapFrom(p => p.State.ToString()));
        }
    }

    public class PaymentDtoValidation : AbstractValidator<PaymentDto>
    {
        public PaymentDtoValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull().NotEmpty();
            RuleFor(x => x.DateOfOrderPayment).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}