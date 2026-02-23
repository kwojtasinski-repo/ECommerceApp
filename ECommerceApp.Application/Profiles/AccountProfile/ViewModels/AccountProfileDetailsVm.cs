using AutoMapper;
using ECommerceApp.Application.Mapping;
using System.Collections.Generic;

namespace ECommerceApp.Application.Profiles.AccountProfile.ViewModels
{
    public class AccountProfileDetailsVm : IMapFrom<global::ECommerceApp.Domain.Profiles.AccountProfile.AccountProfile>
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool IsCompany { get; set; }
        public string? NIP { get; set; }
        public string? CompanyName { get; set; }
        public List<AddressVm> Addresses { get; set; } = new();
        public List<ContactDetailVm> ContactDetails { get; set; } = new();

        public void Mapping(Profile profile)
        {
            profile.CreateMap<global::ECommerceApp.Domain.Profiles.AccountProfile.AccountProfile, AccountProfileDetailsVm>()
                .ForMember(d => d.Addresses, opt => opt.MapFrom(s => s.Addresses))
                .ForMember(d => d.ContactDetails, opt => opt.MapFrom(s => s.ContactDetails));
        }
    }
}
