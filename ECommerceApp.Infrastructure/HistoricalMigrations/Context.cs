using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Database
{
    /// <summary>
    /// DEPRECATED - do not use for new code. This is the empty legacy monolith context from before the
    /// bounded-context split. It is kept purely for the historical record: the original EF migrations in this
    /// folder (<c>*.Designer.cs</c>, <c>ContextModelSnapshot</c>) reference <c>typeof(Context)</c>, and we want
    /// to preserve the migration history as it was. It is not registered in DI and nothing at runtime uses it.
    /// Each bounded context now owns its own DbContext and migrations. This folder will be removed in time.
    /// </summary>
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
