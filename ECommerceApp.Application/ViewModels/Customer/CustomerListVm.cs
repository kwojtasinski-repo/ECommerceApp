using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.ContactDetail;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerListVm : BaseVm, IMapFrom<CustomerDetailsVm>
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }

        public virtual ICollection<ContactDetailVm> ContactDetails { get; set; }
        public virtual ICollection<AddressVm> Addresses { get; set; }
        public virtual ICollection<CustomerOrdersVm> Orders { get; set; }
        public ICollection<CustomerPaymentsVm> Payments { get; set; }
        public ICollection<CustomerRefundsVm> Refunds { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<CustomerDetailsVm, CustomerListVm>()
                .ForMember(c => c.Id, opt => opt.MapFrom(cust => cust.Id))
                .ForMember(c => c.UserId, opt => opt.MapFrom(cust => cust.UserId))
                .ForMember(c => c.FirstName, opt => opt.MapFrom(cust => cust.FirstName))
                .ForMember(c => c.LastName, opt => opt.MapFrom(cust => cust.LastName))
                .ForMember(c => c.IsCompany, opt => opt.MapFrom(cust => cust.IsCompany))
                .ForMember(c => c.NIP, opt => opt.MapFrom(cust => cust.NIP))
                .ForMember(c => c.CompanyName, opt => opt.MapFrom(cust => cust.CompanyName))
                .ForMember(c => c.ContactDetails, opt => opt.MapFrom(cust => cust.ContactDetails))
                .ForMember(c => c.Addresses, opt => opt.MapFrom(cust => cust.Addresses))
                .ForMember(c => c.Orders, opt => opt.MapFrom(cust => cust.Orders))
                .ForMember(c => c.Payments, opt => opt.MapFrom(cust => cust.Payments))
                .ForMember(c => c.Refunds, opt => opt.MapFrom(cust => cust.Refunds));
        }
    }
}
