using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using ECommerceApp.Domain.Profiles.AccountProfile;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    internal sealed class AccountAddressService : IAccountAddressService
    {
        private readonly IAddressRepository _repository;
        private readonly IMapper _mapper;

        public AccountAddressService(IAddressRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<int> AddAsync(AddAddressDto dto)
        {
            var address = Address.Create(
                dto.AccountProfileId,
                dto.Street,
                dto.BuildingNumber,
                dto.FlatNumber,
                dto.ZipCode,
                dto.City,
                dto.Country);

            return await _repository.AddAsync(address);
        }

        public async Task<bool> UpdateAsync(UpdateAddressDto dto)
        {
            var address = await _repository.GetByIdAsync(dto.Id);
            if (address is null)
                throw new BusinessException($"Address with id {dto.Id} was not found");

            address.Update(dto.Street, dto.BuildingNumber, dto.FlatNumber, dto.ZipCode, dto.City, dto.Country);
            await _repository.UpdateAsync(address);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            if (!await _repository.ExistsByIdAndUserIdAsync(id, userId))
                return false;

            return await _repository.DeleteAsync(id);
        }

        public async Task<AddressVm?> GetAsync(int id, string userId)
        {
            var address = await _repository.GetByIdAndUserIdAsync(id, userId);
            return address is null ? null : _mapper.Map<AddressVm>(address);
        }
    }
}
