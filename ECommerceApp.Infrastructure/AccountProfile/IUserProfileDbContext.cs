using ECommerceApp.Domain.AccountProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.AccountProfile
{
    internal interface IUserProfileDbContext
    {
        DbSet<UserProfile> UserProfiles { get; }
        EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
