using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ItemServiceTests : BaseTest<IItemService>
    {
        [Fact]
        public void given_valid_id_should_return_item_details()
        {
            var id = 1;

            var item = _service.GetItemDetails(id);

            item.ShouldNotBeNull();
            item.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null_item_details()
        {
            var id = 145756846;

            var item = _service.GetItemDetails(id);

            item.ShouldBeNull();
        }

        [Fact]
        public void given_valid_item_should_add()
        {
            var item = CreateItem(0);

            var id = _service.AddItem(new AddItemDto { Name = item.Name, Description = item.Description, CurrencyId = item.Currency.Id, Quantity = item.Quantity, Warranty = item.Warranty, BrandId = item.Brand.Id, Cost = item.Cost, TypeId = item.Type.Id });

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_null_item_when_add_item_should_throw_an_exception()
        {
            var item = CreateItem(1245);

            var exception = Should.Throw<BusinessException>(() => _service.AddItem(null));

            exception.Message.ShouldBe($"Not accept null value {nameof(AddItemDto)}");
        }

        [Fact]
        public void given_valid_item_should_update()
        {
            var id = 1;
            var item = _service.GetItemDetails(id);
            var name = "NameItem1234";
            item.Name = name;
            var itemToUpdate = new UpdateItemDto { Id = id, Name = item.Name, Description = item.Description, CurrencyId = item.Currency.Id, Quantity = item.Quantity, Warranty = item.Warranty, BrandId = item.Brand.Id, Cost = item.Cost, TypeId = item.Type.Id };

            _service.UpdateItem(itemToUpdate);

            var itemUpdated = _service.GetItemDetails(id);
            itemUpdated.Name.ShouldBe(name);
            itemUpdated.Tags.Count.ShouldBe(itemToUpdate.TagsId.Count());
        }

        [Fact]
        public void given_items_in_db_should_return_list()
        {
            var items = _service.GetAllItems();

            items.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_page_size_page_no_search_string_should_return_list()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var items = _service.GetAllItemsForList(pageSize, pageNo, searchString);

            items.Count.ShouldBeGreaterThan(0);
            items.Items.Count.ShouldBeGreaterThan(0);
            items.PageSize.ShouldBe(pageSize);
            items.CurrentPage.ShouldBe(pageNo);
            items.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_valid_page_size_page_no_search_string_should_return_item_list_with_tags()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var items = _service.GetAllItemsWithTags(pageSize, pageNo, searchString);

            items.Count.ShouldBeGreaterThan(0);
            items.ItemTags.Count.ShouldBeGreaterThan(0);
            items.PageSize.ShouldBe(pageSize);
            items.CurrentPage.ShouldBe(pageNo);
            items.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_valid_id_should_delete_item()
        {
            var item = CreateItem(0);
            var id = _service.AddItem(new AddItemDto { Name = item.Name, Description = item.Description, CurrencyId = item.Currency.Id, Quantity = item.Quantity, Warranty = item.Warranty, BrandId = item.Brand.Id, Cost = item.Cost, TypeId = item.Type.Id });

            _service.DeleteItem(id);

            var itemDeleted = _service.GetItemDetails(id);
            itemDeleted.ShouldBeNull();
        }

        [Fact]
        public void given_items_in_db_should_return_items()
        {
            var items = _service.GetItemsAddToCart();

            items.Count.ShouldBeGreaterThan(0);
        }

        private static ItemDto CreateItem(int id)
        {
            var item = new ItemDto
            {
                Id = id,
                Brand = new BrandDto { Id = 1 },
                Cost = 100M,
                Currency = new CurrencyDto { Id = 1 },
                Name = "Img12",
                Description = "This is description 123",
                Quantity = 25,
                Type = new TypeDto { Id = 1 },
                Warranty = "123"
            };
            return item;
        }
    }
}
