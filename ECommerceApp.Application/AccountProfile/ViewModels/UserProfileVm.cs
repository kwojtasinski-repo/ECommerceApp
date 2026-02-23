using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.AccountProfile.ViewModels
{
    public class UserProfileVm : IMapFrom<global::ECommerceApp.Domain.AccountProfile.UserProfile>
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public bool IsCompany { get; set; }
        public string? NIP { get; set; }
        public string? CompanyName { get; set; }
        public string Email { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;

        public void Mapping(Profile profile)
        {
            profile.CreateMap<global::ECommerceApp.Domain.AccountProfile.UserProfile, UserProfileVm>();
        }
    }
}
