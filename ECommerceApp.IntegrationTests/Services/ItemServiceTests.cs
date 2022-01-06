using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public void given_valid_id_should_return_item()
        {
            var id = 1;

            var item = _service.GetItemById(id);

            item.ShouldNotBeNull();
            item.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null_item()
        {
            var id = 1457564456;

            var item = _service.GetItemById(id);

            item.ShouldBeNull();
        }

        [Fact]
        public void given_valid_item_should_add()
        {
            var item = CreateItem(0);

            var id = _service.AddItem(item);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_item_should_throw_an_exception()
        {
            var item = CreateItem(1245);

            var exception = Should.Throw<BusinessException>(() => _service.AddItem(item));

            exception.Message.ShouldBe("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_item_should_update()
        {
            var id = 1;
            var item = _service.Get(id);
            var name = "NameItem1234";
            item.Name = name;
            var itemToUpdate = new NewItemVm { Id = item.Id, Description = item.Description, BrandId = item.BrandId, Cost = item.Cost, CurrencyId = item.CurrencyId, Name = item.Name, Quantity = item.Quantity, TypeId = item.TypeId, Warranty = item.Warranty, ItemTags = new List<ItemsWithTagsVm> { new ItemsWithTagsVm { ItemId = item.Id, TagId = 2 } } };

            _service.UpdateItem(itemToUpdate);

            var itemUpdated = _service.Get(id);
            itemUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public void given_items_in_db_should_return_list()
        {
            var items = _service.GetAllItems();

            items.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_experession_should_return_list()
        {
            var items = _service.GetAllItems(i => true);

            items.Count().ShouldBeGreaterThan(0);
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
            var id = _service.AddItem(item);

            _service.DeleteItem(id);

            var itemDeleted = _service.Get(id);
            itemDeleted.ShouldBeNull();
        }

        [Fact]
        public void given_items_in_db_should_return_items()
        {
            var items = _service.GetItemsAddToCart();

            items.Count.ShouldBeGreaterThan(0);
        }

        private NewItemVm CreateItem(int id)
        {
            var item = new NewItemVm
            {
                Id = id,
                BrandId = 1,
                Cost = 100M,
                CurrencyId = 1,
                Name = "Img12",
                Description = "This is description 123",
                Quantity = 25,
                TypeId = 1,
                Warranty = "123"
            };
            return item;
        }
    }
}
