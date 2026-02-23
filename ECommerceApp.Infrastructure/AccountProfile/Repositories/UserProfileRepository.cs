using ECommerceApp.Domain.AccountProfile;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.AccountProfile.Repositories
{
    internal sealed class UserProfileRepository : IUserProfileRepository
    {
        private readonly UserProfileDbContext _context;

        public UserProfileRepository(UserProfileDbContext context)
        {
            _context = context;
        }

        public async Task<UserProfileId> AddAsync(UserProfile profile)
        {
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
            return profile.Id;
        }

        public async Task<UserProfile?> GetByIdAsync(UserProfileId id)
            => await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == id);

        public async Task<UserProfile?> GetByIdAndUserIdAsync(UserProfileId id, string userId)
            => await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        public async Task<UserProfile?> GetByUserIdAsync(string userId)
            => await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        public async Task UpdateAsync(UserProfile profile)
        {
            _context.UserProfiles.Update(profile);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(UserProfileId id)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile is null)
                return false;
            _context.UserProfiles.Remove(profile);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsByIdAsync(UserProfileId id)
            => await _context.UserProfiles.AnyAsync(p => p.Id == id);

        public async Task<bool> ExistsByIdAndUserIdAsync(UserProfileId id, string userId)
            => await _context.UserProfiles.AnyAsync(p => p.Id == id && p.UserId == userId);

        public async Task<List<UserProfile>> GetAllAsync(int pageSize, int pageNo, string searchString)
        {
            if (pageNo < 1)
            {
                pageNo = 1;
            }

            return await _context.UserProfiles
                .AsNoTracking()
                .IgnoreAutoIncludes()
                .Where(p => string.IsNullOrEmpty(searchString) ||
                            p.FirstName.Contains(searchString) ||
                            p.LastName.Contains(searchString))
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountAllAsync(string searchString)
            => await _context.UserProfiles
                .AsNoTracking()
                .IgnoreAutoIncludes()
                .CountAsync(p => string.IsNullOrEmpty(searchString) ||
                                 p.FirstName.Contains(searchString) ||
                                 p.LastName.Contains(searchString));

        public async Task<List<UserProfile>> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString)
        {
            if (pageNo < 1)
            {
                pageNo = 1;
            }

            return await _context.UserProfiles
                .AsNoTracking()
                .IgnoreAutoIncludes()
                .Where(p => p.UserId == userId &&
                            (string.IsNullOrEmpty(searchString) ||
                             p.FirstName.Contains(searchString) ||
                             p.LastName.Contains(searchString)))
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountByUserIdAsync(string userId, string searchString)
            => await _context.UserProfiles
                .AsNoTracking()
                .IgnoreAutoIncludes()
                .CountAsync(p => p.UserId == userId &&
                                 (string.IsNullOrEmpty(searchString) ||
                                  p.FirstName.Contains(searchString) ||
                                  p.LastName.Contains(searchString)));

        public async Task<List<UserProfile>> GetAllByUserIdAsync(string userId)
            => await _context.UserProfiles
                .AsNoTracking()
                .IgnoreAutoIncludes()
                .Where(p => p.UserId == userId)
                .Take(UserProfileConstants.MaxUnpaginatedResults)
                .ToListAsync();
    }
}
