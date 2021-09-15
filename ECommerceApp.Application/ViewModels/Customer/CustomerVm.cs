using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Customer>
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCompany { get; set; }
        public string NIP { get; set; } // NIP contatins 11 numbers, can be null if private person order sth
        public string CompanyName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Customer, CustomerVm>()
                .ForMember(c => c.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(c => c.FirstName, opt => opt.MapFrom(src => src.FirstName))
                .ForMember(c => c.LastName, opt => opt.MapFrom(src => src.LastName))
                .ForMember(c => c.IsCompany, opt => opt.MapFrom(src => src.IsCompany))
                .ForMember(c => c.NIP, opt => opt.MapFrom(src => src.NIP))
                .ForMember(c => c.CompanyName, opt => opt.MapFrom(src => src.CompanyName))
                .ReverseMap()
                .ForAllOtherMembers(c => c.Ignore());
        }
    }
}
