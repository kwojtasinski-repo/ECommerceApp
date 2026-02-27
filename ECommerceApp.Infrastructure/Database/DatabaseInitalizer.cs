using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    internal sealed class DatabaseInitalizer : IDatabaseInitializer
    {
        private readonly IEnumerable<IDbContextMigrator> _migrators;
        private readonly IConfiguration _configuration;

        public DatabaseInitalizer(IEnumerable<IDbContextMigrator> migrators, IConfiguration configuration)
        {
            _migrators = migrators;
            _configuration = configuration;
        }

        public async Task Initialize()
        {
            if (!_configuration.GetValue<bool>("Database:RunMigrationsOnStart"))
            {
                return;
            }

            foreach (var migrator in _migrators)
            {
                await migrator.MigrateAsync();
            }
        }
    }
}
