using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using FluentValidation;
using System;
using System.Text.Json.Serialization;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class PaymentVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public int Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public int OrderId { get; set; } // 1:1 Payment Order
        public int OrderNumber { get; set; }
        [JsonIgnore]
        public string CustomerName { get; set; }
        [JsonIgnore]
        public decimal OrderCost { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<PaymentVm, ECommerceApp.Domain.Model.Payment>().ReverseMap()
                .ForMember(o => o.OrderNumber, opt => opt.MapFrom(o => o.Order.Number))
                .ForMember(c => c.CustomerName, opt => opt.MapFrom(c => c.Customer.FirstName + " " +
                               c.Customer.LastName + " " + c.Customer.NIP + " " + c.Customer.CompanyName))
                //.ForMember(oc => oc.OrderCost, opt => opt.Ignore());
                .ForMember(oc => oc.OrderCost, opt => opt.MapFrom(o => o.Order.Cost));
        }
    }

    public class NewPaymentValidation : AbstractValidator<PaymentVm>
    {
        public NewPaymentValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull();
            RuleFor(x => x.DateOfOrderPayment).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}