using ECommerceApp.Application.Permissions;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Security.Claims;

namespace ECommerceApp.API.Controllers
{
    [ApiController]
    public class BaseController : ControllerBase
    {
        public const string ManagingRole = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}";
        public const string MaintenanceRole = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}";

        protected string GetUserId()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }

            return userId?.Value ?? "";
        }

        protected string GetUserRole()
        {
            var role = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            return role?.Value ?? "";
        }
    }
}
