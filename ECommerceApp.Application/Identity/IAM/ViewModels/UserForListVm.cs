using ECommerceApp.Domain.Identity.IAM;

namespace ECommerceApp.Application.Identity.IAM.ViewModels
{
    public class UserForListVm
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }

        public static UserForListVm FromDomain(ApplicationUser s) => new()
        {
            Id = s.Id,
            UserName = s.UserName,
            Email = s.Email
        };
    }
}
