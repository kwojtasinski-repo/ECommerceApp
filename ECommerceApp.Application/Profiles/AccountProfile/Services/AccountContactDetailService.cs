using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using ECommerceApp.Domain.Profiles.AccountProfile;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    internal sealed class AccountContactDetailService : IAccountContactDetailService
    {
        private readonly IContactDetailRepository _repository;
        private readonly IMapper _mapper;

        public AccountContactDetailService(IContactDetailRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<int> AddAsync(AddContactDetailDto dto)
        {
            var contactDetail = ContactDetail.Create(
                dto.AccountProfileId,
                dto.ContactDetailTypeId,
                dto.Information);

            return await _repository.AddAsync(contactDetail);
        }

        public async Task<bool> UpdateAsync(UpdateContactDetailDto dto)
        {
            var contactDetail = await _repository.GetByIdAsync(dto.Id);
            if (contactDetail is null)
                throw new BusinessException($"ContactDetail with id {dto.Id} was not found");

            contactDetail.Update(dto.ContactDetailTypeId, dto.Information);
            await _repository.UpdateAsync(contactDetail);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            if (!await _repository.ExistsByIdAndUserIdAsync(id, userId))
                return false;

            return await _repository.DeleteAsync(id);
        }

        public async Task<ContactDetailVm?> GetAsync(int id, string userId)
        {
            var contactDetail = await _repository.GetByIdAndUserIdAsync(id, userId);
            return contactDetail is null ? null : _mapper.Map<ContactDetailVm>(contactDetail);
        }

        public async Task<List<ContactDetailVm>> GetAllAsync()
        {
            var contactDetails = await _repository.GetAllAsync();
            return _mapper.Map<List<ContactDetailVm>>(contactDetails);
        }
    }
}
