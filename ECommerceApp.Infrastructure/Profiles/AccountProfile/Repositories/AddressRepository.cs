using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Repositories
{
    internal sealed class AddressRepository : IAddressRepository
    {
        private readonly AccountProfileDbContext _context;

        public AddressRepository(AccountProfileDbContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(Address address)
        {
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();
            return address.Id;
        }

        public async Task<Address?> GetByIdAsync(int id)
            => await _context.Addresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task<Address?> GetByIdAndUserIdAsync(int id, string userId)
            => await _context.Addresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id &&
                    _context.AccountProfiles.Any(p => p.Id == a.AccountProfileId && p.UserId == userId));

        public async Task UpdateAsync(Address address)
        {
            _context.Addresses.Update(address);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id);
            if (address is null)
                return false;
            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsByIdAndUserIdAsync(int id, string userId)
            => await _context.Addresses
                .AnyAsync(a => a.Id == id &&
                    _context.AccountProfiles.Any(p => p.Id == a.AccountProfileId && p.UserId == userId));
    }
}
