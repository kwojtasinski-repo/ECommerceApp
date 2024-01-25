using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;

namespace ECommerceApp.Application.DTO
{
    public class PaymentDetailsDto : IMapFrom<Domain.Model.Payment>
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public decimal Cost { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Domain.Model.Payment, PaymentDetailsDto>()
                .ForMember(p => p.Status, opt => opt.MapFrom(src => src.State.ToString()))
                .ForMember(p => p.FirstName, opt => opt.MapFrom(src => src.Customer.FirstName))
                .ForMember(p => p.LastName, opt => opt.MapFrom(src => src.Customer.LastName))
                .ForMember(p => p.OrderNumber, opt => opt.MapFrom(src => src.Order.Number))
                .ForMember(p => p.CurrencyName, opt => opt.MapFrom(src => src.Currency.Code));
        }
    }
}
