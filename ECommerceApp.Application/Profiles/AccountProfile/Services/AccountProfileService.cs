using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using ECommerceApp.Domain.Profiles.AccountProfile;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AP = ECommerceApp.Domain.Profiles.AccountProfile;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    internal sealed class AccountProfileService : IAccountProfileService
    {
        private readonly IAccountProfileRepository _repository;
        private readonly IMapper _mapper;

        public AccountProfileService(IAccountProfileRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<int> CreateAsync(CreateAccountProfileDto dto)
        {
            var (profile, _) = AP.AccountProfile.Create(
                dto.UserId,
                dto.FirstName,
                dto.LastName,
                dto.IsCompany,
                dto.NIP,
                dto.CompanyName);

            return await _repository.AddAsync(profile);
        }

        public async Task<bool> UpdateAsync(UpdateAccountProfileDto dto)
        {
            var profile = await _repository.GetByIdAsync(dto.Id);
            if (profile is null)
                throw new BusinessException($"AccountProfile with id {dto.Id} was not found");

            profile.UpdatePersonalInfo(dto.FirstName, dto.LastName, dto.IsCompany, dto.NIP, dto.CompanyName);
            await _repository.UpdateAsync(profile);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
            => await _repository.DeleteAsync(id);

        public async Task<AccountProfileDetailsVm?> GetDetailsAsync(int id)
        {
            var profile = await _repository.GetByIdWithDetailsAsync(id);
            return profile is null ? null : _mapper.Map<AccountProfileDetailsVm>(profile);
        }

        public async Task<AccountProfileDetailsVm?> GetDetailsByUserIdAsync(string userId)
        {
            var profile = await _repository.GetByUserIdAsync(userId);
            if (profile is null)
                return null;
            profile = await _repository.GetByIdWithDetailsAsync(profile.Id);
            return profile is null ? null : _mapper.Map<AccountProfileDetailsVm>(profile);
        }

        public async Task<AccountProfileVm?> GetAsync(int id, string userId)
        {
            var profile = await _repository.GetByIdAndUserIdAsync(id, userId);
            return profile is null ? null : _mapper.Map<AccountProfileVm>(profile);
        }

        public async Task<AccountProfileVm?> GetByUserIdAsync(string userId)
        {
            var profile = await _repository.GetByUserIdAsync(userId);
            return profile is null ? null : _mapper.Map<AccountProfileVm>(profile);
        }

        public async Task<AccountProfileListVm> GetAllAsync(int pageSize, int pageNo, string searchString)
        {
            var profiles = await _repository.GetAllAsync(pageSize, pageNo, searchString);
            return new AccountProfileListVm
            {
                Profiles = _mapper.Map<List<AccountProfileForListVm>>(profiles),
                CurrentPage = pageNo,
                PageSize = pageSize,
                SearchString = searchString,
                Count = await _repository.CountAllAsync(searchString)
            };
        }

        public async Task<AccountProfileListVm> GetAllByUserIdAsync(string userId, int pageSize, int pageNo, string searchString)
        {
            var profiles = await _repository.GetAllByUserIdAsync(userId, pageSize, pageNo, searchString);
            return new AccountProfileListVm
            {
                Profiles = _mapper.Map<List<AccountProfileForListVm>>(profiles),
                CurrentPage = pageNo,
                PageSize = pageSize,
                SearchString = searchString,
                Count = await _repository.CountByUserIdAsync(userId, searchString)
            };
        }

        public async Task<bool> ExistsAsync(int id, string userId)
            => await _repository.ExistsByIdAndUserIdAsync(id, userId);
    }
}
