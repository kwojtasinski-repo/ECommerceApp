using ECommerceApp.Application.Exceptions;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ECommerceApp.Web.Controllers
{
    public class BaseController : Controller
    {
        public readonly string[] ManagingRoles = new string[] { UserPermissions.Roles.Administrator, UserPermissions.Roles.Manager };
        public readonly string[] MaintenanceRoles = new string[] { UserPermissions.Roles.Administrator, UserPermissions.Roles.Manager, UserPermissions.Roles.Service };
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

        protected Dictionary<string, object> MapExceptionToResponseStatus(Exception exception)
        {
            if (exception is null)
            {
                throw new ArgumentNullException($"{nameof(exception)} is null");
            }

            if (exception is BusinessException businessException)
            {
                return new Dictionary<string, object> { { "Error", businessException.Message }, { "ErrorCode", businessException.ErrorCode }, { "Params", businessException.Arguments } };
            }

            return new Dictionary<string, object> { { "Error", exception.Message } };
        }
    }
}
