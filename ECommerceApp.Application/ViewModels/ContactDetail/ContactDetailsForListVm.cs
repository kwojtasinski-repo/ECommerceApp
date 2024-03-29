﻿using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.ContactDetail
{
    public class ContactDetailsForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.ContactDetail>
    {
        public string ContactDetailInformation { get; set; }
        public int ContactDetailTypeId { get; set; }
        public string ContactDetailTypeName { get; set; }
        public int CustomerId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.ContactDetail, ContactDetailsForListVm>()
                .ForMember(c => c.Id, opt => opt.MapFrom(c => c.Id))
                .ForMember(c => c.ContactDetailInformation, opt => opt.MapFrom(c => c.ContactDetailInformation))
                .ForMember(c => c.ContactDetailTypeName, opt => opt.MapFrom(c => c.ContactDetailType.Name))
                .ForMember(c => c.ContactDetailTypeId, opt => opt.MapFrom(c => c.ContactDetailTypeId))
                .ForMember(c => c.CustomerId, opt => opt.MapFrom(c => c.CustomerId));
        }
    }
}
