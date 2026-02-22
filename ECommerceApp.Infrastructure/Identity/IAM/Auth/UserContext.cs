using ECommerceApp.Application;
using ECommerceApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ECommerceApp.Infrastructure.Identity.IAM.Auth
{
    internal sealed class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string UserId => _httpContextAccessor.GetUserId();

        public string Role => _httpContextAccessor.GetUserRole();
    }
}
