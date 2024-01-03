using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Text.Json.Serialization;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class PaymentVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public string Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public int OrderNumber { get; set; }
        public int CurrencyId { get; set; }
        [JsonIgnore]
        public string CustomerName { get; set; }
        [JsonIgnore]
        public decimal Cost { get; set; }
        [JsonIgnore]
        public string CurrencyName { get; set; }
        public Domain.Model.PaymentState State { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<PaymentVm, ECommerceApp.Domain.Model.Payment>()
                .ForMember(p => p.DateOfOrderPayment, opt => opt.MapFrom(p => SetFormat(p.DateOfOrderPayment)))
                .ReverseMap()
                .ForMember(o => o.OrderNumber, opt => opt.MapFrom(o => o.Order.Number))
                .ForMember(c => c.CustomerName, opt => opt.MapFrom(c => (c.Customer.NIP != null && c.Customer.CompanyName != null) ?
                            c.Customer.FirstName + " " + c.Customer.LastName + " " + c.Customer.NIP + " " + c.Customer.CompanyName
                            : c.Customer.FirstName + " " + c.Customer.LastName))
                .ForMember(p => p.CurrencyName, opt => opt.MapFrom(c => c.Currency.Code));
        }

        private static DateTime SetFormat(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
        }
    }

    public class NewPaymentValidation : AbstractValidator<PaymentVm>
    {
        public NewPaymentValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull().NotEmpty();
            RuleFor(x => x.DateOfOrderPayment).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}