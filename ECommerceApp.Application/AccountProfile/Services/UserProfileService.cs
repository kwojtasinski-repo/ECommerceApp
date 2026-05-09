using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.AccountProfile;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Services
{
    internal sealed class UserProfileService : IUserProfileService
    {
        private readonly IUserProfileRepository _repository;

        public UserProfileService(IUserProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<int> CreateAsync(CreateUserProfileDto dto)
        {
            var profile = UserProfile.Create(
                dto.UserId,
                dto.FirstName,
                dto.LastName,
                dto.IsCompany,
                dto.NIP,
                dto.CompanyName,
                dto.Email,
                dto.PhoneNumber);

            return (await _repository.AddAsync(profile)).Value;
        }

        public async Task<bool> UpdatePersonalInfoAsync(UpdateUserProfileDto dto)
        {
            var profile = await _repository.GetByIdAsync(new UserProfileId(dto.Id), track: true);
            if (profile is null)
                throw new BusinessException($"UserProfile with id {dto.Id} was not found");

            profile.UpdatePersonalInfo(dto.FirstName, dto.LastName, dto.IsCompany, dto.NIP, dto.CompanyName);
            await _repository.UpdateAsync(profile);
            return true;
        }

        public async Task<bool> UpdateContactInfoAsync(UpdateContactInfoDto dto)
        {
            var profile = await _repository.GetByIdAsync(new UserProfileId(dto.Id), track: true);
            if (profile is null)
                throw new BusinessException($"UserProfile with id {dto.Id} was not found");

            profile.UpdateContactInfo(dto.Email, dto.PhoneNumber);
            await _repository.UpdateAsync(profile);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
            => await _repository.DeleteAsync(new UserProfileId(id));

        public async Task<UserProfileDetailsVm> GetDetailsAsync(int id)
        {
            var profile = await _repository.GetByIdAsync(new UserProfileId(id));
            return profile is null ? null : UserProfileDetailsVm.FromDomain(profile);
        }

        public async Task<UserProfileDetailsVm> GetDetailsByUserIdAsync(string userId)
        {
            var profile = await _repository.GetByUserIdAsync(userId);
            return profile is null ? null : UserProfileDetailsVm.FromDomain(profile);
        }

        public async Task<UserProfileVm> GetAsync(int id, string userId)
        {
            var profile = await _repository.GetByIdAndUserIdAsync(new UserProfileId(id), userId);
            return profile is null ? null : UserProfileVm.FromDomain(profile);
        }

        public async Task<UserProfileVm> GetByUserIdAsync(string userId)
        {
            var profile = await _repository.GetByUserIdAsync(userId);
            return profile is null ? null : UserProfileVm.FromDomain(profile);
        }

        public async Task<UserProfileListVm> GetAllAsync(int pageSize, int pageNo, string searchString)
        {
            var profiles = await _repository.GetAllAsync(pageSize, pageNo, searchString);
            return new UserProfileListVm
            {
                Profiles = profiles.Select(UserProfileForListVm.FromDomain).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                SearchString = searchString,
                Count = await _repository.CountAllAsync(searchString)
            };
        }

        public async Task<UserProfileListVm> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString)
        {
            var profiles = await _repository.GetAllByUserIdAsync(userId, pageSize, pageNo, searchString);
            return new UserProfileListVm
            {
                Profiles = profiles.Select(UserProfileForListVm.FromDomain).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                SearchString = searchString,
                Count = await _repository.CountByUserIdAsync(userId, searchString)
            };
        }

        public async Task<bool> ExistsAsync(int id, string userId)
            => await _repository.ExistsByIdAndUserIdAsync(new UserProfileId(id), userId);

        public async Task<bool> AddAddressAsync(int userProfileId, string userId, AddAddressDto dto)
        {
            var profile = await _repository.GetByIdAndUserIdAsync(new UserProfileId(userProfileId), userId, track: true);
            if (profile is null)
                throw new BusinessException($"UserProfile with id {userProfileId} was not found");

            profile.AddAddress(dto.Street, dto.BuildingNumber, dto.FlatNumber, dto.ZipCode, dto.City, dto.Country);
            await _repository.UpdateAsync(profile);
            return true;
        }

        public async Task<bool> UpdateAddressAsync(int userProfileId, string userId, UpdateAddressDto dto)
        {
            var profile = await _repository.GetByIdAndUserIdAsync(new UserProfileId(userProfileId), userId, track: true);
            if (profile is null)
                throw new BusinessException($"UserProfile with id {userProfileId} was not found");

            if (!profile.UpdateAddress(dto.AddressId, dto.Street, dto.BuildingNumber, dto.FlatNumber, dto.ZipCode, dto.City, dto.Country))
                throw new BusinessException($"Address with id {dto.AddressId} was not found");

            await _repository.UpdateAsync(profile);
            return true;
        }

        public async Task<bool> RemoveAddressAsync(int userProfileId, int addressId, string userId)
        {
            var profile = await _repository.GetByIdAndUserIdAsync(new UserProfileId(userProfileId), userId, track: true);
            if (profile is null)
                throw new BusinessException($"UserProfile with id {userProfileId} was not found");

            var result = profile.RemoveAddress(addressId);
            if (result == RemoveAddressResult.NotFound)
                throw new BusinessException($"Address with id {addressId} was not found");

            if (result == RemoveAddressResult.LastAddress)
                throw new BusinessException("Cannot remove the last address from a profile");

            await _repository.UpdateAsync(profile);
            return true;
        }
    }
}
