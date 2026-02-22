using AutoMapper;
using ECommerceApp.Application.Mapping;
using Microsoft.AspNetCore.Identity;

namespace ECommerceApp.Application.Identity.IAM.ViewModels
{
    public class RoleVm : IMapFrom<IdentityRole>
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<IdentityRole, RoleVm>();
        }
    }
}
