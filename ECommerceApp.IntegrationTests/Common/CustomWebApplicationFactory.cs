using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Identity.IAM;
using Flurl.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.Common
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
                    // remove only the legacy Context and its options; per-BC DbContexts keep their own options
                    var contextDb = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(Context));
                    if (contextDb != null)
                    {
                        services.Remove(contextDb);
                        var options = services.Where(r =>
                            r.ServiceType == typeof(DbContextOptions) ||
                            r.ServiceType == typeof(DbContextOptions<Context>)).ToList();

                        foreach (var option in options)
                        {
                            services.Remove(option);
                        }
                    }

                    // Also replace IamDbContext with InMemory
                    var iamDb = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(DbContextOptions<IamDbContext>));
                    if (iamDb != null)
                    {
                        services.Remove(iamDb);
                    }

                    var servicesProvider = new ServiceCollection()
                        .AddEntityFrameworkInMemoryDatabase()
                        .BuildServiceProvider();

                    services.AddDbContext<Context>(options =>
                    {
                        options.UseInMemoryDatabase("InMemoryDatabase");
                        options.UseInternalServiceProvider(servicesProvider);
                    });

                    services.AddDbContext<IamDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("InMemoryIamDatabase");
                        options.UseInternalServiceProvider(servicesProvider);
                    });

                    services.AddScoped<IDatabaseInitializer, TestDatabaseInitializer>();
                    OverrideServicesImplementation(services);

                    var sp = services.BuildServiceProvider();

                    using var scope = sp.CreateScope();
                    var scopedServices = scope.ServiceProvider;
                    var context = scopedServices.GetRequiredService<Context>();
                    var iamContext = scopedServices.GetRequiredService<IamDbContext>();
                    var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory<TStartup>>>();

                    context.Database.EnsureCreated();
                    iamContext.Database.EnsureCreated();

                    try
                    {
                        Utilities.InitilizeDbForTests(context);
                        Utilities.InitializeIamUsers(scopedServices).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the " +
                                            $"database with test messages. Error: {ex.Message}");
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
            var jsonToken = await client.Request("api/auth/login")
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
