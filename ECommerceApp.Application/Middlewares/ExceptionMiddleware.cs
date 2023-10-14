using ECommerceApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Middlewares
{
    public class ExceptionMiddleware : IMiddleware
    {
        private readonly IErrorMapToResponse _errorMapToResponse;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(IErrorMapToResponse errorMapToResponse, ILogger<ExceptionMiddleware> logger)
        {
            _errorMapToResponse = errorMapToResponse;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch(Exception exception)
            {
                _logger.LogError(exception, exception.Message);
                await HandleExceptionAsync(context, exception);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var error = _errorMapToResponse.Map(exception);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int) (error?.StatusCode ?? HttpStatusCode.BadRequest);
            var response = error?.ToString();

            if (response is null)
            {
                await context.Response.WriteAsync(string.Empty);
                return;
            }

            await context.Response.WriteAsync(error.ToString());
        }

    }
}
