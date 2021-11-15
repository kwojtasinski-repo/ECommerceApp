using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class ContactDetailTypeVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.ContactDetail>
    {
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ContactDetailType, ContactDetailTypeVm>();
        }
    }

    public class ContactDetailTypeValidation : AbstractValidator<ContactDetailTypeVm>
    {
        public ContactDetailTypeValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Name).NotNull();
        }
    }
}
