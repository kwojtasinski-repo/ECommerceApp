using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ErrorMapToResponseTests : BaseTest<IErrorMapToResponse>
    {
        public ErrorMapToResponseTests(ITestOutputHelper output) : base(output) { }

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

        [Fact]
        public void given_business_exception_with_code_should_map_codes()
        {
            var exception = new BusinessException("message").AddCode("couponInvalidDiscount");

            var response = _service.Map(exception);

            response.Codes.ShouldNotBeNull();
            response.Codes.Count.ShouldBe(1);
            response.Codes[0].Code.ShouldBe("couponInvalidDiscount");
            response.Codes[0].Parameters.ShouldBeEmpty();
        }

        [Fact]
        public void given_business_exception_with_code_and_parameter_should_map_code_with_parameter()
        {
            var exception = new BusinessException("message")
                .AddCode(ErrorCode.Create("couponCodeAlreadyExists", ErrorParameter.Create("code", "TEST")));

            var response = _service.Map(exception);

            response.Codes.ShouldNotBeNull();
            response.Codes[0].Code.ShouldBe("couponCodeAlreadyExists");
            response.Codes[0].Parameters.ShouldHaveSingleItem();
            response.Codes[0].Parameters[0].Name.ShouldBe("code");
            response.Codes[0].Parameters[0].Value.ShouldBe("TEST");
        }

        [Fact]
        public void given_business_exception_without_codes_should_have_null_codes()
        {
            var response = _service.Map(new BusinessException("no codes"));

            response.Codes.ShouldBeNull();
        }

        [Fact]
        public void given_business_exception_with_codes_json_should_contain_codes_key()
        {
            var exception = new BusinessException("message").AddCode("couponInvalidDiscount");

            var json = _service.Map(exception).ToString();

            json.ShouldContain("\"codes\"");
            json.ShouldContain("\"couponInvalidDiscount\"");
        }

        [Fact]
        public void given_business_exception_without_codes_json_should_omit_codes_key()
        {
            var json = _service.Map(new BusinessException("no codes")).ToString();

            json.ShouldNotContain("\"codes\"");
        }
    }
}

