using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IAddressRepository
    {
        bool DeleteAddress(int addressId);
        int AddAddress(Address address);
        Address GetAddressById(int addressId);
        void UpdateAddress(Address address);
        Address GetAddressById(int id, string userId);
        bool ExistsByIdAndUserId(int id, string userId);
    }
}
