using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Services.ContactDetails
{
    public class ContactDetailService : IContactDetailService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        private readonly IContactDetailRepository _contactDetailRepository;
        private readonly IContactDetailTypeRepository _contactDetailTypeRepository;

        public ContactDetailService(IContactDetailRepository contactDetailRepository, IMapper mapper, IHttpContextAccessor httpContextAccessor, IContactDetailTypeRepository contactDetailTypeRepository)
        {
            _httpContextAccessor = httpContextAccessor;
            _contactDetailRepository = contactDetailRepository;
            _mapper = mapper;
            _contactDetailTypeRepository = contactDetailTypeRepository;
        }

        public int AddContactDetail(ContactDetailDto contactDetailDto)
        {
            if (contactDetailDto is null)
            {
                throw new BusinessException($"{typeof(ContactDetailVm).Name} cannot be null");
            }

            if (contactDetailDto.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var userId = _httpContextAccessor.GetUserId();
            var customerIds = _contactDetailRepository.GetCustomersIds(userId);

            if (!customerIds.Any(c => c == contactDetailDto.CustomerId))
            {
                throw new BusinessException("Customer not exists check your id");
            }

            var contactDetail = _mapper.Map<ContactDetail>(contactDetailDto);
            var id = _contactDetailRepository.AddContactDetail(contactDetail);
            return id;
        }

        public bool DeleteContactDetail(int id)
        {
            return _contactDetailRepository.DeleteContactDetail(id);
        }

        public IEnumerable<ContactDetailDto> GetAllContactDetails()
        {
            return _mapper.Map<List<ContactDetailDto>>(_contactDetailRepository.GetAllContactDetails());
        }

        public ContactDetailDto GetContactDetailById(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
            return _mapper.Map<ContactDetailDto>(_contactDetailRepository.GetContactDetailByIdAndUserId(id, userId));
        }

        public bool UpdateContactDetail(ContactDetailDto contactDetailDto)
        {
            if (contactDetailDto is null)
            {
                throw new BusinessException($"{typeof(ContactDetailVm).Name} cannot be null");
            }

            var contactDetail = _contactDetailRepository.GetContactDetailById(contactDetailDto.Id);
            if (contactDetail is null)
            {
                return false;
            }

            var contactDetailType = _contactDetailTypeRepository.GetContactDetailTypeById(contactDetailDto.ContactDetailTypeId)
                ?? throw new BusinessException($"Contact Detail with id '{contactDetailDto.ContactDetailTypeId}' was not found");
            contactDetail.ContactDetailInformation = contactDetailDto.ContactDetailInformation;
            contactDetail.ContactDetailType = contactDetailType;
            contactDetail.ContactDetailTypeId = contactDetailDto.ContactDetailTypeId;
            _contactDetailRepository.UpdateContactDetail(contactDetail);
            return true;
        }

        public bool ContactDetailExists(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
            return _contactDetailRepository.ExistsByIdAndUserId(id, userId);
        }

        public ContactDetailsForListVm GetContactDetails(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
            var contactDetail = _contactDetailRepository.GetContactDetailById(id, userId);
            var contactDetailVm = _mapper.Map<ContactDetailsForListVm>(contactDetail);
            return contactDetailVm;
        }
    }
}
