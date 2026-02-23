using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.AccountProfile.ViewModels
{
    public class UserProfileForListVm : IMapFrom<global::ECommerceApp.Domain.AccountProfile.UserProfile>
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool IsCompany { get; set; }
        public string? CompanyName { get; set; }
        public string Email { get; set; } = default!;

        public void Mapping(Profile profile)
        {
            profile.CreateMap<global::ECommerceApp.Domain.AccountProfile.UserProfile, UserProfileForListVm>();
        }
    }
}
