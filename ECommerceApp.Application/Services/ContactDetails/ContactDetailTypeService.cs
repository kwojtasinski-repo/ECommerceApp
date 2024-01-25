using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public class ContactDetailTypeService : IContactDetailTypeService
    {
        private readonly IMapper _mapper;
        private readonly IContactDetailTypeRepository _contactDetailTypeRepository;

        public ContactDetailTypeService(IContactDetailTypeRepository contactDetailTypeRepository, IMapper mapper)
        {
            _mapper = mapper;
            _contactDetailTypeRepository = contactDetailTypeRepository;
        }

        public int AddContactDetailType(ContactDetailTypeDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(ContactDetailTypeDto).Name} cannot be null");
            }

            var entity = _mapper.Map<ContactDetailType>(model);
            var id = _contactDetailTypeRepository.AddContactDetailType(entity);
            return id;
        }

        public bool ContactDetailTypeExists(int id)
        {
            var contactDetailType = _contactDetailTypeRepository.GetContactDetailTypeById(id);
            var exists = contactDetailType != null;

            if (exists)
            {
                return true;
            }

            return false;
        }

        public ContactDetailTypeDto GetContactDetailType(int id)
        {
            var contactDetailType = _contactDetailTypeRepository.GetContactDetailTypeById(id);
            return _mapper.Map<ContactDetailTypeDto>(contactDetailType);
        }

        public IEnumerable<ContactDetailTypeDto> GetContactDetailTypes(Expression<Func<ContactDetailType, bool>> expression)
        {
            var contactDetailTypes = _contactDetailTypeRepository.GetAllContactDetailTypes().Where(expression);
            var contactDetailTypesVm = contactDetailTypes.ProjectTo<ContactDetailTypeDto>(_mapper.ConfigurationProvider).ToList();
            return contactDetailTypesVm;
        }

        public void UpdateContactDetailType(ContactDetailTypeDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(ContactDetailTypeDto).Name} cannot be null");
            }

            var entity = _contactDetailTypeRepository.GetContactDetailTypeById(model.Id)
                ?? throw new BusinessException($"Contact detail with id '{model.Id}' was not found");
            entity.Name = model.Name;
            _contactDetailTypeRepository.UpdateContactDetailType(entity);
        }
    }
}
