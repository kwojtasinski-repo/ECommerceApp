using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Linq;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class OrderItemServiceTests : BaseTest<IOrderItemService>
    {
        [Fact]
        public void given_valid_id_should_return_order_item_details()
        {
            var id = 1;

            var item = _service.GetOrderItemDetails(id);

            item.ShouldNotBeNull();
            item.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null_order_item_details()
        {
            var id = 146373;

            var item = _service.GetOrderItemDetails(id);

            item.ShouldBeNull();
        }

        [Fact]
        public void given_order_items_should_return_list()
        {
            var orderItems = _service.GetOrderItems().ToList();

            orderItems.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_expression_should_return_list_order_item()
        {
            var orderItems = _service.GetOrderItems(oi => true);

            orderItems.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_order_items_should_return_list_for_realization()
        {
            var orderItems = _service.GetOrderItemsForRealization(PROPER_CUSTOMER_ID);

            orderItems.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_page_size_page_no_search_string_should_return_order_items()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var items = _service.GetOrderItems(pageSize, pageNo, searchString);

            items.Count.ShouldBeGreaterThan(0);
            items.ItemOrders.Count.ShouldBeGreaterThan(0);
            items.CurrentPage.ShouldBe(pageNo);
            items.PageSize.ShouldBe(pageSize);
            items.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_valid_user_id_should_return_order_item_count()
        {
            var userId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

            var count = _service.OrderItemCount(userId);

            count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_user_id_page_size_page_no_should_return_list_order_items_not_ordred() 
        {
            var pageSize = 20;
            var pageNo = 1;
            var userId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

            var orderItems = _service.GetOrderItemsNotOrderedByUserId(userId, pageSize, pageNo);

            orderItems.ItemOrders.Count.ShouldBeGreaterThan(0);
            orderItems.Count.ShouldBeGreaterThan(0);
            orderItems.SearchString.ShouldBe("");
            orderItems.PageSize.ShouldBe(pageSize);
            orderItems.CurrentPage.ShouldBe(pageNo);
        }

        [Fact]
        public void given_valid_item_id_should_return_all_order_items_filtered_by_item_id()
        {
            var pageSize = 20;
            var pageNo = 1;
            var itemId = 1;

            var orderItems = _service.GetAllItemsOrderedByItemId(itemId, pageSize, pageNo);

            orderItems.ItemOrders.Count.ShouldBeGreaterThan(0);
            orderItems.Count.ShouldBeGreaterThan(0);
            orderItems.SearchString.ShouldBe("");
            orderItems.PageSize.ShouldBe(pageSize);
            orderItems.CurrentPage.ShouldBe(pageNo);
        }

        [Fact]
        public void given_valid_order_item_should_update()
        {
            var orderItem = CreateOrderItem(0);
            var id = _service.AddOrderItem(orderItem);
            var orderId = 1;
            orderItem.Id = id;
            orderItem.OrderId = orderId;

            _service.UpdateOrderItem(orderItem);

            var orderUpdated = _service.Get(id);
            orderUpdated.ShouldNotBeNull();
            orderUpdated.OrderId.HasValue.ShouldBeTrue();
            orderUpdated.OrderId.Value.ShouldBe(orderId);
        }

        [Fact]
        public void given_valid_order_item_should_delete()
        {
            var orderItem = CreateOrderItem(0);
            var id = _service.AddOrderItem(orderItem);

            _service.DeleteOrderItem(id);

            var itemDeleted = _service.Get(id);
            itemDeleted.ShouldBeNull();
        }

        private OrderItemDto CreateOrderItem(int id)
        {
            var orderItem = new OrderItemDto
            {
                Id = id,
                ItemId = 1,
                ItemOrderQuantity = 1,
                UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e"
            };
            return orderItem;
        }
    }
}
