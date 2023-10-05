using ECommerceApp.Infrastructure.Database;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.Common
{
    internal class TestDatabaseInitializer : IDatabaseInitializer
    {
        public Task Initialize()
        {
            return Task.CompletedTask;
        }
    }
}
