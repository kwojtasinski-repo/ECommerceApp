using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Database
{
    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.UseUtcDateTimes();
        }
    }
}
