using ECommerceApp.API;
using Flurl.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.Common
{
    public class BaseApiTest<TStartup> : CustomWebApplicationFactory<TStartup> where TStartup : class
    {
        public FlurlClient Client { get; private set; }

        public BaseApiTest()
        {
            var httpClient = CreateClient();
            Client = new FlurlClient(httpClient);
            var token = GetTokenAsync().Result;
            Client.WithHeader("Authorization", $"Bearer {token}");
        }

        private async Task<string> GetTokenAsync()
        {
            var testUser = new UserModel { Email = "test@test", Password = "Test@test12" };
            var jsontoken = await Client.Request("api/login")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(testUser)
                .ReceiveString();

            var deserializedToken = JsonConvert.DeserializeObject<Dictionary<string,string>>(jsontoken);
            deserializedToken.TryGetValue("token", out var token);

            return token;
        }
    }
}
