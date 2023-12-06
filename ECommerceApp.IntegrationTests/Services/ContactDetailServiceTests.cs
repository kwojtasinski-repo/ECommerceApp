using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Linq;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ContactDetailServiceTests : BaseTest<IContactDetailService>
    {
        [Fact]
        public void given_valid_id_should_return_contact_detail()
        {
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var id = 1;
            var contactDetailInformation = "867123563";

            var contactDetail = _service.GetContactDetailById(id);

            contactDetail.ShouldNotBeNull();
            contactDetail.ShouldBeOfType<ContactDetailDto>();
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
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var id = 1;
            var contactDetailInformation = "867123563";

            var contactDetail = _service.GetContactDetailById(id);

            contactDetail.ShouldNotBeNull();
            contactDetail.ShouldBeOfType<ContactDetailDto>();
            contactDetail.ContactDetailInformation.ShouldBe(contactDetailInformation);
        }

        [Fact]
        public void given_valid_id_and_invalid_user_id_shouldnt_return_contact_detail()
        {
            SetHttpContextUserId("");
            var id = 1;

            var contactDetail = _service.GetContactDetailById(id);

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
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var id = 1;

            var exists = _service.ContactDetailExists(id);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_valid_id_and_invalid_user_id_contact_detail_shouldnt_exists()
        {
            SetHttpContextUserId("");
            var id = 1;

            var exists = _service.ContactDetailExists(id);

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
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
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
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var contactDetail = CreateContactDetail(0);
            var id = _service.AddContactDetail(contactDetail);
            contactDetail = _service.GetContactDetailById(id);
            var contactDetailInformation = "CD1234";
            contactDetail.ContactDetailInformation = contactDetailInformation;

            _service.UpdateContactDetail(contactDetail);

            var contactDetailUpdated = _service.GetContactDetailById(id);
            contactDetailUpdated.ShouldNotBeNull();
            contactDetailUpdated.ShouldBeOfType<ContactDetailDto>();
            contactDetailUpdated.ContactDetailInformation.ShouldBe(contactDetailInformation);
        }

        [Fact]
        public void given_valid_id_should_delete_contact_detail()
        {
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var contactDetail = CreateContactDetail(0);
            var id = _service.AddContactDetail(contactDetail);

            _service.DeleteContactDetail(id);

            var contactDetailDeleted = _service.GetContactDetailById(id);
            contactDetailDeleted.ShouldBeNull();
        }

        private static ContactDetailDto CreateContactDetail(int id)
        {
            var brand = new ContactDetailDto
            {
                Id = id,
                ContactDetailInformation = "test124",
                ContactDetailTypeId = 1,
                CustomerId = 1
            };
            return brand;
        }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessorTest>();
        }
    }
}
