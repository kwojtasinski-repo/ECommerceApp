using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace ECommerceApp.Web.E2E.Infrastructure
{
    public sealed class PlaywrightSqlFixture : IAsyncLifetime
    {
        public const string CollectionName = "PlaywrightSqlServer";

        private readonly MsSqlContainer _container = new MsSqlBuilder().Build();
        private PlaywrightWebApplicationFactory _factory;

        public static string ConnectionString { get; private set; }

        public IServiceProvider Services => (_factory ?? throw new InvalidOperationException(
            $"{nameof(PlaywrightSqlFixture)} has not been initialized yet.")).StartKestrelHost();

        public string ServerAddress => _factory?.ServerAddress ?? throw new InvalidOperationException(
            $"{nameof(PlaywrightSqlFixture)} has not been initialized yet.");

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            _factory = new PlaywrightWebApplicationFactory(ConnectionString);
            _ = Services;
        }

        public async ValueTask DisposeAsync()
        {
            if (_factory != null)
            {
                _factory.Dispose();
            }

            await _container.DisposeAsync();
            ConnectionString = null;
        }
    }
}