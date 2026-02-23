using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.ViewModels;
using ECommerceApp.Domain.Profiles.AccountProfile;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    internal sealed class AccountContactDetailTypeService : IAccountContactDetailTypeService
    {
        private readonly IContactDetailTypeRepository _repository;
        private readonly IMapper _mapper;

        public AccountContactDetailTypeService(IContactDetailTypeRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<int> AddAsync(AddContactDetailTypeDto dto)
        {
            var type = ContactDetailType.Create(dto.Name);
            return await _repository.AddAsync(type);
        }

        public async Task<bool> UpdateAsync(UpdateContactDetailTypeDto dto)
        {
            var type = await _repository.GetByIdAsync(dto.Id);
            if (type is null)
                throw new BusinessException($"ContactDetailType with id {dto.Id} was not found");

            type.UpdateName(dto.Name);
            await _repository.UpdateAsync(type);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
            => await _repository.DeleteAsync(id);

        public async Task<ContactDetailTypeVm?> GetAsync(int id)
        {
            var type = await _repository.GetByIdAsync(id);
            return type is null ? null : _mapper.Map<ContactDetailTypeVm>(type);
        }

        public async Task<List<ContactDetailTypeVm>> GetAllAsync()
        {
            var types = await _repository.GetAllAsync();
            return _mapper.Map<List<ContactDetailTypeVm>>(types);
        }
    }
}
