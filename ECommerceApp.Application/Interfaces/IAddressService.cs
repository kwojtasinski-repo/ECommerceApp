using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Interfaces
{
    public interface IAddressService : IAbstractService<AddressVm, IAddressRepository, Address>
    {
        int AddAddress(AddressVm AddressVm);
        void DeleteAddress(int id);
        AddressVm GetAddress(int id);
        AddressVm GetAddressDetail(int id, string userId);
        void UpdateAddress(AddressVm AddressVm);
        IEnumerable<AddressVm> GetAllAddresss(Expression<Func<Address, bool>> expression);
        bool AddressExists(int id);
        bool AddressExists(int id, string userId);
    }
}
