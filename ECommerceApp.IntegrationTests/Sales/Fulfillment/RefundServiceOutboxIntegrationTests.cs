using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Fulfillment
{
    public class RefundServiceOutboxIntegrationTests
        : BcBaseTest<IRefundService>, IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public RefundServiceOutboxIntegrationTests(
            ITestOutputHelper output,
            MessageProcessingOperationsFixture messageProcessing) : base(output)
        {
            _messageProcessing = messageProcessing;
        }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        private async Task<int> SeedRefundAsync(int orderId, CancellationToken ct = default)
        {
            var repo = GetRequiredService<IRefundRepository>();
            var items = new[] { RefundItem.Create(10, 2) };
            var refund = Refund.Create(orderId, "e2e test reason", onWarranty: false, items, "user-1");
            return await repo.AddAsync(refund, ct);
        }

        // Read order through a fresh scope each call to avoid stale tracked instances
        private async Task<Order> GetOrderAsync(
            int orderId,
            CancellationToken cancellationToken)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            return await repo.GetByIdWithItemsAsync(orderId, cancellationToken);
        }

        [Fact]
        public async Task ApproveRefundAsync_EnqueuesOutboxMessage_AndOrderEventuallyHasRefundAssignedEvent()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var refundId = await SeedRefundAsync(orderId, CancellationToken);

            var result = await _service.ApproveRefundAsync(refundId, CancellationToken);
            result.ShouldBe(RefundOperationResult.Success);

            var finalOrder = await _messageProcessing.WaitUntilAsync(
                new RefundAssignedOperation(this, orderId));

            finalOrder.ShouldNotBeNull();
            finalOrder.Events.Count(e => e.EventType == OrderEventType.RefundAssigned).ShouldBe(1);
        }

        private sealed class RefundAssignedOperation
            : IMessageProcessingOperation<Order>
        {
            private readonly RefundServiceOutboxIntegrationTests _test;
            private readonly int _orderId;

            public RefundAssignedOperation(
                RefundServiceOutboxIntegrationTests test,
                int orderId)
            {
                _test = test;
                _orderId = orderId;
            }

            public Task<Order> ReadAsync(CancellationToken cancellationToken)
            {
                return _test.GetOrderAsync(_orderId, cancellationToken);
            }

            public bool IsCompleted(Order state)
            {
                return state is not null
                    && state.Events.Any(e => e.EventType == OrderEventType.RefundAssigned);
            }

            public string Describe(Order state)
            {
                return state is null
                    ? $"Order {_orderId} was not found."
                    : $"Order {_orderId} has not received the RefundAssigned event.";
            }
        }
    }
}
