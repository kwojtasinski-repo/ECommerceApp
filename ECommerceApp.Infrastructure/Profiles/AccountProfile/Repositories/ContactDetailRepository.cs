using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Repositories
{
    internal sealed class ContactDetailRepository : IContactDetailRepository
    {
        private readonly AccountProfileDbContext _context;

        public ContactDetailRepository(AccountProfileDbContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(ContactDetail contactDetail)
        {
            _context.ContactDetails.Add(contactDetail);
            await _context.SaveChangesAsync();
            return contactDetail.Id;
        }

        public async Task<ContactDetail?> GetByIdAsync(int id)
            => await _context.ContactDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

        public async Task<ContactDetail?> GetByIdAndUserIdAsync(int id, string userId)
            => await _context.ContactDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id &&
                    _context.AccountProfiles.Any(p => p.Id == c.AccountProfileId && p.UserId == userId));

        public async Task<List<ContactDetail>> GetAllAsync()
            => await _context.ContactDetails
                .AsNoTracking()
                .ToListAsync();

        public async Task UpdateAsync(ContactDetail contactDetail)
        {
            _context.ContactDetails.Update(contactDetail);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var contactDetail = await _context.ContactDetails.FirstOrDefaultAsync(c => c.Id == id);
            if (contactDetail is null)
                return false;
            _context.ContactDetails.Remove(contactDetail);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsByIdAndUserIdAsync(int id, string userId)
            => await _context.ContactDetails
                .AnyAsync(c => c.Id == id &&
                    _context.AccountProfiles.Any(p => p.Id == c.AccountProfileId && p.UserId == userId));
    }
}
