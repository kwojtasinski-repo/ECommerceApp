using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM;
using Flurl.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>, IHaveXunitSink where TStartup : class
    {
        public XunitLogSink Sink { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            try
            {
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.test.json"), optional: false, reloadOnChange: false);
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddProvider(new XunitLoggerProvider(Sink));
                });

                builder.ConfigureServices(services =>
                {
                    services.ReplaceDbContextWithInMemory<IamDbContext>("InMemoryIamDatabase");

                    services.AddScoped<IDatabaseInitializer, TestDatabaseInitializer>();
                    OverrideServicesImplementation(services);

                    var sp = services.BuildServiceProvider();

                    using var scope = sp.CreateScope();
                    var scopedServices = scope.ServiceProvider;
                    var iamContext = scopedServices.GetRequiredService<IamDbContext>();
                    var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory<TStartup>>>();

                    iamContext.Database.EnsureCreated();

                    try
                    {
                        Utilities.InitializeIamUsers(scopedServices).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the database with test messages. Error: {Error}", ex.Message);
                    }
                })
                .UseEnvironment("test");
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected virtual void OverrideServicesImplementation(IServiceCollection services)
        {

        }

        public async Task<FlurlClient> GetAuthenticatedClient(CancellationToken ct = default)
        {
            var httpClient = CreateClient();
            var client = new FlurlClient(httpClient, httpClient.BaseAddress?.ToString() ?? "/");
            var token = await GetTokenAsync(client, ct);
            client.WithHeader("Authorization", $"Bearer {token}");
            return client;
        }

        private async Task<string> GetTokenAsync(FlurlClient client, CancellationToken ct = default)
        {
            var testUser = new SignInDto("test@test", "Test@test12");
            var jsonToken = await client.Request("api/auth/login")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(testUser, cancellationToken: ct)
                .ReceiveString();

            var deserializedToken = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonToken);
            deserializedToken.TryGetValue("token", out var token);

            return token;
        }

    }
}

