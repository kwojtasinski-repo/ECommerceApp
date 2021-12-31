using ECommerceApp.API;
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
            var testUser = new UserModel { Email = "test@test", Password = "Test@test12" };

            var jsonToken = await _client.Request("api/login")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(testUser)
                .ReceiveJson<Dictionary<string, string>>();

            jsonToken.ShouldNotBeNull();
            jsonToken.TryGetValue("token", out var token);
            token.ShouldNotBeNullOrWhiteSpace();
            token.Length.ShouldBeGreaterThan(1);
        }

        [Fact]
        public async Task given_invalid_credentials_should()
        {
            var testUser = new UserModel { Email = "123", Password = "123" };

            var response = await _client.Request("api/login")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(testUser);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Unauthorized);
        }
    }
}
