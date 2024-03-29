﻿using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Application.Services.Addresses
{
    public class AddressService : IAddressService
    {
        private readonly IAddressRepository _addressRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IMapper _mapper;
        private readonly IUserContext _userContext;

        public AddressService(IAddressRepository addressRepository, ICustomerRepository customerRepository, IMapper mapper, IUserContext userContext)
        {
            _addressRepository = addressRepository;
            _customerRepository = customerRepository;
            _mapper = mapper;
            _userContext = userContext;
        }

        public int AddAddress(AddressDto addressDto)
        {
            if (addressDto is null)
            {
                throw new BusinessException($"{typeof(AddressVm).Name} cannot be null");
            }

            if (addressDto.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if (addressDto.CustomerId <= 0)
            {
                throw new BusinessException("Given ivalid customer id");
            }

            var userId = _userContext.UserId;
            if (!_customerRepository.CustomerExists(addressDto.CustomerId, userId))
            {
                throw new BusinessException("Cannot add address check your customer id", ErrorCode.Create("customerNotExists"));
            }

            var address = _mapper.Map<Address>(addressDto);
            var id = _addressRepository.AddAddress(address);
            return id;
        }

        public bool AddressExists(int id)
        {
            var userId = _userContext.UserId;
            return _addressRepository.ExistsByIdAndUserId(id, userId);
        }

        public bool DeleteAddress(int id)
        {
            var addresses = _addressRepository.GetCountByIdAndUserId(id, _userContext.UserId);
            if (addresses == 0)
            {
                return false;
            }

            if (addresses == 1)
            {
                throw new BusinessException("You cannot delete address if you only have 1", ErrorCode.Create("addressDeletePolicy"));
            }

            return !_addressRepository.DeleteAddress(id);
        }

        public AddressDto GetAddress(int id)
        {
            if (!UserPermissions.Roles.MaintenanceRoles.Contains(_userContext.Role)
                && !_addressRepository.ExistsByIdAndUserId(id, _userContext.UserId))
            {
                return null;
            }

            var adress = _addressRepository.GetAddressById(id);
            var adressDto = _mapper.Map<AddressDto>(adress);
            return adressDto;
        }

        public AddressDto GetAddressDetail(int id)
        {
            var userId = _userContext.UserId;
            var adress = _addressRepository.GetAddressById(id, userId);
            var adressDto = _mapper.Map<AddressDto>(adress);
            return adressDto;
        }

        public bool UpdateAddress(AddressDto addressDto)
        {
            if (addressDto is null)
            {
                throw new BusinessException($"{typeof(AddressVm).Name} cannot be null");
            }

            var userId = _userContext.UserId;
            Address address;
            if (!UserPermissions.Roles.MaintenanceRoles.Contains(_userContext.Role))
            {
                address = _addressRepository.GetAddressById(addressDto.Id ?? 0, userId);
            }
            else
            {
                address = _addressRepository.GetAddressById(addressDto.Id ?? 0);
            }

            if (address is null)
            {
                return false;
            }
            
            address.City = addressDto.City;
            address.Country = addressDto.Country;
            address.Street = addressDto.Street;
            address.BuildingNumber = addressDto.BuildingNumber;
            address.FlatNumber = addressDto.FlatNumber;
            address.ZipCode = AddressDto.MapToZipCodeNumber(addressDto.ZipCode);
            _addressRepository.UpdateAddress(address);
            return true;
        }
    }
}
