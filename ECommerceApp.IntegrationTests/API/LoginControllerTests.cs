using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class LoginControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>> 
    {
        private readonly FlurlClient _client;

        public LoginControllerTests(CustomWebApplicationFactory<Startup> customWebApplicationFactory)
        {
            var httpClient = customWebApplicationFactory.CreateClient();
            _client = new FlurlClient(httpClient);
        }

        [Fact]
        public async Task given_valid_credentials_should_return_token()
        {
            var testUser = new SignInDto("test@test", "Test@test12");

            var jsonToken = await _client.Request("api/login")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(testUser)
                .ReceiveJson<Dictionary<string, string>>();

            jsonToken.ShouldNotBeNull();
            jsonToken.TryGetValue("accessToken", out var token);
            token.ShouldNotBeNullOrWhiteSpace();
            token.Length.ShouldBeGreaterThan(1);
            jsonToken.TryGetValue("refreshToken", out var refreshToken);
            refreshToken.ShouldNotBeNull();
        }

        [Fact]
        public async Task given_invalid_credentials_should_return_bad_request()
        {
            var testUser = new SignInDto("123", "123");

            var response = await _client.Request("api/login")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(testUser);

            response.StatusCode.ShouldBe((int) HttpStatusCode.BadRequest);
        }
    }
}
