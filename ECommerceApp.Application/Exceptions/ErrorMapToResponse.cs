using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using System;

namespace ECommerceApp.Application.Exceptions
{
    public class ErrorMapToResponse : IErrorMapToResponse
    {
        public ExceptionResponse Map(Exception exception)
        {
            var response = exception switch
            {
                BusinessException ex => new ExceptionResponse(ex.Message, System.Net.HttpStatusCode.BadRequest),
                FileException ex => new ExceptionResponse(ex.Message, System.Net.HttpStatusCode.BadRequest),
                SaveFileIssueException ex => new ExceptionResponse(ex.Message, System.Net.HttpStatusCode.BadRequest),
                _ => new ExceptionResponse("Something bad happen", System.Net.HttpStatusCode.InternalServerError)
            };

            return response;
        }
    }
}
