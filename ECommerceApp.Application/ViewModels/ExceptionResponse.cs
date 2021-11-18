using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ECommerceApp.Application.ViewModels
{
    public class ExceptionResponse
    {
        public string Response { get; }
        public HttpStatusCode StatusCode { get; }

        public ExceptionResponse(string response, HttpStatusCode statusCode)
        {
            Response = response;
            StatusCode = statusCode;
        }

        public override string ToString()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            var responseString = JsonSerializer.Serialize(this, options);
            return responseString;
        }
    }
}
