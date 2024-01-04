﻿using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class OrderServiceTests : BaseTest<IOrderService>
    {
        [Fact]
        public void given_valid_id_should_delete_order()
        {
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var order = CreateAddOrderDto(0);
            var id = _service.AddOrder(order);

            _service.DeleteOrder(id);

            var orderAdded = _service.Get(id);
            orderAdded.ShouldBeNull();
        }

        [Fact]
        public void given_valid_refund_id_should_delete_refund_from_order()
        {
            var orderId = 4;
            var refundId = 1;

            _service.DeleteRefundFromOrder(refundId);

            var order = _service.Get(orderId);
            order.ShouldNotBeNull();
            order.RefundId.ShouldBeNull();
        }

        [Fact]
        public void given_valid_order_id_should_return_order()
        {
            var id = 1;

            var order = _service.GetOrderById(id);

            order.ShouldNotBeNull();
            order.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_order_id_should_return_null()
        {
            var id = 1346456;

            var order = _service.GetOrderById(id);

            order.ShouldBeNull();
        }


        [Fact]
        public void given_valid_order_id_should_return_order_details()
        {
            var id = 1;

            var order = _service.GetOrderDetail(id);

            order.ShouldNotBeNull();
            order.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_order_id_should_return_null_order_details()
        {
            var id = 1346456;

            var order = _service.GetOrderDetail(id);

            order.ShouldBeNull();
        }

        [Fact]
        public void given_valid_order_id_should_return_order_for_realization()
        {
            var id = 1;

            var order = _service.GetOrderForRealization(id);

            order.ShouldNotBeNull();
            order.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_order_id_should_return_null_order_for_realization()
        {
            var id = 13523565;

            var order = _service.GetOrderForRealization(id);

            order.ShouldBeNull();
        }

        [Fact]
        public void given_valid_order_id_should_return_readonly_order()
        {
            var id = 1;

            var order = _service.GetOrderByIdReadOnly(id);

            order.ShouldNotBeNull();
            order.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_order_id_should_return_null_readonly_order()
        {
            var id = 13523565;

            var order = _service.GetOrderByIdReadOnly(id);

            order.ShouldBeNull();
        }

        [Fact]
        public void given_valid_order_should_update_with_existed_order_items()
        {
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var addOrder = CreateAddOrderDto(0);
            var id = _service.AddOrder(addOrder);
            var order = _service.GetOrderById(id);
            order.OrderItems.Add(new OrderItemDto { Id = 2, ItemId = 5, ItemOrderQuantity = 1, UserId = PROPER_CUSTOMER_ID });

            _service.UpdateOrderWithExistedOrderItemsIds(order);

            var orderUpdated = _service.GetOrderById(id);
            orderUpdated.OrderItems.Count.ShouldBeGreaterThan(1);
        }

        [Fact]
        public void given_orders_in_db_when_get_all_by_expression_should_return_list_orders()
        {
            var orders = _service.GetAllOrders(o => true);

            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_orders_in_db_when_get_all_by_invalid_expression_should_return_empty_list_orders()
        {
            var orders = _service.GetAllOrders(o => o.Id == 3235646);

            orders.Count.ShouldBe(0);
        }

        [Fact]
        public void given_orders_in_db_when_get_all_by_user_id_should_return_list_orders()
        {
            var orders = _service.GetAllOrdersByUserId(PROPER_CUSTOMER_ID);

            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_orders_in_db_when_get_all_by_invalid_user_id_should_return_empty_list_orders()
        {
            var orders = _service.GetAllOrdersByUserId("");

            orders.Count.ShouldBe(0);
        }

        [Fact]
        public void given_orders_in_db_when_get_all_by_customer_id_should_return_list_orders()
        {
            var id = 1;

            var orders = _service.GetAllOrdersByCustomerId(id);

            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_orders_in_db_when_get_all_by_invalid_customer_id_should_return_empty_list_orders()
        {
            var id = 1356473462;

            var orders = _service.GetAllOrdersByCustomerId(id);

            orders.Count.ShouldBe(0);
        }

        [Fact]
        public void given_orders_in_db_should_return_all_orders()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var orders = _service.GetAllOrders(pageSize, pageNo, searchString);

            orders.SearchString.ShouldBe(searchString);
            orders.PageSize.ShouldBe(pageSize);
            orders.CurrentPage.ShouldBe(pageNo);
            orders.Orders.Count.ShouldBeGreaterThan(0);
            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_orders_in_db_should_return_all_orders_by_customer_id()
        {
            int pageSize = 20;
            int pageNo = 1;
            var customerId = 1;

            var orders = _service.GetAllOrdersByCustomerId(customerId, pageSize, pageNo);

            orders.PageSize.ShouldBe(pageSize);
            orders.CurrentPage.ShouldBe(pageNo);
            orders.Orders.Count.ShouldBeGreaterThan(0);
            orders.Count.ShouldBeGreaterThan(0);
        }

        private static AddOrderDto CreateAddOrderDto(int id)
        {
            var order = new AddOrderDto
            {
                Id = id,
                CustomerId = id,
                OrderItems = new List<OrderItemsIdsDto>
                {
                    new OrderItemsIdsDto { Id = 3 }
                }
            };
            return order;
        }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessorTest>();
        }
    }
}
