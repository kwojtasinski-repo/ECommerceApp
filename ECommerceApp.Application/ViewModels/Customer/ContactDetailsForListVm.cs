using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class ContactDetailsForListVm : IMapFrom<ECommerceApp.Domain.Model.ContactDetail>
    {
        public int Id { get; set; }
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public string ContactDetailTypeName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ContactDetail, ContactDetailsForListVm>()
                .ForMember(c => c.ContactDetailTypeName, opt => opt.MapFrom(c => c.ContactDetailType.Name));
        }
    }

    public class ContactDetailsForListValidation : AbstractValidator<ContactDetailsForListVm>
    {
        public ContactDetailsForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.ContactDetailInformation).NotNull();
            RuleFor(x => x.ContactDetailTypeId).NotNull();
        }
    }
}
