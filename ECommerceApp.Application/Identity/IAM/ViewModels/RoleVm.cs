using Microsoft.AspNetCore.Identity;

namespace ECommerceApp.Application.Identity.IAM.ViewModels
{
    public class RoleVm
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public static RoleVm FromDomain(IdentityRole s) => new()
        {
            Id = s.Id,
            Name = s.Name
        };
    }
}
