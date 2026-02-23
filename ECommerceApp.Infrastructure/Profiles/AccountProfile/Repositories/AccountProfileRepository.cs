using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AP = ECommerceApp.Domain.Profiles.AccountProfile;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Repositories
{
    internal sealed class AccountProfileRepository : IAccountProfileRepository
    {
        private readonly AccountProfileDbContext _context;

        public AccountProfileRepository(AccountProfileDbContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(AP.AccountProfile profile)
        {
            _context.AccountProfiles.Add(profile);
            await _context.SaveChangesAsync();
            return profile.Id;
        }

        public async Task<AP.AccountProfile?> GetByIdAsync(int id)
            => await _context.AccountProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<AP.AccountProfile?> GetByIdWithDetailsAsync(int id)
            => await _context.AccountProfiles
                .Include(p => p.Addresses)
                .Include(p => p.ContactDetails)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<AP.AccountProfile?> GetByIdAndUserIdAsync(int id, string userId)
            => await _context.AccountProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        public async Task<AP.AccountProfile?> GetByUserIdAsync(string userId)
            => await _context.AccountProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

        public async Task UpdateAsync(AP.AccountProfile profile)
        {
            _context.AccountProfiles.Update(profile);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var profile = await _context.AccountProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile is null)
                return false;
            _context.AccountProfiles.Remove(profile);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsByIdAsync(int id)
            => await _context.AccountProfiles.AnyAsync(p => p.Id == id);

        public async Task<bool> ExistsByIdAndUserIdAsync(int id, string userId)
            => await _context.AccountProfiles.AnyAsync(p => p.Id == id && p.UserId == userId);

        public async Task<List<AP.AccountProfile>> GetAllAsync(int pageSize, int pageNo, string searchString)
            => await _context.AccountProfiles
                .AsNoTracking()
                .Where(p => string.IsNullOrEmpty(searchString) ||
                            p.FirstName.Contains(searchString) ||
                            p.LastName.Contains(searchString))
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountAllAsync(string searchString)
            => await _context.AccountProfiles
                .CountAsync(p => string.IsNullOrEmpty(searchString) ||
                                 p.FirstName.Contains(searchString) ||
                                 p.LastName.Contains(searchString));

        public async Task<List<AP.AccountProfile>> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString)
            => await _context.AccountProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId &&
                            (string.IsNullOrEmpty(searchString) ||
                             p.FirstName.Contains(searchString) ||
                             p.LastName.Contains(searchString)))
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountByUserIdAsync(string userId, string searchString)
            => await _context.AccountProfiles
                .CountAsync(p => p.UserId == userId &&
                                 (string.IsNullOrEmpty(searchString) ||
                                  p.FirstName.Contains(searchString) ||
                                  p.LastName.Contains(searchString)));

        public async Task<List<AP.AccountProfile>> GetAllByUserIdAsync(string userId)
            => await _context.AccountProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .ToListAsync();
    }
}
