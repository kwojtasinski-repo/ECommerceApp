using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Permissions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

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
                var errorModel = BuildErrorModel(businessException.ErrorCode, businessException.Arguments);
                return errorModel.AsDictionaryObject();
            }

            return new Dictionary<string, object> { { "Error", exception.Message } };
        }

        protected object MapExceptionAsRouteValues(Exception exception)
        {
            if (exception is null)
            {
                throw new ArgumentNullException($"{nameof(exception)} is null");
            }

            if (exception is BusinessException businessException)
            {
                var errorModel = BuildErrorModel(businessException.ErrorCode, businessException.Arguments);
                return errorModel.AsOject();
            }

            return new { Error = exception.Message };
        }

        protected ErrorModel BuildErrorModel(string errorCode, IDictionary<string, string> paramsValues = null)
        {
            return new ErrorModel(errorCode, paramsValues);
        }

        protected NewErrorModel BuildErrorModel(BusinessException businessException)
        {
            return new NewErrorModel(businessException.Codes ?? Enumerable.Empty<ErrorCode>());
        }

        protected NewErrorModel BuildErrorModel(IEnumerable<ErrorCode> codes)
        {
            return new NewErrorModel(codes ?? Enumerable.Empty<ErrorCode>());
        }

        protected NewErrorModel BuildErrorModel(ErrorCode code)
        {
            var errorCodes = new List<ErrorCode>();
            if (code is null)
            {
                return new NewErrorModel(Enumerable.Empty<ErrorCode>());
            }

            errorCodes.Add(code);
            return new NewErrorModel(errorCodes);
        }

        protected ErrorModel BuildErrorModel(string error, string errorCode, IDictionary<string, string> paramsValues = null)
        {
            return new ErrorModel(error, errorCode, paramsValues);
        }

        protected class NewErrorModel
        {
            public IEnumerable<ErrorCode> Codes { get; set; } = new List<ErrorCode>();

            public NewErrorModel(IEnumerable<ErrorCode> codes)
            {
                Codes = codes;
            }

            public object AsOjectRoute()
            {
                if (!Codes.Any())
                {
                    return new { };
                }

                return new
                {
                    Error = Serialize()
                };
            }

            public QueryCollection AsQueryCollection()
            {
                return new QueryCollection(new Dictionary<string, StringValues>
                {
                    { "Error", new StringValues(Serialize()) }
                });
            }

            public string Serialize()
            {
                return JsonSerializer.Serialize(Codes);
            }
        }

        protected class ErrorModel
        {
            public string Error { get; set; }
            public string ErrorCode { get; set; }
            public IDictionary<string, string> Params = new Dictionary<string, string>();

            public ErrorModel(string errorCode, IDictionary<string, string> paramsValues = null)
            {
                ErrorCode = errorCode;
                Params = paramsValues;
            }

            public ErrorModel(string error, string errorCode, IDictionary<string, string> paramsValues = null)
            {
                ErrorCode = error;
                ErrorCode = errorCode;
                Params = paramsValues;
            }

            public string GenerateParamsString()
            {
                var paramsString = new StringBuilder();

                foreach (var arg in Params)
                {
                    paramsString.Append($"{arg.Key}={arg.Value},");
                }

                if (paramsString.Length > 0)
                {
                    paramsString.Remove(paramsString.Length - 1, 1);
                }

                return paramsString.ToString();
            }

            public string AsErrorCodeString()
            {
                return $"Error={ErrorCode}&Params={GenerateParamsString()}";
            }

            public object AsOject()
            {
                if (string.IsNullOrWhiteSpace(ErrorCode))
                {
                    return new { Error };
                }

                return new { Error = ErrorCode, Params = GenerateParamsString() };
            }

            public Dictionary<string, object> AsDictionaryObject()
            {
                if (string.IsNullOrWhiteSpace(ErrorCode))
                {
                    return new Dictionary<string, object> { { "Error", Error } };
                }

                if (Params is null || !Params.Any())
                {
                    return new Dictionary<string, object> { { "Error", Error }, { "ErrorCode", ErrorCode } };
                }

                return new Dictionary<string, object> { { "Error", Error ?? string.Empty }, { "ErrorCode", ErrorCode }, { "Params", Params } };
            }

            public QueryCollection AsQueryCollection()
            {
                return new QueryCollection(new Dictionary<string, StringValues>
                {
                    { "Error", new StringValues(ErrorCode ?? string.Empty) },
                    { "Params", new StringValues(GenerateParamsString()) }
                });
            }
        }
    }
}
