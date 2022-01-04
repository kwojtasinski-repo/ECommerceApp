using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ContactDetailServiceTests : BaseTest<IContactDetailService>
    {
        private readonly string PROPER_CUSTOMER_ID = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

        [Fact]
        public void given_valid_id_should_return_contact_detail()
        {
            var id = 1;
            var contactDetailInformation = "867123563";

            var contactDetail = _service.GetContactDetailById(id);

            contactDetail.ShouldNotBeNull();
            contactDetail.ShouldBeOfType<ContactDetailVm>();
            contactDetail.ContactDetailInformation.ShouldBe(contactDetailInformation);
        }

        [Fact]
        public void given_invalid_id_shouldnt_return_contact_detail()
        {
            var id = 123;

            var contactDetail = _service.GetContactDetailById(id);

            contactDetail.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_and_user_id_should_return_contact_detail()
        {
            var id = 1;
            var contactDetailInformation = "867123563";

            var contactDetail = _service.GetContactDetailById(id, PROPER_CUSTOMER_ID);

            contactDetail.ShouldNotBeNull();
            contactDetail.ShouldBeOfType<ContactDetailVm>();
            contactDetail.ContactDetailInformation.ShouldBe(contactDetailInformation);
        }

        [Fact]
        public void given_valid_id_and_invalid_user_id_shouldnt_return_contact_detail()
        {
            var id = 1;

            var contactDetail = _service.GetContactDetailById(id, "");

            contactDetail.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_contact_detail_should_exists()
        {
            var id = 1;

            var exists = _service.ContactDetailExists(cd => cd.Id == id);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_invalid_expression_contact_detail_shouldnt_exists()
        {
            var id = 13252;

            var exists = _service.ContactDetailExists(cd => cd.Id == id);

            exists.ShouldBeFalse();
        }

        [Fact]
        public void given_valid_id_and_user_id_contact_detail_should_exists()
        {
            var id = 1;

            var exists = _service.ContactDetailExists(id, PROPER_CUSTOMER_ID);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_valid_id_and_invalid_user_id_contact_detail_shouldnt_exists()
        {
            var id = 1;

            var exists = _service.ContactDetailExists(id, "");

            exists.ShouldBeFalse();
        }

        [Fact]
        public void given_valid_expression_should_return_contact_details()
        {
            var contactDetails = _service.GetAllContactDetails(b => true);

            contactDetails.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_expression_should_return_empty_contact_details()
        {
            var contactDetails = _service.GetAllContactDetails(b => b.ContactDetailInformation == "asf3525wewqeefw");

            contactDetails.Count().ShouldBe(0);
        }

        [Fact]
        public void given_valid_contact_detail_should_add()
        {
            var contactDetail = CreateContactDetail(0);

            var id = _service.AddContactDetail(contactDetail);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_contact_detail_should_throw_an_exception()
        {
            var contactDetail = CreateContactDetail(234);

            var exception = Should.Throw<BusinessException>(() => _service.AddContactDetail(contactDetail));

            exception.Message.ShouldBe("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_contact_detail_with_invalid_customer_id_should_throw_an_exception()
        {
            var contactDetail = CreateContactDetail(0);
            contactDetail.CustomerId = 999;

            var exception = Should.Throw<BusinessException>(() => _service.AddContactDetail(contactDetail));

            exception.Message.ShouldBe("Customer not exists check your id");
        }

        [Fact]
        public void given_valid_contact_detail_should_update()
        {
            var contactDetail = CreateContactDetail(0);
            var id = _service.AddContactDetail(contactDetail);
            contactDetail = _service.Get(id);
            var contactDetailInformation = "CD1234";
            contactDetail.ContactDetailInformation = contactDetailInformation;

            _service.UpdateContactDetail(contactDetail);

            var brandUpdated = _service.Get(id);
            brandUpdated.ShouldNotBeNull();
            brandUpdated.ShouldBeOfType<ContactDetailVm>();
            brandUpdated.ContactDetailInformation.ShouldBe(contactDetailInformation);
        }

        [Fact]
        public void given_valid_id_should_delete_contact_detail()
        {
            var contactDetail = CreateContactDetail(0);
            var id = _service.AddContactDetail(contactDetail);

            _service.DeleteContactDetail(id);

            var brandDeleted = _service.Get(id);
            brandDeleted.ShouldBeNull();
        }

        private ContactDetailVm CreateContactDetail(int id)
        {
            var brand = new ContactDetailVm
            {
                Id = id,
                ContactDetailInformation = "test124",
                ContactDetailTypeId = 1,
                CustomerId = 1
            };
            return brand;
        }
    }
}
