using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Services.ContactDetailType
{
    public class ContactDetailTypeTests : BaseTest
    {
        private readonly Mock<IContactDetailTypeRepository> _contactDetailTypeRepository;

        public ContactDetailTypeTests()
        {
            _contactDetailTypeRepository = new Mock<IContactDetailTypeRepository>();
        }

        [Fact]
        public void given_valid_contact_detail_type_should_add()
        {
            var contactDetailType = CreateContactDetailTypeDto(0);
            var contactDetailTypeService = new ContactDetailTypeService(_contactDetailTypeRepository.Object, _mapper);

            contactDetailTypeService.AddContactDetailType(contactDetailType);

            _contactDetailTypeRepository.Verify(cdt => cdt.AddContactDetailType(It.IsAny<Domain.Model.ContactDetailType>()), Times.Once);
        }

        [Fact]
        public void given_valid_contact_detail_type_should_exists()
        {
            int id = 1;
            var contactDetailType = CreateContactDetailType(id);
            _contactDetailTypeRepository.Setup(cdt => cdt.GetContactDetailTypeById(id)).Returns(contactDetailType);
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

        private static ContactDetailTypeDto CreateContactDetailTypeDto(int id)
        {
            var contactDetailType = new ContactDetailTypeDto
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
