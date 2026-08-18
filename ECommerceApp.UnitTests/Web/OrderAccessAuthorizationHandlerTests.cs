using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AwesomeAssertions;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Web.Areas.Presale.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ECommerceApp.UnitTests.Web
{
    public class OrderAccessAuthorizationHandlerTests
    {
        private const string GuestScheme = "GuestAccess";

        [Fact]
        public async Task MaintenanceRole_BypassesOwnership()
        {
            var result = await AuthorizeAsync(
                new[] { new Claim(ClaimTypes.Role, UserPermissions.Roles.Manager) },
                new OrderAccessResource(7, "different-user"));

            result.Should().BeTrue();
        }

        [Fact]
        public async Task RegisteredUser_OwnsOrder_Succeeds()
        {
            var result = await AuthorizeAsync(
                new[] { new Claim(ClaimTypes.NameIdentifier, "user-42") },
                new OrderAccessResource(7, "user-42"));

            result.Should().BeTrue();
        }

        [Fact]
        public async Task RegisteredUser_DoesNotOwnOrder_Fails()
        {
            var result = await AuthorizeAsync(
                new[] { new Claim(ClaimTypes.NameIdentifier, "user-42") },
                new OrderAccessResource(7, "user-99"));

            result.Should().BeFalse();
        }

        [Fact]
        public async Task GuestAccess_MatchingUserAndOrderClaims_Succeeds()
        {
            var result = await AuthorizeAsync(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "guest-42"),
                    new Claim("OrderAccessOrderId", "7")
                },
                new OrderAccessResource(7, "guest-42"),
                GuestScheme);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task GuestAccess_DifferentOrderClaim_Fails()
        {
            var result = await AuthorizeAsync(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "guest-42"),
                    new Claim("OrderAccessOrderId", "7")
                },
                new OrderAccessResource(8, "guest-42"),
                GuestScheme);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task GuestAccess_DifferentUserClaim_Fails()
        {
            var result = await AuthorizeAsync(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "guest-42"),
                    new Claim("OrderAccessOrderId", "7")
                },
                new OrderAccessResource(7, "guest-99"),
                GuestScheme);

            result.Should().BeFalse();
        }

        private static async Task<bool> AuthorizeAsync(
            IEnumerable<Claim> claims,
            OrderAccessResource resource,
            string authenticationType = "Identity.Application")
        {
            var identity = new ClaimsIdentity(claims, authenticationType);
            var user = new ClaimsPrincipal(identity);
            var context = new AuthorizationHandlerContext(
                new[] { new OrderAccessRequirement() },
                user,
                resource);

            await new OrderAccessAuthorizationHandler().HandleAsync(context);
            return context.HasSucceeded;
        }
    }
}