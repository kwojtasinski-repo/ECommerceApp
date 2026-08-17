using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace ECommerceApp.Web.Areas.Presale.Authorization
{
    public sealed class OrderAccessAuthorizationHandler
        : AuthorizationHandler<OrderAccessRequirement, OrderAccessResource>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OrderAccessRequirement requirement,
            OrderAccessResource resource)
        {
            if (HasMaintenanceRole(context.User))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var callerId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(callerId) || callerId != resource.UserId)
                return Task.CompletedTask;

            if (context.User.Identity?.AuthenticationType == GuestAccessDefaults.AuthenticationScheme)
            {
                var scopedOrderId = context.User.FindFirstValue(GuestAccessDefaults.OrderIdClaim);
                if (scopedOrderId != resource.OrderId.ToString())
                    return Task.CompletedTask;
            }

            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        private static bool HasMaintenanceRole(ClaimsPrincipal user)
        {
            return user.IsInRole(UserPermissions.Roles.Administrator)
                || user.IsInRole(UserPermissions.Roles.Manager)
                || user.IsInRole(UserPermissions.Roles.Service);
        }
    }
}