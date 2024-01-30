using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application;
using Microsoft.AspNetCore.Http;

namespace ECommerceApp.UnitTests.Common
{
    public class UserContextTest : IUserContext
    {
        public string UserId {  get; set; }

        public string Role {  get; set; }

        public UserContextTest()
        {
            
        }

        public UserContextTest(IHttpContextAccessor httpContextAccessor)
        {
            UserId = httpContextAccessor.GetUserId();
            Role = httpContextAccessor.GetUserRole();
        }
    }
}
