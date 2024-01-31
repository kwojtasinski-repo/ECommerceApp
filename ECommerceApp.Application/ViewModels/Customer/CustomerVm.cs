using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerVm : IMapFrom<Domain.Model.Customer>
    {
        public CustomerDto Customer { get; set; }
        public List<ContactDetailDto> ContactDetails { get; set; } = new List<ContactDetailDto>();
        public List<ContactDetailTypeDto> ContactDetailTypes { get; set; } = new List<ContactDetailTypeDto>();
        public virtual List<AddressDto> Addresses { get; set; } = new List<AddressDto>();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<CustomerVm, Domain.Model.Customer>().ReverseMap()
                .ForMember(p => p.ContactDetails, opt => opt.MapFrom(ps => ps.ContactDetails))
                .ForMember(p => p.Addresses, opt => opt.MapFrom(ps => ps.Addresses));
        }
    }

    public class CustomerVmValidation : AbstractValidator<CustomerVm>
    {
        public CustomerVmValidation()
        {
            RuleFor(x => x.Customer).SetValidator(new CustomerDtoValidator());
        }
    }
}
