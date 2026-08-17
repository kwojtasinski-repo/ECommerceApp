using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace ECommerceApp.Web.Areas.Presale.Authorization
{
    public interface IOrderAccessAuthorizer
    {
        Task<bool> AuthorizeAsync(
            ClaimsPrincipal user,
            OrderAccessResource resource,
            CancellationToken cancellationToken = default);
    }

    internal sealed class OrderAccessAuthorizer : IOrderAccessAuthorizer
    {
        private readonly IAuthorizationService _authorizationService;

        public OrderAccessAuthorizer(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        public async Task<bool> AuthorizeAsync(
            ClaimsPrincipal user,
            OrderAccessResource resource,
            CancellationToken cancellationToken = default)
        {
            var result = await _authorizationService.AuthorizeAsync(
                user,
                resource,
                "OrderAccess");
            return result.Succeeded;
        }
    }
}