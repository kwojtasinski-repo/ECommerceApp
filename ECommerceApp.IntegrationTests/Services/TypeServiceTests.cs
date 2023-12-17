using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class TypeServiceTests : BaseTest<ITypeService>
    {
        [Fact]
        public void given_valid_id_should_return_type_details()
        {
            var typeId = 1;

            var type = _service.GetTypeDetails(typeId);

            type.ShouldNotBeNull();
            type.Id.ShouldBe(typeId);
        }

        [Fact]
        public void given_invalid_id_should_return_null_type_details()
        {
            var typeId = 198676;

            var type = _service.GetTypeDetails(typeId);

            type.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_should_return_type()
        {
            var typeId = 1;

            var type = _service.GetTypeById(typeId);

            type.ShouldNotBeNull();
            type.Id.ShouldBe(typeId);
        }

        [Fact]
        public void given_invalid_id_should_return_null_tag()
        {
            var typeId = 5477841;

            var type = _service.GetTypeById(typeId);

            type.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_should_return_list_types()
        {
            var types = _service.GetTypes(t => true);

            types.ShouldNotBeEmpty();
        }

        [Fact]
        public void given_valid_page_size_page_number_search_string_should_return_list_types()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var types = _service.GetTypes(pageSize, pageNo, searchString);

            types.Count.ShouldBeGreaterThan(0);
            types.Types.Count.ShouldBeGreaterThan(0);
            types.PageSize.ShouldBe(pageSize);
            types.SearchString.ShouldBe(searchString);
            types.CurrentPage.ShouldBe(pageNo);
        }

        [Fact]
        public void given_valid_id_should_delete_type()
        {
            var type = new TypeDto { Id = 0, Name = "Type #1" };
            var id = _service.AddType(type);

            _service.DeleteType(id);

            var typeDeleted = _service.GetTypeById(id);
            typeDeleted.ShouldBeNull();
        }
    }
}
