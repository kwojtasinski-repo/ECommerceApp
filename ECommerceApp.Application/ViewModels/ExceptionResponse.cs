using ECommerceApp.Application.Exceptions;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECommerceApp.Application.ViewModels
{
    public class ExceptionResponse
    {
        public string Response { get; }
        public HttpStatusCode StatusCode { get; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ErrorCodeDto> Codes { get; }

        public ExceptionResponse(string response, HttpStatusCode statusCode,
            IReadOnlyList<ErrorCodeDto> codes = null)
        {
            Response = response;
            StatusCode = statusCode;
            Codes = codes;
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
