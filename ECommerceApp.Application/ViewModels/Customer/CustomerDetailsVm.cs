using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerDetailsVm : IMapFrom<ECommerceApp.Domain.Model.Customer>
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }

        public virtual List<ContactDetailsForListVm> ContactDetails { get; set; }
        public virtual List<AddressDetailVm> Addresses { get; set; }
        public virtual List<ECommerceApp.Domain.Model.Order> Orders { get; set; }
        public virtual List<ECommerceApp.Domain.Model.OrderItem> OrderItems { get; set; }
        public List<Payment> Payments { get; set; }
        public List<Refund> Refunds { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Customer, CustomerDetailsVm>()
                .ForMember(c => c.Addresses, opt => opt.MapFrom(c => c.Addresses))
                .ReverseMap();
        }

        public class CustomerDetailsValidation : AbstractValidator<CustomerDetailsVm>
        {
            public CustomerDetailsValidation()
            {
                RuleFor(x => x.Id).NotNull();
                RuleFor(x => x.FirstName).NotNull();
                RuleFor(x => x.LastName).NotNull();
                RuleFor(x => x.IsCompany).NotNull();
                RuleFor(x => x.NIP).Length(9);
                RuleFor(x => x.CompanyName).MaximumLength(100);
            }
        }
    }
}
