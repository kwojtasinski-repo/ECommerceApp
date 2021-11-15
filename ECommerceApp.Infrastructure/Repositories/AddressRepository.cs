using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class AddressRepository : GenericRepository<Address>, IAddressRepository
    {
        public AddressRepository(Context context) : base(context)
        {
        }

        public int AddAddress(Address address)
        {
            _context.Addresses.Add(address);
            _context.SaveChanges();
            return address.Id;
        }

        public void DeleteAddress(int addressId)
        {
            var address = _context.Addresses.Find(addressId);

            if (address != null)
            {
                _context.Addresses.Remove(address);
                _context.SaveChanges();
            }
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

        public IQueryable<Address> GetAllAddresses()
        {
            var addresses = _context.Addresses.AsQueryable();
            return addresses;
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
