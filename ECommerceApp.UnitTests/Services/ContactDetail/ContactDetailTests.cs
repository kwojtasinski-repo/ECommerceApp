﻿using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.UnitTests.Services.ContactDetail
{
    public class ContactDetailTests : BaseTest
    {
        private readonly Mock<IContactDetailRepository> _contactDetailRepository;
        private readonly Mock<IContactDetailTypeRepository> _contactDetailTypeRepository;
        private readonly UserContextTest _userContext;

        public ContactDetailTests()
        {
            _contactDetailRepository = new Mock<IContactDetailRepository>();
            _contactDetailTypeRepository = new Mock<IContactDetailTypeRepository>();
            _userContext = new UserContextTest();
        }

        private ContactDetailService CreateContactDetailService()
            => new (_contactDetailRepository.Object, _mapper, _userContext, _contactDetailTypeRepository.Object);

        [Fact]
        public void given_invalid_contact_detail_should_throw_an_exception()
        {
            var contactDetail = CreateContactDetailDto();
            var contactDetailService = CreateContactDetailService();

            Action action = () => contactDetailService.AddContactDetail(contactDetail);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_invalid_customer_id_should_throw_an_exception()
        {
            var contactDetail = CreateContactDetailDto();
            contactDetail.Id = 0;
            _contactDetailRepository.Setup(c => c.GetCustomersIds(It.IsAny<string>())).Returns(new List<int>());
            var contactDetailService = CreateContactDetailService();

            Action action = () => contactDetailService.AddContactDetail(contactDetail);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Customer not exists check your id");
        }

        [Fact]
        public void given_valid_contact_detail_and_valid_user_id_should_add()
        {
            var contactDetail = CreateContactDetailDto();
            contactDetail.Id = 0;
            var userId = Guid.NewGuid().ToString();
            _userContext.UserId = userId;
            var customerIds = new List<int> { 1, 2, 3 };
            _contactDetailRepository.Setup(cd => cd.GetCustomersIds(userId)).Returns(customerIds);
            var contactDetailService = CreateContactDetailService();

            contactDetailService.AddContactDetail(contactDetail);

            _contactDetailRepository.Verify(cd => cd.AddContactDetail(It.IsAny<Domain.Model.ContactDetail>()), Times.Once);
        }

        [Fact]
        public void given_invalid_contact_detail_and_user_id_should_throw_an_exception()
        {
            var contactDetail = CreateContactDetailDto();
            var userId = Guid.NewGuid().ToString();
            _userContext.UserId = userId;
            var contactDetailService = CreateContactDetailService();

            Action action = () => contactDetailService.AddContactDetail(contactDetail);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_contact_detail_and_invalid_user_id_should_throw_an_exception()
        {
            var contactDetail = CreateContactDetailDto();
            contactDetail.Id = 0;
            var userId = Guid.NewGuid().ToString();
            _userContext.UserId = userId;
            _contactDetailRepository.Setup(c => c.GetCustomersIds(It.IsAny<string>())).Returns(new List<int>());
            var contactDetailService = CreateContactDetailService();

            Action action = () => contactDetailService.AddContactDetail(contactDetail);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Customer not exists check your id");
        }

        [Fact]
        public void given_valid_contact_detail_and_invalid_user_id_should_return_false()
        {
            int id = 1;
            string userId = Guid.NewGuid().ToString();
            _userContext.UserId = userId;
            var contactDetails = CreateContactDetails();
            contactDetails.ForEach(cd => cd.Customer = new Domain.Model.Customer { Id = ++id });
            _contactDetailRepository.Setup(cd => cd.GetAllContactDetails()).Returns(contactDetails);
            var contactDetailService = CreateContactDetailService();

            var exists = contactDetailService.ContactDetailExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_invalid_contact_detail_and_invalid_user_id_should_return_false()
        {
            int id = 1;
            _userContext.UserId = Guid.NewGuid().ToString();
            var contactDetailService = CreateContactDetailService();

            var exists = contactDetailService.ContactDetailExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_valid_contact_detail_id_shouldnt_exists()
        {
            int id = 1;
            var contactDetailService = CreateContactDetailService();

            var exists = contactDetailService.ContactDetailExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_contact_detail_when_add_should_throw_an_exception()
        {
            var contactDetailService = CreateContactDetailService();

            Action action = () => contactDetailService.AddContactDetail(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_contact_detail_when_update_should_throw_an_exception()
        {
            var contactDetailService = CreateContactDetailService();

            Action action = () => contactDetailService.UpdateContactDetail(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private ContactDetailDto CreateContactDetailDto()
        {
            var contactDetail = new ContactDetailDto()
            {
                Id = 1,
                ContactDetailInformation = "123456789",
                ContactDetailTypeId = 1,
                CustomerId = 1
            };
            return contactDetail;
        }

        private static List<Domain.Model.ContactDetail> CreateContactDetails()
        {
            var contactDetails = new List<Domain.Model.ContactDetail>
            {
                CreateContactDetail(1),
                CreateContactDetail(2),
                CreateContactDetail(3)
            };
            return contactDetails;
        }

        private static Domain.Model.ContactDetail CreateContactDetail(int? id)
        {
            var contactDetail = new Domain.Model.ContactDetail
            {
                Id = id ?? 1,
                CustomerId = 1,
                ContactDetailTypeId = 1,
                ContactDetailInformation = "123456789"
            };
            return contactDetail;
        }
    }
}
