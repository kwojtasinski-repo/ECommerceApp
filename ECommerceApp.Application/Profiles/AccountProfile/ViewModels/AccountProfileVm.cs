using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.Profiles.AccountProfile.ViewModels
{
    public class AccountProfileVm : IMapFrom<global::ECommerceApp.Domain.Profiles.AccountProfile.AccountProfile>
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool IsCompany { get; set; }
        public string? NIP { get; set; }
        public string? CompanyName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<global::ECommerceApp.Domain.Profiles.AccountProfile.AccountProfile, AccountProfileVm>();
        }
    }
}
