using ECommerceApp.Application.DTO;
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
using System.Linq;
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
                    services.ReplaceDbContextWithInMemory<Context>("InMemoryDatabase");
                    services.ReplaceDbContextWithInMemory<IamDbContext>("InMemoryIamDatabase");

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

