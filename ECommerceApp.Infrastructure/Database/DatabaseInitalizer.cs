using ECommerceApp.Infrastructure.Identity.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    internal sealed class DatabaseInitalizer : IDatabaseInitializer
    {
        private readonly Context _context;
        private readonly IamDbContext _iamContext;
        private readonly IConfiguration _configuration;

        public DatabaseInitalizer(Context context, IamDbContext iamContext, IConfiguration configuration)
        {
            _context = context;
            _iamContext = iamContext;
            _configuration = configuration;
        }

        public async Task Initialize()
        {
            if (!_configuration.GetValue<bool>("Database:RunMigrationsOnStart"))
            {
                return;
            }

            await _context.Database.MigrateAsync();
            await _iamContext.Database.MigrateAsync();
        }
    }
}
