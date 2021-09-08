﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class ContactDetailVm : BaseVm, IMapFrom<ContactDetailsForListVm>
    {
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public int CustomerId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ContactDetailsForListVm, ContactDetailVm>()
                .ForMember(c => c.Id, opt => opt.MapFrom(co => co.Id))
                .ForMember(c => c.ContactDetailInformation, opt => opt.MapFrom(co => co.ContactDetailInformation))
                .ForMember(c => c.ContactDetailTypeId, opt => opt.MapFrom(co => co.ContactDetailTypeId))
                .ForMember(c => c.CustomerId, opt => opt.MapFrom(co => co.CustomerId));
        }

        public NewContactDetailVm MapToNewContactDetail()
        {
            var contact = new NewContactDetailVm()
            {
                Id = this.Id,
                ContactDetailInformation = this.ContactDetailInformation,
                ContactDetailTypeId = this.ContactDetailTypeId,
                CustomerId = this.CustomerId
            };

            return contact;
        }
    }

    public class ContactDetailValidation : AbstractValidator<ContactDetailVm>
    {
        public ContactDetailValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.ContactDetailTypeId).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
