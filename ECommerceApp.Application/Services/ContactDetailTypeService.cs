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

        public IEnumerable<ContactDetailTypeVm> GetContactDetailTypes(Expression<Func<ContactDetailType, bool>> expression)
        {
            var contactDetailTypes = _repo.GetAll().Where(expression);
            var contactDetailTypesVm = contactDetailTypes.ProjectTo<ContactDetailTypeVm>(_mapper.ConfigurationProvider).ToList();
            return contactDetailTypesVm;
        }
    }
}
