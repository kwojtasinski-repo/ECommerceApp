using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ECommerceApp.Application.Services.Addresses
{
    public class AddressService : IAddressService
    {
        private readonly IAddressRepository _addressRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AddressService(IAddressRepository addressRepository, ICustomerRepository customerRepository, IMapper mapper, IHttpContextAccessor httpContextAccessor)
        {
            _addressRepository = addressRepository;
            _customerRepository = customerRepository;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
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

            var userId = _httpContextAccessor.GetUserId();
            if (!_customerRepository.CustomerExists(addressDto.CustomerId, userId))
            {
                throw new BusinessException("Cannot add address check your customer id");
            }

            var address = _mapper.Map<Address>(addressDto);
            var id = _addressRepository.AddAddress(address);
            return id;
        }

        public bool AddressExists(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
            var address = _addressRepository.GetAll().Include(c => c.Customer).Where(a => a.Id == id && a.Customer.UserId == userId).AsNoTracking().FirstOrDefault();

            if (address == null)
            {
                return false;
            }

            return true;
        }

        public bool DeleteAddress(int id)
        {
            return !_addressRepository.DeleteAddress(id);
        }

        public AddressDto GetAddress(int id)
        {
            var adress = _addressRepository.GetAddressById(id);
            var adressDto = _mapper.Map<AddressDto>(adress);
            return adressDto;
        }

        public AddressDto GetAddressDetail(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
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


            var userId = _httpContextAccessor.GetUserId();
            var address = _addressRepository.GetAddressById(addressDto.Id ?? 0, userId);
            if (address == null)
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
