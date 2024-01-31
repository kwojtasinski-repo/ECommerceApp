using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.ContactDetail;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerDetailsVm : IMapFrom<ECommerceApp.Domain.Model.Customer>
    {
        public CustomerDto Customer { get; set; }
        public virtual List<ContactDetailsForListVm> ContactDetails { get; set; } = new List<ContactDetailsForListVm>();
        public virtual List<AddressDto> Addresses { get; set; } = new List<AddressDto>();
        public virtual List<CustomerOrdersVm> Orders { get; set; } = new List<CustomerOrdersVm>();
        public List<CustomerPaymentsVm> Payments { get; set; } = new List<CustomerPaymentsVm>();
        public List<CustomerRefundsVm> Refunds { get; set; } = new List<CustomerRefundsVm>();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Customer, CustomerDetailsVm>()
                .ForMember(c => c.Customer, opt => opt.MapFrom(c => c))
                .ForMember(c => c.Addresses, opt => opt.MapFrom(c => c.Addresses))
                .ForMember(c => c.ContactDetails, opt => opt.MapFrom(c => c.ContactDetails))
                .ForMember(c => c.Orders, opt => opt.MapFrom(c => c.Orders))
                .ForMember(c => c.Payments, opt => opt.MapFrom(c => c.Payments))
                .ForMember(c => c.Refunds, opt => opt.MapFrom(c => c.Refunds))
                .ReverseMap();
        }
    }
}
