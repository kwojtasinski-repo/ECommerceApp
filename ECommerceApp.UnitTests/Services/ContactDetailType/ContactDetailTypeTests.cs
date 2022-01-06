using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Domain.Interface;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ECommerceApp.UnitTests.Services.ContactDetailType
{
    public class ContactDetailTypeTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<IContactDetailTypeRepository> _contactDetailTypeRepository;

        public ContactDetailTypeTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
            _contactDetailTypeRepository = new Mock<IContactDetailTypeRepository>();
        }

        [Fact]
        public void given_valid_contact_detail_type_should_add()
        {
            var contactDetailType = CreateContactDetailTypeVm(0);
            var contactDetailTypeService = new ContactDetailTypeService(_contactDetailTypeRepository.Object, _mapper);

            contactDetailTypeService.Add(contactDetailType);

            _contactDetailTypeRepository.Verify(cdt => cdt.Add(It.IsAny<Domain.Model.ContactDetailType>()), Times.Once);
        }

        [Fact]
        public void given_invalid_contact_detail_type_should_throw_an_exception()
        {
            var contactDetailType = CreateContactDetailTypeVm(1);
            var contactDetailTypeService = new ContactDetailTypeService(_contactDetailTypeRepository.Object, _mapper);

            Action action = () => contactDetailTypeService.Add(contactDetailType);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_contact_detail_type_should_exists()
        {
            int id = 1;
            var contactDetailType = CreateContactDetailType(id);
            _contactDetailTypeRepository.Setup(cdt => cdt.GetById(id)).Returns(contactDetailType);
            var contactDetailTypeService = new ContactDetailTypeService(_contactDetailTypeRepository.Object, _mapper);
            
            var exists = contactDetailTypeService.ContactDetailTypeExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_contact_detail_type_shouldnt_exists()
        {
            int id = 1;
            var contactDetailTypeService = new ContactDetailTypeService(_contactDetailTypeRepository.Object, _mapper);

            var exists = contactDetailTypeService.ContactDetailTypeExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_contact_detail_type_when_add_should_throw_an_exception()
        {
            var contactDetailTypeService = new ContactDetailTypeService(_contactDetailTypeRepository.Object, _mapper);

            Action action = () => contactDetailTypeService.AddContactDetailType(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_contact_detail_type_when_update_should_throw_an_exception()
        {
            var contactDetailTypeService = new ContactDetailTypeService(_contactDetailTypeRepository.Object, _mapper);

            Action action = () => contactDetailTypeService.UpdateContactDetailType(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private ContactDetailTypeVm CreateContactDetailTypeVm(int id)
        {
            var contactDetailType = new ContactDetailTypeVm
            {
                Id = id,
                Name = "name"
            };
            return contactDetailType;
        }

        private Domain.Model.ContactDetailType CreateContactDetailType(int id)
        {
            var contactDetailType = new Domain.Model.ContactDetailType 
            { 
                Id = id,
                Name = "name"
            };
            return contactDetailType;
        }
    }
}
