using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class TimeManagementDbContextFactory : IDesignTimeDbContextFactory<TimeManagementDbContext>
    {
        public TimeManagementDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TimeManagementDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new TimeManagementDbContext(optionsBuilder.Options);
        }
    }
}
