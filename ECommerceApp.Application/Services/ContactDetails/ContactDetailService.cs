using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public class ContactDetailService : AbstractService<ContactDetailVm, IContactDetailRepository, ContactDetail>, IContactDetailService
    {
        public ContactDetailService(IContactDetailRepository contactDetailRepository, IMapper mapper) : base(contactDetailRepository, mapper)
        {
        }

        public int AddContactDetail(ContactDetailVm contactDetailVm)
        {
            if (contactDetailVm is null)
            {
                throw new BusinessException($"{typeof(ContactDetailVm).Name} cannot be null");
            }

            if (contactDetailVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customersId = _repo.GetCustomersIds().ToList();
            bool customerIdExists = false;
            foreach (var custId in customersId)
            {
                if (custId == contactDetailVm.CustomerId)
                {
                    customerIdExists = true;
                    break;
                }
            }

            if (customerIdExists)
            {
                var contactDetail = _mapper.Map<ContactDetail>(contactDetailVm);
                var id = _repo.AddContactDetail(contactDetail);
                return id;
            }
            else
            {
                throw new BusinessException("Customer not exists check your id");
            }
        }

        public void DeleteContactDetail(int id)
        {
            _repo.DeleteContactDetail(id);
        }

        public IEnumerable<ContactDetailVm> GetAllContactDetails(Expression<Func<ContactDetail, bool>> expression)
        {
            var contactDetailTypes = _repo.GetAll().Where(expression);
            var contactDetailTypesVm = contactDetailTypes.ProjectTo<ContactDetailVm>(_mapper.ConfigurationProvider).ToList();
            return contactDetailTypesVm;
        }

        public ContactDetailVm GetContactDetailById(int id)
        {
            var contactDetail = _repo.GetById(id);
            var contactDetailVm = _mapper.Map<ContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public ContactDetailVm GetContactDetailById(int id, string userId)
        {
            var contactDetail = _repo.GetAll().Include(c => c.Customer).Where(cd => cd.Id == id && cd.Customer.UserId == userId).FirstOrDefault();
            var contactDetailVm = _mapper.Map<ContactDetailVm>(contactDetail);
            return contactDetailVm;
        }

        public ContactDetailsForListVm GetContactDetails(int id)
        {
            var contactDetail = _repo.GetAll().Include(cdt => cdt.ContactDetailType).Where(cd => cd.Id == id).FirstOrDefault();
            var contactDetailVm = _mapper.Map<ContactDetailsForListVm>(contactDetail);
            return contactDetailVm;
        }

        public void UpdateContactDetail(ContactDetailVm contactDetailVm)
        {
            if (contactDetailVm is null)
            {
                throw new BusinessException($"{typeof(ContactDetailVm).Name} cannot be null");
            }

            var contactDetail = _mapper.Map<ContactDetail>(contactDetailVm);
            _repo.UpdateContactDetail(contactDetail);
        }

        public int AddContactDetail(ContactDetailVm model, string userId)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(ContactDetailVm).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var customersId = _repo.GetCustomersIds(c => c.UserId == userId).ToList();
            bool customerIdExists = false;
            foreach (var custId in customersId)
            {
                if (custId == model.CustomerId)
                {
                    customerIdExists = true;
                    break;
                }
            }

            if (customerIdExists)
            {
                var contactDetail = _mapper.Map<ContactDetail>(model);
                var id = _repo.AddContactDetail(contactDetail);
                return id;
            }
            else
            {
                throw new BusinessException("Customer not exists check your id");
            }
        }

        public bool ContactDetailExists(Expression<Func<ContactDetail, bool>> expression)
        {
            var contactDetail = _repo.GetAll().Where(expression).FirstOrDefault();

            if (contactDetail == null)
            {
                return false;
            }

            return true;
        }

        public bool ContactDetailExists(int id, string userId)
        {
            var contactDetail = _repo.GetAll().Include(c => c.Customer).Where(cd => cd.Id == id && cd.Customer.UserId == userId).AsNoTracking().FirstOrDefault();

            if (contactDetail == null)
            {
                return false;
            }

            return true;
        }

        public ContactDetailsForListVm GetContactDetails(int id, string userId)
        {
            var contactDetail = _repo.GetContactDetailById(id, userId);
            var contactDetailVm = _mapper.Map<ContactDetailsForListVm>(contactDetail);
            return contactDetailVm;
        }
    }
}
