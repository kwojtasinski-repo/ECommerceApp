using Microsoft.AspNetCore.Identity;
using System;

namespace ECommerceApp.Domain.Identity.IAM
{
    public class ApplicationUser : IdentityUser<string>
    {
        public ApplicationUser()
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}
