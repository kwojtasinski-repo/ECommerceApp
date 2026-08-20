using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Payments
{
    public class PaymentWindowExpiredJobOutboxIntegrationTests
        : BcBaseTest<IMessageBroker>, IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public PaymentWindowExpiredJobOutboxIntegrationTests(
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

        private async Task<int> SeedPaymentAsync(int orderId, CancellationToken ct = default)
        {
            var repo = GetRequiredService<IPaymentRepository>();
            var payment = Payment.Create(new PaymentOrderId(orderId), 100m, 1, DateTime.UtcNow.AddHours(24), PROPER_CUSTOMER_ID);
            await repo.AddAsync(payment, ct);
            var seeded = await repo.GetByOrderIdAsync(orderId, ct);
            return seeded!.Id.Value;
        }

        private async Task<OrderStatus> GetOrderStatusAsync(
            int orderId,
            CancellationToken cancellationToken)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = await repo.GetByIdWithItemsAsync(orderId, cancellationToken);
            return order!.Status;
        }

        [Fact]
        public async Task ExecuteJob_ExpiringPayment_ShouldEnqueueOutbox_AndOrderEventuallyTransitionsToCancelled()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var paymentId = await SeedPaymentAsync(orderId, CancellationToken);

            // Resolve the scheduled task instance for PaymentWindowExpiredJob
            using var scope = Services.CreateScope();
            var tasks = scope.ServiceProvider.GetServices<IScheduledTask>();
            var job = tasks.FirstOrDefault(t => t.TaskName == Application.Sales.Payments.Handlers.PaymentWindowExpiredJob.JobTaskName);
            if (job == null)
                throw new InvalidOperationException("PaymentWindowExpiredJob not registered as IScheduledTask in test host.");

            // Execute the job for the seeded payment
            await job.ExecuteAsync(new JobExecutionContext(paymentId.ToString(), Guid.NewGuid().ToString()), CancellationToken);

            var finalStatus = await _messageProcessing.WaitUntilAsync(
                new OrderStatusOperation(this, orderId, OrderStatus.Cancelled));

            finalStatus.ShouldBe(OrderStatus.Cancelled);
        }

        private sealed class OrderStatusOperation
            : IMessageProcessingOperation<OrderStatus>
        {
            private readonly PaymentWindowExpiredJobOutboxIntegrationTests _test;
            private readonly int _orderId;
            private readonly OrderStatus _expectedStatus;

            public OrderStatusOperation(
                PaymentWindowExpiredJobOutboxIntegrationTests test,
                int orderId,
                OrderStatus expectedStatus)
            {
                _test = test;
                _orderId = orderId;
                _expectedStatus = expectedStatus;
            }

            public Task<OrderStatus> ReadAsync(CancellationToken cancellationToken)
            {
                return _test.GetOrderStatusAsync(_orderId, cancellationToken);
            }

            public bool IsCompleted(OrderStatus state)
            {
                return state == _expectedStatus;
            }

            public string Describe(OrderStatus state)
            {
                return $"Order {_orderId} has status {state}, expected {_expectedStatus}.";
            }
        }
    }
}
