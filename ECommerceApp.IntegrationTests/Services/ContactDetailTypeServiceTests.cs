using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Linq;
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
            contactDetail.ShouldBeOfType<ContactDetailTypeDto>();
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
        public void given_valid_contact_detail_should_update()
        {
            var contactDetail = CreateContactDetailType(0);
            var id = _service.AddContactDetailType(contactDetail);
            contactDetail = _service.GetContactDetailType(id);
            var name = "CD1234";
            contactDetail.Name = name;

            _service.UpdateContactDetailType(contactDetail);

            var brandUpdated = _service.GetContactDetailType(id);
            brandUpdated.ShouldNotBeNull();
            brandUpdated.ShouldBeOfType<ContactDetailTypeDto>();
            brandUpdated.Name.ShouldBe(name);
        }

        private static ContactDetailTypeDto CreateContactDetailType(int id)
        {
            var brand = new ContactDetailTypeDto
            {
                Id = id,
                Name = "test124"
            };
            return brand;
        }
    }
}
