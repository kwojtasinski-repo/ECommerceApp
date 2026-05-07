using ECommerceApp.Infrastructure.Database;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public class TestDatabaseInitializer : IDatabaseInitializer
    {
        public Task Initialize()
        {
            return Task.CompletedTask;
        }
    }
}

