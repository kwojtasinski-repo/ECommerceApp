using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.Profiles.AccountProfile.ViewModels
{
    public class ContactDetailVm : IMapFrom<global::ECommerceApp.Domain.Profiles.AccountProfile.ContactDetail>
    {
        public int Id { get; set; }
        public string Information { get; set; } = default!;
        public int ContactDetailTypeId { get; set; }
        public int AccountProfileId { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<global::ECommerceApp.Domain.Profiles.AccountProfile.ContactDetail, ContactDetailVm>();
        }
    }
}
