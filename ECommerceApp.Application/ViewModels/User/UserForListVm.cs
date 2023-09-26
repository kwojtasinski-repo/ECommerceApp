using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.ViewModels.User
{
    public class UserForListVm : IMapFrom<ApplicationUser>
    {
        public string Id { get; set; }
        public string UserName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ApplicationUser, UserForListVm>();
        }
    }
}
