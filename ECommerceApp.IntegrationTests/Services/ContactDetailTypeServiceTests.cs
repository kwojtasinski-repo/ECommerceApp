using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.ViewModels.ContactDetailType;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ContactDetailTypeServiceTests : BaseTest<IContactDetailTypeService>
    {
        [Fact]
        public void given_valid_id_should_return_contact_detail_type()
        {
            var id = 1;
            var Name = "PhoneNumber";

            var contactDetail = _service.GetContactDetailType(id);

            contactDetail.ShouldNotBeNull();
            contactDetail.ShouldBeOfType<ContactDetailTypeVm>();
            contactDetail.Name.ShouldBe(Name);
        }

        [Fact]
        public void given_invalid_id_shouldnt_return_contact_detail_type()
        {
            var id = 123;

            var contactDetail = _service.GetContactDetailType(id);

            contactDetail.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_contact_detail_type_should_exists()
        {
            var id = 1;

            var exists = _service.ContactDetailTypeExists(id);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_invalid_expression_contact_detail_type_shouldnt_exists()
        {
            var id = 13252;

            var exists = _service.ContactDetailTypeExists(id);

            exists.ShouldBeFalse();
        }

        [Fact]
        public void given_valid_expression_should_return_contact_detail_types()
        {
            var contactDetails = _service.GetContactDetailTypes(b => true);

            contactDetails.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_expression_should_return_empty_contact_detail_types()
        {
            var contactDetails = _service.GetContactDetailTypes(b => b.Name == "asf3525wewqeefw");

            contactDetails.Count().ShouldBe(0);
        }

        [Fact]
        public void given_valid_contact_detail_type_should_add()
        {
            var contactDetail = CreateContactDetailType(0);

            var id = _service.AddContactDetailType(contactDetail);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_contact_detail_type_should_throw_an_exception()
        {
            var contactDetail = CreateContactDetailType(234);

            var exception = Should.Throw<BusinessException>(() => _service.AddContactDetailType(contactDetail));

            exception.Message.ShouldBe("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_contact_detail_should_update()
        {
            var contactDetail = CreateContactDetailType(0);
            var id = _service.AddContactDetailType(contactDetail);
            contactDetail = _service.Get(id);
            var name = "CD1234";
            contactDetail.Name = name;

            _service.UpdateContactDetailType(contactDetail);

            var brandUpdated = _service.Get(id);
            brandUpdated.ShouldNotBeNull();
            brandUpdated.ShouldBeOfType<ContactDetailTypeVm>();
            brandUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public void given_valid_id_should_delete_contact_detail()
        {
            var contactDetail = CreateContactDetailType(0);
            var id = _service.AddContactDetailType(contactDetail);

            _service.Delete(id);

            var brandDeleted = _service.Get(id);
            brandDeleted.ShouldBeNull();
        }

        private ContactDetailTypeVm CreateContactDetailType(int id)
        {
            var brand = new ContactDetailTypeVm
            {
                Id = id,
                Name = "test124"
            };
            return brand;
        }
    }
}
