using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class ContactDetailTypeService : AbstractService<ContactDetailTypeVm, IContactDetailTypeRepository, ContactDetailType>, IContactDetailTypeService
    {
        public ContactDetailTypeService(IContactDetailTypeRepository contactDetailTypeRepository, IMapper mapper) : base(contactDetailTypeRepository, mapper)
        {
        }

        public int AddContactDetailType(ContactDetailTypeVm model)
        {
            var id = Add(model);
            return id;
        }

        public bool ContactDetailTypeExists(int id)
        {
            var contactDetailType = Get(id);
            var exists = contactDetailType != null;

            if (exists)
            {
                return true;
            }

            return false;
        }

        public ContactDetailTypeVm GetContactDetailType(int id)
        {
            var contactDetailType = Get(id);
            return contactDetailType;
        }

        public IEnumerable<ContactDetailTypeVm> GetContactDetailTypes(Expression<Func<ContactDetailType, bool>> expression)
        {
            var contactDetailTypes = _repo.GetAll().Where(expression);
            var contactDetailTypesVm = contactDetailTypes.ProjectTo<ContactDetailTypeVm>(_mapper.ConfigurationProvider).ToList();
            return contactDetailTypesVm;
        }

        public void UpdateContactDetailType(ContactDetailTypeVm model)
        {
            var contactDetailType = _mapper.Map<ContactDetailType>(model);
            _repo.UpdateContactDetailType(contactDetailType);
        }
    }
}
