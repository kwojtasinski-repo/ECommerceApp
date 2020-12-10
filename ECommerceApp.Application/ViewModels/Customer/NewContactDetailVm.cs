﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class NewContactDetailVm : IMapFrom<ECommerceApp.Domain.Model.ContactDetail>
    {
        public int Id { get; set; }
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public string ContactDetailTypeName { get; set; }
        public int CustomerId { get; set; }
        public List<ContactDetailTypeVm> ContactDetailTypes { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewContactDetailVm, ECommerceApp.Domain.Model.ContactDetail>().ReverseMap()
                .ForMember(s => s.ContactDetailTypeName, opt => opt.MapFrom(d => d.ContactDetailType.Name));
        }
    }
}
