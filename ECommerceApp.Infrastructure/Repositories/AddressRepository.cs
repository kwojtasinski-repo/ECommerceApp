using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class AddressRepository : IAddressRepository
    {
        private readonly Context _context;

        public AddressRepository(Context context)
        {
            _context = context;
        }

        public int AddAddress(Address address)
        {
            _context.Addresses.Add(address);
            _context.SaveChanges();
            return address.Id;
        }

        public bool DeleteAddress(int addressId)
        {
            var address = _context.Addresses.Find(addressId);
            
            if (address is null)
            {
                return false;
            }

            _context.Addresses.Remove(address);
            _context.SaveChanges();
            return true;
        }

        public bool ExistsByIdAndUserId(int id, string userId)
        {
            return _context.Addresses
                .AsNoTracking()
                .Any(a => a.Id == id && a.Customer.UserId == userId);
        }

        public Address GetAddressById(int addressId)
        {
            var address = _context.Addresses.FirstOrDefault(c => c.Id == addressId);
            return address;
        }

        public Address GetAddressById(int id, string userId)
        {
            var address = _context.Addresses.Include(c => c.Customer).Where(a => a.Id == id && a.Customer.UserId == userId).FirstOrDefault();
            return address;
        }

        public void UpdateAddress(Address address)
        {
            _context.Attach(address);
            _context.Entry(address).Property("Street").IsModified = true;
            _context.Entry(address).Property("BuildingNumber").IsModified = true;
            _context.Entry(address).Property("FlatNumber").IsModified = true;
            _context.Entry(address).Property("ZipCode").IsModified = true;
            _context.Entry(address).Property("City").IsModified = true;
            _context.Entry(address).Property("Country").IsModified = true;
            _context.SaveChanges();
        }
    }
}
