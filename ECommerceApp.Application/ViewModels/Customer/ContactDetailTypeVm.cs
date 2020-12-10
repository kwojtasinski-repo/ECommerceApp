using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class ContactDetailTypeVm : IMapFrom<ECommerceApp.Domain.Model.ContactDetail>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ContactDetailType, ContactDetailTypeVm>();
        }
    }
}
