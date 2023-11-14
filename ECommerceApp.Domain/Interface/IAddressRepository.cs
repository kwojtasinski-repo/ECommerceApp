using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IAddressRepository : IGenericRepository<Address>
    {
        bool DeleteAddress(int addressId);
        int AddAddress(Address address);
        Address GetAddressById(int addressId);
        IQueryable<Address> GetAllAddresses();
        void UpdateAddress(Address address);
        Address GetAddressById(int id, string userId);
    }
}
