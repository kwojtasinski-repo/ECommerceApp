using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ErrorMapToResponseTests : BaseTest<IErrorMapToResponse>
    {
        [Fact]
        public void given_save_file_exception_should_map_to_error_response()
        {
            var text = "This is text of exception";
            var statusCode = HttpStatusCode.BadRequest;
            var saveFileIssueException = new SaveFileIssueException(text);

            var response = _service.Map(saveFileIssueException);

            response.StatusCode.ShouldBe(statusCode);
            response.Response.ShouldBe(text);
        }

        [Fact]
        public void given_file_exception_should_map_to_error_response()
        {
            var text = "Text file exception";
            var statusCode = HttpStatusCode.BadRequest;
            var saveFileIssueException = new FileException(text);

            var response = _service.Map(saveFileIssueException);

            response.StatusCode.ShouldBe(statusCode);
            response.Response.ShouldBe(text);
        }

        [Fact]
        public void given_business_exception_should_map_to_error_response()
        {
            var text = "Text BusinessException";
            var statusCode = HttpStatusCode.BadRequest;
            var saveFileIssueException = new BusinessException(text);

            var response = _service.Map(saveFileIssueException);

            response.StatusCode.ShouldBe(statusCode);
            response.Response.ShouldBe(text);
        }

        [Fact]
        public void given_generic_exception_should_map_to_error_response()
        {
            var text = "Something bad happen";
            var statusCode = HttpStatusCode.InternalServerError;
            var saveFileIssueException = new InvalidOperationException(text);

            var response = _service.Map(saveFileIssueException);

            response.StatusCode.ShouldBe(statusCode);
            response.Response.ShouldBe(text);
        }
    }
}
