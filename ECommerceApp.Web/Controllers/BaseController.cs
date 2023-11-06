using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;

namespace ECommerceApp.Web.Controllers
{
    public class BaseController : Controller
    {
        public readonly string[] ManagingPermissions = new string[] { UserPermissions.Roles.Administrator, UserPermissions.Roles.Manager };
        public readonly string[] MaintenancePermissions = new string[] { UserPermissions.Roles.Administrator, UserPermissions.Roles.Manager, UserPermissions.Roles.Service };

        protected Claim GetUserId()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }

            return userId;
        }

        protected Claim GetUserRole() 
        {
            var role = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            return role;
        }
    }
}
