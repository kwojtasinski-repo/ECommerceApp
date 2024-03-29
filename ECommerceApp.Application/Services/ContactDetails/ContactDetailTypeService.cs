﻿using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

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

        public IEnumerable<ContactDetailTypeDto> GetContactDetailTypes()
        {
            return _mapper.Map<List<ContactDetailTypeDto>>(_contactDetailTypeRepository.GetAllContactDetailTypes());
        }

        public bool UpdateContactDetailType(ContactDetailTypeDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(ContactDetailTypeDto).Name} cannot be null");
            }

            var entity = _contactDetailTypeRepository.GetContactDetailTypeById(model.Id);
            if (entity is null)
            {
                return false;
            }

            entity.Name = model.Name;
            _contactDetailTypeRepository.UpdateContactDetailType(entity);
            return true;
        }
    }
}
