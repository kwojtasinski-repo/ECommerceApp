using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IAddressRepository : IGenericRepository<Address>
    {
        void DeleteAddress(int addressId);
        int AddAddress(Address address);
        Address GetAddressById(int addressId);
        IQueryable<Address> GetAllAddresses();
        void UpdateAddress(Address address);
        Address GetAddressById(int id, string userId);
    }
}
