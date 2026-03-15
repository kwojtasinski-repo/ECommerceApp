using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Exceptions
{
    public class ErrorMapToResponse : IErrorMapToResponse
    {
        public ExceptionResponse Map(Exception exception)
        {
            var response = exception switch
            {
                BusinessException ex => new ExceptionResponse(
                    ex.Message,
                    System.Net.HttpStatusCode.BadRequest,
                    MapCodes(ex.Codes)),
                FileException ex => new ExceptionResponse(ex.Message, System.Net.HttpStatusCode.BadRequest),
                SaveFileIssueException ex => new ExceptionResponse(ex.Message, System.Net.HttpStatusCode.BadRequest),
                _ => new ExceptionResponse("Something bad happen", System.Net.HttpStatusCode.InternalServerError)
            };

            return response;
        }

        private static IReadOnlyList<ErrorCodeDto> MapCodes(IEnumerable<ErrorCode> codes)
        {
            if (codes is null || !codes.Any())
            {
                return null;
            }

            return codes.Select(c => new ErrorCodeDto(
                c.Code,
                c.Parameters?.Select(p => new ErrorParameterDto(p.Name, p.Value?.ToString() ?? string.Empty))
                    .ToList()
                    ?? new List<ErrorParameterDto>()
            )).ToList();
        }
    }
}
