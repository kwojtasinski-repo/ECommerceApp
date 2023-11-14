using ECommerceApp.Application.DTO;

namespace ECommerceApp.Application.Services.Addresses
{
    public interface IAddressService
    {
        int AddAddress(AddressDto addressDto);
        bool DeleteAddress(int id);
        AddressDto GetAddress(int id);
        AddressDto GetAddressDetail(int id);
        bool UpdateAddress(AddressDto addressDto);
        bool AddressExists(int id);
    }
}
