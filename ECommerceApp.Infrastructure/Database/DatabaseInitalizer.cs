using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    internal sealed class DatabaseInitalizer : IDatabaseInitializer
    {
        private readonly Context _context;
        private readonly IConfiguration _configuration;

        public DatabaseInitalizer(Context context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task Initialize()
        {
            if (!_configuration.GetValue<bool>("Database:RunMigrationsOnStart"))
            {
                return;
            }

            await _context.Database.MigrateAsync();
        }
    }
}
