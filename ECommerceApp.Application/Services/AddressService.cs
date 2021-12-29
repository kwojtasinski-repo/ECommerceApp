using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class AddressService : AbstractService<AddressVm, IAddressRepository, Address>, IAddressService
    {
        private readonly ICustomerService _customerService;

        public AddressService(IAddressRepository addressRepository, IMapper mapper, ICustomerService customerService) : base(addressRepository, mapper)
        {
            _customerService = customerService;
        }

        public int AddAddress(AddressVm addressVm)
        {
            if (addressVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if(addressVm.CustomerId <= 0)
            {
                throw new BusinessException("Given ivalid customer id");
            }

            var address = _mapper.Map<Address>(addressVm);
            var id = _repo.AddAddress(address);
            return id;
        }

        public int AddAddress(AddressVm model, string userId)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customers = _customerService.GetAllCustomers(c => c.UserId == userId);
            bool customerIdExists = false;
            foreach (var cust in customers)
            {
                if (cust.Id == model.CustomerId)
                {
                    customerIdExists = true;
                    break;
                }
            }

            if (!customerIdExists)
            {
                throw new BusinessException("Cannot add address check your customer id");
            }

            var address = _mapper.Map<Address>(model);
            var id = _repo.AddAddress(address);
            return id;
        }

        public bool AddressExists(int id)
        {
            var address = _repo.GetAddressById(id);

            if (address == null)
            {
                return false;
            }

            return true;
        }

        public bool AddressExists(int id, string userId)
        {
            var address = _repo.GetAll().Include(c => c.Customer).Where(a => a.Id == id && a.Customer.UserId == userId).AsNoTracking().FirstOrDefault();

            if (address == null)
            {
                return false;
            }

            return true;
        }

        public void DeleteAddress(int id)
        {
            _repo.DeleteAddress(id);
        }

        public AddressVm GetAddress(int id)
        {
            var adress = _repo.GetAddressById(id);
            var adressVm = _mapper.Map<AddressVm>(adress);
            return adressVm;
        }

        public AddressVm GetAddressDetail(int id, string userId)
        {
            var adress = _repo.GetAddressById(id, userId);
            var adressVm = _mapper.Map<AddressVm>(adress);
            return adressVm;
        }

        public IEnumerable<AddressVm> GetAllAddresss(Expression<Func<Address, bool>> expression)
        {
            var addresses = _repo.GetAll().Where(expression);
            var addressesVm = _mapper.Map<List<AddressVm>>(addresses);
            return addressesVm;
        }

        public void UpdateAddress(AddressVm AddressVm)
        {
            var address = _mapper.Map<Address>(AddressVm);
            _repo.UpdateAddress(address);
        }
    }
}
