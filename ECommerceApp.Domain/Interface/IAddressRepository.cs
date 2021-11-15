using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IAddressRepository : IGenericRepository<Address>
    {
        void DeleteAddress(int AddressId);
        int AddAddress(Address Address);
        Address GetAddressById(int AddressId);
        IQueryable<Address> GetAllAddresses();
        void UpdateAddress(Address Address);
    }
}
