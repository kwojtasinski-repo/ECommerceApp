using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Addresses
{
    public class AddressService : AbstractService<AddressVm, IAddressRepository, Address>, IAddressService
    {
        private readonly ICustomerService _customerService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AddressService(IAddressRepository addressRepository, IMapper mapper, ICustomerService customerService, IHttpContextAccessor httpContextAccessor) : base(addressRepository, mapper)
        {
            _customerService = customerService;
            _httpContextAccessor = httpContextAccessor;
        }

        public int AddAddress(AddressVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(AddressVm).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if (model.CustomerId <= 0)
            {
                throw new BusinessException("Given ivalid customer id");
            }

            var userId = _httpContextAccessor.GetUserId();
            var customers = _customerService.GetAllCustomers(c => c.UserId == userId);
            if (!customers.Any(c => c.Id == model.CustomerId))
            {
                throw new BusinessException("Cannot add address check your customer id");
            }

            var address = _mapper.Map<Address>(model);
            var id = _repo.AddAddress(address);
            return id;
        }

        public bool AddressExists(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
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

        public AddressVm GetAddressDetail(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
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

        public void UpdateAddress(AddressVm addressVm)
        {
            if (addressVm is null)
            {
                throw new BusinessException($"{typeof(AddressVm).Name} cannot be null");
            }

            var address = _mapper.Map<Address>(addressVm);
            _repo.UpdateAddress(address);
        }
    }
}
