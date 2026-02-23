using ECommerceApp.Domain.Profiles.AccountProfile;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile.Repositories
{
    internal sealed class ContactDetailTypeRepository : IContactDetailTypeRepository
    {
        private readonly AccountProfileDbContext _context;

        public ContactDetailTypeRepository(AccountProfileDbContext context)
        {
            _context = context;
        }

        public async Task<int> AddAsync(ContactDetailType contactDetailType)
        {
            _context.ContactDetailTypes.Add(contactDetailType);
            await _context.SaveChangesAsync();
            return contactDetailType.Id;
        }

        public async Task<ContactDetailType?> GetByIdAsync(int id)
            => await _context.ContactDetailTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

        public async Task<List<ContactDetailType>> GetAllAsync()
            => await _context.ContactDetailTypes
                .AsNoTracking()
                .ToListAsync();

        public async Task UpdateAsync(ContactDetailType contactDetailType)
        {
            _context.ContactDetailTypes.Update(contactDetailType);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var type = await _context.ContactDetailTypes.FirstOrDefaultAsync(t => t.Id == id);
            if (type is null)
                return false;
            _context.ContactDetailTypes.Remove(type);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
