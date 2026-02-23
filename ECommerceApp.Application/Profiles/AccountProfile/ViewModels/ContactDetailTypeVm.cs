using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.Profiles.AccountProfile.ViewModels
{
    public class ContactDetailTypeVm : IMapFrom<global::ECommerceApp.Domain.Profiles.AccountProfile.ContactDetailType>
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;

        public void Mapping(Profile profile)
        {
            profile.CreateMap<global::ECommerceApp.Domain.Profiles.AccountProfile.ContactDetailType, ContactDetailTypeVm>();
        }
    }
}
