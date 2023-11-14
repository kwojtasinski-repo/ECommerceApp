using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.ContactDetail;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class NewCustomerVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Customer>
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }

        public virtual List<NewContactDetailVm> ContactDetails { get; set; }
        public virtual List<AddressDto> Addresses { get; set; }

        public string AnnonymousUserName { get; set; }
        public string AnnonymousUserPassword { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewCustomerVm, ECommerceApp.Domain.Model.Customer>().ReverseMap()
                .ForMember(p => p.ContactDetails, opt => opt.MapFrom(ps => ps.ContactDetails))
                .ForMember(p => p.Addresses, opt => opt.MapFrom(ps => ps.Addresses));
        }
    }

    public class NewCustomerValidation : AbstractValidator<NewCustomerVm>
    {
        public NewCustomerValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.FirstName).NotNull();
            RuleFor(x => x.LastName).NotNull();
            RuleFor(x => x.IsCompany).NotNull();
            RuleFor(x => x.NIP).Length(9);
            RuleFor(x => x.CompanyName).NotNull();
        }
    }
}
