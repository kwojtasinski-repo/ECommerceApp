using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.Infrastructure.Database;
using Flurl.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.Common
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            try
            {
                builder.ConfigureServices(services =>
                {
                    // remove implementation of infrastructure dbContext
                    var contextDb = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(Context));
                    if (contextDb != null)
                    {
                        services.Remove(contextDb);
                        var options = services.Where(r => (r.ServiceType == typeof(DbContextOptions))
                          || (r.ServiceType.IsGenericType && r.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))).ToList();
                        
                        foreach (var option in options)
                        {
                            services.Remove(option);
                        }
                    }

                    var servicesProvider = new ServiceCollection()
                        .AddEntityFrameworkInMemoryDatabase()
                        .BuildServiceProvider();

                    services.AddDbContext<Context>(options =>
                    {
                        options.UseInMemoryDatabase("InMemoryDatabase");
                        options.UseInternalServiceProvider(servicesProvider);
                    });

                    services.AddScoped<IDatabaseInitializer, TestDatabaseInitializer>();
                    OverrideServicesImplementation(services);

                    var sp = services.BuildServiceProvider();

                    using var scope = sp.CreateScope();
                    var scopedServices = scope.ServiceProvider;
                    var context = scopedServices.GetRequiredService<Context>();
                    var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory<TStartup>>>();

                    context.Database.EnsureCreated();

                    try
                    {
                        Utilities.InitilizeDbForTests(context);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the " +
                                            $"database with test messages. Error: {ex.Message}");
                    }
                })
                .UseEnvironment("Test");
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected virtual void OverrideServicesImplementation(IServiceCollection services)
        {

        }

        public async Task<FlurlClient> GetAuthenticatedClient()
        {
            var httpClient = CreateClient();
            var client = new FlurlClient(httpClient);
            var token = await GetTokenAsync(client);
            client.WithHeader("Authorization", $"Bearer {token}");
            return client;
        }

        private async Task<string> GetTokenAsync(FlurlClient client)
        {
            var testUser = new SignInDto("test@test", "Test@test12");
            var jsonToken = await client.Request("api/login")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(testUser)
                .ReceiveString();

            var deserializedToken = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonToken);
            deserializedToken.TryGetValue("token", out var token);

            return token;
        }
    }
}
